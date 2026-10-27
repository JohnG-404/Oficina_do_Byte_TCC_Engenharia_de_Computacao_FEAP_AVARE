using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Assistente de configuração de um InteractableItem, em 2 colunas lado a
/// lado: AÇÃO (sempre visível) e CONSEQUÊNCIA (só aparece depois de
/// escolher uma Ação) — só as opções que aquele item permite aparecem em
/// cada coluna. Clicar numa opção apenas a seleciona (destaca); só quando
/// as duas colunas tiverem uma opção escolhida o botão "Salvar e Aplicar"
/// fica clicável. "Cancelar" fecha sem aplicar nada.
///
/// Cada coluna clona os próprios botões a partir do SEU template
/// (actionButtonTemplate / consequenceButtonTemplate) — dois GameObjects
/// desativados dentro do painel (ActionButtonTemplate/
/// ConsequenceButtonTemplate). Edite a aparência de cada um livremente no
/// Editor (cor, sprite, fonte...) pra dar um design diferente pra cada
/// coluna; o destaque de "selecionado" liga/desliga um filho
/// "SelectedIndicator" em vez de sobrescrever a cor do botão, então não
/// atrapalha nenhum design que você aplicar.
///
/// Pra ligar cada opção ao Unity, o painel monta sozinho (em tempo de
/// execução, sem precisar mexer na cena/prefab):
/// - em cada opção de Ação, uma segunda linha menor com o equivalente no
///   Unity (ex: "Is Trigger · OnTriggerEnter2D"). Se o template já tiver um
///   filho "UnityLine" com TMP_Text, ele é usado no lugar;
/// - duas linhas embaixo das colunas: "Quem ativa: tag Player" (só
///   informativa) e "Repetir: Uma vez / Sempre" (escolha do jogador, que
///   vai pro InteractableItem.Configure). Ficam num filho "RegrasExtras" do
///   painel: se você criar um com esse nome (ou arrastar um RectTransform
///   pra rulesRowsRoot), as linhas são montadas dentro dele; senão ele é
///   criado à esquerda do botão Salvar.
///
/// Também mostra avisos rápidos (ShowHint) — ex: quando falta um item
/// pré-requisito pra Ação "Precisa de outro item".
/// </summary>
public class InteractionConfigUI : MonoBehaviour
{
    public static InteractionConfigUI Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    [Header("Referências (ver Tools > Interação > Montar Cena de Interação)")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;

    [Header("Coluna Ação (sempre visível)")]
    [SerializeField] private RectTransform actionOptionsContainer;
    [SerializeField] private Button actionButtonTemplate;

    [Header("Coluna Consequência (só aparece após escolher uma Ação)")]
    [SerializeField] private GameObject consequenceColumnRoot;
    [SerializeField] private RectTransform consequenceOptionsContainer;
    [SerializeField] private Button consequenceButtonTemplate;
    [Tooltip("Opcional: aviso mostrado no lugar da coluna Consequência enquanto nenhuma Ação foi escolhida.")]
    [SerializeField] private GameObject consequencePlaceholder;

    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [Tooltip("Opcional: um segundo botão que também fecha sem aplicar (ex: \"Cancelar\" ao lado do Salvar, além do X).")]
    [SerializeField] private Button cancelButtonSecundario;

    [SerializeField] private GameObject hintRoot;
    [SerializeField] private TMP_Text hintText;

    [Header("Reconfigurar (item já configurado)")]
    [Tooltip("Faixa \"Reconfigurar item · Atual: Ação » Consequência\" no topo do painel, só visível ao reabrir um item configurado.")]
    [SerializeField] private GameObject reconfigurarRoot;
    [SerializeField] private TMP_Text reconfigurarTexto;
    [Tooltip("Rótulo \"Configurar item\" que dá lugar à faixa Reconfigurar.")]
    [SerializeField] private GameObject rotuloConfigurar;
    [Tooltip("Texto do botão de confirmar ao reconfigurar. O texto normal é lido do próprio botão.")]
    [SerializeField] private string textoSalvarMudanca = "Salvar mudança";

    [Header("Equivalente no Unity (linha menor em cada Ação)")]
    [Tooltip("Cor da linha com o equivalente no Unity (ciano, como nas peças de código).")]
    [SerializeField] private Color unityLineColor = new Color(0.263f, 0.820f, 0.851f, 1f);
    [Tooltip("Fonte de código da linha do Unity e dos chips. Vazio = fonte mono do tema Laboratório (ou a padrão do TMP).")]
    [SerializeField] private TMP_FontAsset codeFont;
    [Tooltip("Tamanho da fonte da linha do equivalente no Unity.")]
    [SerializeField] private float unityLineFontSize = 16f;
    [Tooltip("Espaçamento fixo entre letras (tag <mspace> do TMP, em em) pra linha parecer código monoespaçado. 0 = desliga.")]
    [SerializeField] private float unityLineMonoSpacing = 0.56f;
    [Tooltip("Espaço livre à direita da linha, pra não passar por cima do checkbox (Check) do botão.")]
    [SerializeField] private float unityLineRightMargin = 70f;
    [Tooltip("Parte de baixo do botão (0..1) reservada pra linha do Unity; o nome da opção sobe pro resto.")]
    [Range(0.2f, 0.6f)]
    [SerializeField] private float unityLineHeightFraction = 0.42f;

    [Header("Linhas 'Quem ativa' e 'Repetir' (montadas sozinhas)")]
    [Tooltip("Opcional: onde montar as duas linhas. Vazio = usa um filho 'RegrasExtras' do painel, ou cria um à esquerda do botão Salvar.")]
    [SerializeField] private RectTransform rulesRowsRoot;
    [SerializeField] private string whoActivatesLabel = "Quem ativa:";
    [SerializeField] private string whoActivatesChip = "tag Player";
    [SerializeField] private string whoActivatesHint = "inimigos e caixas passam sem ativar";
    [SerializeField] private string repeatLabel = "Repetir:";
    [SerializeField] private string repeatOnceText = "Uma vez";
    [SerializeField] private string repeatAlwaysText = "Sempre";
    [Tooltip("Aviso ao lado dos botões quando a Consequência escolhida não pode repetir (Sempre desabilitado).")]
    [SerializeField] private string repeatLockedHint = "essa consequência só acontece uma vez";
    [SerializeField] private float rowsFontSize = 20f;
    [SerializeField] private float chipFontSize = 16f;
    [SerializeField] private float rowsHintFontSize = 15f;
    [SerializeField] private Color rowsLabelColor = new Color(0.75f, 0.8f, 0.9f, 1f);
    [SerializeField] private Color rowsHintColor = new Color(0.55f, 0.6f, 0.71f, 1f);
    [SerializeField] private Color chipTextColor = new Color(0.86f, 0.9f, 0.96f, 1f);
    [SerializeField] private Color chipSelectedColor = new Color(0.96f, 0.77f, 0.26f, 1f);
    [SerializeField] private Color chipBackgroundColor = new Color(0.06f, 0.09f, 0.16f, 1f);
    [SerializeField] private Color chipBorderColor = new Color(0.27f, 0.32f, 0.42f, 1f);

    private const string UnityLineName = "UnityLine";
    private const string RulesRootName = "RegrasExtras";

    private static readonly Dictionary<ActionType, string> ActionLabels = new Dictionary<ActionType, string>
    {
        { ActionType.PressButton, "Pressionar um botão" },
        { ActionType.WalkNear, "Apenas passar perto" },
        { ActionType.JumpOnTop, "Pular em cima" },
        { ActionType.RequiresOtherItem, "Precisar de outro item" },
        { ActionType.RequiresOneCard, "Ter coletado 1 cartão" },
        { ActionType.RequiresTwoCards, "Ter coletado 2 cartões" },
        { ActionType.Touch, "Tocar nele" },
        { ActionType.StayOnTop, "Ficar em cima 1 segundo" },
        { ActionType.LeaveArea, "Sair de perto" },
        { ActionType.RequiresMissingHealth, "Ter perdido vida" },
        { ActionType.PressButtonThreeTimes, "Apertar 3 vezes" },
    };

    // O que cada Ação é no Unity, fiel ao que InteractableItem faz de verdade:
    // PressButton/WalkNear/cartões/outro item usam a ProximityZone (Is Trigger,
    // OnTriggerEnter2D); JumpOnTop usa a TopZone, um trigger fino no topo;
    // Touch é colisão sólida (OnCollisionEnter2D).
    private static readonly Dictionary<ActionType, string> ActionUnityEquivalents = new Dictionary<ActionType, string>
    {
        { ActionType.PressButton, "trigger + botão Interagir" },
        { ActionType.WalkNear, "Is Trigger · OnTriggerEnter2D" },
        { ActionType.JumpOnTop, "trigger no topo · OnTriggerEnter2D" },
        { ActionType.RequiresOtherItem, "if (outroItem.ativado)" },
        { ActionType.RequiresOneCard, "if (cartoes >= 1)" },
        { ActionType.RequiresTwoCards, "if (cartoes >= 2)" },
        { ActionType.Touch, "OnCollisionEnter2D" },
        { ActionType.StayOnTop, "OnTriggerStay2D + tempo" },
        { ActionType.LeaveArea, "OnTriggerExit2D" },
        { ActionType.RequiresMissingHealth, "if (vida < vidaMax)" },
        { ActionType.PressButtonThreeTimes, "if (++cliques == 3)" },
    };

    private static readonly Dictionary<ConsequenceType, string> ConsequenceLabels = new Dictionary<ConsequenceType, string>
    {
        { ConsequenceType.HealPlayer, "Curar o jogador" },
        { ConsequenceType.OpenDoor, "Abrir uma porta" },
        { ConsequenceType.LaunchPlayer, "Lançar o jogador" },
        { ConsequenceType.CompleteLevel, "Passar a fase" },
        { ConsequenceType.RestartLevel, "Reiniciar a fase" },
        { ConsequenceType.ActivateMovingPlatform, "Ativar a plataforma" },
        { ConsequenceType.CollectCard, "Pegar o cartão" },
        { ConsequenceType.DamageOneHeart, "Causar 1 de dano" },
        { ConsequenceType.DamageTwoHearts, "Causar 2 de dano" },
        { ConsequenceType.Repel, "Repelir o jogador" },
        { ConsequenceType.BreakAndRespawn, "Quebrar e voltar depois" },
        { ConsequenceType.BounceAndReturn, "Subir e voltar" },
        { ConsequenceType.DisableForSeconds, "Desligar por 3 segundos" },
        { ConsequenceType.TurnOnBehindPlayer, "Ligar depois que passar" },
        { ConsequenceType.ReturnToSafePoint, "Voltar ao ponto seguro" },
        { ConsequenceType.SinkIntoGround, "Afundar no chão" },
        { ConsequenceType.ShieldForSeconds, "Dar escudo por 3 segundos" },
        { ConsequenceType.IncreaseMaxHealth, "Aumentar a vida máxima" },
        { ConsequenceType.SlowPlayer, "Deixar o jogador lento" },
        { ConsequenceType.InvertControls, "Inverter os controles" },
        { ConsequenceType.FallAfterDelay, "Cair depois de um tempo" },
        { ConsequenceType.MakeSlippery, "Ficar escorregadia" },
        { ConsequenceType.LaunchSideways, "Lançar para o lado" },
        { ConsequenceType.OpenDoorForSeconds, "Abrir por 5 segundos" },
        { ConsequenceType.PassableWhileNear, "Deixar passar quando perto" },
        { ConsequenceType.OneWayFromBelow, "Deixar passar por baixo" },
        { ConsequenceType.TurnOnLamp, "Acender a lâmpada" },
        { ConsequenceType.TurnOnLampForSeconds, "Acender por 5 segundos" },
    };

    // Um "chip" (etiqueta com borda) das linhas Quem ativa/Repetir.
    private class Chip
    {
        public GameObject root;
        public Button button;
        public Image background;
        public Image border;
        public TMP_Text label;
    }

    private InteractableItem currentItem;
    private ActionType? chosenAction;
    private ConsequenceType? chosenConsequence;
    private bool chosenRepeats;
    private Button selectedActionButton;
    private Button selectedConsequenceButton;
    private readonly List<GameObject> spawnedActionButtons = new List<GameObject>();
    private readonly List<GameObject> spawnedConsequenceButtons = new List<GameObject>();
    private Coroutine hintRoutine;

    private bool rulesRowsBuilt;
    private GameObject repeatRow;
    private Chip onceChip;
    private Chip alwaysChip;
    private TMP_Text repeatHintText;

    // Reconfigurar: escolha que o item tinha ao abrir o painel.
    private bool reconfigurando;
    private ActionType acaoAntiga;
    private ConsequenceType consequenciaAntiga;
    private bool repetirAntigo;
    private TMP_Text confirmLabel;
    private string textoSalvarNormal;

    private void Awake()
    {
        Instance = this;

        if (panelRoot != null) panelRoot.SetActive(false);
        if (hintRoot != null) hintRoot.SetActive(false);
        if (reconfigurarRoot != null) reconfigurarRoot.SetActive(false);
        if (confirmButton != null)
        {
            confirmLabel = confirmButton.GetComponentInChildren<TMP_Text>(true);
            if (confirmLabel != null) textoSalvarNormal = confirmLabel.text;
        }
        if (actionButtonTemplate != null) actionButtonTemplate.gameObject.SetActive(false);
        if (consequenceButtonTemplate != null) consequenceButtonTemplate.gameObject.SetActive(false);

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(Confirm);
            confirmButton.onClick.AddListener(Confirm);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Close);
            cancelButton.onClick.AddListener(Close);
        }

        if (cancelButtonSecundario != null)
        {
            cancelButtonSecundario.onClick.RemoveListener(Close);
            cancelButtonSecundario.onClick.AddListener(Close);
        }
    }

    /// <summary>Assistente aberto na tela (o destaque dos itens some enquanto isso).</summary>
    public bool PainelAberto => panelRoot != null && panelRoot.activeSelf;

    // Textos das opções, usados também pelo destaque dos itens (DestaqueItensInteracao).
    public static string NomeAcao(ActionType a) => ActionLabels.TryGetValue(a, out string s) ? s : a.ToString();
    public static string EquivalenteUnity(ActionType a) => ActionUnityEquivalents.TryGetValue(a, out string s) ? s : "";
    public static string NomeConsequencia(ConsequenceType c) => ConsequenceLabels.TryGetValue(c, out string s) ? s : c.ToString();

    /// <summary>Abre o assistente pro item clicado (ver InteractableItem.OnMouseDown).</summary>
    public void OpenFor(InteractableItem item)
    {
        if (panelRoot == null || actionOptionsContainer == null || actionButtonTemplate == null)
        {
            Debug.LogError("[InteractionConfigUI] Referências de UI não configuradas — rode Tools > Interação > Montar Cena de Interação.");
            return;
        }

        currentItem = item;
        chosenAction = null;
        chosenConsequence = null;
        chosenRepeats = false;
        selectedActionButton = null;
        selectedConsequenceButton = null;

        panelRoot.SetActive(true);
        AudioManager.AvisarPainel(aberto: true);
        if (titleText != null) titleText.text = item.DisplayName;

        // Item já configurado: o mesmo painel, com a escolha atual marcada.
        reconfigurando = item.IsConfigured;
        if (reconfigurando)
        {
            acaoAntiga = item.ChosenAction;
            consequenciaAntiga = item.ChosenConsequence;
            repetirAntigo = item.ChosenRepeats;
        }
        MostrarReconfigurar();

        if (consequenceColumnRoot != null) consequenceColumnRoot.SetActive(false);
        if (consequencePlaceholder != null) consequencePlaceholder.SetActive(true);
        ClearButtons(spawnedConsequenceButtons);
        RefreshConfirmInteractable();

        EnsureRulesRows();
        RefreshRepeatRow();

        BuildActionColumn();

        if (reconfigurando)
        {
            MarcarEscolhaAtual();
        }
    }

    // Marca a Ação e a Consequência que o item já tem, como se o jogador
    // tivesse clicado nelas, e o "Repetir" que ele tinha escolhido.
    private void MarcarEscolhaAtual()
    {
        int i = IndexOf(currentItem.AllowedActions, acaoAntiga);
        if (i < 0 || i >= spawnedActionButtons.Count)
        {
            return;
        }
        SelectAction(acaoAntiga, spawnedActionButtons[i].GetComponent<Button>());

        int j = IndexOf(currentItem.AllowedConsequences, consequenciaAntiga);
        if (j < 0 || j >= spawnedConsequenceButtons.Count)
        {
            return;
        }
        SelectConsequence(consequenciaAntiga, spawnedConsequenceButtons[j].GetComponent<Button>());
        chosenRepeats = repetirAntigo;
        RefreshRepeatRow();
    }

    private static int IndexOf<T>(IReadOnlyList<T> list, T value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(list[i], value)) return i;
        }
        return -1;
    }

    private void MostrarReconfigurar()
    {
        if (reconfigurarRoot != null) reconfigurarRoot.SetActive(reconfigurando);
        if (rotuloConfigurar != null) rotuloConfigurar.SetActive(!reconfigurando);
        if (reconfigurando && reconfigurarTexto != null)
        {
            reconfigurarTexto.text = $"<color=#F5C542>Reconfigurar item</color>   <color=#8C98B5>Atual:</color> {NomeAcao(acaoAntiga)} » {NomeConsequencia(consequenciaAntiga)}";
        }
        if (confirmLabel != null)
        {
            confirmLabel.text = reconfigurando ? textoSalvarMudanca : textoSalvarNormal;
        }
    }

    private void BuildActionColumn()
    {
        ClearButtons(spawnedActionButtons);

        foreach (ActionType action in currentItem.AllowedActions)
        {
            ActionType captured = action;
            Button button = CreateOptionButton(actionOptionsContainer, actionButtonTemplate, ActionLabels[captured], spawnedActionButtons);

            if (ActionUnityEquivalents.TryGetValue(captured, out string unityEquivalent))
            {
                AddUnityLine(button, unityEquivalent);
            }

            button.onClick.AddListener(() => SelectAction(captured, button));
        }
    }

    private void SelectAction(ActionType action, Button button)
    {
        chosenAction = action;
        HighlightSelected(button, ref selectedActionButton);

        if (consequenceColumnRoot != null) consequenceColumnRoot.SetActive(true);
        if (consequencePlaceholder != null) consequencePlaceholder.SetActive(false);
        BuildConsequenceColumn();
        RefreshConfirmInteractable();
    }

    private void BuildConsequenceColumn()
    {
        ClearButtons(spawnedConsequenceButtons);
        selectedConsequenceButton = null;

        foreach (ConsequenceType consequence in currentItem.AllowedConsequences)
        {
            ConsequenceType captured = consequence;
            Button button = CreateOptionButton(consequenceOptionsContainer, consequenceButtonTemplate, ConsequenceLabels[captured], spawnedConsequenceButtons);
            button.onClick.AddListener(() => SelectConsequence(captured, button));

            // Se o jogador trocar de Ação depois de já ter escolhido uma
            // Consequência, mantém o destaque de quem já estava selecionado.
            if (chosenConsequence.HasValue && chosenConsequence.Value == consequence)
            {
                HighlightSelected(button, ref selectedConsequenceButton);
            }
        }
    }

    private void SelectConsequence(ConsequenceType consequence, Button button)
    {
        // Trocou de Consequência: o "Repetir" volta pro padrão dela. Clicar de
        // novo na mesma mantém o que o jogador já tinha escolhido.
        if (!chosenConsequence.HasValue || chosenConsequence.Value != consequence)
        {
            chosenRepeats = InteractableItem.DefaultRepeats(consequence);
        }

        chosenConsequence = consequence;
        HighlightSelected(button, ref selectedConsequenceButton);
        RefreshRepeatRow();
        RefreshConfirmInteractable();
    }

    private void HighlightSelected(Button newSelection, ref Button currentSelectionField)
    {
        if (currentSelectionField != null)
        {
            SetSelected(currentSelectionField, false);
        }

        SetSelected(newSelection, true);
        currentSelectionField = newSelection;
    }

    // Liga/desliga o indicador de selecionado em vez de trocar a cor do
    // botão — assim o design de cada template (definido no Editor) fica
    // intacto, só ganha um destaque por cima. Suporta os dois formatos:
    // o checkbox novo ("Check/CheckOK", o "V" que aparece dentro do slot) e
    // o antigo "SelectedIndicator" (templates montados pelo builder antigo).
    private static void SetSelected(Button button, bool selected)
    {
        Transform indicator = button.transform.Find("Check/CheckOK") ?? button.transform.Find("SelectedIndicator");
        if (indicator != null)
        {
            indicator.gameObject.SetActive(selected);
        }

        // Destaque extra do tema Laboratório (faixa azul na linha), se o template tiver.
        Transform destaque = button.transform.Find("Selecionado");
        if (destaque != null)
        {
            destaque.gameObject.SetActive(selected);
        }
    }

    private void RefreshConfirmInteractable()
    {
        if (confirmButton != null)
        {
            confirmButton.interactable = chosenAction.HasValue && chosenConsequence.HasValue;
        }
    }

    private Button CreateOptionButton(RectTransform container, Button template, string label, List<GameObject> trackList)
    {
        Button button = Instantiate(template, container);
        button.gameObject.SetActive(true);

        TMP_Text buttonLabel = FindOptionLabel(button);
        if (buttonLabel != null) buttonLabel.text = label;

        SetSelected(button, false);
        trackList.Add(button.gameObject);
        return button;
    }

    // O nome da opção: o filho "Label" do template; se não houver, o primeiro
    // TMP_Text que não seja a linha do equivalente no Unity.
    private static TMP_Text FindOptionLabel(Button button)
    {
        Transform labelTransform = button.transform.Find("Label");
        TMP_Text label = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
        if (label != null)
        {
            return label;
        }

        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.gameObject.name != UnityLineName)
            {
                return text;
            }
        }

        return null;
    }

    // Segunda linha da opção de Ação, com o equivalente no Unity. O botão
    // mantém a altura do template: o nome da opção sobe pra parte de cima e a
    // linha nova ocupa a parte de baixo.
    private void AddUnityLine(Button button, string code)
    {
        string formatted = unityLineMonoSpacing > 0f
            ? $"<mspace={unityLineMonoSpacing.ToString(System.Globalization.CultureInfo.InvariantCulture)}em>{code}</mspace>"
            : code;

        // Template com uma linha própria desenhada no Editor: só preenche.
        Transform existing = button.transform.Find(UnityLineName);
        if (existing != null)
        {
            TMP_Text existingText = existing.GetComponent<TMP_Text>();
            if (existingText != null)
            {
                existingText.text = formatted;
                existing.gameObject.SetActive(true);
                return;
            }
        }

        TMP_Text label = FindOptionLabel(button);
        float left = 16f;

        if (label != null)
        {
            RectTransform labelRect = label.rectTransform;

            // Só mexe no nome da opção se ele estiver esticado na altura do
            // botão (o formato do template atual); outro layout fica como está.
            if (Mathf.Approximately(labelRect.anchorMin.y, 0f) && Mathf.Approximately(labelRect.anchorMax.y, 1f))
            {
                labelRect.anchorMin = new Vector2(labelRect.anchorMin.x, unityLineHeightFraction);

                // Com menos altura, o nome pode precisar diminuir um pouco pra
                // caber numa linha; no tamanho original, nada muda.
                float originalSize = label.fontSize;
                label.enableAutoSizing = true;
                label.fontSizeMax = originalSize;
                label.fontSizeMin = Mathf.Min(14f, originalSize);
            }

            if (Mathf.Approximately(labelRect.anchorMin.x, 0f))
            {
                left = labelRect.offsetMin.x;
            }
        }

        var lineGO = new GameObject(UnityLineName, typeof(RectTransform));
        var lineRect = (RectTransform)lineGO.transform;
        lineRect.SetParent(button.transform, false);
        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(1f, unityLineHeightFraction);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.offsetMin = new Vector2(left, 2f);
        lineRect.offsetMax = new Vector2(-unityLineRightMargin, 0f);

        var tmp = lineGO.AddComponent<TextMeshProUGUI>();
        tmp.font = CodeFont() ?? (label != null ? label.font : null);
        tmp.richText = true;
        tmp.fontSize = unityLineFontSize;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = unityLineFontSize;
        tmp.fontSizeMin = Mathf.Min(10f, unityLineFontSize);
        tmp.color = unityLineColor;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        tmp.text = formatted;
    }

    private static void ClearButtons(List<GameObject> list)
    {
        foreach (GameObject go in list)
        {
            Destroy(go);
        }
        list.Clear();
    }

    // ---------- Linhas "Quem ativa" e "Repetir" ----------

    // Monta as duas linhas uma única vez, na primeira abertura do painel
    // (com o painel já ativo, pra posição do botão Salvar estar calculada).
    private void EnsureRulesRows()
    {
        if (rulesRowsBuilt || panelRoot == null)
        {
            return;
        }

        rulesRowsBuilt = true;

        RectTransform root = rulesRowsRoot;
        if (root == null)
        {
            Transform found = panelRoot.transform.Find(RulesRootName);
            root = found != null ? found as RectTransform : null;
        }
        if (root == null)
        {
            root = CreateRulesRoot();
        }
        if (root == null)
        {
            return;
        }

        RectTransform whoRow = CreateRow(root, "QuemAtiva", new Vector2(0f, 0.5f), new Vector2(1f, 1f));
        CreateRowText(whoRow, "Rotulo", whoActivatesLabel, rowsFontSize, rowsLabelColor, useOptionFont: true);
        Chip tagChip = CreateChip(whoRow, "ChipTagPlayer", whoActivatesChip, clickable: false);
        StyleChip(tagChip, selected: true, interactable: true);
        CreateRowText(whoRow, "Dica", whoActivatesHint, rowsHintFontSize, rowsHintColor, useOptionFont: false, ocupaOResto: true);

        RectTransform repeatRect = CreateRow(root, "Repetir", new Vector2(0f, 0f), new Vector2(1f, 0.5f));
        repeatRow = repeatRect.gameObject;
        CreateRowText(repeatRect, "Rotulo", repeatLabel, rowsFontSize, rowsLabelColor, useOptionFont: true);
        onceChip = CreateChip(repeatRect, "ChipUmaVez", repeatOnceText, clickable: true);
        alwaysChip = CreateChip(repeatRect, "ChipSempre", repeatAlwaysText, clickable: true);
        repeatHintText = CreateRowText(repeatRect, "Dica", "", rowsHintFontSize, rowsHintColor, useOptionFont: false, ocupaOResto: true);

        onceChip.button.onClick.AddListener(() => SetRepeats(false));
        alwaysChip.button.onClick.AddListener(() => SetRepeats(true));
    }

    // Área das duas linhas: canto de baixo à esquerda do painel, na altura do
    // botão Salvar e terminando antes dele (onde o painel atual tem espaço livre).
    private RectTransform CreateRulesRoot()
    {
        var panelRect = panelRoot.transform as RectTransform;
        if (panelRect == null)
        {
            return null;
        }

        float left = 40f;
        float bottom = 24f;
        float width = 620f;
        const float height = 88f;

        if (confirmButton != null && confirmButton.transform is RectTransform confirmRect)
        {
            Canvas.ForceUpdateCanvases();

            var corners = new Vector3[4];
            confirmRect.GetWorldCorners(corners);
            Vector2 confirmBottomLeft = panelRect.InverseTransformPoint(corners[0]);
            Rect panelArea = panelRect.rect;

            float confirmLeft = confirmBottomLeft.x - panelArea.xMin;
            float confirmBottom = confirmBottomLeft.y - panelArea.yMin;

            if (confirmLeft > left + 300f)
            {
                width = confirmLeft - left - 24f;
            }
            if (confirmBottom > 0f && confirmBottom < panelArea.height * 0.5f)
            {
                bottom = confirmBottom;
            }
        }

        var go = new GameObject(RulesRootName, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(panelRect, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(left, bottom);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RectTransform CreateRow(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return rect;
    }

    // ocupaOResto: a dica fica com a largura que sobrar na linha depois do
    // rótulo e dos chips e quebra em até duas linhas, em vez de passar da
    // área das regras (que fica ao lado dos botões Cancelar/Salvar).
    private TMP_Text CreateRowText(Transform parent, string name, string text, float size, Color color, bool useOptionFont, bool ocupaOResto = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = PickFont(useOptionFont);
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = ocupaOResto ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        if (ocupaOResto)
        {
            var resto = go.AddComponent<LayoutElement>();
            resto.minWidth = 0f;
            resto.preferredWidth = 0f;
            resto.flexibleWidth = 1f;
        }
        tmp.raycastTarget = false;
        tmp.text = text;
        return tmp;
    }

    private Chip CreateChip(Transform parent, string name, string text, bool clickable)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        // Borda + fundo arredondados (sprite branco do tema, tingido). Sem o
        // tema, os dois viram retângulos lisos e o resultado é o mesmo de antes.
        Sprite arredondado = TemaLaboratorio.Atual != null ? TemaLaboratorio.Atual.bordaArredondada : null;
        var border = go.AddComponent<Image>();
        TemaLabUI.DefinirSprite(border, arredondado, chipBorderColor);
        border.color = chipBorderColor;
        border.raycastTarget = clickable;

        Image background = TemaLabUI.Imagem(go.transform, "Fundo", arredondado, chipBackgroundColor);
        background.color = chipBackgroundColor;
        TemaLabUI.Esticar(background.rectTransform, 2.5f);
        background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 7, 8);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.font = CodeFont() ?? PickFont(false);
        tmp.richText = true;
        tmp.fontSize = chipFontSize;
        tmp.color = chipTextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        tmp.text = unityLineMonoSpacing > 0f
            ? $"<mspace={unityLineMonoSpacing.ToString(System.Globalization.CultureInfo.InvariantCulture)}em>{text}</mspace>"
            : text;

        Button button = null;
        if (clickable)
        {
            button = go.AddComponent<Button>();
            button.targetGraphic = border;
        }

        return new Chip { root = go, button = button, background = background, border = border, label = tmp };
    }

    // Fonte das opções (a do template) pros rótulos; a padrão do TMP pro
    // resto (fica mais legível em tamanho pequeno e combina com as linhas
    // de código).
    private TMP_FontAsset PickFont(bool useOptionFont)
    {
        if (useOptionFont && actionButtonTemplate != null)
        {
            TMP_Text optionLabel = FindOptionLabel(actionButtonTemplate);
            if (optionLabel != null && optionLabel.font != null)
            {
                return optionLabel.font;
            }
        }

        TMP_FontAsset corpo = TemaLabUI.FonteDe(TemaLabUI.Fonte.Corpo);
        if (corpo != null)
        {
            return corpo;
        }

        return titleText != null ? titleText.font : null;
    }

    private TMP_FontAsset CodeFont()
    {
        if (codeFont != null)
        {
            return codeFont;
        }

        return TemaLaboratorio.Atual != null && TemaLaboratorio.Atual.mono != null
            ? TemaLaboratorio.Atual.mono
            : TMP_Settings.defaultFontAsset;
    }

    private void StyleChip(Chip chip, bool selected, bool interactable)
    {
        if (chip == null)
        {
            return;
        }

        if (chip.button != null)
        {
            chip.button.interactable = interactable;
        }

        Color accent = selected ? chipSelectedColor : chipTextColor;
        if (!interactable)
        {
            accent.a = 0.35f;
        }

        chip.label.color = accent;
        Color borda = selected ? chipSelectedColor : chipBorderColor;
        if (!interactable)
        {
            borda.a = 0.35f;
        }
        chip.border.color = borda;
    }

    private void SetRepeats(bool repeats)
    {
        if (!chosenConsequence.HasValue)
        {
            return;
        }

        if (repeats && !InteractableItem.CanRepeat(chosenConsequence.Value))
        {
            return;
        }

        chosenRepeats = repeats;
        RefreshRepeatRow();
    }

    // A linha "Repetir" só aparece depois de escolher a Consequência, já com
    // o padrão dela marcado. Consequências de uso único travam em "Uma vez".
    private void RefreshRepeatRow()
    {
        if (repeatRow == null)
        {
            return;
        }

        repeatRow.SetActive(chosenConsequence.HasValue);
        if (!chosenConsequence.HasValue)
        {
            return;
        }

        bool canRepeat = InteractableItem.CanRepeat(chosenConsequence.Value);
        if (!canRepeat)
        {
            chosenRepeats = false;
        }

        StyleChip(onceChip, selected: !chosenRepeats, interactable: true);
        StyleChip(alwaysChip, selected: chosenRepeats, interactable: canRepeat);

        if (repeatHintText != null)
        {
            repeatHintText.text = canRepeat ? "" : repeatLockedHint;
        }
    }

    // Confirma a configuração escolhida e fecha (ver ConfirmButton).
    private void Confirm()
    {
        if (currentItem != null && chosenAction.HasValue && chosenConsequence.HasValue)
        {
            bool mudou = !reconfigurando || chosenAction.Value != acaoAntiga ||
                         chosenConsequence.Value != consequenciaAntiga || chosenRepeats != repetirAntigo;
            if (mudou)
            {
                if (reconfigurando)
                {
                    RegistroAprendizagem.Registrar("reconfigurou",
                        $"{currentItem.DisplayName} | {acaoAntiga} | {consequenciaAntiga} | {(repetirAntigo ? "Sempre" : "UmaVez")}" +
                        $" -> {chosenAction.Value} | {chosenConsequence.Value} | {(chosenRepeats ? "Sempre" : "UmaVez")}");
                }
                currentItem.Configure(chosenAction.Value, chosenConsequence.Value, chosenRepeats);
            }
        }

        Close();
    }

    // Fecha sem aplicar nada (ver CancelButton) — também chamado por Confirm() ao terminar.
    private void Close()
    {
        ClearButtons(spawnedActionButtons);
        ClearButtons(spawnedConsequenceButtons);

        currentItem = null;
        chosenAction = null;
        chosenConsequence = null;
        chosenRepeats = false;
        selectedActionButton = null;
        selectedConsequenceButton = null;
        reconfigurando = false;
        if (reconfigurarRoot != null) reconfigurarRoot.SetActive(false);
        if (rotuloConfigurar != null) rotuloConfigurar.SetActive(true);
        if (confirmLabel != null && textoSalvarNormal != null) confirmLabel.text = textoSalvarNormal;

        if (panelRoot != null && panelRoot.activeSelf)
        {
            AudioManager.AvisarPainel(aberto: false);
        }
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>Mostra um aviso rápido no rodapé da tela (some sozinho).</summary>
    public void ShowHint(string message, float seconds = 2.5f)
    {
        if (hintRoot == null || hintText == null)
        {
            Debug.Log($"[InteractionConfigUI] {message}");
            return;
        }

        hintText.text = message;
        hintRoot.SetActive(true);

        if (hintRoutine != null) StopCoroutine(hintRoutine);

        // Chamado às vezes com o painel ainda fechado (ex: clicar num item já
        // configurado) — nesse caso este componente está num GameObject
        // inativo e StartCoroutine falharia. O aviso ainda aparece, só não
        // some sozinho dessa vez.
        if (isActiveAndEnabled)
        {
            hintRoutine = StartCoroutine(HideHintAfter(seconds));
        }
    }

    private IEnumerator HideHintAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (hintRoot != null) hintRoot.SetActive(false);
    }
}
