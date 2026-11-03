using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Painel de pausa exibido ao apertar ESC durante o gameplay: "Continuar",
/// "Reiniciar fase" (com confirmação, ver ConfirmarReinicioUI e ReinicioFase),
/// "Configurações" e "Menu".
///
/// Ponto de entrada da UI persistente do jogo. Se nenhuma instância existir
/// ainda, se auto-constrói na primeira cena carregada (Canvas + EventSystem,
/// se faltar + este painel + o SettingsPanelUI com abas) e funciona em
/// qualquer cena a partir daí — sem nenhuma configuração manual.
///
/// Também pode ser montado/ajustado visualmente no Editor (veja
/// Tools > UI do Jogo > Montar UI de Pausa e Configurações): a estrutura é
/// reconhecida pelo nome dos filhos ("PausePanel" e seus botões) e os
/// cliques são reconectados no Awake(), então editar cores/tamanhos/fontes
/// no Editor não quebra o funcionamento.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    public static PauseMenuUI Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    // Nome da cena do menu principal (Build Settings), usado pelo botão "Menu".
    private const string MainMenuSceneName = "Menu";

    private GameObject pausePanelRoot;
    private ConfirmarReinicioUI confirmarReinicio;
    private GameObject reiniciarButton;
    private GameObject dicaReiniciar;
    private GameObject rodapePausa;
    private bool eventSystemWatchSubscribed;

    // Congela a fase (Time.timeScale = 0) enquanto a pausa ou as Configurações
    // abertas a partir dela estão na tela, e devolve o valor anterior ao fechar.
    private bool congelado;
    private float timeScaleAntesDaPausa = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        // Tenta usar um prefab customizado (Assets/Resources/GameUICanvas.prefab)
        // primeiro, pra qualquer cena que carregar primeiro usar o MESMO visual
        // que você ajustou no Editor — sem isso, uma cena sem esse prefab
        // (ou carregada antes do Menu) montava um painel genérico do zero.
        GameObject prefab = Resources.Load<GameObject>("GameUICanvas");
        if (prefab != null)
        {
            // Instantiate já dispara o Awake() do PauseMenuUI copiado, que
            // cuida de Instance/DontDestroyOnLoad/EnsureBuilt() sozinho.
            Object.Instantiate(prefab);
        }

        if (Instance != null)
        {
            return; // funcionou via prefab
        }

        // Sem prefab (ou prefab sem PauseMenuUI): monta do zero por código, como antes.
        var canvasRoot = new GameObject("GameUICanvas (Persistente)");
        BuildCanvas(canvasRoot);

        var pauseMenu = canvasRoot.AddComponent<PauseMenuUI>();
        Instance = pauseMenu;
        pauseMenu.EnsureBuilt();
    }

    private static void BuildCanvas(GameObject canvasRoot)
    {
        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Acima dos puzzles (400) e do código montado (500), abaixo do véu de queda (900).
        canvas.sortingOrder = 600;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        canvasRoot.AddComponent<GraphicRaycaster>();
    }

    private static void EnsureEventSystemExists()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        var go = new GameObject("EventSystem (Fallback Persistente)");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureBuilt();
    }

    /// <summary>
    /// Constrói (ou reconecta, se já existir) a estrutura deste painel e
    /// garante o SettingsPanelUI como irmão. Público para poder ser chamado
    /// explicitamente por uma ferramenta de Editor ou pelo bootstrap
    /// automático, já que Awake() nem sempre roda de forma síncrona fora do
    /// Play Mode.
    /// </summary>
    public void EnsureBuilt()
    {
        EnsureEventSystemExists();

        if (!eventSystemWatchSubscribed)
        {
            eventSystemWatchSubscribed = true;
            SceneManager.sceneLoaded += (scene, mode) => EnsureEventSystemExists();
        }

        BuildOrFindStructure();
        WireEvents();

        pausePanelRoot.SetActive(false);
        if (rodapePausa != null) rodapePausa.SetActive(false);

        // Busca com "true" pra incluir um SettingsPanel salvo desativado na cena.
        // Se já existir, força EnsureBuilt() nele explicitamente — não dá pra confiar
        // no Awake() dele, porque Awake() nunca roda enquanto o GameObject está
        // desativado (e o normal é justamente terminar desativado, já que ele se
        // esconde sozinho ao fim da própria construção).
        SettingsPanelUI existingSettings = GetComponentInChildren<SettingsPanelUI>(true);
        if (existingSettings == null)
        {
            SettingsPanelUI.CreateUnder(transform);
        }
        else
        {
            existingSettings.EnsureBuilt();
        }
    }

    private void BuildOrFindStructure()
    {
        Transform existing = transform.Find("PausePanel");
        pausePanelRoot = existing != null ? existing.gameObject : BuildPausePanel();

        // Montados no prefab por Tools > UI Laboratório > 5. Reiniciar fase.
        Transform botao = pausePanelRoot.transform.Find("ReiniciarButton");
        reiniciarButton = botao != null ? botao.gameObject : null;
        // Dicas de teclas no rodapé da tela, fora da janela (ligadas junto com a pausa).
        Transform rodape = transform.Find("RodapePausa");
        rodapePausa = rodape != null ? rodape.gameObject : null;
        Transform dica = rodape != null ? rodape.Find("BarraDicas/Dica_Reiniciar") : null;
        dicaReiniciar = dica != null ? dica.gameObject : null;
        confirmarReinicio = GetComponentInChildren<ConfirmarReinicioUI>(true);
        if (confirmarReinicio != null)
        {
            confirmarReinicio.gameObject.SetActive(false);
        }
    }

    private GameObject BuildPausePanel()
    {
        var panel = new GameObject("PausePanel", typeof(RectTransform));
        var panelRect = (RectTransform)panel.transform;
        panelRect.SetParent(transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(420f, 360f);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.92f);

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18;
        layout.padding = new RectOffset(30, 30, 100, 30);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var title = UIBuildUtils.CreateText(panelRect, "Titulo", "PAUSADO", 30, TextAlignmentOptions.Top);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 60f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);

        CreateButtonPlaceholder(panelRect, "ContinuarButton", "Continuar");
        CreateButtonPlaceholder(panelRect, "ConfiguracoesButton", "Configurações");
        CreateButtonPlaceholder(panelRect, "MenuButton", "Menu");

        return panel;
    }

    private static void CreateButtonPlaceholder(RectTransform parent, string name, string label)
    {
        var buttonGO = UIBuildUtils.CreateButtonWithLabel(parent, name, label, out _);
        buttonGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 56f);
    }

    private void WireEvents()
    {
        WireButton("ContinuarButton", HidePausePanelOnly);
        WireButton("ReiniciarButton", AbrirConfirmacaoReinicio);
        WireButton("ConfiguracoesButton", OpenSettingsFromPause);
        WireButton("MenuButton", GoToMainMenu);
    }

    private void WireButton(string childName, UnityEngine.Events.UnityAction action)
    {
        Transform t = pausePanelRoot.transform.Find(childName);
        if (t == null)
        {
            return;
        }

        Button button = t.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void Update()
    {
        AtualizarCongelamento();

        // Durante a transição "Compilando" o Esc não faz nada: a grade cobre a
        // tela, e a pausa (ou as Configurações, no Menu) abriria escondida e
        // apareceria aberta na cena nova, com a fase congelada.
        if (TrocaDeCena.EmAndamento)
        {
            return;
        }

        if (InputManager.Instance == null || InputManager.Instance.UICancelAction == null)
        {
            return;
        }

        AbrirConfirmacaoPelaTecla();

        if (!InputManager.Instance.UICancelAction.WasPressedThisFrame())
        {
            return;
        }

        // Durante um rebind, ESC só cancela o rebind (o InputManager já trata isso).
        if (InputManager.Instance.IsRebinding)
        {
            return;
        }

        // Confirmação "Reiniciar a fase?" por cima da pausa: Esc = Cancelar.
        if (ConfirmacaoAberta)
        {
            confirmarReinicio.Cancelar();
            return;
        }

        // Esc de uma janela que fecha com Esc fecha só ela.
        if (EscDeOutraJanela())
        {
            return;
        }

        // No Menu principal não existe "gameplay" pra pausar/continuar, então ESC
        // pula o painel de pausa e alterna direto a tela de Configurações.
        if (SceneManager.GetActiveScene().name == MainMenuSceneName)
        {
            if (SettingsPanelUI.Instance == null)
            {
                return;
            }

            if (SettingsPanelUI.Instance.IsVisible)
            {
                SettingsPanelUI.Instance.Hide();
            }
            else
            {
                SettingsPanelUI.Instance.Show(cameFromPauseMenu: false);
            }

            return;
        }

        // Cenas de jogo: ESC "volta um nível" — fecha Configurações primeiro (se
        // aberta), senão alterna o painel de pausa.
        if (SettingsPanelUI.Instance != null && SettingsPanelUI.Instance.IsVisible)
        {
            SettingsPanelUI.Instance.Hide();
            return;
        }

        if (pausePanelRoot.activeSelf)
        {
            HidePausePanelOnly();
        }
        else
        {
            ShowPausePanelOnly();
        }
    }

    /// <summary>
    /// Janelas que tratam o próprio Esc: abertas (ou no frame em que fecharam),
    /// a pausa e as Configurações ignoram o Esc. Janela nova com Esc entra aqui.
    /// </summary>
    private static bool EscDeOutraJanela()
    {
        return NovoJogadorUI.SegurandoEsc
            || CreditosUI.SegurandoEsc
            || SeletorFasesUI.SegurandoEsc
            || ObjetoAnimatorUI.SegurandoEsc
            || LampadaAnimatorUI.SegurandoEsc
            || IADiagramaUI.SegurandoEsc;
    }

    private void AtualizarCongelamento()
    {
        bool deveCongelar = SceneManager.GetActiveScene().name != MainMenuSceneName
            && (PausaAberta || (SettingsPanelUI.Instance != null && SettingsPanelUI.Instance.IsVisible));

        if (deveCongelar == congelado)
        {
            return;
        }

        congelado = deveCongelar;
        if (congelado)
        {
            timeScaleAntesDaPausa = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = timeScaleAntesDaPausa;
        }
    }

    /// <summary>Painel de pausa na tela (a fase fica congelada enquanto ele está aberto).</summary>
    public bool PausaAberta => pausePanelRoot != null && pausePanelRoot.activeSelf;

    /// <summary>Confirmação "Reiniciar a fase?" na tela (por cima da pausa).</summary>
    public bool ConfirmacaoAberta => confirmarReinicio != null && confirmarReinicio.Aberto;

    public void ShowPausePanelOnly()
    {
        AjustarBotaoReiniciar();
        pausePanelRoot.SetActive(true);
        if (rodapePausa != null) rodapePausa.SetActive(true);
    }

    /// <summary>
    /// "Reiniciar fase" só aparece nas fases (no Questionario a pausa fica sem
    /// ele) e a navegação por teclado/controle segue os botões visíveis.
    /// </summary>
    private void AjustarBotaoReiniciar()
    {
        bool mostrar = ReinicioFase.CenaDeFase;
        if (reiniciarButton != null) reiniciarButton.SetActive(mostrar);
        if (dicaReiniciar != null) dicaReiniciar.SetActive(mostrar);

        var ordem = new System.Collections.Generic.List<Selectable>();
        foreach (string nome in new[] { "ContinuarButton", "ReiniciarButton", "ConfiguracoesButton", "MenuButton" })
        {
            Transform t = pausePanelRoot.transform.Find(nome);
            Selectable s = t != null ? t.GetComponent<Selectable>() : null;
            if (s != null && t.gameObject.activeSelf)
            {
                ordem.Add(s);
            }
        }

        for (int i = 0; i < ordem.Count; i++)
        {
            ordem[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i > 0 ? ordem[i - 1] : null,
                selectOnDown = i < ordem.Count - 1 ? ordem[i + 1] : null,
            };
        }

        if (reiniciarButton != null)
        {
            reiniciarButton.GetComponent<Button>().interactable = ReinicioFase.PodeReiniciar;
        }
    }

    // Na pausa, um toque na tecla de Reiniciar abre a confirmação (a dica "R Reiniciar").
    private void AbrirConfirmacaoPelaTecla()
    {
        InputManager input = InputManager.Instance;
        if (!PausaAberta || ConfirmacaoAberta || input.IsRebinding || input.RestartAction == null
            || (SettingsPanelUI.Instance != null && SettingsPanelUI.Instance.IsVisible))
        {
            return;
        }

        if (reiniciarButton != null && reiniciarButton.activeSelf && input.RestartAction.WasPressedThisFrame())
        {
            AbrirConfirmacaoReinicio();
        }
    }

    private void AbrirConfirmacaoReinicio()
    {
        if (confirmarReinicio == null || !ReinicioFase.PodeReiniciar)
        {
            return;
        }

        confirmarReinicio.Abrir(ReiniciarPelaPausa, null);
    }

    private void ReiniciarPelaPausa()
    {
        // Mesma ordem do GoToMainMenu: fecha tudo e solta o congelamento antes da troca de cena.
        SettingsPanelUI.Instance?.Hide();
        HidePausePanelOnly();
        congelado = false;
        Time.timeScale = 1f;
        ReinicioFase.Executar("botão pausa");
    }

    public void HidePausePanelOnly()
    {
        pausePanelRoot.SetActive(false);
        if (rodapePausa != null) rodapePausa.SetActive(false);
    }

    private void OpenSettingsFromPause()
    {
        HidePausePanelOnly();
        SettingsPanelUI.Instance?.Show(cameFromPauseMenu: true);
    }

    private void GoToMainMenu()
    {
        // Ordem importa: SettingsPanelUI.Hide() pode reabrir o painel de
        // pausa sozinho (efeito colateral de "voltar pro menu de pausa" —
        // ver returnToPauseMenuOnHide em SettingsPanelUI), se em algum
        // momento anterior as Configurações tiverem sido abertas a partir
        // da pausa. Por isso HidePausePanelOnly() roda por ÚLTIMO aqui, pra
        // garantir que o painel de pausa termine escondido de qualquer
        // jeito antes de trocar de cena.
        SettingsPanelUI.Instance?.Hide();
        HidePausePanelOnly();
        congelado = false;
        Time.timeScale = 1f;
        TrocaDeCena.Carregar(MainMenuSceneName);
    }
}
