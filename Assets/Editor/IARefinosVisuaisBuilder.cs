using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Refinos visuais das cenas IA e IA_Aplicacao, salvos nas cenas:
/// - Guarda e Aliado com o visor do Player em vermelho (olhos bravos) e
///   verde (olhos felizes). Sprites em Assets/Art/Personagens/NPC_IA; NPCVisorAnimador troca parado/correr.
/// - a instrução do topo da IA_Aplicacao vira uma placa "Missão" escura
///   com a tecla R separada à direita.
/// Idempotente. Recusa em Play ou com a cena suja. Backup em
/// SceneBackups/refinos_ia/.
/// </summary>
public static class IARefinosVisuaisBuilder
{
    private const string CenaIA = "Assets/Scenes/IA.unity";
    private const string CenaAplicacao = "Assets/Scenes/IA_Aplicacao.unity";
    private const string PastaSprites = "Assets/Art/Personagens/NPC_IA/";
    private const float LarguraCorpoPx = 262f;   // largura do corpo no quadro de 384 px
    private const float PixelsPorUnidade = 256f;

    [MenuItem("Tools/IA/Salvar visor do guarda e do aliado")]
    public static void MenuVisor()
    {
        foreach (string l in AplicarVisor()) Debug.Log("[IARefinosVisuais] " + l);
    }

    [MenuItem("Tools/IA/Placa Missão na IA_Aplicacao")]
    public static void MenuMissao()
    {
        foreach (string l in AplicarMissao()) Debug.Log("[IARefinosVisuais] " + l);
    }

    // ------------------------------------------------------------------ Visor do guarda e do aliado

    public static List<string> AplicarVisor()
    {
        var log = new List<string>();
        Sprite[] gParado = Importar("guarda_visor_idle", log), gCorrer = Importar("guarda_visor_run", log);
        Sprite[] aParado = Importar("aliado_visor_idle", log), aCorrer = Importar("aliado_visor_run", log);
        if (gParado.Length == 0 || aParado.Length == 0)
        {
            log.Add("ERRO: rode antes python Tools/gerar_npc_visor.py.");
            return log;
        }

        foreach (string path in new[] { CenaIA, CenaAplicacao })
        {
            NaCena(path, log, cena =>
            {
                int n = 0;
                foreach (GameObject raiz in cena.GetRootGameObjects())
                {
                    foreach (GuardaIA g in raiz.GetComponentsInChildren<GuardaIA>(true))
                    {
                        n += TrocarVisual(g, 1f, gParado, gCorrer, log);
                    }
                    foreach (AliadoIA a in raiz.GetComponentsInChildren<AliadoIA>(true))
                    {
                        n += TrocarVisual(a, 0.7f, aParado, aCorrer, log);
                    }
                }
                log.Add($"{cena.name}: {n} personagem(ns) com visor.");
            });
        }
        return log;
    }

    private static int TrocarVisual(MonoBehaviour npc, float tamanho, Sprite[] parado, Sprite[] correr, List<string> log)
    {
        var so = new SerializedObject(npc);
        var visual = so.FindProperty("visual").objectReferenceValue as Transform;
        if (visual == null)
        {
            log.Add($"AVISO: {npc.name} sem Visual.");
            return 0;
        }

        var sr = visual.GetComponent<SpriteRenderer>();
        Undo.RecordObject(sr, "Visor NPC");
        Undo.RecordObject(visual, "Visor NPC");
        sr.sprite = parado[0];
        sr.color = Color.white;
        // Mesmo tamanho de corpo do quadrado liso de antes (tamanho em unidades).
        float k = tamanho / (LarguraCorpoPx / PixelsPorUnidade);
        visual.localScale = new Vector3(k, k, 1f);

        var anim = visual.GetComponent<NPCVisorAnimador>();
        if (anim == null) anim = Undo.AddComponent<NPCVisorAnimador>(visual.gameObject);
        anim.Configurar(parado, correr);
        EditorUtility.SetDirty(anim);
        return 1;
    }

    private static Sprite[] Importar(string prefixo, List<string> log)
    {
        string[] arquivos = System.IO.Directory.GetFiles(PastaSprites, prefixo + "_*.png").OrderBy(f => f).ToArray();
        var sprites = new List<Sprite>();
        foreach (string arq in arquivos)
        {
            string path = arq.Replace('\\', '/');
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null)
            {
                AssetDatabase.ImportAsset(path);
                imp = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (imp == null) continue;

            // Mesma importação dos quadros do visor do Player.
            bool mudou = imp.textureType != TextureImporterType.Sprite || imp.spritePixelsPerUnit != PixelsPorUnidade
                         || imp.spritePivot != new Vector2(0.5f, 0.375f) || imp.mipmapEnabled;
            if (mudou)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                var cfg = new TextureImporterSettings();
                imp.ReadTextureSettings(cfg);
                cfg.spriteAlignment = (int)SpriteAlignment.Custom;
                cfg.spritePivot = new Vector2(0.5f, 0.375f);
                cfg.spritePixelsPerUnit = PixelsPorUnidade;
                cfg.mipmapEnabled = false;
                cfg.filterMode = FilterMode.Bilinear;
                cfg.alphaIsTransparency = true;
                imp.SetTextureSettings(cfg);
                imp.SaveAndReimport();
            }
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) sprites.Add(s);
        }
        log.Add($"{prefixo}: {sprites.Count} quadros.");
        return sprites.ToArray();
    }

    // ------------------------------------------------------------------ Placa Missão

    public static List<string> AplicarMissao()
    {
        var log = new List<string>();
        NaCena(CenaAplicacao, log, cena =>
        {
            TextMeshProUGUI dica = null;
            foreach (GameObject raiz in cena.GetRootGameObjects())
            {
                dica = raiz.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "DicaClique");
                if (dica != null) break;
            }
            if (dica == null)
            {
                log.Add("ERRO: DicaClique não encontrado na IA_Aplicacao.");
                return;
            }

            Transform canvas = dica.transform.parent;
            Transform velha = canvas.Find("PlacaMissao");
            if (velha != null) Undo.DestroyObjectImmediate(velha.gameObject);

            TemaLaboratorio tema = TemaLabUI.Tema;
            Image placa = TemaLabUI.Imagem(canvas, "PlacaMissao", tema != null ? tema.painelLinha : null, TemaLabUI.Painel);
            Undo.RegisterCreatedObjectUndo(placa.gameObject, "Placa Missão");
            // Ao lado da placa VIDA (canto de cima à esquerda), na mesma altura.
            TemaLabUI.Posicionar(placa.rectTransform, new Vector2(0.5f, 1f), new Vector2(60f, -36f), new Vector2(1180f, 96f));
            placa.transform.SetSiblingIndex(dica.transform.GetSiblingIndex());

            TextMeshProUGUI rotulo = TemaLabUI.Rotulo(placa.transform, "Rotulo", "Missão", 18f, TemaLabUI.Dourado);
            TemaLabUI.Posicionar(rotulo.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(150f, 30f));

            // O texto antigo vira o texto da placa (mesmo objeto, outro lugar).
            Undo.SetTransformParent(dica.transform, placa.transform, "Placa Missão");
            Undo.RecordObject(dica, "Placa Missão");
            dica.text = "Clique no guarda ou no aliado para abrir o cérebro deles.";
            dica.font = TemaLabUI.FonteDe(TemaLabUI.Fonte.CorpoForte);
            dica.color = TemaLabUI.CorTexto;
            dica.fontSize = 26f;
            dica.enableAutoSizing = true;
            dica.fontSizeMin = 18f;
            dica.fontSizeMax = 26f;
            dica.alignment = TextAlignmentOptions.MidlineLeft;
            dica.textWrappingMode = TextWrappingModes.NoWrap;
            dica.fontStyle = FontStyles.Normal;
            RectTransform r = TemaLabUI.Esticar(dica.rectTransform);
            r.offsetMin = new Vector2(160f, 8f);
            r.offsetMax = new Vector2(-250f, -8f);

            RectTransform tecla = TemaLabUI.CaixaArredondada(placa.transform, "Tecla", TemaLabUI.Contorno, TemaLabUI.PainelElevado, 3f);
            RectTransform caixa = tecla;
            while (caixa.parent != placa.transform) caixa = (RectTransform)caixa.parent;
            TemaLabUI.Posicionar(caixa, new Vector2(1f, 0.5f), new Vector2(-200f, 0f), new Vector2(46f, 46f));
            foreach (RectTransform filho in caixa) TemaLabUI.Esticar(filho);
            TextMeshProUGUI k = TemaLabUI.Texto(tecla, "K", "R", 24f, TemaLabUI.CorTexto, TemaLabUI.Fonte.MonoForte, TextAlignmentOptions.Center);
            TemaLabUI.Esticar(k.rectTransform);

            TextMeshProUGUI acao = TemaLabUI.Texto(placa.transform, "Acao", "Recomeçar", 24f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.CorpoForte);
            acao.alignment = TextAlignmentOptions.MidlineLeft;
            acao.textWrappingMode = TextWrappingModes.NoWrap;
            TemaLabUI.Posicionar(acao.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(160f, 40f));

            log.Add("IA_Aplicacao: placa Missão salva.");
        });
        return log;
    }

    // ------------------------------------------------------------------ cenas

    private static void NaCena(string path, List<string> log, Action<Scene> montar)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            log.Add("ERRO: saia do Play Mode antes.");
            return;
        }

        Scene cena = SceneManager.GetSceneByPath(path);
        bool abriAqui = false;
        if (!cena.isLoaded)
        {
            cena = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            abriAqui = true;
        }
        else if (cena.isDirty)
        {
            log.Add($"ERRO: {cena.name} tem mudanças não salvas. Salve ou descarte antes.");
            return;
        }

        montar(cena);
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        log.Add($"{cena.name} salva.");
        if (abriAqui) EditorSceneManager.CloseScene(cena, true);
    }
}
