using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Painel de "Configurações" com abas: Áudio, Gráfico, Controles e (se existir
/// no prefab) Jogo.
///
/// Reconhece a própria estrutura pelo nome dos filhos ("TabBar",
/// "ContentArea", "VoltarButton"): se já existirem (montados/ajustados
/// visualmente no Editor via GameUIBuilder), apenas reconecta os cliques;
/// senão, constrói a estrutura padrão em runtime. Isso permite abrir este
/// GameObject no Editor e mexer em cores, tamanhos, fontes e imagens sem
/// quebrar o funcionamento.
/// </summary>
public class SettingsPanelUI : MonoBehaviour
{
    public static SettingsPanelUI Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

    private static readonly Color TabNormalColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    private static readonly Color TabSelectedColor = new Color(0.1f, 0.4f, 0.75f, 1f);

    private GameObject panelRoot;
    private GameObject audioContent;
    private GameObject graphicsContent;
    private GameObject controlsContent;
    private GameObject gameContent;     // aba "Jogo" (opcional, criada pelo construtor do tema)
    private Button audioTabButton;
    private Button graphicsTabButton;
    private Button controlsTabButton;
    private Button gameTabButton;
    private Button backButton;
    private Button restaurarButton;   // "Restaurar padrão" das abas Gráficos e Jogo (a de Controles tem o seu)
    private int abaAtual;
    private bool returnToPauseMenuOnHide;
    private bool built;

    // Rodapé: durante a troca de tecla, "Esc Voltar" vira "Esc Cancelar".
    private const string RotuloEscTrocando = "Cancelar";
    private TMP_Text rotuloEsc;
    private string rotuloEscNormal;
    private bool rodapeTrocando;

    /// <summary>Cria o painel de configurações dentro do Canvas persistente informado.</summary>
    public static SettingsPanelUI CreateUnder(Transform parent)
    {
        var go = new GameObject("SettingsPanel", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var panel = go.AddComponent<SettingsPanelUI>();

        // Chamado explicitamente (não só via Awake) para ser determinístico
        // independente do Awake() rodar de forma síncrona ou não aqui.
        panel.EnsureBuilt();

        return panel;
    }

    private void Awake()
    {
        // Guarda contra duplicatas — importante porque uma cena pode ter uma
        // cópia deste painel salva nela (ex: montada com uma ferramenta de
        // Editor). Ao recarregar essa cena, a cópia salva volta a existir e
        // tenta se registrar como Instance ANTES de ser destruída (Destroy()
        // só acontece no fim do frame) — sem essa checagem, Instance ficava
        // apontando pra um objeto já destruído, causando
        // MissingReferenceException no próximo Show().
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Se o PauseMenuUI já chamou EnsureBuilt() antes deste Awake (o painel foi
        // desligado antes de acordar), o Awake só roda na primeira vez que o painel
        // é mostrado: construir de novo aqui o esconderia logo depois de abrir.
        if (!built)
        {
            EnsureBuilt();
        }
    }

    /// <summary>
    /// Constrói (ou reconecta, se já existir) a estrutura deste painel e de
    /// suas abas, e define Instance. Público para poder ser chamado
    /// explicitamente de fora (por PauseMenuUI ou uma ferramenta de Editor):
    /// Awake() nunca dispara enquanto o GameObject está desativado — e é
    /// justamente esse o estado normal em que este painel termina, já que
    /// ele se esconde sozinho ao fim da própria construção. Sem uma chamada
    /// explícita, um SettingsPanel salvo desativado nunca inicializaria.
    /// </summary>
    public void EnsureBuilt()
    {
        Instance = this;
        built = true;
        panelRoot = gameObject;
        UIBuildUtils.StretchToParent((RectTransform)transform);

        BuildOrFindStructure();
        WireEvents();

        ShowTab(0);
        panelRoot.SetActive(false);
    }

    private void BuildOrFindStructure()
    {
        if (GetComponent<Image>() == null)
        {
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.9f);
        }

        if (transform.Find("Titulo") == null)
        {
            var titleGO = UIBuildUtils.CreateText(transform, "Titulo", "CONFIGURAÇÕES", 34, TextAlignmentOptions.Top);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 80f);
            titleRect.anchoredPosition = new Vector2(0f, -20f);
        }

        Transform tabBar = transform.Find("TabBar");
        if (tabBar == null)
        {
            tabBar = BuildTabBar();
        }

        audioTabButton = tabBar.Find("AudioTabButton").GetComponent<Button>();
        graphicsTabButton = tabBar.Find("GraphicsTabButton").GetComponent<Button>();
        controlsTabButton = tabBar.Find("ControlsTabButton").GetComponent<Button>();
        Transform gameTabT = tabBar.Find("GameTabButton");
        gameTabButton = gameTabT != null ? gameTabT.GetComponent<Button>() : null;

        Transform contentArea = transform.Find("ContentArea");
        if (contentArea == null)
        {
            contentArea = BuildContentArea();
        }

        audioContent = contentArea.Find("AudioContent").gameObject;
        graphicsContent = contentArea.Find("GraphicsContent").gameObject;
        controlsContent = contentArea.Find("ControlsContent").gameObject;
        Transform gameContentT = contentArea.Find("GameContent");
        gameContent = gameContentT != null ? gameContentT.gameObject : null;

        // Garante que cada aba tenha seu componente e construa/reconecte sua própria estrutura.
        // Chamado explicitamente (não só via Awake) porque Awake() nem sempre roda de forma
        // síncrona fora do Play Mode — sem isso, uma ferramenta de Editor veria abas vazias.
        EnsureComponent<AudioTabUI>(audioContent).EnsureBuilt();
        EnsureComponent<GraphicsTabUI>(graphicsContent).EnsureBuilt();
        EnsureComponent<ControlsTabUI>(controlsContent).EnsureBuilt();

        Transform backT = transform.Find("VoltarButton");
        if (backT == null)
        {
            backT = BuildBackButton().transform;
        }

        backButton = backT.GetComponent<Button>();

        // Só em Play: numa ferramenta de Editor a cópia iria parar no prefab.
        if (Application.isPlaying)
        {
            GarantirBotaoRestaurar(backT as RectTransform);
        }

        GarantirFaixaRodape();
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    /// <summary>
    /// Faixa escura embaixo da barra de teclas (como nos Créditos). O véu é
    /// translúcido, e sem a faixa a barra da tela de trás (Menu, Questionário,
    /// Oficina) aparece misturada com a das Configurações.
    /// </summary>
    private void GarantirFaixaRodape()
    {
        Transform rodape = transform.Find("Rodape");
        if (rodape == null || transform.Find("FaixaLegenda") != null)
        {
            return;
        }

        var faixa = new GameObject("FaixaLegenda", typeof(RectTransform));
        var fx = (RectTransform)faixa.transform;
        fx.SetParent(transform, false);
        fx.anchorMin = new Vector2(0f, 0f);
        fx.anchorMax = new Vector2(1f, 0f);
        fx.pivot = new Vector2(0.5f, 0f);
        fx.anchoredPosition = Vector2.zero;
        fx.sizeDelta = new Vector2(0f, 96f);
        fx.SetSiblingIndex(rodape.GetSiblingIndex());

        var img = faixa.AddComponent<Image>();
        img.color = TemaLabUI.Fundo;
        img.raycastTarget = false;
    }

    private Transform BuildTabBar()
    {
        var tabBar = new GameObject("TabBar", typeof(RectTransform));
        var tabBarRect = (RectTransform)tabBar.transform;
        tabBarRect.SetParent(transform, false);
        tabBarRect.anchorMin = new Vector2(0.15f, 1f);
        tabBarRect.anchorMax = new Vector2(0.85f, 1f);
        tabBarRect.pivot = new Vector2(0.5f, 1f);
        tabBarRect.sizeDelta = new Vector2(0f, 60f);
        tabBarRect.anchoredPosition = new Vector2(0f, -100f);

        var layout = tabBar.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        UIBuildUtils.CreateButtonWithLabel(tabBarRect, "AudioTabButton", "Áudio", out _);
        UIBuildUtils.CreateButtonWithLabel(tabBarRect, "GraphicsTabButton", "Gráficos", out _);
        UIBuildUtils.CreateButtonWithLabel(tabBarRect, "ControlsTabButton", "Controles", out _);

        return tabBar.transform;
    }

    private Transform BuildContentArea()
    {
        var area = new GameObject("ContentArea", typeof(RectTransform));
        var areaRect = (RectTransform)area.transform;
        areaRect.SetParent(transform, false);
        areaRect.anchorMin = new Vector2(0.15f, 0.18f);
        areaRect.anchorMax = new Vector2(0.85f, 0.8f);
        areaRect.offsetMin = Vector2.zero;
        areaRect.offsetMax = Vector2.zero;

        BuildSimpleTabContent(areaRect, "AudioContent");
        BuildSimpleTabContent(areaRect, "GraphicsContent");
        BuildPlainTabContent(areaRect, "ControlsContent");

        return area.transform;
    }

    private static GameObject BuildSimpleTabContent(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        UIBuildUtils.StretchToParent(rect);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 20;
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return go;
    }

    // Sem LayoutGroup de propósito: ControlsTabUI posiciona seus próprios filhos
    // (ScrollArea + botão Restaurar Padrão) com âncoras específicas. Um
    // VerticalLayoutGroup no pai ignoraria essas âncoras e forçaria seu próprio
    // empilhamento, espremendo a área de scroll das linhas a quase zero de altura.
    private static GameObject BuildPlainTabContent(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        UIBuildUtils.StretchToParent(rect);

        return go;
    }

    private GameObject BuildBackButton()
    {
        var backGO = UIBuildUtils.CreateButtonWithLabel(transform, "VoltarButton", "Voltar", out _);
        var backRect = backGO.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.35f, 0f);
        backRect.anchorMax = new Vector2(0.65f, 0f);
        backRect.pivot = new Vector2(0.5f, 0f);
        backRect.sizeDelta = new Vector2(0f, 60f);
        backRect.anchoredPosition = new Vector2(0f, 24f);
        return backGO;
    }

    private void WireEvents()
    {
        audioTabButton.onClick.RemoveAllListeners();
        audioTabButton.onClick.AddListener(() => ShowTab(0));

        graphicsTabButton.onClick.RemoveAllListeners();
        graphicsTabButton.onClick.AddListener(() => ShowTab(1));

        controlsTabButton.onClick.RemoveAllListeners();
        controlsTabButton.onClick.AddListener(() => ShowTab(2));

        if (gameTabButton != null)
        {
            gameTabButton.onClick.RemoveAllListeners();
            gameTabButton.onClick.AddListener(() => ShowTab(3));
        }

        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(Hide);
    }

    // Cópia do botão Voltar, espelhada no canto esquerdo da janela. Volta ao
    // padrão só o que está na aba aberta (ver ConfiguracoesJogo.Restaurar*).
    private void GarantirBotaoRestaurar(RectTransform voltar)
    {
        const string Nome = "RestaurarPadraoAbaButton";
        Transform existente = transform.Find(Nome);
        GameObject go = existente != null ? existente.gameObject : Instantiate(voltar.gameObject, transform);
        go.name = Nome;

        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = new Vector2(-voltar.anchoredPosition.x, voltar.anchoredPosition.y);

        Transform icone = go.transform.Find("Icone");
        if (icone != null) icone.gameObject.SetActive(false);

        Transform rotulo = go.transform.Find("Label");
        if (rotulo != null && rotulo.TryGetComponent(out TMP_Text texto))
        {
            var r = (RectTransform)rotulo;
            r.anchoredPosition = new Vector2(0f, r.anchoredPosition.y);
            r.sizeDelta = new Vector2(-56f, r.sizeDelta.y);
            texto.text = "Restaurar padrão";
            texto.enableAutoSizing = true;
            texto.fontSizeMax = texto.fontSize;
            texto.fontSizeMin = Mathf.Min(18f, texto.fontSize);
            texto.alignment = TextAlignmentOptions.Center;
        }

        restaurarButton = go.GetComponent<Button>();
        restaurarButton.onClick = new Button.ButtonClickedEvent();
        restaurarButton.onClick.AddListener(RestaurarAbaAtual);
        var nav = restaurarButton.navigation;
        nav.mode = Navigation.Mode.Automatic;
        restaurarButton.navigation = nav;
    }

    private void RestaurarAbaAtual()
    {
        if (abaAtual == 1) ConfiguracoesJogo.RestaurarGraficos();
        else if (abaAtual == 3) ConfiguracoesJogo.RestaurarJogo();
    }

    private void ShowTab(int index)
    {
        abaAtual = index;
        if (restaurarButton != null) restaurarButton.gameObject.SetActive(index == 1 || index == 3);

        audioContent.SetActive(index == 0);
        graphicsContent.SetActive(index == 1);
        controlsContent.SetActive(index == 2);
        if (gameContent != null) gameContent.SetActive(index == 3);

        SetTabSelected(audioTabButton, index == 0);
        SetTabSelected(graphicsTabButton, index == 1);
        SetTabSelected(controlsTabButton, index == 2);
        if (gameTabButton != null) SetTabSelected(gameTabButton, index == 3);
    }

    private static void SetTabSelected(Button button, bool selected)
    {
        // Tema Laboratório: a aba troca de sprite (aba_ativa/aba_inativa) em vez de cor.
        var aba = button.GetComponent<AbaVisualUI>();
        if (aba != null)
        {
            aba.Definir(selected);
            return;
        }

        var image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = selected ? TabSelectedColor : TabNormalColor;
        }
    }

    private void Update()
    {
        bool trocando = InputManager.Instance != null && InputManager.Instance.IsRebinding;
        if (trocando == rodapeTrocando)
        {
            return;
        }

        rodapeTrocando = trocando;

        if (rotuloEsc == null)
        {
            Transform t = AcharFilho(transform, "Rotulo_Voltar");
            rotuloEsc = t != null ? t.GetComponent<TMP_Text>() : null;
            rotuloEscNormal = rotuloEsc != null ? rotuloEsc.text : null;
        }

        if (rotuloEsc != null)
        {
            rotuloEsc.text = trocando ? RotuloEscTrocando : rotuloEscNormal;
        }
    }

    private static Transform AcharFilho(Transform pai, string nome)
    {
        foreach (Transform filho in pai)
        {
            if (filho.name == nome) return filho;
            Transform achado = AcharFilho(filho, nome);
            if (achado != null) return achado;
        }
        return null;
    }

    /// <param name="cameFromPauseMenu">
    /// Se true, ao fechar (Voltar/ESC) volta pro Menu de Pausa em vez de fechar tudo.
    /// </param>
    public void Show(bool cameFromPauseMenu)
    {
        returnToPauseMenuOnHide = cameFromPauseMenu;
        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        panelRoot.SetActive(false);

        if (returnToPauseMenuOnHide && PauseMenuUI.Instance != null)
        {
            PauseMenuUI.Instance.ShowPausePanelOnly();
        }
    }
}
