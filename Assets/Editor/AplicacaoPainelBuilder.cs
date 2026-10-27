using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Liga o painel de componentes ao passar o mouse (PainelComponentes) na cena
/// Aplicacao_Fisica:
/// - Tira o que sobrou das ideias anteriores (fantasma, selos, holograma,
///   mini Inspector), inclusive componentes cujo script já foi apagado. Os
///   objetos ficam sem contorno: o painel é o único retorno visual.
/// - Monta o painel no Canvas, no mesmo estilo do inventário.
/// - Avisa na fala de abertura do Prof Byte que dá pra passar o mouse.
///
/// Pode rodar de novo: refaz o painel.
/// </summary>
public static class AplicacaoPainelBuilder
{
    private const string ScenePath = "Assets/Scenes/Aplicacao_Fisica.unity";
    private const string SpriteDir = "Assets/Art/UI/Aplicacao/";
    private const string FontDir = "Assets/Art/UI/Fonts/";
    private const string IconDir = "Assets/Images/Scene_Col_Rig/";
    private const string NomePainel = "PainelComponentes";
    private const string FalaMouse = "Passe o mouse em cima de um objeto pra ver o que ele já tem e o que ainda pode receber.";

    private static readonly string[] ComponentesAntigos = { "ColisaoFantasma", "SelosComponentes", "Materializacao" };
    private static readonly ToolType[] Ordem = { ToolType.Colisao, ToolType.Fisica, ToolType.Movimento, ToolType.Pulo };

    private const float Largura = 360f;
    private static readonly Color Painel = Hex("141B29");
    private static readonly Color PainelBorda = Hex("2E323C");
    private static readonly Color Pilula = Hex("1E2026");
    private static readonly Color Texto = Hex("E6E9EF");
    private static readonly Color TextoApagado = Hex("8A93A6");
    private static readonly Color Fraco = Hex("5C6475");
    private static readonly Color FraseApagada = Hex("6B7385");

    [MenuItem("Tools/Aplicação/Montar Painel de Componentes")]
    public static void BuildMenu()
    {
        foreach (string line in Build()) Debug.Log("[AplicacaoPainelBuilder] " + line);
    }

    public static List<string> Build()
    {
        var log = new List<string>();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            log.Add("ERRO: abra a cena Aplicacao_Fisica antes de rodar.");
            return log;
        }

        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            log.AddRange(LimparAntigos(raiz));
        }

        log.AddRange(MontarPainel(scene));
        log.Add(ExplicarNaAbertura(scene));
        EditorSceneManager.MarkSceneDirty(scene);
        return log;
    }

    private static List<string> LimparAntigos(GameObject raiz)
    {
        var log = new List<string>();
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            GameObject go = t.gameObject;
            int faltando = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (faltando > 0)
            {
                log.Add("script apagado removido: " + go.name);
            }

            foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb != null && System.Array.IndexOf(ComponentesAntigos, mb.GetType().Name) >= 0)
                {
                    log.Add("removido " + mb.GetType().Name + ": " + go.name);
                    Undo.DestroyObjectImmediate(mb);
                }
            }
        }

        if (raiz.GetComponent<Canvas>() != null)
        {
            Transform mini = raiz.transform.Find("MiniInspector");
            if (mini != null)
            {
                Undo.DestroyObjectImmediate(mini.gameObject);
                log.Add("MiniInspector removido do Canvas");
            }
        }

        return log;
    }

    // ------------------------------------------------------------- painel

    private static List<string> MontarPainel(Scene scene)
    {
        var log = new List<string>();
        Canvas canvas = scene.GetRootGameObjects().Select(g => g.GetComponent<Canvas>()).FirstOrDefault(c => c != null);
        if (canvas == null)
        {
            log.Add("ERRO: Canvas não encontrado.");
            return log;
        }

        Transform velho = canvas.transform.Find(NomePainel);
        if (velho != null)
        {
            Undo.DestroyObjectImmediate(velho.gameObject);
        }

        RectTransform raiz = Novo(NomePainel, canvas.transform);
        Undo.RegisterCreatedObjectUndo(raiz.gameObject, "Painel de Componentes");
        Esticar(raiz);
        raiz.SetAsLastSibling();

        RectTransform caixa = Novo("Caixa", raiz);
        caixa.anchorMin = caixa.anchorMax = new Vector2(0.5f, 0.5f);
        caixa.pivot = new Vector2(0f, 1f);
        caixa.sizeDelta = new Vector2(Largura, 300f);
        Moldura(caixa, Painel, PainelBorda, 1f);

        TMP_Text titulo = Rotulo(caixa, "Titulo", "Plataforma1", "Nunito-ExtraBold SDF", 20, Texto, new Vector2(16f, 10f), new Vector2(220f, 30f));

        RectTransform tipo = Novo("Tipo", caixa);
        Canto(tipo, new Vector2(Largura - 16f - 73f, 13f), new Vector2(73f, 24f));
        Pilula_(tipo, Pilula, PainelBorda);
        TMP_Text tipoTexto = Rotulo(tipo, "Texto", "objeto", "Nunito-Bold SDF", 13, TextoApagado, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        Esticar(tipoTexto.rectTransform);

        RectTransform sep = Novo("Separador", caixa);
        Canto(sep, new Vector2(14f, 50f), new Vector2(Largura - 28f, 1f));
        Img(sep, null, PainelBorda, 1f);

        RectTransform rotuloAplicados = Rotulo(caixa, "RotuloAplicados", "APLICADOS", "JetBrainsMono-Bold SDF", 12, Fraco, new Vector2(16f, 63f), new Vector2(200f, 18f)).rectTransform;
        RectTransform vazioAplicados = Rotulo(caixa, "NenhumAplicado", "nenhum ainda", "Nunito-Bold SDF", 14, FraseApagada, new Vector2(16f, 85f), new Vector2(Largura - 32f, 24f)).rectTransform;
        RectTransform rotuloPode = Rotulo(caixa, "RotuloPodeAplicar", "PODE APLICAR", "JetBrainsMono-Bold SDF", 12, Fraco, new Vector2(16f, 123f), new Vector2(200f, 18f)).rectTransform;
        RectTransform vazioPode = Rotulo(caixa, "NadaParaAplicar", "nada mais para aplicar", "Nunito-Bold SDF", 14, FraseApagada, new Vector2(16f, 145f), new Vector2(Largura - 32f, 24f)).rectTransform;

        var linhas = new List<PainelComponentes.Linha>();
        for (int i = 0; i < Ordem.Length; i++)
        {
            linhas.Add(MontarLinha(caixa, Ordem[i], 145f + i * 50f));
        }

        RectTransform sep2 = Novo("SeparadorRodape", caixa);
        Canto(sep2, new Vector2(14f, 350f), new Vector2(Largura - 28f, 1f));
        Img(sep2, null, PainelBorda, 1f);
        TMP_Text rodape = Rotulo(caixa, "Rodape", "Sem Collider2D: o Player atravessa.", "Nunito-Bold SDF", 14, TextoApagado, new Vector2(16f, 351f), new Vector2(Largura - 32f, 38f));
        caixa.gameObject.SetActive(false);

        var painel = raiz.gameObject.AddComponent<PainelComponentes>();
        var so = new SerializedObject(painel);
        so.FindProperty("caixa").objectReferenceValue = caixa;
        so.FindProperty("titulo").objectReferenceValue = titulo;
        so.FindProperty("tipo").objectReferenceValue = tipo;
        so.FindProperty("tipoTexto").objectReferenceValue = tipoTexto;
        so.FindProperty("rotuloAplicados").objectReferenceValue = rotuloAplicados;
        so.FindProperty("vazioAplicados").objectReferenceValue = vazioAplicados;
        so.FindProperty("rotuloPodeAplicar").objectReferenceValue = rotuloPode;
        so.FindProperty("vazioPodeAplicar").objectReferenceValue = vazioPode;
        so.FindProperty("separadorRodape").objectReferenceValue = sep2;
        so.FindProperty("rodape").objectReferenceValue = rodape;
        SerializedProperty arr = so.FindProperty("linhas");
        arr.arraySize = linhas.Count;
        for (int i = 0; i < linhas.Count; i++)
        {
            PainelComponentes.Linha l = linhas[i];
            SerializedProperty e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("ferramenta").intValue = (int)l.ferramenta;
            e.FindPropertyRelative("raiz").objectReferenceValue = l.raiz;
            e.FindPropertyRelative("fundo").objectReferenceValue = l.fundo;
            e.FindPropertyRelative("borda").objectReferenceValue = l.borda;
            e.FindPropertyRelative("icone").objectReferenceValue = l.icone;
            e.FindPropertyRelative("nome").objectReferenceValue = l.nome;
            e.FindPropertyRelative("frase").objectReferenceValue = l.frase;
            e.FindPropertyRelative("estado").objectReferenceValue = l.estado;
            e.FindPropertyRelative("estadoFundo").objectReferenceValue = l.estadoFundo;
            e.FindPropertyRelative("estadoBorda").objectReferenceValue = l.estadoBorda;
            e.FindPropertyRelative("estadoTexto").objectReferenceValue = l.estadoTexto;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("PainelComponentes montado no Canvas " + canvas.name);
        return log;
    }

    private static PainelComponentes.Linha MontarLinha(RectTransform caixa, ToolType t, float y)
    {
        string nome = DraggableToolIcon.NomeNoUnity(t);
        RectTransform raiz = Novo("Linha_" + nome, caixa);
        Canto(raiz, new Vector2(14f, y), new Vector2(Largura - 28f, 44f));
        (Image fundo, Image borda) = Moldura(raiz, Hex("0C111B"), PainelBorda, 1.5f);

        RectTransform iconeRt = Novo("Icone", raiz);
        Canto(iconeRt, new Vector2(8f, 5f), new Vector2(34f, 34f));
        var icone = iconeRt.gameObject.AddComponent<Image>();
        icone.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + Icone(t) + ".png");
        icone.preserveAspect = true;
        icone.raycastTarget = false;

        TMP_Text nomeTxt = Rotulo(raiz, "Nome", nome, "JetBrainsMono-Bold SDF", 15, TextoApagado, new Vector2(52f, 3f), new Vector2(200f, 20f));
        TMP_Text frase = Rotulo(raiz, "Frase", "", "Nunito-Bold SDF", 13, FraseApagada, new Vector2(52f, 22f), new Vector2(220f, 18f));

        RectTransform estado = Novo("Estado", raiz);
        Canto(estado, new Vector2(Largura - 28f - 74f - 10f, 10f), new Vector2(74f, 24f));
        (Image estadoFundo, Image estadoBorda) = Pilula_(estado, Pilula, PainelBorda);
        TMP_Text estadoTexto = Rotulo(estado, "Texto", "ARRASTE", "JetBrainsMono-Bold SDF", 11, Fraco, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        Esticar(estadoTexto.rectTransform);

        return new PainelComponentes.Linha
        {
            ferramenta = t, raiz = raiz, fundo = fundo, borda = borda, icone = icone, nome = nomeTxt, frase = frase,
            estado = estado, estadoFundo = estadoFundo, estadoBorda = estadoBorda, estadoTexto = estadoTexto,
        };
    }

    private static string Icone(ToolType t)
    {
        switch (t)
        {
            case ToolType.Fisica: return "icon_rigidbody2d_v2";
            case ToolType.Colisao: return "icon_collider2d_v2";
            case ToolType.Movimento: return "icon_movement_v2";
            default: return "icon_jump";
        }
    }

    // ------------------------------------------------------------ abertura

    private static string ExplicarNaAbertura(Scene scene)
    {
        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            foreach (AplicacaoLevelFlow flow in raiz.GetComponentsInChildren<AplicacaoLevelFlow>(true))
            {
                var so = new SerializedObject(flow);
                SerializedProperty falas = so.FindProperty("introDialogue");
                for (int i = 0; i < falas.arraySize; i++)
                {
                    if (falas.GetArrayElementAtIndex(i).stringValue == FalaMouse)
                    {
                        return "fala do mouse já estava na abertura";
                    }
                }

                // Logo depois da fala do inventário (ou no fim).
                int pos = falas.arraySize;
                for (int i = 0; i < falas.arraySize; i++)
                {
                    if (falas.GetArrayElementAtIndex(i).stringValue.Contains("inventário"))
                    {
                        pos = i + 1;
                    }
                }

                if (pos >= falas.arraySize)
                {
                    falas.arraySize++;
                    pos = falas.arraySize - 1;
                }
                else
                {
                    falas.InsertArrayElementAtIndex(pos);
                }

                falas.GetArrayElementAtIndex(pos).stringValue = FalaMouse;
                so.ApplyModifiedProperties();
                return "fala do mouse incluída na abertura (posição " + pos + ")";
            }
        }

        return "AVISO: AplicacaoLevelFlow não encontrado";
    }

    // ---------------------------------------------------------------- helpers

    private static Sprite S(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + file);

    private static RectTransform Novo(string nome, Transform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        return rt;
    }

    // Posição a partir do canto superior esquerdo, com y para baixo.
    private static void Canto(RectTransform rt, Vector2 pos, Vector2 tam)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(pos.x, -pos.y);
        rt.sizeDelta = tam;
    }

    private static void Esticar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Image Img(RectTransform rt, Sprite sprite, Color cor, float ppu)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = cor;
        img.raycastTarget = false;
        if (sprite != null)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppu;
        }
        return img;
    }

    private static (Image, Image) Moldura(RectTransform pai, Color fundo, Color borda, float ppu)
    {
        RectTransform f = Novo("Fundo", pai);
        Esticar(f);
        RectTransform b = Novo("Borda", pai);
        Esticar(b);
        return (Img(f, S("inv_arredondado.png"), fundo, ppu), Img(b, S("inv_borda.png"), borda, ppu));
    }

    private static (Image, Image) Pilula_(RectTransform pai, Color fundo, Color borda)
    {
        RectTransform f = Novo("Fundo", pai);
        Esticar(f);
        RectTransform b = Novo("Borda", pai);
        Esticar(b);
        return (Img(f, S("inv_pilula.png"), fundo, 1f), Img(b, S("inv_pilula_borda.png"), borda, 1f));
    }

    private static TMP_Text Rotulo(RectTransform pai, string nome, string texto, string fonte, float tamanho, Color cor, Vector2 pos, Vector2 tam, TextAlignmentOptions alinhamento = TextAlignmentOptions.MidlineLeft)
    {
        RectTransform rt = Novo(nome, pai);
        Canto(rt, pos, tam);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + fonte + ".asset");
        tmp.text = texto;
        tmp.fontSize = tamanho;
        tmp.color = cor;
        tmp.alignment = alinhamento;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Color Hex(string hex, float a = 1f)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        c.a = a;
        return c;
    }
}
