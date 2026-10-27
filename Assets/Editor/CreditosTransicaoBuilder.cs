using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Transição entre cenas e tela de Créditos do Menu:
///
/// Tools > Transição > Criar prefab Compilando a fase
///   Cria Assets/Art/UI/Sprites/celula_grade.png e o prefab
///   Assets/Resources/TransicaoCompilar.prefab (TransicaoCompilar). Sobrescreve.
///
/// Tools > Menu > Criar botão e tela de Créditos
///   Na cena Menu: divide a linha do Sair em "Créditos" + "Sair" e cria a
///   janela "Creditos.cs" (CreditosUI), desligada. Abre a cena em modo
///   Additive se preciso, faz backup, salva e fecha. Rodar de novo recria a
///   janela mantendo os nomes já preenchidos.
/// </summary>
public static class CreditosTransicaoBuilder
{
    private const string MenuPath = "Assets/Scenes/Menu.unity";
    private const string PrefabPath = "Assets/Resources/TransicaoCompilar.prefab";
    private const string CelulaPath = "Assets/Art/UI/Sprites/celula_grade.png";
    private const string IconeCreditos = "Assets/Art/UI/Sprites/icone_trofeu.png";
    private const string IconeMusica = "Assets/Art/UI/Sprites/icone_musica.png";
    private const string PainelCodigoPath = "Assets/Art/UI/Sprites/painel_codigo.png";

    // ================================================================ transição

    [MenuItem("Tools/Transição/Criar prefab Compilando a fase")]
    public static void CriarPrefabTransicao()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[CreditosTransicaoBuilder] Saia do Play antes.");
            return;
        }

        Sprite celula = CriarSpriteCelula();
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject raiz = MontarTransicao(celula);
            SceneManager.MoveGameObjectToScene(raiz, preview);
            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabPath, out bool ok);
            UnityEngine.Object.DestroyImmediate(raiz);
            Debug.Log(ok ? "[CreditosTransicaoBuilder] Prefab salvo em " + PrefabPath : "[CreditosTransicaoBuilder] ERRO ao salvar o prefab.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    // Célula da grade do editor: azul-noite com as linhas da grade nas bordas.
    private static Sprite CriarSpriteCelula()
    {
        const int lado = 64;
        var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        Color fundo = new Color32(0x0E, 0x13, 0x20, 0xFF);
        Color linha = new Color32(0x1E, 0x2B, 0x47, 0xFF);
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                bool borda = x < 1 || y < 1 || x >= lado - 1 || y >= lado - 1;
                tex.SetPixel(x, y, borda ? linha : fundo);
            }
        }
        tex.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(CelulaPath));
        File.WriteAllBytes(CelulaPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(CelulaPath, ImportAssetOptions.ForceUpdate);

        var imp = (TextureImporter)AssetImporter.GetAtPath(CelulaPath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.mipmapEnabled = false;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(CelulaPath);
    }

    private static GameObject MontarTransicao(Sprite celula)
    {
        TemaLaboratorio tema = TemaLabUI.Tema;

        var raiz = new GameObject("TransicaoCompilar", typeof(RectTransform));
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = raiz.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        raiz.AddComponent<GraphicRaycaster>();
        var t = raiz.AddComponent<TransicaoCompilar>();
        Transform r = raiz.transform;

        Image bloqueio = TemaLabUI.Imagem(r, "Bloqueio", null, new Color(0f, 0f, 0f, 0f), raycast: true);
        TemaLabUI.Esticar(bloqueio.rectTransform);

        RectTransform grade = TemaLabUI.Esticar(TemaLabUI.Novo(r, "Grade"));
        Image modelo = TemaLabUI.Imagem(grade, "CelulaModelo", celula, TemaLabUI.Fundo);
        modelo.rectTransform.sizeDelta = new Vector2(60f, 60f);
        modelo.gameObject.SetActive(false);

        // Cartão "Compilando".
        const float w = 820f, h = 250f;
        Image fundoCartao = TemaLabUI.Imagem(r, "Cartao", tema != null ? tema.painelJanela : null, TemaLabUI.Painel);
        TemaLabUI.Centro(fundoCartao.rectTransform, Vector2.zero, new Vector2(w, h));
        var grupo = fundoCartao.gameObject.AddComponent<CanvasGroup>();
        grupo.blocksRaycasts = false;
        grupo.interactable = false;
        Transform c = fundoCartao.transform;

        Image faixa = TemaLabUI.Imagem(c, "FaixaModulo", tema != null ? tema.bordaArredondada : null, TemaLabUI.Verde);
        faixa.color = TemaLabUI.Verde;
        Caixa(faixa.rectTransform, 22f, 26f, 12f, h - 52f);

        TextMeshProUGUI modulo = TemaLabUI.Rotulo(c, "Modulo", "INTERAÇÃO", 24f, TemaLabUI.Verde);
        Caixa(modulo.rectTransform, 66f, 30f, w - 110f, 32f);
        TextMeshProUGUI titulo = TemaLabUI.Texto(c, "Titulo", "Fase 4 · Interação e Vida", 60f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Display, TextAlignmentOptions.MidlineLeft);
        titulo.textWrappingMode = TextWrappingModes.NoWrap;
        titulo.enableAutoSizing = true;
        titulo.fontSizeMin = 36f;
        titulo.fontSizeMax = 60f;
        Caixa(titulo.rectTransform, 66f, 62f, w - 110f, 80f);
        TextMeshProUGUI arquivo = TemaLabUI.Texto(c, "Arquivo", "Carregando Interacao_Vida.unity", 24f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.Mono, TextAlignmentOptions.MidlineLeft);
        arquivo.textWrappingMode = TextWrappingModes.NoWrap;
        Caixa(arquivo.rectTransform, 66f, 146f, w - 110f, 34f);

        Image barra = TemaLabUI.Imagem(c, "Barra", tema != null ? tema.bordaArredondada : null, TemaLabUI.Poco);
        barra.color = TemaLabUI.Poco;
        Caixa(barra.rectTransform, 66f, 194f, w - 110f, 18f);
        Image preenchimento = TemaLabUI.Imagem(barra.transform, "Preenchimento", tema != null ? tema.bordaArredondada : null, TemaLabUI.Verde);
        preenchimento.color = TemaLabUI.Verde;
        RectTransform pr = preenchimento.rectTransform;
        pr.anchorMin = Vector2.zero;
        pr.anchorMax = new Vector2(0.7f, 1f);
        pr.offsetMin = pr.offsetMax = Vector2.zero;

        var so = new SerializedObject(t);
        so.FindProperty("grade").objectReferenceValue = grade;
        so.FindProperty("celulaModelo").objectReferenceValue = modelo;
        so.FindProperty("bloqueio").objectReferenceValue = bloqueio;
        so.FindProperty("cartao").objectReferenceValue = grupo;
        so.FindProperty("faixaModulo").objectReferenceValue = faixa;
        so.FindProperty("textoModulo").objectReferenceValue = modulo;
        so.FindProperty("textoTitulo").objectReferenceValue = titulo;
        so.FindProperty("textoArquivo").objectReferenceValue = arquivo;
        so.FindProperty("barraPreenchimento").objectReferenceValue = preenchimento;

        Color fisica = new Color32(0x4A, 0x8C, 0xF7, 0xFF);
        Color interacao = new Color32(0x52, 0xC9, 0x8A, 0xFF);
        Color animacao = new Color32(0x9B, 0x7B, 0xF0, 0xFF);
        Color ia = new Color32(0xF5, 0xC5, 0x42, 0xFF);
        Color lab = TemaLabUI.Ciano;
        var lista = new (string cena, string modulo, string titulo, Color cor)[]
        {
            ("Menu", "Laboratório", "Menu principal", lab),
            ("Questionario", "Laboratório", "Questionário", lab),
            ("Fisica", "Física", "", fisica),
            ("Aplicacao_Fisica", "Física", "", fisica),
            ("Interacao", "Interação", "", interacao),
            ("Interacao_Vida", "Interação", "", interacao),
            ("Tutorial_Anim", "Animação", "", animacao),
            ("Animacao", "Animação", "", animacao),
            ("IA", "IA", "", ia),
            ("IA_Aplicacao", "IA", "", ia),
        };
        SerializedProperty cenas = so.FindProperty("cenas");
        cenas.arraySize = lista.Length;
        for (int i = 0; i < lista.Length; i++)
        {
            SerializedProperty e = cenas.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("cena").stringValue = lista[i].cena;
            e.FindPropertyRelative("modulo").stringValue = lista[i].modulo;
            e.FindPropertyRelative("titulo").stringValue = lista[i].titulo;
            e.FindPropertyRelative("cor").colorValue = lista[i].cor;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return raiz;
    }

    // ================================================================ créditos no Menu

    [MenuItem("Tools/Menu/Criar botão e tela de Créditos")]
    public static void CriarCreditosNoMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[CreditosTransicaoBuilder] Saia do Play antes.");
            return;
        }

        Scene cena = SceneManager.GetSceneByPath(MenuPath);
        bool abri = false;
        if (cena.isLoaded && cena.isDirty)
        {
            Debug.LogWarning("[CreditosTransicaoBuilder] A cena Menu tem mudanças não salvas. Salve ou descarte antes.");
            return;
        }

        string backup = $"SceneBackups/creditos_transicao_{DateTime.Now:yyyy-MM-dd_HHmm}";
        Directory.CreateDirectory(backup);
        File.Copy(MenuPath, Path.Combine(backup, "Menu.unity"), true);

        if (!cena.isLoaded)
        {
            cena = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Additive);
            abri = true;
        }

        try
        {
            Transform[] todos = cena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform Achar(string nome) => todos.FirstOrDefault(x => x != null && x.name == nome);

            Canvas canvas = Achar("Canvas").GetComponent<Canvas>();
            CreditosUI velho = canvas.GetComponentInChildren<CreditosUI>(true);
            CreditosUI ui = MontarJanela(canvas.transform, velho != null ? velho.transform : null);
            MontarBotao(Achar("Botoes"), Achar("Sair").GetComponent<Button>(), Achar("Configuracoes").GetComponent<Button>(),
                Achar("CrachaJogador") != null ? Achar("CrachaJogador").GetComponent<Selectable>() : null, ui);

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log("[CreditosTransicaoBuilder] Créditos salvos no Menu. Backup em " + backup);
        }
        finally
        {
            if (abri)
            {
                EditorSceneManager.CloseScene(cena, true);
            }
        }
    }

    private static void MontarBotao(Transform botoes, Button sair, Button configuracoes, Selectable cracha, CreditosUI ui)
    {
        Transform linha = botoes.Find("LinhaSair");
        Button creditos;
        if (linha == null)
        {
            var go = new GameObject("LinhaSair", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Créditos");
            linha = go.transform;
            linha.SetParent(botoes, false);
            linha.SetSiblingIndex(sair.transform.GetSiblingIndex());

            LayoutElement leSair = sair.GetComponent<LayoutElement>();
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = leSair.minHeight;
            le.preferredHeight = leSair.preferredHeight;

            var hl = go.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 20f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = true;

            sair.transform.SetParent(linha, false);
            creditos = UnityEngine.Object.Instantiate(sair, linha);
            creditos.name = "Creditos";
            creditos.transform.SetSiblingIndex(0);
        }
        else
        {
            creditos = linha.Find("Creditos").GetComponent<Button>();
        }

        // Créditos ganha mais espaço: "Sair" é curto.
        Larguras(creditos.GetComponent<LayoutElement>(), 3f);
        Larguras(sair.GetComponent<LayoutElement>(), 2f);

        TMP_Text rotulo = creditos.GetComponentInChildren<TMP_Text>(true);
        rotulo.text = "Créditos";
        Image icone = creditos.transform.Find("Icone")?.GetComponent<Image>();
        Sprite trofeu = AssetDatabase.LoadAssetAtPath<Sprite>(IconeCreditos);
        if (icone != null && trofeu != null) icone.sprite = trofeu;

        // Clique: só abre os créditos.
        for (int i = creditos.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(creditos.onClick, i);
        }
        UnityEventTools.AddPersistentListener(creditos.onClick, new UnityAction(ui.Abrir));

        // Navegação: Configurações ↓ Créditos ↔ Sair, ↓ crachá.
        Navegar(configuracoes, (ref Navigation n) => n.selectOnDown = creditos);
        Navegar(creditos, (ref Navigation n) => { n.selectOnUp = configuracoes; n.selectOnDown = cracha; n.selectOnLeft = null; n.selectOnRight = sair; });
        Navegar(sair, (ref Navigation n) => { n.selectOnUp = configuracoes; n.selectOnDown = cracha; n.selectOnLeft = creditos; n.selectOnRight = null; });
        if (cracha != null) Navegar(cracha, (ref Navigation n) => n.selectOnUp = creditos);
    }

    private static void Larguras(LayoutElement le, float flex)
    {
        le.minWidth = 0f;
        le.preferredWidth = 0f;
        le.flexibleWidth = flex;
    }

    private delegate void MudaNavegacao(ref Navigation n);

    private static void Navegar(Selectable s, MudaNavegacao mudar)
    {
        // Navigation é struct: muda a cópia e devolve.
        Navigation n = s.navigation;
        n.mode = Navigation.Mode.Explicit;
        mudar(ref n);
        s.navigation = n;
        EditorUtility.SetDirty(s);
    }

    private static CreditosUI MontarJanela(Transform canvas, Transform antigo)
    {
        // Mantém os nomes já preenchidos se a janela já existia.
        string[] nomes = null;
        string modelo = null;
        if (antigo != null)
        {
            CreditosUI anterior = antigo.GetComponent<CreditosUI>();
            if (anterior != null)
            {
                var sv = new SerializedObject(anterior);
                nomes = new[] { sv.FindProperty("desenvolvimento").stringValue, sv.FindProperty("orientacao").stringValue, sv.FindProperty("instituicao").stringValue };
                modelo = sv.FindProperty("modelo").stringValue;
            }
            Undo.DestroyObjectImmediate(antigo.gameObject);
        }

        TemaLaboratorio tema = TemaLabUI.Tema;
        RectTransform raiz = TemaLabUI.Esticar(TemaLabUI.Novo(canvas, "TelaCreditos"));
        raiz.SetAsLastSibling();
        var ui = raiz.gameObject.AddComponent<CreditosUI>();

        RectTransform painel = TemaLabUI.Esticar(TemaLabUI.Novo(raiz, "Painel"));
        Image veu = TemaLabUI.Veu(painel);
        veu.color = new Color(0.024f, 0.035f, 0.067f, 0.8f);

        const float w = 1360f, h = 820f;
        Image janela = TemaLabUI.Imagem(painel, "Janela", AssetDatabase.LoadAssetAtPath<Sprite>(PainelCodigoPath), TemaLabUI.Poco, raycast: true);
        janela.type = Image.Type.Sliced;
        TemaLabUI.Centro(janela.rectTransform, new Vector2(0f, 24f), new Vector2(w, h));
        Transform j = janela.transform;

        // Barra de título (os pontos e a linha azul já estão no sprite).
        TextMeshProUGUI arquivo = TemaLabUI.Texto(j, "NomeArquivo", "Creditos.cs", 26f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.MonoForte, TextAlignmentOptions.MidlineLeft);
        arquivo.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform ar = arquivo.rectTransform;
        ar.anchorMin = new Vector2(0f, 1f);
        ar.anchorMax = new Vector2(0.5f, 1f);
        ar.pivot = new Vector2(0f, 1f);
        ar.offsetMin = new Vector2(150f, -60f);
        ar.offsetMax = new Vector2(0f, -4f);

        RectTransform musica = TemaLabUI.Novo(j, "Musica");
        musica.anchorMin = musica.anchorMax = musica.pivot = new Vector2(1f, 1f);
        musica.anchoredPosition = new Vector2(-250f, -6f);
        musica.sizeDelta = new Vector2(520f, 52f);
        var hm = musica.gameObject.AddComponent<HorizontalLayoutGroup>();
        hm.childAlignment = TextAnchor.MiddleRight;
        hm.spacing = 10f;
        hm.childControlWidth = hm.childControlHeight = true;
        hm.childForceExpandWidth = hm.childForceExpandHeight = false;
        Image notaIcone = TemaLabUI.Imagem(musica, "Icone", AssetDatabase.LoadAssetAtPath<Sprite>(IconeMusica), TemaLabUI.Verde);
        notaIcone.color = TemaLabUI.Verde;
        notaIcone.preserveAspect = true;
        var leNota = notaIcone.gameObject.AddComponent<LayoutElement>();
        leNota.preferredWidth = leNota.preferredHeight = 26f;
        TextMeshProUGUI nota = TemaLabUI.Texto(musica, "Nome", "musica_07_final_por_do_sol", 20f, TemaLabUI.Verde, TemaLabUI.Fonte.Mono, TextAlignmentOptions.MidlineRight);
        nota.textWrappingMode = TextWrappingModes.NoWrap;

        Button fechar = TemaLabUI.Botao(j, "Voltar", "Voltar", tema != null ? tema.botaoSecundario : null, TemaLabUI.PainelElevado,
            out _, tema != null ? tema.iconeVoltar : null, 22f);
        RectTransform fr = (RectTransform)fechar.transform;
        fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(1f, 1f);
        fr.anchoredPosition = new Vector2(-14f, -8f);
        fr.sizeDelta = new Vector2(170f, 44f);
        RectTransform frConteudo = (RectTransform)fechar.transform.Find("Conteudo");
        if (frConteudo != null)
        {
            frConteudo.offsetMin = new Vector2(12f, 2f);
            frConteudo.offsetMax = new Vector2(-12f, -2f);
        }

        // Área do código: números na calha da esquerda (já desenhada no sprite), texto à direita.
        RectTransform viewport = TemaLabUI.Novo(j, "Rolagem");
        TemaLabUI.Esticar(viewport);
        viewport.offsetMin = new Vector2(0f, 18f);
        viewport.offsetMax = new Vector2(-14f, -74f);
        viewport.gameObject.AddComponent<RectMask2D>();
        Image alvoRolagem = viewport.gameObject.AddComponent<Image>();
        alvoRolagem.color = new Color(0f, 0f, 0f, 0f);

        RectTransform conteudo = TemaLabUI.Novo(viewport, "Conteudo");
        conteudo.anchorMin = new Vector2(0f, 1f);
        conteudo.anchorMax = new Vector2(1f, 1f);
        conteudo.pivot = new Vector2(0.5f, 1f);
        conteudo.offsetMin = conteudo.offsetMax = Vector2.zero;
        var vl = conteudo.gameObject.AddComponent<HorizontalLayoutGroup>();
        vl.padding = new RectOffset(0, 24, 14, 24);
        vl.spacing = 26f;
        vl.childAlignment = TextAnchor.UpperLeft;
        vl.childControlWidth = vl.childControlHeight = true;
        vl.childForceExpandWidth = false;
        vl.childForceExpandHeight = false;
        var fit = conteudo.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI numeros = TemaLabUI.Texto(conteudo, "Linhas", "1", 24f, new Color32(0x4A, 0x58, 0x78, 0xFF), TemaLabUI.Fonte.Mono, TextAlignmentOptions.TopRight);
        numeros.textWrappingMode = TextWrappingModes.NoWrap;
        numeros.lineSpacing = 12f;
        var leNum = numeros.gameObject.AddComponent<LayoutElement>();
        leNum.preferredWidth = leNum.minWidth = 50f;

        TextMeshProUGUI codigo = TemaLabUI.Texto(conteudo, "Codigo", "", 24f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Mono, TextAlignmentOptions.TopLeft);
        codigo.textWrappingMode = TextWrappingModes.NoWrap;
        codigo.lineSpacing = 12f;
        var leCod = codigo.gameObject.AddComponent<LayoutElement>();
        leCod.flexibleWidth = 1f;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = conteudo;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // Legenda de teclas sobre uma faixa escura (cobre a legenda do Menu).
        Image faixa = TemaLabUI.Imagem(painel, "FaixaLegenda", null, TemaLabUI.Fundo);
        RectTransform fx = faixa.rectTransform;
        fx.anchorMin = new Vector2(0f, 0f);
        fx.anchorMax = new Vector2(1f, 0f);
        fx.pivot = new Vector2(0.5f, 0f);
        fx.anchoredPosition = Vector2.zero;
        fx.sizeDelta = new Vector2(0f, 96f);
        TemaLabUI.LegendaTeclas(painel, "↑ ↓", "Rolar", "Esc", "Voltar");

        // Foco do teclado vai pro Voltar e volta pro botão Créditos ao fechar.
        var foco = painel.gameObject.AddComponent<FocoInicialUI>();
        var sf = new SerializedObject(foco);
        SerializedProperty cand = sf.FindProperty("candidatos");
        cand.arraySize = 1;
        cand.GetArrayElementAtIndex(0).objectReferenceValue = fechar;
        sf.ApplyModifiedPropertiesWithoutUndo();

        var so = new SerializedObject(ui);
        so.FindProperty("painel").objectReferenceValue = painel.gameObject;
        so.FindProperty("codigo").objectReferenceValue = codigo;
        so.FindProperty("numeros").objectReferenceValue = numeros;
        so.FindProperty("rolagem").objectReferenceValue = scroll;
        so.FindProperty("botaoFechar").objectReferenceValue = fechar;
        if (nomes != null)
        {
            so.FindProperty("desenvolvimento").stringValue = nomes[0];
            so.FindProperty("orientacao").stringValue = nomes[1];
            so.FindProperty("instituicao").stringValue = nomes[2];
            so.FindProperty("modelo").stringValue = modelo;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        ui.MontarTexto();

        painel.gameObject.SetActive(false);
        return ui;
    }

    // Posição a partir do canto de cima à esquerda do pai.
    private static void Caixa(RectTransform rt, float x, float y, float largura, float altura)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(largura, altura);
    }
}
