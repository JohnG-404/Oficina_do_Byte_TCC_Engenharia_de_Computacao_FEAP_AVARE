using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Monta a tela "Novo jogador" (Assets/Resources/NovoJogador.prefab, com o
/// NovoJogadorUI ligado) e o crachá "Jogando como" no Menu, salvo na cena.
/// Visual do tema Laboratório (TemaLabUI). Depois de criado, o prefab e o
/// crachá podem ser editados à vontade no Editor.
///
/// Tools > Menu > Criar tela Novo jogador: cria o prefab (sobrescreve) e
/// coloca/atualiza o crachá na cena Menu (abre em modo Additive, salva e
/// fecha; faz backup da cena antes).
/// </summary>
public static class NovoJogadorBuilder
{
    private const string PrefabPath = "Assets/Resources/NovoJogador.prefab";
    private const string MenuPath = "Assets/Scenes/Menu.unity";

    private const float LarguraJanela = 1300f;
    private const float AlturaJanela = 800f;
    private const float Margem = 64f;

    private static readonly Color MioloMarcado = new Color32(0x26, 0x40, 0x6E, 0xFF);

    // Fundo opaco atrás do painel "Ver todos": cor do miolo da janela, com
    // folga pra cobrir também a borda dos cartões.
    private static readonly Color MioloJanela = TemaLabUI.Painel;
    private const float FolgaFundoTodos = 12f;

    [MenuItem("Tools/Menu/Criar tela Novo jogador")]
    public static void CriarTudo()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[NovoJogadorBuilder] Saia do Play antes.");
            return;
        }

        CriarPrefab();
        ColocarCrachaNoMenu();
    }

    // ------------------------------------------------------------------
    // Prefab da janela
    // ------------------------------------------------------------------

    public static void CriarPrefab()
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject raiz = MontarJanela();
            SceneManager.MoveGameObjectToScene(raiz, preview);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabPath, out bool ok);
            Object.DestroyImmediate(raiz);
            Debug.Log(ok ? "[NovoJogadorBuilder] Prefab salvo em " + PrefabPath : "[NovoJogadorBuilder] ERRO ao salvar o prefab.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static GameObject MontarJanela()
    {
        TemaLaboratorio tema = TemaLabUI.Tema;

        var raiz = new GameObject("NovoJogador", typeof(RectTransform));
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = raiz.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        raiz.AddComponent<GraphicRaycaster>();
        var ui = raiz.AddComponent<NovoJogadorUI>();
        Transform r = raiz.transform;

        Image veu = TemaLabUI.Veu(r);
        veu.color = new Color(0.024f, 0.035f, 0.067f, 0.78f);

        Image janela = TemaLabUI.Imagem(r, "Janela", tema != null ? tema.painelJanela : null, TemaLabUI.Painel);
        TemaLabUI.Centro(janela.rectTransform, new Vector2(0f, -20f), new Vector2(LarguraJanela, AlturaJanela));
        TemaLabUI.Placa(janela.transform, "Novo jogador", new Vector2(0f, AlturaJanela * 0.5f + 8f), new Vector2(640f, 104f), 50f);

        RectTransform c = TemaLabUI.Novo(janela.transform, "Conteudo");
        TemaLabUI.Esticar(c);
        c.offsetMin = new Vector2(Margem, 44f);
        c.offsetMax = new Vector2(-Margem, -80f);
        float largura = LarguraJanela - Margem * 2f;

        TextMeshProUGUI titulo = TemaLabUI.Texto(c, "Titulo", "Quem vai jogar agora?", 54f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Display);
        titulo.textWrappingMode = TextWrappingModes.NoWrap;
        Caixa(titulo.rectTransform, 0f, 0f, largura, 72f);

        // Os dois cartões.
        float wCartao = (largura - 32f) / 2f;
        Cartao(c, "CartaoApelido", "Com apelido", "Um apelido curto. Não use o nome completo.", 0f, wCartao,
            out Button cartaoApelido, out GameObject marcaApelido, out GameObject pontoApelido);
        Cartao(c, "CartaoAnonimo", "Anônimo", "O jogo te dá um número. Anote pra continuar depois.", wCartao + 32f, wCartao,
            out Button cartaoAnonimo, out GameObject marcaAnonimo, out GameObject pontoAnonimo);

        TMP_InputField campo = CampoApelido(cartaoApelido.transform, wCartao - 60f);
        TextMeshProUGUI contador = TemaLabUI.Texto(cartaoApelido.transform, "Contador", "0/12", 20f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.Mono, TextAlignmentOptions.MidlineRight);
        Caixa(contador.rectTransform, 30f, 272f, wCartao - 60f, 30f);

        RectTransform caixaNumero = TemaLabUI.CaixaArredondada(cartaoAnonimo.transform, "Numero", TemaLabUI.Contorno, TemaLabUI.Poco, 4f);
        Caixa((RectTransform)caixaNumero.parent, 30f, 170f, wCartao - 60f, 96f);
        TextMeshProUGUI numero = TemaLabUI.Texto(caixaNumero, "Texto", "Jogador 001", 50f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.Display, TextAlignmentOptions.MidlineLeft);
        numero.textWrappingMode = TextWrappingModes.NoWrap;
        TemaLabUI.Esticar(numero.rectTransform).offsetMin = new Vector2(26f, 4f);

        // Aviso (apelido repetido, curto demais...).
        TextMeshProUGUI aviso = TemaLabUI.Texto(c, "Aviso", "", 24f, TemaLabUI.Perigo, TemaLabUI.Fonte.CorpoForte, TextAlignmentOptions.MidlineLeft);
        Caixa(aviso.rectTransform, 0f, 432f, largura, 40f);

        // Jogadores já salvos neste PC.
        RectTransform linha = TemaLabUI.Novo(c, "JaJogou");
        Caixa(linha, 0f, 480f, largura, 64f);
        TextMeshProUGUI rotuloLinha = TemaLabUI.Texto(linha, "Rotulo", "Já jogou neste PC? Continue de onde parou:", 25f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.CorpoForte, TextAlignmentOptions.MidlineLeft);
        rotuloLinha.textWrappingMode = TextWrappingModes.NoWrap;
        Caixa(rotuloLinha.rectTransform, 0f, 0f, 520f, 64f);
        RectTransform lista = TemaLabUI.Novo(linha, "Lista");
        Caixa(lista, 530f, 4f, largura - 530f, 56f);
        var hl = lista.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 12f;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = hl.childControlHeight = true;
        hl.childForceExpandWidth = hl.childForceExpandHeight = false;
        Button chipModelo = Chip(lista, "ChipModelo");
        chipModelo.gameObject.SetActive(false);

        // Todos os jogadores (quando não cabem na linha). PainelTodos é a camada
        // ligada/desligada: um bloqueio na tela inteira (nenhum clique passa pro
        // que está atrás) e um fundo opaco que tapa os cartões e a linha.
        RectTransform painelTodos = TemaLabUI.Esticar(TemaLabUI.Novo(c, "PainelTodos"));
        Image bloqueio = TemaLabUI.Imagem(painelTodos, "Bloqueio", null, Color.clear, raycast: true);
        TemaLabUI.Centro(bloqueio.rectTransform, Vector2.zero, new Vector2(8000f, 8000f));
        Image fundoTodos = TemaLabUI.Imagem(painelTodos, "Fundo", tema != null ? tema.bordaArredondada : null, MioloJanela, raycast: true);
        fundoTodos.color = MioloJanela;
        Caixa(fundoTodos.rectTransform, -FolgaFundoTodos, 88f - FolgaFundoTodos, largura + FolgaFundoTodos * 2f, 476f + FolgaFundoTodos * 2f);
        Image cartaoTodos = TemaLabUI.Imagem(painelTodos, "Cartao", tema != null ? tema.painelPopup : null, TemaLabUI.PainelElevado, raycast: true);
        Caixa(cartaoTodos.rectTransform, 0f, 88f, largura, 476f);
        TextMeshProUGUI tituloTodos = TemaLabUI.Texto(cartaoTodos.transform, "Titulo", "Jogadores salvos neste PC", 34f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Display);
        Caixa(tituloTodos.rectTransform, 36f, 24f, largura - 300f, 48f);
        Button fecharTodos = TemaLabUI.Botao(cartaoTodos.transform, "Fechar", "Voltar",
            tema != null ? tema.botaoSecundario : null, TemaLabUI.PainelElevado, out _, tema != null ? tema.iconeVoltar : null, 26f);
        Caixa((RectTransform)fecharTodos.transform, largura - 236f, 18f, 200f, 64f);
        RectTransform grade = Rolagem(cartaoTodos.transform, largura);
        painelTodos.gameObject.SetActive(false);

        Button cancelar = TemaLabUI.Botao(c, "Cancelar", "Cancelar",
            tema != null ? tema.botaoSecundario : null, TemaLabUI.PainelElevado, out _, null, 34f);
        Fundo((RectTransform)cancelar.transform, 0f, 300f, 100f);
        Button comecar = TemaLabUI.Botao(c, "Comecar", "Começar",
            tema != null ? tema.botaoPrimario : null, TemaLabUI.Azul, out _, tema != null ? tema.iconeJogar : null, 36f, Color.white);
        Fundo((RectTransform)comecar.transform, 1f, 400f, 100f);

        // Faixa escura atrás da legenda (cobre a legenda do Menu embaixo).
        Image faixa = TemaLabUI.Imagem(r, "FaixaLegenda", null, TemaLabUI.Fundo);
        RectTransform fr = faixa.rectTransform;
        fr.anchorMin = new Vector2(0f, 0f);
        fr.anchorMax = new Vector2(1f, 0f);
        fr.pivot = new Vector2(0.5f, 0f);
        fr.anchoredPosition = Vector2.zero;
        fr.sizeDelta = new Vector2(0f, 96f);
        TemaLabUI.LegendaTeclas(r, "Tab", "Apelido / Anônimo", "Enter", "Começar", "Esc", "Cancelar");

        // Liga tudo no NovoJogadorUI.
        var so = new SerializedObject(ui);
        so.FindProperty("cartaoApelido").objectReferenceValue = cartaoApelido;
        so.FindProperty("marcaApelido").objectReferenceValue = marcaApelido;
        so.FindProperty("pontoApelido").objectReferenceValue = pontoApelido;
        so.FindProperty("campoApelido").objectReferenceValue = campo;
        so.FindProperty("contador").objectReferenceValue = contador;
        so.FindProperty("cartaoAnonimo").objectReferenceValue = cartaoAnonimo;
        so.FindProperty("marcaAnonimo").objectReferenceValue = marcaAnonimo;
        so.FindProperty("pontoAnonimo").objectReferenceValue = pontoAnonimo;
        so.FindProperty("numeroAnonimo").objectReferenceValue = numero;
        so.FindProperty("linhaSalvos").objectReferenceValue = linha.gameObject;
        so.FindProperty("listaSalvos").objectReferenceValue = lista;
        so.FindProperty("chipModelo").objectReferenceValue = chipModelo;
        so.FindProperty("painelTodos").objectReferenceValue = painelTodos.gameObject;
        so.FindProperty("gradeTodos").objectReferenceValue = grade;
        so.FindProperty("botaoFecharTodos").objectReferenceValue = fecharTodos;
        so.FindProperty("aviso").objectReferenceValue = aviso;
        so.FindProperty("botaoCancelar").objectReferenceValue = cancelar;
        so.FindProperty("botaoComecar").objectReferenceValue = comecar;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Foco de teclado/controle fica nesta janela (e volta ao Menu ao fechar).
        var foco = raiz.AddComponent<FocoInicialUI>();
        var sf = new SerializedObject(foco);
        SerializedProperty cand = sf.FindProperty("candidatos");
        cand.arraySize = 2;
        cand.GetArrayElementAtIndex(0).objectReferenceValue = campo;
        cand.GetArrayElementAtIndex(1).objectReferenceValue = comecar;
        sf.ApplyModifiedPropertiesWithoutUndo();

        // Estado de edição: apelido marcado.
        marcaAnonimo.SetActive(false);
        pontoAnonimo.SetActive(false);
        return raiz;
    }

    private static void Cartao(RectTransform pai, string nome, string titulo, string descricao, float x, float largura,
        out Button botao, out GameObject marca, out GameObject ponto)
    {
        botao = TemaLabUI.Linha(pai, nome);
        Caixa((RectTransform)botao.transform, x, 96f, largura, 320f);

        RectTransform miolo = TemaLabUI.CaixaArredondada(botao.transform, "Marca", TemaLabUI.Azul, MioloMarcado, 4f);
        marca = miolo.parent.gameObject;
        TemaLabUI.Esticar((RectTransform)marca.transform);

        RectTransform anel = TemaLabUI.CaixaArredondada(botao.transform, "Radio", TemaLabUI.TextoSuave, TemaLabUI.Poco, 4f);
        Caixa((RectTransform)anel.parent, 30f, 30f, 36f, 36f);
        Sprite s = TemaLabUI.Tema != null ? TemaLabUI.Tema.bordaArredondada : null;
        Image p = TemaLabUI.Imagem(anel, "Ponto", s, TemaLabUI.Azul);
        p.color = TemaLabUI.Azul;
        TemaLabUI.Esticar(p.rectTransform, 5f);
        ponto = p.gameObject;

        TextMeshProUGUI t = TemaLabUI.Texto(botao.transform, "Titulo", titulo, 40f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Display, TextAlignmentOptions.MidlineLeft);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        Caixa(t.rectTransform, 82f, 22f, largura - 110f, 52f);

        TextMeshProUGUI d = TemaLabUI.Texto(botao.transform, "Descricao", descricao, 24f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.CorpoForte, TextAlignmentOptions.TopLeft);
        Caixa(d.rectTransform, 30f, 86f, largura - 60f, 70f);
    }

    private static TMP_InputField CampoApelido(Transform cartao, float largura)
    {
        RectTransform miolo = TemaLabUI.CaixaArredondada(cartao, "CampoApelido", TemaLabUI.Azul, TemaLabUI.Poco, 4f, raycast: true);
        RectTransform caixa = (RectTransform)miolo.parent;
        Caixa(caixa, 30f, 170f, largura, 96f);

        RectTransform area = TemaLabUI.Novo(caixa, "Area");
        TemaLabUI.Esticar(area);
        area.offsetMin = new Vector2(26f, 8f);
        area.offsetMax = new Vector2(-20f, -8f);
        area.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI dica = TemaLabUI.Texto(area, "Placeholder", "Seu apelido", 40f, new Color(0.6f, 0.66f, 0.77f, 0.6f), TemaLabUI.Fonte.Mono, TextAlignmentOptions.MidlineLeft);
        dica.textWrappingMode = TextWrappingModes.NoWrap;
        TemaLabUI.Esticar(dica.rectTransform);
        TextMeshProUGUI texto = TemaLabUI.Texto(area, "Texto", "", 44f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Mono, TextAlignmentOptions.MidlineLeft);
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.richText = false;
        TemaLabUI.Esticar(texto.rectTransform);

        var campo = caixa.gameObject.AddComponent<TMP_InputField>();
        campo.textViewport = area;
        campo.textComponent = texto;
        campo.placeholder = dica;
        campo.fontAsset = texto.font;
        campo.pointSize = 44f;
        campo.characterLimit = 12;
        campo.lineType = TMP_InputField.LineType.SingleLine;
        campo.caretColor = TemaLabUI.Azul;
        campo.customCaretColor = true;
        campo.caretWidth = 4;
        campo.selectionColor = new Color(0.29f, 0.55f, 0.97f, 0.45f);
        campo.richText = false;
        campo.targetGraphic = caixa.GetComponent<Image>();
        campo.transition = Selectable.Transition.None;
        return campo;
    }

    private static Button Chip(RectTransform pai, string nome)
    {
        RectTransform miolo = TemaLabUI.CaixaArredondada(pai, nome, TemaLabUI.Contorno, TemaLabUI.Poco, 3f, raycast: true);
        GameObject go = miolo.parent.gameObject;
        var botao = go.AddComponent<Button>();
        botao.targetGraphic = go.GetComponent<Image>();
        var cores = botao.colors;
        cores.normalColor = TemaLabUI.Contorno;
        cores.highlightedColor = TemaLabUI.Azul;
        cores.selectedColor = TemaLabUI.Azul;
        cores.pressedColor = TemaLabUI.Ciano;
        cores.colorMultiplier = 1f;
        botao.colors = cores;
        // A cor do contorno vem do ColorTint: a Image fica branca.
        go.GetComponent<Image>().color = Color.white;

        miolo.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var hl = go.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(20, 20, 8, 10);
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = hl.childControlHeight = true;
        hl.childForceExpandWidth = hl.childForceExpandHeight = false;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = 56f;

        // Apelido de 12 letras: a letra diminui até 18 e, se ainda não couber, "…".
        TextMeshProUGUI t = TemaLabUI.Texto(go.transform, "Texto", "Jogador", 24f, TemaLabUI.CorTexto, TemaLabUI.Fonte.MonoForte, TextAlignmentOptions.Center);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.richText = false;
        t.enableAutoSizing = true;
        t.fontSizeMin = 18f;
        t.fontSizeMax = 24f;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return botao;
    }

    private static RectTransform Rolagem(Transform painel, float largura)
    {
        RectTransform vista = TemaLabUI.Novo(painel, "Rolagem");
        Caixa(vista, 36f, 96f, largura - 72f, 350f);
        vista.gameObject.AddComponent<RectMask2D>();
        var img = vista.gameObject.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);

        RectTransform grade = TemaLabUI.Novo(vista, "Grade");
        grade.anchorMin = new Vector2(0f, 1f);
        grade.anchorMax = new Vector2(1f, 1f);
        grade.pivot = new Vector2(0.5f, 1f);
        grade.anchoredPosition = Vector2.zero;
        grade.sizeDelta = new Vector2(0f, 0f);
        var g = grade.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(250f, 56f);
        g.spacing = new Vector2(14f, 14f);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 4;
        var fit = grade.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = vista.gameObject.AddComponent<ScrollRect>();
        scroll.content = grade;
        scroll.viewport = vista;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        return grade;
    }

    // ------------------------------------------------------------------
    // Crachá no Menu
    // ------------------------------------------------------------------

    public static void ColocarCrachaNoMenu()
    {
        Scene menu = SceneManager.GetSceneByPath(MenuPath);
        bool jaAberta = menu.IsValid() && menu.isLoaded;
        if (jaAberta && menu.isDirty)
        {
            Debug.LogWarning("[NovoJogadorBuilder] A cena Menu tem mudanças não salvas — salve ou descarte antes. Nada foi feito.");
            return;
        }

        string pastaBackup = Path.Combine("SceneBackups", "NovoJogador_" + System.DateTime.Now.ToString("yyyy-MM-dd_HHmm"));
        Directory.CreateDirectory(pastaBackup);
        File.Copy(MenuPath, Path.Combine(pastaBackup, "Menu.unity"), true);

        if (!jaAberta) menu = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Additive);

        try
        {
            Transform painel = null;
            foreach (GameObject go in menu.GetRootGameObjects())
            {
                Transform t = go.transform.Find("PainelMenu");
                if (t != null) { painel = t; break; }
            }
            if (painel == null)
            {
                Debug.LogError("[NovoJogadorBuilder] Não achei Canvas/PainelMenu na cena Menu.");
                return;
            }

            Transform velho = painel.Find("CrachaJogador");
            if (velho != null) Object.DestroyImmediate(velho.gameObject);

            Button cracha = MontarCracha(painel);
            Transform sair = painel.Find("Botoes/Sair");
            if (sair != null)
            {
                Button bSair = sair.GetComponent<Button>();
                if (bSair != null && bSair.navigation.mode == Navigation.Mode.Explicit)
                {
                    Navigation n = bSair.navigation;
                    n.selectOnDown = cracha;
                    bSair.navigation = n;
                    var nc = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = bSair };
                    cracha.navigation = nc;
                }
            }

            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            Debug.Log("[NovoJogadorBuilder] Crachá salvo na cena Menu (backup em " + pastaBackup + ").");
        }
        finally
        {
            if (!jaAberta) EditorSceneManager.CloseScene(menu, true);
        }
    }

    private static Button MontarCracha(Transform painel)
    {
        TemaLaboratorio tema = TemaLabUI.Tema;
        Button b = TemaLabUI.Botao(painel, "CrachaJogador", "", tema != null ? tema.botaoSecundario : null, TemaLabUI.PainelElevado, out TextMeshProUGUI vazio);
        Object.DestroyImmediate(vazio.transform.parent.gameObject); // sem o "Conteudo" padrão
        RectTransform rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(64f, -836f);
        rt.sizeDelta = new Vector2(600f, 104f);

        RectTransform seloMiolo = TemaLabUI.CaixaArredondada(b.transform, "Selo", TemaLabUI.Azul, TemaLabUI.Azul, 0f);
        Caixa((RectTransform)seloMiolo.parent, 20f, 16f, 68f, 68f);
        TextMeshProUGUI selo = TemaLabUI.Texto(seloMiolo, "Texto", "?", 32f, Color.white, TemaLabUI.Fonte.Display, TextAlignmentOptions.Center);
        TemaLabUI.Esticar(selo.rectTransform).offsetMin = new Vector2(0f, 3f);

        TextMeshProUGUI rotulo = TemaLabUI.Rotulo(b.transform, "Rotulo", "Quem vai jogar?", 20f, TemaLabUI.TextoSuave);
        rotulo.characterSpacing = 5f;
        Caixa(rotulo.rectTransform, 108f, 20f, 280f, 26f);

        TextMeshProUGUI nome = TemaLabUI.Texto(b.transform, "Nome", "Ninguém escolhido", 40f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Display, TextAlignmentOptions.MidlineLeft);
        nome.textWrappingMode = TextWrappingModes.NoWrap;
        nome.enableAutoSizing = true;
        nome.fontSizeMin = 26f;
        nome.fontSizeMax = 40f;
        nome.richText = false;
        Caixa(nome.rectTransform, 108f, 44f, 290f, 48f);

        RectTransform acaoMiolo = TemaLabUI.CaixaArredondada(b.transform, "Acao", TemaLabUI.Azul, TemaLabUI.Painel, 3f);
        Caixa((RectTransform)acaoMiolo.parent, 410f, 22f, 170f, 60f);
        TextMeshProUGUI acao = TemaLabUI.Texto(acaoMiolo, "Texto", "Escolher", 26f, new Color32(0xCF, 0xE0, 0xFF, 0xFF), TemaLabUI.Fonte.CorpoForte, TextAlignmentOptions.Center);
        acao.textWrappingMode = TextWrappingModes.NoWrap;
        TemaLabUI.Esticar(acao.rectTransform).offsetMin = new Vector2(0f, 2f);

        var cracha = b.gameObject.AddComponent<CrachaJogadorUI>();
        var so = new SerializedObject(cracha);
        so.FindProperty("rotulo").objectReferenceValue = rotulo;
        so.FindProperty("nome").objectReferenceValue = nome;
        so.FindProperty("acao").objectReferenceValue = acao;
        so.FindProperty("selo").objectReferenceValue = selo;
        so.ApplyModifiedPropertiesWithoutUndo();
        return b;
    }

    // ------------------------------------------------------------------

    // Caixa a partir do canto superior esquerdo do pai.
    private static void Caixa(RectTransform rect, float x, float topo, float largura, float altura)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -topo);
        rect.sizeDelta = new Vector2(largura, altura);
    }

    // Botão no rodapé do conteúdo: à esquerda (ancora 0) ou à direita (1).
    private static void Fundo(RectTransform rect, float ancora, float largura, float altura)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(ancora, 0f);
        rect.pivot = new Vector2(ancora, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(largura, altura);
    }
}
