using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Lista suspensa das linhas "Resolução" e "Modo de tela" das Configurações
/// (aba Gráficos): no lugar das setas ◀ ▶, Enter ou clique na linha abrem a
/// lista com todas as opções (GameManager.Resolucoes ou GameManager.NomesModoTela),
/// e o jogador vê todas de uma vez. ↑↓ (ou a roda do mouse) percorrem, Enter/clique escolhe, Esc ou clique
/// fora fecham sem mudar nada.
///
/// A opção escolhida é só aplicada (não gravada) e abre a confirmação
/// ConfirmarResolucaoUI: sem confirmar em alguns segundos, volta à anterior.
///
/// Montada em runtime pelo OpcaoConfigUI da linha, com as cores e sprites do
/// tema Laboratório já usados na própria linha (caixa do valor, fonte).
/// </summary>
public class ListaResolucaoUI : MonoBehaviour
{
    private const float AlturaItem = 52f;
    private const int ItensVisiveis = 7;
    private const float Margem = 8f;
    private const float LarguraMinima = 320f;
    private const float EspacoRodape = 110f;   // faixa da legenda de teclas, embaixo da janela

    private static ListaResolucaoUI aberta;
    private static int frameFechou = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        aberta = null;
        frameFechou = -1;
    }

    public static bool Aberta => aberta != null && aberta.overlay != null && aberta.overlay.activeSelf;

    /// <summary>
    /// Lista ou confirmação abertas (ou fechadas neste frame com Esc): a pausa e
    /// as Configurações não tratam esse Esc (ver PauseMenuUI.EscDeOutraJanela).
    /// </summary>
    public static bool SegurandoEsc => Aberta || Time.frameCount == frameFechou || ConfirmarResolucaoUI.SegurandoEsc;

    private RectTransform caixa;       // caixa do valor na linha: a lista abre colada nela
    private TMP_Text modeloTexto;      // "Valor" da linha: fonte e cor da lista
    private RectTransform botaoAbrir;  // o ▼ ao lado da caixa: a lista vai da caixa até ele
    private bool ehModoTela;           // false = resoluções, true = modos de tela

    private GameObject overlay;
    private RectTransform painel;
    private RectTransform viewport;
    private RectTransform conteudo;
    private ScrollRect scroll;
    private Scrollbar barra;
    private readonly List<Button> itens = new List<Button>();
    private GameObject ultimoSelecionado;

    public void Configurar(RectTransform caixaValor, TMP_Text valor, RectTransform seta, bool modoTela = false)
    {
        caixa = caixaValor;
        modeloTexto = valor;
        botaoAbrir = seta;
        ehModoTela = modoTela;
    }

    private static int Quantidade(GameManager gm, bool modo) => modo ? GameManager.NomesModoTela.Length : gm.Resolucoes.Count;
    private static int IndiceAtual(GameManager gm, bool modo) => modo ? gm.indiceModoTela : gm.indiceResolucao;

    // ------------------------------------------------------------------

    public void Abrir()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || Quantidade(gm, ehModoTela) == 0 || caixa == null)
        {
            return;
        }

        Construir();
        MontarItens(gm);

        overlay.transform.SetAsLastSibling();
        overlay.SetActive(true);
        Posicionar(Quantidade(gm, ehModoTela));
        aberta = this;

        int atual = Mathf.Clamp(IndiceAtual(gm, ehModoTela), 0, itens.Count - 1);
        Selecionar(itens[atual].gameObject);
        MostrarItem(atual);
    }

    public void Fechar()
    {
        if (overlay == null || !overlay.activeSelf)
        {
            return;
        }

        overlay.SetActive(false);
        if (aberta == this) aberta = null;
        frameFechou = Time.frameCount;
        Selecionar(gameObject);
    }

    private void Escolher(int indice)
    {
        GameManager gm = GameManager.Instance;
        Fechar();
        if (gm == null || indice == IndiceAtual(gm, ehModoTela))
        {
            return;
        }

        int resolucaoAnterior = gm.indiceResolucao;
        int modoAnterior = gm.indiceModoTela;
        if (ehModoTela) gm.DefinirModoTela(indice, salvar: false);
        else gm.DefinirIndiceResolucao(indice, salvar: false);

        // Sem a janela de confirmação (não achou o modelo), fica a opção nova.
        if (!ConfirmarResolucaoUI.Abrir(transform, resolucaoAnterior, modoAnterior, ehModoTela, gameObject))
        {
            gm.SalvarTela();
        }
    }

    private void OnDisable()
    {
        // A aba ou a janela fechou com a lista aberta.
        if (overlay != null && overlay.activeSelf)
        {
            overlay.SetActive(false);
            if (aberta == this) aberta = null;
        }
    }

    private void Update()
    {
        if (!Aberta || aberta != this)
        {
            return;
        }

        InputManager im = InputManager.Instance;
        if (im != null && im.UICancelAction != null && im.UICancelAction.WasPressedThisFrame())
        {
            Fechar();
            return;
        }

        // Navegando por teclado, a lista rola junto com o item em foco.
        GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (sel != ultimoSelecionado)
        {
            ultimoSelecionado = sel;
            int i = sel != null ? itens.FindIndex(b => b.gameObject == sel) : -1;
            if (i >= 0) MostrarItem(i);
        }
    }

    // ------------------------------------------------------------------

    private void Construir()
    {
        if (overlay != null)
        {
            return;
        }

        // Fica na raiz das Configurações, por cima das outras linhas.
        SettingsPanelUI janela = GetComponentInParent<SettingsPanelUI>(true);
        Transform pai = janela != null ? janela.transform : transform.parent;

        overlay = new GameObject(ehModoTela ? "ListaModoTela" : "ListaResolucao", typeof(RectTransform));
        var ov = (RectTransform)overlay.transform;
        ov.SetParent(pai, false);
        ov.anchorMin = Vector2.zero;
        ov.anchorMax = Vector2.one;
        ov.offsetMin = ov.offsetMax = Vector2.zero;

        // Clique fora da lista fecha.
        var fundo = overlay.AddComponent<Image>();
        fundo.color = new Color(0f, 0f, 0f, 0f);
        var fora = overlay.AddComponent<Button>();
        fora.transition = Selectable.Transition.None;
        fora.navigation = new Navigation { mode = Navigation.Mode.None };
        fora.onClick.AddListener(Fechar);

        painel = Novo("Lista", ov);
        painel.anchorMin = painel.anchorMax = new Vector2(0.5f, 0.5f);
        var painelImg = painel.gameObject.AddComponent<Image>();
        Image caixaImg = caixa.GetComponent<Image>();
        if (caixaImg != null && caixaImg.sprite != null)
        {
            painelImg.sprite = caixaImg.sprite;
            painelImg.type = Image.Type.Sliced;
            painelImg.color = caixaImg.color;
        }
        else
        {
            painelImg.color = TemaLabUI.PainelElevado;
        }

        viewport = Novo("Viewport", painel);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(Margem, Margem);
        viewport.offsetMax = new Vector2(-Margem, -Margem);
        viewport.gameObject.AddComponent<RectMask2D>();
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0f);   // recebe a roda do mouse entre os itens

        conteudo = Novo("Conteudo", viewport);
        conteudo.anchorMin = new Vector2(0f, 1f);
        conteudo.anchorMax = new Vector2(1f, 1f);
        conteudo.pivot = new Vector2(0.5f, 1f);
        conteudo.anchoredPosition = Vector2.zero;
        conteudo.sizeDelta = Vector2.zero;
        var vlg = conteudo.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var csf = conteudo.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = painel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = conteudo;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = AlturaItem * 0.6f;
        scroll.inertia = false;

        // Barrinha à direita, só quando há mais itens que cabem.
        RectTransform barraRT = Novo("Barra", painel);
        barraRT.anchorMin = new Vector2(1f, 0f);
        barraRT.anchorMax = new Vector2(1f, 1f);
        barraRT.pivot = new Vector2(1f, 0.5f);
        barraRT.offsetMin = new Vector2(-Margem - 6f, Margem + 4f);
        barraRT.offsetMax = new Vector2(-Margem + 2f, -Margem - 4f);
        var trilho = barraRT.gameObject.AddComponent<Image>();
        trilho.color = TemaLabUI.Poco;
        RectTransform area = Novo("Area", barraRT);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = area.offsetMax = Vector2.zero;
        RectTransform pegador = Novo("Pegador", area);
        pegador.offsetMin = pegador.offsetMax = Vector2.zero;
        var pegadorImg = pegador.gameObject.AddComponent<Image>();
        pegadorImg.color = TemaLabUI.TextoSuave;
        barra = barraRT.gameObject.AddComponent<Scrollbar>();
        barra.handleRect = pegador;
        barra.targetGraphic = pegadorImg;
        barra.direction = Scrollbar.Direction.BottomToTop;
        barra.navigation = new Navigation { mode = Navigation.Mode.None };
        scroll.verticalScrollbar = barra;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        overlay.SetActive(false);
    }

    private void MontarItens(GameManager gm)
    {
        int total = Quantidade(gm, ehModoTela);
        int indiceAtual = IndiceAtual(gm, ehModoTela);
        Vector2Int nativa = new Vector2Int(Display.main.systemWidth, Display.main.systemHeight);

        while (itens.Count < total) itens.Add(NovoItem(itens.Count));
        for (int i = 0; i < itens.Count; i++) itens[i].gameObject.SetActive(i < total);

        string corSuave = ColorUtility.ToHtmlStringRGB(TemaLabUI.TextoSuave);
        for (int i = 0; i < total; i++)
        {
            bool atual = i == indiceAtual;
            var t = itens[i].GetComponentInChildren<TMP_Text>(true);
            if (ehModoTela)
            {
                t.text = GameManager.NomesModoTela[i];
            }
            else
            {
                Vector2Int r = gm.Resolucoes[i];
                t.text = r == nativa
                    ? $"{r.x} × {r.y}  <color=#{corSuave}><size=80%>nativa</size></color>"
                    : $"{r.x} × {r.y}";
            }
            t.color = atual ? TemaLabUI.Verde : TemaLabUI.CorTexto;

            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp = i > 0 ? itens[i - 1] : null;
            nav.selectOnDown = i < total - 1 ? itens[i + 1] : null;
            itens[i].navigation = nav;
        }
    }

    private Button NovoItem(int indice)
    {
        RectTransform r = Novo("Item", conteudo);
        var le = r.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = AlturaItem;
        le.minHeight = AlturaItem;

        var img = r.gameObject.AddComponent<Image>();
        img.color = Color.white;
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.ColorTint;
        Color foco = TemaLabUI.Azul;
        b.colors = new ColorBlock
        {
            normalColor = new Color(1f, 1f, 1f, 0f),
            highlightedColor = new Color(foco.r, foco.g, foco.b, 0.22f),
            selectedColor = new Color(foco.r, foco.g, foco.b, 0.38f),
            pressedColor = new Color(foco.r, foco.g, foco.b, 0.6f),
            disabledColor = new Color(1f, 1f, 1f, 0f),
            colorMultiplier = 1f,
            fadeDuration = 0.06f,
        };
        b.onClick.AddListener(() => Escolher(indice));

        RectTransform tr = Novo("Texto", r);
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(16f, 0f);
        tr.offsetMax = new Vector2(-16f, 0f);
        var t = tr.gameObject.AddComponent<TextMeshProUGUI>();
        if (modeloTexto != null)
        {
            t.font = modeloTexto.font;
            t.fontSharedMaterial = modeloTexto.fontSharedMaterial;
            t.fontSize = modeloTexto.fontSize;
        }
        else
        {
            t.fontSize = 24f;
        }
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return b;
    }

    private void Posicionar(int quantidade)
    {
        var ov = (RectTransform)overlay.transform;
        int visiveis = Mathf.Min(quantidade, ItensVisiveis);
        float altura = visiveis * AlturaItem + Margem * 2f;
        // Na largura da caixa do valor até o ▼, alinhada com eles.
        Rect c = caixa.rect;
        float esquerda = ov.InverseTransformPoint(caixa.TransformPoint(new Vector3(c.xMin, 0f, 0f))).x;
        float direita = ov.InverseTransformPoint(caixa.TransformPoint(new Vector3(c.xMax, 0f, 0f))).x;
        if (botaoAbrir != null && botaoAbrir.gameObject.activeInHierarchy)
        {
            Rect s = botaoAbrir.rect;
            direita = Mathf.Max(direita, ov.InverseTransformPoint(botaoAbrir.TransformPoint(new Vector3(s.xMax, 0f, 0f))).x);
        }
        float largura = Mathf.Max(direita - esquerda, LarguraMinima);
        float centroX = esquerda + largura * 0.5f;
        painel.sizeDelta = new Vector2(largura, altura);

        bool rola = quantidade > ItensVisiveis;
        barra.gameObject.SetActive(rola);
        viewport.offsetMax = new Vector2(rola ? -Margem - 10f : -Margem, -Margem);

        // Abre logo abaixo da caixa do valor; se não couber acima da legenda, abre para cima.
        Vector2 baixo = ov.InverseTransformPoint(caixa.TransformPoint(new Vector3(c.center.x, c.yMin, 0f)));
        Vector2 cima = ov.InverseTransformPoint(caixa.TransformPoint(new Vector3(c.center.x, c.yMax, 0f)));
        float limiteInferior = ov.rect.yMin + EspacoRodape;

        if (baixo.y - 6f - altura >= limiteInferior)
        {
            painel.pivot = new Vector2(0.5f, 1f);
            painel.anchoredPosition = new Vector2(centroX, baixo.y - 6f);
        }
        else
        {
            painel.pivot = new Vector2(0.5f, 0f);
            painel.anchoredPosition = new Vector2(centroX, cima.y + 6f);
        }
    }

    // Rola só o necessário pra o item i ficar inteiro na lista.
    private void MostrarItem(int i)
    {
        Canvas.ForceUpdateCanvases();
        float alturaVista = viewport.rect.height;
        float topo = i * AlturaItem;
        float baseItem = topo + AlturaItem;
        float y = conteudo.anchoredPosition.y;

        if (topo < y) y = topo;
        else if (baseItem > y + alturaVista) y = baseItem - alturaVista;

        float maximo = Mathf.Max(0f, conteudo.rect.height - alturaVista);
        conteudo.anchoredPosition = new Vector2(conteudo.anchoredPosition.x, Mathf.Clamp(y, 0f, maximo));
        scroll.velocity = Vector2.zero;
    }

    private static void Selecionar(GameObject go)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
    }

    private static RectTransform Novo(string nome, Transform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(pai, false);
        return r;
    }
}
