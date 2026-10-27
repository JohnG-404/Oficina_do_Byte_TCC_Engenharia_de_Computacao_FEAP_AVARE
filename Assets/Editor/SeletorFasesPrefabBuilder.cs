using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gera o prefab editável da tela "Fases" (Assets/Resources/SeletorFases.prefab)
/// com o visual padrão, já com o SeletorFasesUI e o cartão-modelo
/// (CartaoFaseUI) ligados. Depois disso o design é todo seu: abra o prefab e
/// mude posições, sprites, fontes, cores e a grade.
///
/// "Criar" não sobrescreve um prefab que já existe (pra não perder suas
/// edições); "Recriar" volta ao visual padrão.
/// </summary>
public static class SeletorFasesPrefabBuilder
{
    private const string PrefabPath = "Assets/Resources/SeletorFases.prefab";
    private const string CadeadoPath = "Assets/Images/EditorKit/icones/interface/icone_cadeado@2x.png";
    private const string ConcluidaPath = "Assets/Images/EditorKit/icones/interface/icone_confirmar@2x.png";

    private static readonly Color Fundo = new Color32(0x14, 0x17, 0x1E, 0xF5);
    private static readonly Color Cartao = new Color32(0x23, 0x27, 0x33, 0xFF);
    private static readonly Color CartaoBloqueado = new Color32(0x1B, 0x1E, 0x26, 0xFF);
    private static readonly Color Destaque = new Color32(0x4C, 0x8D, 0xFF, 0xFF);
    private static readonly Color Verde = new Color32(0x4F, 0xC8, 0x73, 0xFF);
    private static readonly Color Cinza = new Color32(0x4A, 0x50, 0x5E, 0xFF);
    private static readonly Color Botao = new Color32(0x2E, 0x33, 0x40, 0xFF);
    private static readonly Color BotaoPerigo = new Color32(0x8A, 0x2E, 0x2E, 0xFF);
    private static readonly Color TextoApagado = new Color32(0x9A, 0xA3, 0xB2, 0xFF);
    private static readonly Color Amarelo = new Color32(0xF5, 0xC5, 0x42, 0xFF);

    [MenuItem("Tools/Menu/Criar Prefab do Seletor de Fases")]
    public static void CriarMenu()
    {
        if (File.Exists(PrefabPath))
        {
            Debug.LogWarning("[SeletorFasesPrefabBuilder] O prefab já existe em " + PrefabPath + " — não foi mexido. Use 'Recriar' pra voltar ao visual padrão.");
            return;
        }
        Criar();
    }

    [MenuItem("Tools/Menu/Recriar Prefab do Seletor de Fases (volta ao padrão)")]
    public static void RecriarMenu()
    {
        if (File.Exists(PrefabPath) && !EditorUtility.DisplayDialog("Recriar prefab",
            "Isso apaga as edições feitas no SeletorFases.prefab e volta ao visual padrão. Continuar?", "Recriar", "Cancelar"))
        {
            return;
        }
        Criar();
    }

    public static void Criar()
    {
        // Monta numa cena de preview, pra não sujar a cena aberta.
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject raiz = Montar();
            SceneManager.MoveGameObjectToScene(raiz, preview);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabPath, out bool ok);
            Object.DestroyImmediate(raiz);
            Debug.Log(ok
                ? "[SeletorFasesPrefabBuilder] Prefab salvo em " + PrefabPath + ". Abra ele pra editar o visual."
                : "[SeletorFasesPrefabBuilder] ERRO ao salvar o prefab.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static GameObject Montar()
    {
        var raiz = new GameObject("SeletorFases", typeof(RectTransform));
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = raiz.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        raiz.AddComponent<GraphicRaycaster>();
        var ui = raiz.AddComponent<SeletorFasesUI>();

        RectTransform fundo = Rect("Fundo", raiz.transform);
        Esticar(fundo);
        fundo.gameObject.AddComponent<Image>().color = Fundo;

        Texto(fundo, "Titulo", "Fases", 64, FontStyles.Bold, Color.white, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(120f, -60f), new Vector2(900f, 80f));
        TMP_Text subtitulo = Texto(fundo, "Subtitulo", "2 de 8 fases concluídas", 28, FontStyles.Normal, TextoApagado, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(120f, -140f), new Vector2(1400f, 44f));

        RectTransform grade = Rect("Grade", fundo);
        grade.anchorMin = Vector2.zero;
        grade.anchorMax = Vector2.one;
        grade.offsetMin = new Vector2(120f, 190f);
        grade.offsetMax = new Vector2(-120f, -200f);
        var layout = grade.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(310f, 300f);
        layout.spacing = new Vector2(24f, 24f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;
        layout.childAlignment = TextAnchor.UpperCenter;

        TMP_Text aviso = Texto(fundo, "Aviso", "", 28, FontStyles.Italic, Amarelo, TextAlignmentOptions.Left,
            new Vector2(0f, 0f), new Vector2(120f, 110f), new Vector2(1100f, 60f));

        Button voltar = Botao_(fundo, "Voltar", "Voltar", Botao, new Vector2(1f, 0f), new Vector2(-704f, 80f));
        Button apagar = Botao_(fundo, "ApagarProgresso", "Apagar progresso", BotaoPerigo, new Vector2(1f, 0f), new Vector2(-400f, 80f));

        // Confirmação
        RectTransform conf = Rect("Confirmacao", fundo);
        Esticar(conf);
        conf.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        RectTransform caixa = Rect("Caixa", conf);
        caixa.anchorMin = caixa.anchorMax = new Vector2(0.5f, 0.5f);
        caixa.sizeDelta = new Vector2(900f, 360f);
        caixa.gameObject.AddComponent<Image>().color = Cartao;
        Texto(caixa, "Pergunta", "Apagar todo o progresso?", 44, FontStyles.Bold, Color.white, TextAlignmentOptions.Top,
            new Vector2(0.5f, 1f), new Vector2(-400f, -40f), new Vector2(800f, 60f));
        Texto(caixa, "Detalhe", "As fases voltam a ficar bloqueadas e o questionário inicial volta a aparecer. Volume, teclas e tela continuam como estão.",
            28, FontStyles.Normal, TextoApagado, TextAlignmentOptions.Top, new Vector2(0.5f, 1f), new Vector2(-400f, -110f), new Vector2(800f, 120f));
        Button cancelar = Botao_(caixa, "Cancelar", "Cancelar", Botao, new Vector2(0.5f, 0f), new Vector2(-300f, 40f));
        Button confirmar = Botao_(caixa, "Apagar", "Apagar", BotaoPerigo, new Vector2(0.5f, 0f), new Vector2(20f, 40f));
        conf.gameObject.SetActive(false);

        // Cartão-modelo (fora da Grade, desligado)
        RectTransform modelos = Rect("Modelos", raiz.transform);
        modelos.anchorMin = modelos.anchorMax = new Vector2(0.5f, 0.5f);
        CartaoFaseUI cartao = MontarCartao(modelos);
        cartao.gameObject.SetActive(false);

        var so = new SerializedObject(ui);
        so.FindProperty("grade").objectReferenceValue = grade;
        so.FindProperty("cartaoModelo").objectReferenceValue = cartao;
        so.FindProperty("subtitulo").objectReferenceValue = subtitulo;
        so.FindProperty("aviso").objectReferenceValue = aviso;
        so.FindProperty("botaoVoltar").objectReferenceValue = voltar;
        so.FindProperty("botaoApagar").objectReferenceValue = apagar;
        so.FindProperty("confirmacao").objectReferenceValue = conf.gameObject;
        so.FindProperty("botaoCancelar").objectReferenceValue = cancelar;
        so.FindProperty("botaoConfirmarApagar").objectReferenceValue = confirmar;
        so.ApplyModifiedPropertiesWithoutUndo();

        return raiz;
    }

    private static CartaoFaseUI MontarCartao(Transform pai)
    {
        RectTransform rt = Rect("CartaoFase", pai);
        rt.sizeDelta = new Vector2(310f, 300f);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = Cartao;
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(3f, -3f);
        var botao = rt.gameObject.AddComponent<Button>();
        var cores = botao.colors;
        cores.highlightedColor = new Color(0.82f, 0.88f, 1f, 1f);
        cores.selectedColor = cores.highlightedColor;
        cores.pressedColor = new Color(0.7f, 0.75f, 0.85f, 1f);
        botao.colors = cores;

        TMP_Text numero = Texto(rt, "Numero", "1", 56, FontStyles.Bold, Destaque, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(22f, -16f), new Vector2(160f, 70f));
        TMP_Text titulo = Texto(rt, "Titulo", "Nome da fase", 30, FontStyles.Bold, Color.white, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(22f, -90f), new Vector2(266f, 80f));
        TMP_Text conceito = Texto(rt, "Conceito", "O que a fase ensina", 21, FontStyles.Normal, TextoApagado, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(22f, -168f), new Vector2(266f, 60f));
        TMP_Text status = Texto(rt, "Status", "Disponível", 21, FontStyles.Bold, Destaque, TextAlignmentOptions.BottomLeft,
            new Vector2(0f, 0f), new Vector2(22f, 16f), new Vector2(266f, 70f));

        GameObject cadeado = Icone(rt, "IconeBloqueada", CadeadoPath, Cinza);
        GameObject concluida = Icone(rt, "IconeConcluida", ConcluidaPath, Verde);

        var c = rt.gameObject.AddComponent<CartaoFaseUI>();
        var so = new SerializedObject(c);
        so.FindProperty("fundo").objectReferenceValue = img;
        so.FindProperty("borda").objectReferenceValue = outline;
        so.FindProperty("botao").objectReferenceValue = botao;
        so.FindProperty("numero").objectReferenceValue = numero;
        so.FindProperty("titulo").objectReferenceValue = titulo;
        so.FindProperty("conceito").objectReferenceValue = conceito;
        so.FindProperty("status").objectReferenceValue = status;
        so.FindProperty("iconeBloqueada").objectReferenceValue = cadeado;
        so.FindProperty("iconeConcluida").objectReferenceValue = concluida;
        Estilo(so.FindProperty("estiloBloqueada"), CartaoBloqueado, Cinza, Cinza, TextoApagado, TextoApagado, Cinza);
        Estilo(so.FindProperty("estiloDisponivel"), Cartao, Destaque, Destaque, Color.white, TextoApagado, Destaque);
        Estilo(so.FindProperty("estiloConcluida"), Cartao, Verde, Verde, Color.white, TextoApagado, Verde);
        so.ApplyModifiedPropertiesWithoutUndo();
        return c;
    }

    private static void Estilo(SerializedProperty p, Color fundo, Color borda, Color numero, Color titulo, Color conceito, Color status)
    {
        p.FindPropertyRelative("fundo").colorValue = fundo;
        p.FindPropertyRelative("borda").colorValue = borda;
        p.FindPropertyRelative("numero").colorValue = numero;
        p.FindPropertyRelative("titulo").colorValue = titulo;
        p.FindPropertyRelative("conceito").colorValue = conceito;
        p.FindPropertyRelative("status").colorValue = status;
    }

    private static GameObject Icone(RectTransform pai, string nome, string spritePath, Color cor)
    {
        RectTransform rt = Rect(nome, pai);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-18f, -18f);
        rt.sizeDelta = new Vector2(44f, 44f);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        img.color = cor;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return rt.gameObject;
    }

    // ------------------------------------------------------------------

    private static RectTransform Rect(string nome, Transform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        return (RectTransform)go.transform;
    }

    private static void Esticar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Âncora num ponto e posiciona pelo canto do elemento do mesmo lado.
    private static void Posicionar(RectTransform rt, Vector2 ancora, Vector2 posicao, Vector2 tamanho)
    {
        rt.anchorMin = rt.anchorMax = ancora;
        rt.pivot = new Vector2(0f, ancora.y);
        rt.anchoredPosition = posicao;
        rt.sizeDelta = tamanho;
    }

    private static TMP_Text Texto(Transform pai, string nome, string texto, float tamanho, FontStyles estilo, Color cor,
        TextAlignmentOptions alinhamento, Vector2 ancora, Vector2 posicao, Vector2 dimensao)
    {
        RectTransform rt = Rect(nome, pai);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.text = texto;
        t.fontSize = tamanho;
        t.fontStyle = estilo;
        t.color = cor;
        t.alignment = alinhamento;
        t.richText = true;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        Posicionar(rt, ancora, posicao, dimensao);
        return t;
    }

    private static Button Botao_(Transform pai, string nome, string rotulo, Color cor, Vector2 ancora, Vector2 posicao)
    {
        RectTransform rt = Rect(nome, pai);
        Posicionar(rt, ancora, posicao, new Vector2(280f, 80f));
        rt.gameObject.AddComponent<Image>().color = cor;
        var b = rt.gameObject.AddComponent<Button>();
        var cores = b.colors;
        cores.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cores.selectedColor = cores.highlightedColor;
        b.colors = cores;

        TMP_Text label = Texto(rt, "Label", rotulo, 30, FontStyles.Normal, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.zero, Vector2.zero);
        Esticar(label.rectTransform);
        return b;
    }
}
