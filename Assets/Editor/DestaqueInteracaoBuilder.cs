using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Monta nas cenas Interacao e Interacao_Vida o destaque dos itens
/// configuráveis (DestaqueItensInteracao): o painel que aparece ao passar o
/// mouse, no estilo do PainelComponentes da Física, e uma etiqueta
/// "Ação → Consequência" por item, que só aparece depois de configurado.
///
/// Na Interacao_Vida a etiqueta tem duas
/// linhas, fica embaixo das plataformas falsas, não sai da tela e fica
/// transparente com o jogador atrás. A Interacao continua igual.
///
/// Monta as cenas que estiverem abertas e marca como modificadas (salvar com
/// Ctrl+S). Pode rodar de novo: refaz tudo.
/// </summary>
public static class DestaqueInteracaoBuilder
{
    private const string ScenePath = "Assets/Scenes/Interacao.unity";
    private const string VidaPath = "Assets/Scenes/Interacao_Vida.unity";
    private const string SpriteDir = "Assets/Art/UI/Aplicacao/";
    private const string FontDir = "Assets/Art/UI/Fonts/";
    private const string NomeRaiz = "DestaqueItens";

    private const float Largura = 400f;
    private static readonly Color Painel = Hex("141B29");
    private static readonly Color PainelBorda = Hex("2E323C");
    private static readonly Color Pilula = Hex("1E2026");
    private static readonly Color Texto = Hex("E6E9EF");
    private static readonly Color TextoApagado = Hex("8A93A6");
    private static readonly Color Fraco = Hex("5C6475");
    private static readonly Color Ciano = Hex("43D1D9");
    private static readonly Color Amarelo = Hex("F5C442");

    // Consequências de plataforma: a etiqueta vai pra baixo, porque em cima é
    // onde o jogador pisa.
    private static readonly ConsequenceType[] DePlataforma =
    {
        ConsequenceType.BreakAndRespawn, ConsequenceType.BounceAndReturn, ConsequenceType.FallAfterDelay,
        ConsequenceType.MakeSlippery, ConsequenceType.OneWayFromBelow,
        // A lâmpada fica no teto: em cima dela não sobra tela.
        ConsequenceType.TurnOnLamp, ConsequenceType.TurnOnLampForSeconds,
    };

    [MenuItem("Tools/Interação/Montar destaque dos itens")]
    public static void BuildMenu()
    {
        bool algum = false;
        foreach (string path in new[] { ScenePath, VidaPath })
        {
            Scene s = SceneManager.GetSceneByPath(path);
            if (!s.IsValid() || !s.isLoaded) continue;
            algum = true;
            foreach (string line in Build(path)) Debug.Log("[DestaqueInteracaoBuilder] " + line);
        }

        if (!algum) Debug.Log("[DestaqueInteracaoBuilder] ERRO: abra a cena Interacao ou a Interacao_Vida antes de rodar.");
    }

    public static List<string> Build() => Build(ScenePath);

    public static List<string> Build(string scenePath)
    {
        var log = new List<string>();
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            log.Add("ERRO: abra a cena " + System.IO.Path.GetFileNameWithoutExtension(scenePath) + " antes de rodar.");
            return log;
        }

        bool vida = scenePath == VidaPath;

        InteractionConfigUI config = scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<InteractionConfigUI>(true)).FirstOrDefault();
        Canvas canvas = config != null ? config.GetComponentInParent<Canvas>(true) : null;
        if (canvas == null)
        {
            log.Add("ERRO: Canvas do InteractionConfigUI não encontrado.");
            return log;
        }

        Transform velho = canvas.transform.Find(NomeRaiz);
        if (velho != null)
        {
            Undo.DestroyObjectImmediate(velho.gameObject);
        }

        RectTransform raiz = Novo(NomeRaiz, canvas.transform);
        Undo.RegisterCreatedObjectUndo(raiz.gameObject, "Destaque dos itens");
        Esticar(raiz);

        // Logo antes do assistente (que fica por cima quando abre).
        Transform irmaoConfig = config.transform;
        while (irmaoConfig != null && irmaoConfig.parent != canvas.transform) irmaoConfig = irmaoConfig.parent;
        if (irmaoConfig != null) raiz.SetSiblingIndex(irmaoConfig.GetSiblingIndex());

        var itens = scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<InteractableItem>(true))
            .OrderBy(i => i.name).ToList();

        RectTransform pastaEtiquetas = Novo("Etiquetas", raiz);
        Esticar(pastaEtiquetas);
        var etiquetas = new List<DestaqueItensInteracao.Etiqueta>();
        foreach (InteractableItem item in itens)
        {
            etiquetas.Add(vida
                ? MontarEtiquetaDuasLinhas(pastaEtiquetas, item, item.AllowedConsequences.Any(DePlataforma.Contains))
                : MontarEtiqueta(pastaEtiquetas, item));
        }

        var destaque = raiz.gameObject.AddComponent<DestaqueItensInteracao>();
        var so = new SerializedObject(destaque);
        MontarPainel(raiz, so);

        SerializedProperty arr = so.FindProperty("etiquetas");
        arr.arraySize = etiquetas.Count;
        for (int i = 0; i < etiquetas.Count; i++)
        {
            SerializedProperty e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("item").objectReferenceValue = etiquetas[i].item;
            e.FindPropertyRelative("raiz").objectReferenceValue = etiquetas[i].raiz;
            e.FindPropertyRelative("acao").objectReferenceValue = etiquetas[i].acao;
            e.FindPropertyRelative("consequencia").objectReferenceValue = etiquetas[i].consequencia;
            e.FindPropertyRelative("embaixo").boolValue = etiquetas[i].embaixo;
            e.FindPropertyRelative("ponta").objectReferenceValue = etiquetas[i].ponta;
            e.FindPropertyRelative("grupo").objectReferenceValue = etiquetas[i].grupo;
        }

        if (vida)
        {
            InteracaoPlayerController jogador = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<InteracaoPlayerController>(true)).FirstOrDefault();
            so.FindProperty("manterNaTela").boolValue = true;
            so.FindProperty("clarearAtrasDoJogador").boolValue = true;
            so.FindProperty("jogador").objectReferenceValue = jogador != null ? jogador.transform : null;
            if (jogador == null) log.Add("AVISO: jogador não encontrado; a etiqueta não vai clarear atrás dele.");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        int embaixo = etiquetas.Count(e => e.embaixo);
        log.Add($"destaque montado em {scene.name} (Canvas {canvas.name}) com {etiquetas.Count} etiquetas" +
                (vida ? $", {embaixo} embaixo das plataformas" : ""));
        return log;
    }

    // ---------------------------------------------------------------- painel

    private static void MontarPainel(RectTransform raiz, SerializedObject so)
    {
        RectTransform caixa = Novo("Painel", raiz);
        caixa.anchorMin = caixa.anchorMax = new Vector2(0.5f, 0.5f);
        caixa.pivot = new Vector2(0f, 1f);
        caixa.sizeDelta = new Vector2(Largura, 252f);
        Moldura(caixa, Painel, PainelBorda, 1f);

        TMP_Text titulo = Rotulo(caixa, "Titulo", "Porta", "Nunito-ExtraBold SDF", 19, Texto, new Vector2(16f, 10f), new Vector2(250f, 30f));
        titulo.enableAutoSizing = true;
        titulo.fontSizeMin = 14;
        titulo.fontSizeMax = 19;

        RectTransform tipo = Novo("Tipo", caixa);
        Canto(tipo, new Vector2(Largura - 16f - 118f, 13f), new Vector2(118f, 24f));
        (Image tipoFundo, Image tipoBorda) = Pilula_(tipo, Pilula, PainelBorda);
        TMP_Text tipoTexto = Rotulo(tipo, "Texto", "configurável", "Nunito-Bold SDF", 13, TextoApagado, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        Esticar(tipoTexto.rectTransform);

        Separador(caixa, "Separador", 50f);
        Rotulo(caixa, "RotuloAcao", "AÇÃO", "JetBrainsMono-Bold SDF", 12, Fraco, new Vector2(16f, 59f), new Vector2(200f, 22f));
        DestaqueItensInteracao.Linha acao = MontarLinha(caixa, "LinhaAcao", 83f);
        Rotulo(caixa, "RotuloConsequencia", "CONSEQUÊNCIA", "JetBrainsMono-Bold SDF", 12, Fraco, new Vector2(16f, 135f), new Vector2(200f, 22f));
        DestaqueItensInteracao.Linha cons = MontarLinha(caixa, "LinhaConsequencia", 159f);
        Separador(caixa, "SeparadorRodape", 213f);
        TMP_Text rodape = Rotulo(caixa, "Rodape", "Clique para escolher Ação e Consequência.", "Nunito-Bold SDF", 14, TextoApagado, new Vector2(16f, 214f), new Vector2(Largura - 32f, 36f));
        caixa.gameObject.SetActive(false);

        so.FindProperty("caixa").objectReferenceValue = caixa;
        so.FindProperty("titulo").objectReferenceValue = titulo;
        so.FindProperty("tipo").objectReferenceValue = tipo;
        so.FindProperty("tipoFundo").objectReferenceValue = tipoFundo;
        so.FindProperty("tipoBorda").objectReferenceValue = tipoBorda;
        so.FindProperty("tipoTexto").objectReferenceValue = tipoTexto;
        so.FindProperty("rodape").objectReferenceValue = rodape;
        GravarLinha(so.FindProperty("linhaAcao"), acao);
        GravarLinha(so.FindProperty("linhaConsequencia"), cons);
    }

    private static DestaqueItensInteracao.Linha MontarLinha(RectTransform caixa, string nome, float y)
    {
        RectTransform raiz = Novo(nome, caixa);
        Canto(raiz, new Vector2(14f, y), new Vector2(Largura - 28f, 44f));
        (Image fundo, Image borda) = Moldura(raiz, Hex("0C111B"), PainelBorda, 1.5f);

        RectTransform pontoRt = Novo("Ponto", raiz);
        Canto(pontoRt, new Vector2(14f, 15f), new Vector2(14f, 14f));
        Image ponto = Img(pontoRt, S("inv_circulo.png"), Fraco, 1f);
        ponto.type = Image.Type.Simple;

        TMP_Text nomeTxt = Rotulo(raiz, "Nome", "a escolher", "Nunito-ExtraBold SDF", 15, TextoApagado, new Vector2(38f, 3f), new Vector2(270f, 20f));
        TMP_Text sub = Rotulo(raiz, "Sub", "", "JetBrainsMono-Bold SDF", 12, Fraco, new Vector2(38f, 22f), new Vector2(290f, 18f));

        RectTransform pilula = Novo("Clique", raiz);
        Canto(pilula, new Vector2(Largura - 28f - 70f - 10f, 10f), new Vector2(70f, 24f));
        Pilula_(pilula, Pilula, PainelBorda);
        TMP_Text pilulaTxt = Rotulo(pilula, "Texto", "CLIQUE", "JetBrainsMono-Bold SDF", 11, Fraco, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        Esticar(pilulaTxt.rectTransform);

        return new DestaqueItensInteracao.Linha { fundo = fundo, borda = borda, ponto = ponto, nome = nomeTxt, sub = sub, pilula = pilula };
    }

    private static void GravarLinha(SerializedProperty p, DestaqueItensInteracao.Linha l)
    {
        p.FindPropertyRelative("fundo").objectReferenceValue = l.fundo;
        p.FindPropertyRelative("borda").objectReferenceValue = l.borda;
        p.FindPropertyRelative("ponto").objectReferenceValue = l.ponto;
        p.FindPropertyRelative("nome").objectReferenceValue = l.nome;
        p.FindPropertyRelative("sub").objectReferenceValue = l.sub;
        p.FindPropertyRelative("pilula").objectReferenceValue = l.pilula;
    }

    // -------------------------------------------------------------- etiqueta

    private static DestaqueItensInteracao.Etiqueta MontarEtiqueta(RectTransform pasta, InteractableItem item)
    {
        RectTransform raiz = Novo("Etiqueta_" + item.name, pasta);
        raiz.anchorMin = raiz.anchorMax = new Vector2(0.5f, 0.5f);
        raiz.pivot = new Vector2(0.5f, 0f);
        raiz.sizeDelta = new Vector2(240f, 30f);

        var hlg = raiz.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 13, 0, 0);
        hlg.spacing = 7f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        var fitter = raiz.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Ponta embaixo, apontando pro item (atrás da pílula).
        RectTransform cauda = Novo("Ponta", raiz);
        Ignorar(cauda);
        cauda.anchorMin = cauda.anchorMax = new Vector2(0.5f, 0f);
        cauda.pivot = new Vector2(0.5f, 0.5f);
        cauda.sizeDelta = new Vector2(11f, 11f);
        cauda.anchoredPosition = new Vector2(0f, 1f);
        cauda.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Img(cauda, null, Painel, 1f);

        RectTransform fundo = Novo("Fundo", raiz);
        Ignorar(fundo);
        Esticar(fundo);
        Img(fundo, S("inv_pilula.png"), Painel, 1f);
        RectTransform borda = Novo("Borda", raiz);
        Ignorar(borda);
        Esticar(borda);
        Img(borda, S("inv_pilula_borda.png"), PainelBorda, 1f);

        RectTransform pontoRt = Novo("Ponto", raiz);
        Image ponto = Img(pontoRt, S("inv_circulo.png"), Ciano, 1f);
        ponto.type = Image.Type.Simple;
        var le = pontoRt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 8f;
        le.preferredHeight = 8f;

        TMP_Text acao = TextoEtiqueta(raiz, "Acao", "Pressionar um botão", "Nunito-Bold SDF", Texto);
        TextoEtiqueta(raiz, "Seta", "→", "JetBrainsMono-Bold SDF", TextoApagado);
        TMP_Text cons = TextoEtiqueta(raiz, "Consequencia", "Abrir uma porta", "Nunito-Bold SDF", Amarelo);

        raiz.gameObject.SetActive(false);
        return new DestaqueItensInteracao.Etiqueta { item = item, raiz = raiz, acao = acao, consequencia = cons };
    }

    // Interacao_Vida: cartão de duas linhas ("● Ação" / "→ Consequência"), mais
    // estreito que a plataforma; embaixo do item quando é plataforma.
    private static DestaqueItensInteracao.Etiqueta MontarEtiquetaDuasLinhas(RectTransform pasta, InteractableItem item, bool embaixo)
    {
        RectTransform raiz = Novo("Etiqueta_" + item.name, pasta);
        raiz.anchorMin = raiz.anchorMax = new Vector2(0.5f, 0.5f);
        raiz.pivot = new Vector2(0.5f, embaixo ? 1f : 0f);
        raiz.sizeDelta = new Vector2(180f, 44f);

        var vlg = raiz.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 5, 6);
        vlg.spacing = -3f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        var fitter = raiz.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var grupo = raiz.gameObject.AddComponent<CanvasGroup>();
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        // Ponta apontando pro item, atrás do cartão: embaixo quando a etiqueta
        // fica em cima do item, em cima quando ela fica embaixo.
        RectTransform cauda = Novo("Ponta", raiz);
        Ignorar(cauda);
        cauda.anchorMin = cauda.anchorMax = new Vector2(0.5f, embaixo ? 1f : 0f);
        cauda.pivot = new Vector2(0.5f, 0.5f);
        cauda.sizeDelta = new Vector2(11f, 11f);
        cauda.anchoredPosition = new Vector2(0f, embaixo ? -1f : 1f);
        cauda.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Img(cauda, null, Painel, 1f);

        RectTransform fundo = Novo("Fundo", raiz);
        Ignorar(fundo);
        Esticar(fundo);
        Img(fundo, S("inv_arredondado.png"), Painel, 1f);
        RectTransform borda = Novo("Borda", raiz);
        Ignorar(borda);
        Esticar(borda);
        Img(borda, S("inv_borda.png"), PainelBorda, 1f);

        RectTransform linhaAcao = Linha(raiz, "LinhaAcao", 6f);
        RectTransform pontoRt = Novo("Ponto", linhaAcao);
        Image ponto = Img(pontoRt, S("inv_circulo.png"), Ciano, 1f);
        ponto.type = Image.Type.Simple;
        var le = pontoRt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 7f;
        le.preferredHeight = 7f;
        TMP_Text acao = TextoLinha(linhaAcao, "Acao", "Tocar nele", "Nunito-Bold SDF", Texto, 14f);

        RectTransform linhaCons = Linha(raiz, "LinhaConsequencia", 5f);
        TextoLinha(linhaCons, "Seta", "→", "JetBrainsMono-Bold SDF", TextoApagado, 13f);
        TMP_Text cons = TextoLinha(linhaCons, "Consequencia", "Causar 1 de dano", "Nunito-Bold SDF", Amarelo, 14f);

        raiz.gameObject.SetActive(false);
        return new DestaqueItensInteracao.Etiqueta
        {
            item = item, raiz = raiz, acao = acao, consequencia = cons, embaixo = embaixo, ponta = cauda, grupo = grupo,
        };
    }

    private static RectTransform Linha(RectTransform pai, string nome, float espaco)
    {
        RectTransform rt = Novo(nome, pai);
        var hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = espaco;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        return rt;
    }

    private static TMP_Text TextoLinha(RectTransform pai, string nome, string texto, string fonte, Color cor, float tamanho)
    {
        TMP_Text tmp = TextoEtiqueta(pai, nome, texto, fonte, cor);
        tmp.fontSize = tamanho;
        return tmp;
    }

    private static TMP_Text TextoEtiqueta(RectTransform pai, string nome, string texto, string fonte, Color cor)
    {
        RectTransform rt = Novo(nome, pai);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + fonte + ".asset");
        tmp.text = texto;
        tmp.fontSize = 15;
        tmp.color = cor;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Ignorar(RectTransform rt)
    {
        rt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
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

    private static void Separador(RectTransform caixa, string nome, float y)
    {
        RectTransform sep = Novo(nome, caixa);
        Canto(sep, new Vector2(14f, y), new Vector2(Largura - 28f, 1f));
        Img(sep, null, PainelBorda, 1f);
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
