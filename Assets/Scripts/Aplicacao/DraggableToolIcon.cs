using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ícone arrastável do inventário (Rigidbody2D, Collider2D, Movimento ou
/// Pulo). O jogador clica e arrasta até qualquer objeto do mundo que
/// implemente IToolReceiver (objetos comuns via PhysicsTarget, ou o Player via
/// AplicacaoPlayerController); ao soltar em cima de um, aplica a ferramenta
/// correspondente e o ícone volta pro lugar no inventário.
///
/// Usos: com "Usos Disponíveis" = -1 a ferramenta é ilimitada. Com um número,
/// cada aplicação válida gasta um uso (mostrado no canto do ícone) — soltar
/// num objeto que não aceita a ferramenta, ou que já a tem, não gasta nada.
///
/// Usa os eventos de arrastar do próprio EventSystem (IBeginDragHandler /
/// IDragHandler / IEndDragHandler), que já funcionam com o novo Input
/// System via InputSystemUIInputModule — nada de Input.GetMouseButton.
///
/// Visual do cartão (opcional, montado por AplicacaoInventarioBuilder): com o
/// mouse em cima mostra a dica (InventarioDica); arrastando, o cartão mostra
/// "solte no objeto" no lugar do nome; esgotado, o cartão inteiro fica
/// apagado e o selo de usos fica cinza.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DraggableToolIcon : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private ToolType toolType;

    [Tooltip("Quantas vezes esta mecânica pode ser aplicada na fase. -1 = sem limite.")]
    [SerializeField] private int usosDisponiveis = -1;

    [Tooltip("Opcional: texto dos usos restantes. Se vazio e houver limite, é criado sozinho no canto do ícone.")]
    [SerializeField] private TMP_Text usosLabel;

    [Header("Visual do cartão (opcional)")]
    [Tooltip("Frase curta mostrada ao passar o mouse em cima.")]
    [TextArea(1, 3)]
    [SerializeField] private string dica;
    [Tooltip("Cartão onde o ícone mora: âncora da dica e o que fica apagado quando os usos acabam.")]
    [SerializeField] private RectTransform cartao;
    [Tooltip("Nome e etiqueta do cartão, escondidos enquanto o ícone é arrastado.")]
    [SerializeField] private GameObject conteudoCartao;
    [Tooltip("Texto \"solte no objeto\", mostrado no cartão enquanto o ícone é arrastado.")]
    [SerializeField] private GameObject espacoVazio;
    [Tooltip("Fundo do selo de usos (fica cinza quando esgota).")]
    [SerializeField] private Image seloUsos;
    [SerializeField] private Color seloEsgotado = new Color(0.227f, 0.247f, 0.290f, 1f);

    public ToolType Tool => toolType;
    public bool TemLimite => usosDisponiveis >= 0;

    /// <summary>Ícone sendo arrastado agora (null quando nenhum). Usado pela prévia de Materializacao.</summary>
    public static DraggableToolIcon Arrastando { get; private set; }

    /// <summary>Objeto embaixo do cursor durante o arrasto (null quando nenhum).</summary>
    public static IToolReceiver AlvoSobCursor { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Arrastando = null;
        AlvoSobCursor = null;
    }

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas rootCanvas;
    private Vector2 homeAnchoredPosition;
    private int usosRestantes;
    private bool dragging;
    private CanvasGroup cartaoGrupo;
    private int indiceCartao;

    /// <summary>Nome da ferramenta como aparece no Unity (usado em dicas e no registro).</summary>
    public static string NomeNoUnity(ToolType tool)
    {
        switch (tool)
        {
            case ToolType.Fisica: return "Rigidbody2D";
            case ToolType.Colisao: return "Collider2D";
            case ToolType.Movimento: return "Movimento";
            case ToolType.Pulo: return "Pulo";
            default: return tool.ToString();
        }
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        usosRestantes = usosDisponiveis;

        if (cartao != null)
        {
            cartaoGrupo = cartao.GetComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        homeAnchoredPosition = rectTransform.anchoredPosition;

        if (TemLimite && usosLabel == null)
        {
            GameObject labelGO = UIBuildUtils.CreateText(transform, "Usos", "", 22, TextAlignmentOptions.BottomRight);
            RectTransform labelRect = labelGO.GetComponent<RectTransform>();
            UIBuildUtils.StretchToParent(labelRect);
            labelRect.offsetMin = new Vector2(4f, 4f);
            labelRect.offsetMax = new Vector2(-6f, -4f);
            usosLabel = labelGO.GetComponent<TMP_Text>();
            usosLabel.fontStyle = FontStyles.Bold;
        }

        if (seloUsos != null)
        {
            seloUsos.gameObject.SetActive(TemLimite);
        }

        UpdateUsesVisual();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (TemLimite && usosRestantes <= 0)
        {
            // Cancela o arrasto: sem pointerDrag, OnDrag/OnEndDrag não são chamados.
            eventData.pointerDrag = null;
            return;
        }

        dragging = true;
        Arrastando = this;
        AlvoSobCursor = null;
        InventarioDica.Esconder();
        SetArrastandoVisual(true);

        // Deixa o raycast "atravessar" o ícone enquanto arrasta, pra dar pra
        // detectar o objeto do mundo embaixo do cursor ao soltar.
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.6f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging)
        {
            return;
        }

        float scale = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
        rectTransform.anchoredPosition += eventData.delta / Mathf.Max(scale, 0.0001f);
        AlvoSobCursor = FindTargetUnderPointer(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging)
        {
            return;
        }

        dragging = false;
        LimparArrasto();
        canvasGroup.blocksRaycasts = true;
        SetArrastandoVisual(false);

        IToolReceiver target = FindTargetUnderPointer(eventData);
        if (target != null)
        {
            TryApply(target);
        }

        // Sempre volta pro slot do inventário, tenha acertado um objeto ou não.
        rectTransform.anchoredPosition = homeAnchoredPosition;
        UpdateUsesVisual();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (dragging || eventData.dragging)
        {
            return;
        }

        InventarioDica.Mostrar(NomeNoUnity(toolType), dica, ToolColors.For(toolType), cartao != null ? cartao : rectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        InventarioDica.Esconder();
    }

    private void OnDisable()
    {
        InventarioDica.Esconder();
        LimparArrasto();
    }

    private void LimparArrasto()
    {
        if (Arrastando == this)
        {
            Arrastando = null;
            AlvoSobCursor = null;
        }
    }

    // Enquanto arrasta: o cartão vai pro fim da lista (desenhado por cima dos
    // vizinhos, pro ícone não passar por baixo deles) e mostra "solte no objeto".
    private void SetArrastandoVisual(bool arrastando)
    {
        if (conteudoCartao != null) conteudoCartao.SetActive(!arrastando);
        if (espacoVazio != null) espacoVazio.SetActive(arrastando);

        if (cartao == null)
        {
            return;
        }

        if (arrastando)
        {
            indiceCartao = cartao.GetSiblingIndex();
            cartao.SetAsLastSibling();
        }
        else
        {
            cartao.SetSiblingIndex(indiceCartao);
        }
    }

    private void TryApply(IToolReceiver target)
    {
        string targetName = target is Component component ? component.name : "?";
        string toolName = NomeNoUnity(toolType);

        if (!target.CanReceive(toolType))
        {
            RegistroAprendizagem.Registrar("ferramenta_recusada", $"{toolName} | {targetName}");
            AplicacaoLevelFlow.Instance?.ShowToolNotAccepted(toolType);
            return;
        }

        if (target.HasTool(toolType))
        {
            // Já tem — não gasta um uso à toa.
            return;
        }

        target.ApplyTool(toolType);
        if (TemLimite)
        {
            usosRestantes--;
        }

        RegistroAprendizagem.Registrar("ferramenta_aplicada", $"{toolName} | {targetName}");
        AplicacaoLevelFlow.Instance?.NotifyToolApplied(toolType, target);
    }

    private void UpdateUsesVisual()
    {
        if (!TemLimite)
        {
            canvasGroup.alpha = 1f;
            return;
        }

        if (usosLabel != null)
        {
            usosLabel.text = "x" + Mathf.Max(0, usosRestantes);
        }

        // Esgotado: fica apagado e não arrasta mais. Com cartão, apaga o
        // cartão inteiro (o ícone vai junto) e deixa o selo cinza.
        bool esgotado = usosRestantes <= 0;
        if (cartaoGrupo != null)
        {
            cartaoGrupo.alpha = esgotado ? 0.4f : 1f;
            canvasGroup.alpha = 1f;
        }
        else
        {
            canvasGroup.alpha = esgotado ? 0.35f : 1f;
        }

        if (seloUsos != null)
        {
            seloUsos.color = esgotado ? seloEsgotado : ToolColors.For(toolType);
        }
    }

    // O projeto tem Physics2D.queriesHitTriggers desligado (ajuste da cena
    // Interacao) — e tudo que ainda não recebeu Collider2D é Trigger, inclusive
    // o Player. Nesta versão do Unity, a flag global vale mesmo com
    // useTriggers = true no filtro, então ela é ligada só durante a busca.
    private static readonly List<Collider2D> hits = new List<Collider2D>();

    private static IToolReceiver FindTargetUnderPointer(PointerEventData eventData)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return null;
        }

        Vector2 worldPoint = cam.ScreenToWorldPoint(eventData.position);
        var filter = new ContactFilter2D { useTriggers = true };
        bool queriesHitTriggers = Physics2D.queriesHitTriggers;
        try
        {
            Physics2D.queriesHitTriggers = true;
            Physics2D.OverlapPoint(worldPoint, filter, hits);
        }
        finally
        {
            Physics2D.queriesHitTriggers = queriesHitTriggers;
        }

        // Num bloco, quem é atingido é o collider do bloco (ou de um tile
        // filho) — o receptor fica no bloco, por isso GetComponentInParent.
        // Se o Player estiver na frente de um bloco, ele tem prioridade.
        IToolReceiver found = null;
        foreach (Collider2D hit in hits)
        {
            IToolReceiver receiver = hit.GetComponentInParent<IToolReceiver>();
            if (receiver == null)
            {
                continue;
            }

            if (receiver is AplicacaoPlayerController)
            {
                return receiver;
            }

            found ??= receiver;
        }

        return found;
    }
}
