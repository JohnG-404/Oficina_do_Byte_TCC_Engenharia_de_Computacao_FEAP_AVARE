using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Preenche e salva na cena Interacao_Vida os
/// dados do puzzle da HUD na moldura dos puzzles (HudLicao → HudPuzzleUI): ícone,
/// moldura, categoria e linha curta de cada cartão, o "bloco" de cada script
/// e os textos novos do cabeçalho, da Prévia e da Dica.
///
/// O puzzle continua montado em runtime (como o do Animator); o que fica na
/// cena é o que dá pra ver e ajustar no Inspector da HudLicao. Pode rodar de
/// novo: só reescreve esses campos. Recusa com Play ligado ou cena suja e faz
/// backup da cena antes de salvar.
/// </summary>
public static class HudPuzzleMolduraBuilder
{
    private const string ScenePath = "Assets/Scenes/Interacao_Vida.unity";
    private const string Pecas = "Assets/Images/EditorKit/pecas/";
    private const string Icones = "Assets/Images/EditorKit/icones/categorias/";

    private static readonly Color Lima = new Color(0.6392157f, 0.9019608f, 0.20784314f);
    private static readonly Color Azul = new Color(0.23921569f, 0.64705884f, 1f);
    private static readonly Color Rosa = new Color(0.95686275f, 0.44705883f, 0.7137255f);
    private static readonly Color Anil = new Color(0.55f, 0.58f, 1f);

    private struct Cartao
    {
        public string moldura, icone, categoria, subtitulo;
        public Color cor;
        public bool iconeVida;
    }

    // As linhas curtas não dizem em qual script a peça fica, pra não entregar a resposta.
    private static readonly Dictionary<string, Cartao> Cartoes = new Dictionary<string, Cartao>
    {
        { "vidaAtual", new Cartao { moldura = "peca_velocity", iconeVida = true, categoria = "CAMPO", subtitulo = "o número da vida", cor = Anil } },
        { "OnHealthChanged", new Cartao { moldura = "peca_input", icone = "icone_ontriggerenter2d_rosa", categoria = "EVENTO", subtitulo = "avisa quem ouve", cor = Rosa } },
        { "AtualizarQuadrados", new Cartao { moldura = "peca_update", icone = "icone_update_lima", categoria = "MÉTODO", subtitulo = "redesenha a barra", cor = Lima } },
        { "Start", new Cartao { moldura = "peca_update", icone = "icone_start_lima", categoria = "MÉTODO", subtitulo = "roda uma vez", cor = Lima } },
        { "Destroy(barra)", new Cartao { moldura = "peca_transform", icone = "icone_destroy_azul", categoria = "MÉTODO", subtitulo = "apaga a barra", cor = Azul } },
    };

    private static readonly Dictionary<string, string> Blocos = new Dictionary<string, string>
    {
        { "PlayerHealth", "o dado e o evento" },
        { "HUD", "a tela" },
    };

    [MenuItem("Tools/Fases/Puzzle da HUD na moldura (item 6)")]
    public static void BuildMenu()
    {
        foreach (string linha in Build()) Debug.Log("[HudPuzzleMolduraBuilder] " + linha);
    }

    public static List<string> Build()
    {
        var log = new List<string>();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            log.Add("ERRO: saia do Play antes de rodar.");
            return log;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                log.Add($"ERRO: a cena {SceneManager.GetSceneAt(i).path} tem mudanças não salvas.");
                return log;
            }
        }

        string anterior = SceneManager.GetActiveScene().path;
        Scene cena = SceneManager.GetSceneByPath(ScenePath);
        bool abriu = false;
        if (!cena.IsValid() || !cena.isLoaded)
        {
            cena = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            abriu = true;
        }

        HudLicao licao = cena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HudLicao>(true)).FirstOrDefault();
        if (licao == null)
        {
            log.Add("ERRO: HudLicao não encontrada na Interacao_Vida.");
            if (abriu) EditorSceneManager.CloseScene(cena, true);
            return log;
        }

        string pasta = $"SceneBackups/item6_builder_{System.DateTime.Now:yyyy-MM-dd_HHmmss}";
        Directory.CreateDirectory(pasta);
        File.Copy(ScenePath, Path.Combine(pasta, "Interacao_Vida.unity.bak"), true);
        log.Add("Backup: " + pasta);

        Sprite vidaCheia = TemaLaboratorio.Atual != null ? TemaLaboratorio.Atual.vidaCheia : null;
        var so = new SerializedObject(licao);

        SerializedProperty pecas = so.FindProperty("pecas");
        for (int i = 0; i < pecas.arraySize; i++)
        {
            SerializedProperty p = pecas.GetArrayElementAtIndex(i);
            string nome = p.FindPropertyRelative("nome").stringValue;
            if (!Cartoes.TryGetValue(nome, out Cartao c))
            {
                log.Add($"AVISO: peça sem cartão definido: {nome}");
                continue;
            }

            p.FindPropertyRelative("moldura").objectReferenceValue = Carregar(Pecas + c.moldura + ".png", log);
            p.FindPropertyRelative("icone").objectReferenceValue = c.iconeVida ? vidaCheia : Carregar(Icones + c.icone + ".png", log);
            p.FindPropertyRelative("categoria").stringValue = c.categoria;
            p.FindPropertyRelative("corCategoria").colorValue = c.cor;
            p.FindPropertyRelative("subtitulo").stringValue = c.subtitulo;
            log.Add($"Cartão {nome}: {c.categoria}");
        }

        SerializedProperty encaixes = so.FindProperty("encaixes");
        for (int i = 0; i < encaixes.arraySize; i++)
        {
            SerializedProperty e = encaixes.GetArrayElementAtIndex(i);
            if (Blocos.TryGetValue(e.FindPropertyRelative("legenda").stringValue, out string bloco))
            {
                e.FindPropertyRelative("bloco").stringValue = bloco;
            }
        }

        // Textos: os valores padrão do HudPuzzleUI.Textos (os antigos da cena ficam
        // só onde o texto não mudou de sentido).
        var padrao = new HudPuzzleUI.Textos();
        SerializedProperty t = so.FindProperty("textosPuzzle");
        Texto(t, "fase", padrao.fase);
        Texto(t, "etapa", padrao.etapa);
        Texto(t, "aba", padrao.aba);
        Texto(t, "comentario", padrao.comentario);
        Texto(t, "barraParada", padrao.barraParada);
        Texto(t, "barraConectada", padrao.barraConectada);
        Texto(t, "textoVida", padrao.textoVida);
        Texto(t, "textoBarraParada", padrao.textoBarraParada);
        Texto(t, "textoBarraConectada", padrao.textoBarraConectada);
        Texto(t, "objetivo", padrao.objetivo);
        Texto(t, "progressoSub", padrao.progressoSub);
        Texto(t, "instrucao", padrao.instrucao);
        Texto(t, "descricaoPadrao", padrao.descricaoPadrao);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        log.Add("Interacao_Vida salva.");

        if (abriu && cena.path != anterior)
        {
            EditorSceneManager.CloseScene(cena, true);
        }
        return log;
    }

    private static void Texto(SerializedProperty textos, string campo, string valor)
    {
        SerializedProperty p = textos.FindPropertyRelative(campo);
        if (p != null) p.stringValue = valor;
    }

    private static Sprite Carregar(string caminho, List<string> log)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
        if (s == null) log.Add("AVISO: sprite não encontrado: " + caminho);
        return s;
    }
}
