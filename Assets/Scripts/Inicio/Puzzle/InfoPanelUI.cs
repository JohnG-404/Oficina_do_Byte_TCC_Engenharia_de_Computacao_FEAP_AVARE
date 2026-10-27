using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla o painel de descrição da cena (Fisica): título + descrição (e
/// ícone) da peça sob o mouse (ver PieceHoverInfo).
///
/// Dois modos:
/// 1) Painel novo ("Descrição", layout EditorKit): os textos e o ícone já
///    existem na cena — arraste-os nos campos "Textos e ícone existentes" e
///    este script só troca o conteúdo deles.
/// 2) Painel antigo ("Info", sprite de fundo): se nenhum texto externo foi
///    atribuído, monta sozinho um Canvas World Space do tamanho do sprite
///    (ver EnsureBuilt), com RectMask2D cortando texto que passar da borda.
///
/// Cada puzzle da cena tem o seu próprio painel — por isso "Instance" é
/// registrado em OnEnable (o puzzle que acabou de ficar ativo vira o dono) e
/// liberado em OnDisable, em vez de fixado uma única vez no Awake.
/// </summary>
public class InfoPanelUI : MonoBehaviour
{
    public static InfoPanelUI Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    [Header("Textos e ícone existentes na cena (painel novo)")]
    [SerializeField] private TMP_Text externalTitle;
    [SerializeField] private TMP_Text externalDescription;
    [Tooltip("Ícone grande do painel — mostra o ícone da peça sob o mouse. Fica escondido quando o mouse não está em nenhuma peça (a menos que haja um Default Icon).")]
    [SerializeField] private SpriteRenderer externalIcon;
    [SerializeField] private Sprite defaultIcon;

    [Header("Texto padrão (mouse fora de qualquer peça)")]
    [SerializeField] private string defaultTitle = "";
    [TextArea(1, 4)]
    [SerializeField] private string defaultDescription = "";

    [Header("Aparência (usada só ao criar automaticamente, painel antigo)")]
    [SerializeField] private float titleFontSize = 42f;
    [SerializeField] private float descriptionFontSize = 32f;
    [SerializeField] private Color textColor = Color.black;
    [SerializeField] private TextAlignmentOptions titleAlignment = TextAlignmentOptions.Left;
    [SerializeField] private TextAlignmentOptions descriptionAlignment = TextAlignmentOptions.Left;
    [Tooltip("Margem interna entre o texto e a borda do painel, em % da largura/altura.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float paddingPercent = 0.08f;

    [Header("Posição padrão (só na primeira montagem do painel antigo — depois, mova livremente no Editor)")]
    [Tooltip("Área do título dentro do painel, em % (0 a 1) da área útil (já descontada a margem). Y cresce pra cima.")]
    [SerializeField] private Rect defaultTitleArea = new Rect(0f, 0.7f, 1f, 0.3f);
    [Tooltip("Área da descrição dentro do painel, em % (0 a 1) da área útil.")]
    [SerializeField] private Rect defaultDescriptionArea = new Rect(0f, 0f, 1f, 0.62f);

    [Header("Aviso de peça errada")]
    [SerializeField] private string avisoErroTitulo = "Essa peça não vai aqui.";
    [SerializeField] private string avisoErroTexto = "O código voltou ao começo.";

    private TMP_Text titleText;
    private TMP_Text descriptionText;

    // O que o painel deveria mostrar agora (hover, padrão, dica da IA...).
    // Durante o aviso de erro só isso é atualizado; ao fim do aviso o painel
    // volta exatamente pra cá.
    private string pedidoTitulo = "";
    private string pedidoTexto = "";
    private Sprite pedidoIcone;

    // Aviso de peça errada (fundo vermelho escuro, borda vermelha, ✕).
    private bool avisoAtivo;
    private Coroutine avisoRotina;
    private SpriteRenderer avisoFundo;
    private SpriteRenderer fundoOriginal;
    private SpriteRenderer[] tingidos = new SpriteRenderer[0];
    private Color[] coresTingidos = new Color[0];
    private Color corTituloOriginal;
    private Color corIconeOriginal;

    private static readonly Color CorTituloAviso = new Color32(0xFF, 0x8A, 0x8A, 0xFF);
    private static readonly Color TintaAba = new Color32(0xEF, 0x6B, 0x6B, 0xFF);

    private void Awake()
    {
        EnsureBuilt();
    }

    private void OnEnable()
    {
        Instance = this;
        EnsureBuilt();
        Clear();
    }

    private void OnDisable()
    {
        EsconderAvisoErro();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Constrói (ou reconecta, se já existir) a estrutura deste painel.
    /// Público pra poder ser chamado por uma ferramenta de Editor (Tools >
    /// Inicio > Montar Painel de Info), o que deixa o resultado salvo na
    /// cena e editável — sem isso, a estrutura só existiria durante o Play
    /// Mode e se perderia ao sair dele.
    /// </summary>
    public void EnsureBuilt()
    {
        if (externalTitle != null || externalDescription != null)
        {
            titleText = externalTitle;
            descriptionText = externalDescription;
            return;
        }

        Transform existing = transform.Find("InfoCanvas");
        if (existing != null)
        {
            titleText = existing.Find("Content/Title")?.GetComponent<TMP_Text>();
            descriptionText = existing.Find("Content/Description")?.GetComponent<TMP_Text>();
            return;
        }

        var spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            return;
        }

        Vector2 worldSize = spriteRenderer.sprite != null
            ? (Vector2)spriteRenderer.bounds.size
            : new Vector2(4f, 2f);

        const float refWidth = 1000f;
        float refHeight = refWidth * (worldSize.y / worldSize.x);

        var canvasGO = new GameObject("InfoCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = Vector3.zero;

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerID = spriteRenderer.sortingLayerID;
        canvas.sortingOrder = spriteRenderer.sortingOrder + 1;

        var canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(refWidth, refHeight);
        canvasRect.pivot = new Vector2(0.5f, 0.5f);

        // Escala o Canvas (em "unidades de UI") pra ocupar exatamente o
        // tamanho do sprite no mundo, compensando a escala já herdada do pai.
        Vector3 parentLossyScale = transform.lossyScale;
        float scaleX = worldSize.x / refWidth / Mathf.Max(parentLossyScale.x, 0.0001f);
        float scaleY = worldSize.y / refHeight / Mathf.Max(parentLossyScale.y, 0.0001f);
        canvasGO.transform.localScale = new Vector3(scaleX, scaleY, 1f);

        // Corta qualquer texto que ultrapasse a borda do painel.
        canvasGO.AddComponent<RectMask2D>();

        float paddingX = refWidth * paddingPercent;
        float paddingY = refHeight * paddingPercent;

        var content = new GameObject("Content", typeof(RectTransform));
        var contentRect = (RectTransform)content.transform;
        contentRect.SetParent(canvasRect, false);
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(paddingX, paddingY);
        contentRect.offsetMax = new Vector2(-paddingX, -paddingY);

        // Sem layout group nenhum aqui: Title e Description recebem áreas
        // (ancoras) independentes só como ponto de partida — depois de
        // montado, mova/redimensione cada um à vontade no Editor.
        titleText = CreateText(contentRect, "Title", titleFontSize, FontStyles.Bold, titleAlignment, defaultTitleArea);
        descriptionText = CreateText(contentRect, "Description", descriptionFontSize, FontStyles.Normal, descriptionAlignment, defaultDescriptionArea);
    }

    private TMP_Text CreateText(Transform parent, string name, float fontSize, FontStyles style, TextAlignmentOptions alignment, Rect area)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);

        // "area" é só o ponto de partida (% da região útil do painel) — o
        // resultado é um RectTransform comum, então dá pra arrastar/redimensionar
        // depois no Editor sem nada recolocando ele no lugar.
        rect.anchorMin = new Vector2(area.xMin, area.yMin);
        rect.anchorMax = new Vector2(area.xMax, area.yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = textColor;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;

        return tmp;
    }

    /// <summary>Mostra título + descrição de uma peça (chamado por PieceHoverInfo ao passar o mouse).</summary>
    public void ShowDescription(string title, string description)
    {
        ShowDescription(title, description, null);
    }

    /// <summary>Mostra título + descrição + ícone de uma peça.</summary>
    public void ShowDescription(string title, string description, Sprite icon)
    {
        pedidoTitulo = title;
        pedidoTexto = description;
        pedidoIcone = icon;
        if (avisoAtivo) return;

        if (titleText != null) titleText.text = title;
        if (descriptionText != null) descriptionText.text = description;
        SetIcon(icon);
    }

    /// <summary>
    /// Troca o texto padrão (o que aparece com o mouse fora das peças) e já
    /// mostra. Usado pelo CodigoEncaixeUI pra dar a dica da lacuna atual.
    /// </summary>
    public void DefinirPadrao(string title, string description)
    {
        defaultTitle = title;
        defaultDescription = description;
        Clear();
        // (Durante o aviso de erro, Clear só guarda o pedido.)
    }

    /// <summary>Volta ao texto padrão (chamado quando o mouse sai da peça).</summary>
    public void Clear()
    {
        ShowDescription(defaultTitle, defaultDescription, defaultIcon);
    }

    /// <summary>
    /// Peça errada (PuzzleControl): por "segundos", o painel vira um aviso —
    /// fundo vermelho escuro com borda vermelha, ✕ no lugar da lâmpada e
    /// "Essa peça não vai aqui." / "O código voltou ao começo.". Depois volta
    /// sozinho ao que deveria estar mostrando.
    /// </summary>
    public void MostrarAvisoErro(float segundos)
    {
        if (!avisoAtivo)
        {
            avisoAtivo = true;
            AplicarVisualAviso(true);
        }

        if (titleText != null) titleText.text = $"<b>{avisoErroTitulo}</b>";
        if (descriptionText != null) descriptionText.text = avisoErroTexto;

        if (avisoRotina != null) StopCoroutine(avisoRotina);
        avisoRotina = isActiveAndEnabled ? StartCoroutine(EsperarAviso(segundos)) : null;
    }

    /// <summary>Tira o aviso na hora (fim do tempo, acerto ou puzzle fechado).</summary>
    public void EsconderAvisoErro()
    {
        if (avisoRotina != null)
        {
            StopCoroutine(avisoRotina);
            avisoRotina = null;
        }

        if (!avisoAtivo) return;
        avisoAtivo = false;
        AplicarVisualAviso(false);
        ShowDescription(pedidoTitulo, pedidoTexto, pedidoIcone);
    }

    private System.Collections.IEnumerator EsperarAviso(float segundos)
    {
        yield return new WaitForSecondsRealtime(segundos);
        avisoRotina = null;
        EsconderAvisoErro();
    }

    // Liga/desliga o visual do aviso. O fundo do painel (o maior sprite filho)
    // é trocado por uma moldura gerada por código; os outros sprites (a aba
    // "Dica") ficam avermelhados; o ícone vira um ✕ vermelho.
    private void AplicarVisualAviso(bool ligar)
    {
        if (ligar)
        {
            fundoOriginal = null;
            float maiorArea = 0f;
            var outros = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr == externalIcon || sr == avisoFundo) continue;
                outros.Add(sr);
                float area = sr.bounds.size.x * sr.bounds.size.y;
                if (area > maiorArea)
                {
                    maiorArea = area;
                    fundoOriginal = sr;
                }
            }
            outros.Remove(fundoOriginal);

            tingidos = outros.ToArray();
            coresTingidos = new Color[tingidos.Length];
            for (int i = 0; i < tingidos.Length; i++)
            {
                coresTingidos[i] = tingidos[i].color;
                tingidos[i].color = TintaAba;
            }

            if (fundoOriginal != null)
            {
                if (avisoFundo == null)
                {
                    var go = new GameObject("AvisoErroFundo");
                    go.transform.SetParent(transform, false);
                    avisoFundo = go.AddComponent<SpriteRenderer>();
                    avisoFundo.sprite = PiscaErro.MolduraAviso;
                }
                avisoFundo.sortingLayerID = fundoOriginal.sortingLayerID;
                avisoFundo.sortingOrder = fundoOriginal.sortingOrder;
                PiscaErro.Cobrir(avisoFundo, fundoOriginal.bounds.center, fundoOriginal.bounds.size);
                avisoFundo.gameObject.SetActive(true);
                fundoOriginal.enabled = false;
            }

            if (titleText != null)
            {
                corTituloOriginal = titleText.color;
                titleText.color = CorTituloAviso;
            }
            if (externalIcon != null)
            {
                corIconeOriginal = externalIcon.color;
                externalIcon.sprite = PiscaErro.Xis;
                externalIcon.color = VidaUI.Vermelho;
                externalIcon.enabled = true;
            }
            return;
        }

        for (int i = 0; i < tingidos.Length; i++)
        {
            if (tingidos[i] != null) tingidos[i].color = coresTingidos[i];
        }
        tingidos = new SpriteRenderer[0];

        if (fundoOriginal != null) fundoOriginal.enabled = true;
        if (avisoFundo != null) avisoFundo.gameObject.SetActive(false);
        if (titleText != null) titleText.color = corTituloOriginal;
        if (externalIcon != null) externalIcon.color = corIconeOriginal;
    }

    // Sem ícone: esconde o renderer (em vez de deixar o ícone da última peça
    // sob o mouse "grudado" no painel).
    private void SetIcon(Sprite icon)
    {
        if (externalIcon == null)
        {
            return;
        }

        externalIcon.sprite = icon;
        externalIcon.enabled = icon != null;
    }
}
