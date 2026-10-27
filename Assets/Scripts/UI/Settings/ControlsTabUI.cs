using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Conteúdo da aba "Controles": lista de ações remapeáveis (ControlRowUI)
/// mais o botão "Restaurar Padrão".
///
/// Reconhece a própria estrutura pelo nome dos filhos ("ScrollArea",
/// "RestaurarPadraoButton"): se já existirem (montados/ajustados no Editor
/// via GameUIBuilder), apenas reconecta o clique; senão, constrói a
/// estrutura padrão em runtime. As linhas de ação são sempre geradas
/// dinamicamente a partir de InputManager.RebindableActions.
///
/// Em Play, a lista ganha barra de rolagem, o aviso "mais N abaixo" e rola
/// sozinha até a linha selecionada pelas setas (com 7 ações, cabem 5 de cada vez).
/// </summary>
public class ControlsTabUI : MonoBehaviour
{
    private readonly List<ControlRowUI> spawnedRows = new List<ControlRowUI>();
    private bool rowsBuilt;
    private RectTransform rowsContainer;
    private Button restoreButton;

    private static readonly Color CorTrilho = new Color32(16, 22, 35, 255);
    private static readonly Color CorAlca = new Color32(110, 124, 152, 255);
    private static readonly Color CorPainel = new Color32(27, 36, 56, 255);
    private static readonly Color CorPilula = new Color32(36, 48, 73, 255);
    private static readonly Color CorTexto = new Color32(232, 238, 249, 255);

    // Faixa reservada embaixo da lista para o "mais N abaixo": a lista termina
    // acima dela, então o aviso nunca cobre uma linha.
    private const float AlturaPe = 40f;

    private ScrollRect rolagem;
    private RectTransform viewport;
    private GameObject maisAbaixo;
    private TMP_Text maisAbaixoTexto;
    private int linhasAbaixo = -1;
    private GameObject ultimaSelecao;

    private void Awake()
    {
        EnsureBuilt();
    }

    private void OnEnable()
    {
        if (!rowsBuilt)
        {
            BuildRows();
        }

        if (rowsBuilt && rolagem == null)
        {
            MontarRolagem();
        }

        // Sempre abre no começo da lista.
        if (rowsContainer != null)
        {
            rowsContainer.anchoredPosition = new Vector2(rowsContainer.anchoredPosition.x, 0f);
        }
        ultimaSelecao = null;
        linhasAbaixo = -1;
    }

    private void LateUpdate()
    {
        if (rolagem == null || rowsContainer == null)
        {
            return;
        }

        SeguirSelecao();
        AtualizarMaisAbaixo();
    }

    /// <summary>Barra de rolagem na margem direita e aviso "mais N abaixo" no pé da lista.</summary>
    private void MontarRolagem()
    {
        rolagem = rowsContainer != null ? rowsContainer.GetComponentInParent<ScrollRect>() : null;
        if (rolagem == null)
        {
            return;
        }

        viewport = rolagem.viewport != null ? rolagem.viewport : (RectTransform)rolagem.transform;
        var area = (RectTransform)rolagem.transform.parent;
        viewport.offsetMin = new Vector2(viewport.offsetMin.x, viewport.offsetMin.y + AlturaPe);

        // A roda do mouse anda cerca de uma linha por clique.
        rolagem.scrollSensitivity = 16f;
        rolagem.movementType = ScrollRect.MovementType.Clamped;

        Sprite arredondado = CriarSpriteArredondado();

        var barraGO = new GameObject("BarraRolagem", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        var barraRect = (RectTransform)barraGO.transform;
        barraRect.SetParent(area, false);
        barraRect.anchorMin = new Vector2(1f, 0f);
        barraRect.anchorMax = new Vector2(1f, 1f);
        barraRect.pivot = new Vector2(0f, 0.5f);
        barraRect.offsetMin = new Vector2(12f, AlturaPe);
        barraRect.offsetMax = new Vector2(22f, 0f);
        var trilho = barraGO.GetComponent<Image>();
        trilho.sprite = arredondado;
        trilho.type = Image.Type.Sliced;
        trilho.color = CorTrilho;

        var deslize = new GameObject("Deslize", typeof(RectTransform));
        var deslizeRect = (RectTransform)deslize.transform;
        deslizeRect.SetParent(barraRect, false);
        UIBuildUtils.StretchToParent(deslizeRect);

        var alcaGO = new GameObject("Alca", typeof(RectTransform), typeof(Image));
        var alcaRect = (RectTransform)alcaGO.transform;
        alcaRect.SetParent(deslizeRect, false);
        alcaRect.offsetMin = new Vector2(-1f, 0f);
        alcaRect.offsetMax = new Vector2(1f, 0f);
        var alca = alcaGO.GetComponent<Image>();
        alca.sprite = arredondado;
        alca.type = Image.Type.Sliced;
        alca.color = CorAlca;

        var barra = barraGO.GetComponent<Scrollbar>();
        barra.handleRect = alcaRect;
        barra.targetGraphic = alca;
        barra.direction = Scrollbar.Direction.BottomToTop;
        barra.transition = Selectable.Transition.None;
        barra.navigation = new Navigation { mode = Navigation.Mode.None };

        rolagem.verticalScrollbar = barra;
        rolagem.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // Pé da lista, na faixa reservada: fundo na cor do painel e a pílula "↓ mais N abaixo".
        maisAbaixo = new GameObject("MaisAbaixo", typeof(RectTransform), typeof(Image));
        var maisRect = (RectTransform)maisAbaixo.transform;
        maisRect.SetParent(area, false);
        maisRect.anchorMin = new Vector2(0f, 0f);
        maisRect.anchorMax = new Vector2(1f, 0f);
        maisRect.pivot = new Vector2(0.5f, 0f);
        maisRect.anchoredPosition = Vector2.zero;
        maisRect.sizeDelta = new Vector2(0f, AlturaPe);
        var degrade = maisAbaixo.GetComponent<Image>();
        degrade.sprite = CriarSpriteDegrade();
        degrade.color = CorPainel;
        degrade.raycastTarget = false;

        var pilulaGO = new GameObject("Pilula", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        var pilulaRect = (RectTransform)pilulaGO.transform;
        pilulaRect.SetParent(maisRect, false);
        pilulaRect.anchorMin = pilulaRect.anchorMax = new Vector2(0.5f, 0f);
        pilulaRect.pivot = new Vector2(0.5f, 0f);
        pilulaRect.anchoredPosition = new Vector2(0f, 4f);
        var pilula = pilulaGO.GetComponent<Image>();
        pilula.sprite = arredondado;
        pilula.type = Image.Type.Sliced;
        pilula.color = CorPilula;
        pilula.raycastTarget = false;
        var hl = pilulaGO.GetComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(18, 18, 3, 3);
        hl.childControlWidth = hl.childControlHeight = true;
        var csf = pilulaGO.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var textoGO = new GameObject("Texto", typeof(RectTransform));
        textoGO.transform.SetParent(pilulaRect, false);
        maisAbaixoTexto = textoGO.AddComponent<TextMeshProUGUI>();
        TMP_Text modelo = rowsContainer.GetComponentInChildren<TMP_Text>(true);
        if (modelo != null)
        {
            maisAbaixoTexto.font = modelo.font;
        }
        maisAbaixoTexto.fontSize = 22;
        maisAbaixoTexto.color = CorTexto;
        maisAbaixoTexto.alignment = TextAlignmentOptions.Center;
        maisAbaixoTexto.raycastTarget = false;
        maisAbaixoTexto.textWrappingMode = TextWrappingModes.NoWrap;

        maisAbaixo.SetActive(false);
    }

    /// <summary>Quando as setas selecionam uma linha fora da área visível, rola até ela.</summary>
    private void SeguirSelecao()
    {
        GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (sel == ultimaSelecao)
        {
            return;
        }

        // No primeiro quadro a lista ainda não tem altura: tenta de novo no próximo.
        if (rowsContainer.rect.height <= 0f)
        {
            return;
        }
        ultimaSelecao = sel;

        if (sel == null || !sel.transform.IsChildOf(rowsContainer) || sel.transform == rowsContainer)
        {
            return;
        }

        Transform linha = sel.transform;
        while (linha.parent != rowsContainer)
        {
            linha = linha.parent;
        }

        Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, linha);
        Rect v = viewport.rect;
        float limiteBaixo = v.yMin;
        float delta = 0f;
        if (b.min.y < limiteBaixo)
        {
            delta = limiteBaixo - b.min.y;
        }
        else if (b.max.y > v.yMax)
        {
            delta = v.yMax - b.max.y;
        }

        if (!Mathf.Approximately(delta, 0f))
        {
            rolagem.velocity = Vector2.zero;
            rowsContainer.anchoredPosition += new Vector2(0f, delta);
        }
    }

    private void AtualizarMaisAbaixo()
    {
        if (maisAbaixo == null)
        {
            return;
        }

        Rect v = viewport.rect;
        int abaixo = 0;
        foreach (Transform linha in rowsContainer)
        {
            if (!linha.gameObject.activeSelf)
            {
                continue;
            }

            Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, linha);
            if (b.min.y < v.yMin - 1f)
            {
                abaixo++;
            }
        }

        if (abaixo == linhasAbaixo)
        {
            return;
        }

        linhasAbaixo = abaixo;
        maisAbaixo.SetActive(abaixo > 0);
        if (abaixo > 0)
        {
            maisAbaixoTexto.text = "↓  mais " + abaixo + " abaixo";
        }
    }

    private static Sprite spriteArredondado;
    private static Sprite spriteDegrade;

    private static Sprite CriarSpriteArredondado()
    {
        if (spriteArredondado != null)
        {
            return spriteArredondado;
        }

        const int tam = 24;
        const float raio = 11.5f;
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, raio, tam - raio);
                float cy = Mathf.Clamp(y + 0.5f, raio, tam - raio);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(raio - d + 0.5f)));
            }
        }
        tex.Apply();
        spriteArredondado = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(11, 11, 11, 11));
        return spriteArredondado;
    }

    private static Sprite CriarSpriteDegrade()
    {
        if (spriteDegrade != null)
        {
            return spriteDegrade;
        }

        const int alt = 32;
        var tex = new Texture2D(1, alt, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < alt; y++)
        {
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, 1f - y / (float)(alt - 1)));
        }
        tex.Apply();
        spriteDegrade = Sprite.Create(tex, new Rect(0, 0, 1, alt), new Vector2(0.5f, 0.5f));
        return spriteDegrade;
    }

    /// <summary>
    /// Constrói (ou reconecta, se já existir) a estrutura estática desta aba
    /// (ScrollView + botão Restaurar Padrão). Público para poder ser chamado
    /// explicitamente por uma ferramenta de Editor, já que Awake() nem sempre
    /// roda de forma síncrona fora do Play Mode. As linhas de ação em si só
    /// são geradas em Play (dependem do InputManager em runtime).
    /// </summary>
    public void EnsureBuilt()
    {
        RemoveConflictingParentLayout();
        BuildOrFindStructure();
        WireEvents();
    }

    // Uma versão anterior colocava um VerticalLayoutGroup no container desta aba,
    // que ignora as âncoras próprias do ScrollArea/botão Restaurar Padrão e
    // esprime a área de scroll a quase zero de altura. Remove se encontrar, pra
    // uma cena salva com a estrutura antiga se corrigir sozinha.
    private void RemoveConflictingParentLayout()
    {
        var layout = GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            DestroyImmediate(layout);
        }
    }

    private void BuildOrFindStructure()
    {
        Transform scrollAreaT = transform.Find("ScrollArea");
        Transform restoreT = transform.Find("RestaurarPadraoButton");

        if (scrollAreaT != null && restoreT != null)
        {
            rowsContainer = FindRowsContainer(scrollAreaT);
            restoreButton = restoreT.GetComponent<Button>();
            return;
        }

        var scrollArea = new GameObject("ScrollArea", typeof(RectTransform));
        var scrollAreaRect = (RectTransform)scrollArea.transform;
        scrollAreaRect.SetParent(transform, false);
        scrollAreaRect.anchorMin = new Vector2(0f, 0.16f);
        scrollAreaRect.anchorMax = new Vector2(1f, 1f);
        scrollAreaRect.offsetMin = Vector2.zero;
        scrollAreaRect.offsetMax = Vector2.zero;

        rowsContainer = BuildScrollView(scrollAreaRect);

        var restoreGO = UIBuildUtils.CreateButtonWithLabel(transform, "RestaurarPadraoButton", "Restaurar padrão", out _);
        var restoreRect = restoreGO.GetComponent<RectTransform>();
        restoreRect.anchorMin = new Vector2(0.25f, 0f);
        restoreRect.anchorMax = new Vector2(0.75f, 0.12f);
        restoreRect.offsetMin = Vector2.zero;
        restoreRect.offsetMax = Vector2.zero;
        restoreButton = restoreGO.GetComponent<Button>();
    }

    private static RectTransform BuildScrollView(RectTransform parent)
    {
        var scrollGO = new GameObject("ScrollView", typeof(RectTransform));
        var scrollRect = (RectTransform)scrollGO.transform;
        scrollRect.SetParent(parent, false);
        UIBuildUtils.StretchToParent(scrollRect);

        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scrollGO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
        scrollGO.AddComponent<Mask>().showMaskGraphic = true;

        var content = new GameObject("Content", typeof(RectTransform));
        var contentRect = (RectTransform)content.transform;
        contentRect.SetParent(scrollRect, false);
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);

        var vLayout = content.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = 16;
        vLayout.padding = new RectOffset(0, 0, 12, 12);
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = false;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;

        return contentRect;
    }

    // Localiza o "Content" do ScrollView já existente (montado no Editor ou em execução anterior).
    private static RectTransform FindRowsContainer(Transform scrollAreaRoot)
    {
        Transform scrollView = scrollAreaRoot.Find("ScrollView");
        if (scrollView == null)
        {
            return null;
        }

        Transform content = scrollView.Find("Content");
        return content != null ? (RectTransform)content : null;
    }

    private void WireEvents()
    {
        if (restoreButton == null)
        {
            return;
        }

        restoreButton.onClick.RemoveListener(RestoreDefaults);
        restoreButton.onClick.AddListener(RestoreDefaults);
    }

    public void RestoreDefaults()
    {
        InputManager.Instance?.ResetBindings();
    }

    private void BuildRows()
    {
        ControlRowUI rowPrefab = Resources.Load<ControlRowUI>("ControlRowUI");

        if (InputManager.Instance == null || rowPrefab == null || rowsContainer == null)
        {
            Debug.LogWarning("[ControlsTabUI] InputManager, ControlRowUI (Resources) ou container não disponíveis.");
            return;
        }

        foreach (RebindActionInfo info in InputManager.Instance.RebindableActions)
        {
            ControlRowUI row = Instantiate(rowPrefab, rowsContainer);
            row.Configure(info);
            spawnedRows.Add(row);
        }

        rowsBuilt = true;
    }
}
