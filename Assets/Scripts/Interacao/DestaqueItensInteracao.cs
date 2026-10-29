using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Mostra o que é clicável na cena Interacao, sem contorno nos objetos:
/// - painel ao lado do mouse, no estilo do PainelComponentes da Física, com o
///   nome do item, se já foi configurado e a Ação/Consequência escolhidas
///   (ou "a escolher" e o convite pra clicar);
/// - etiqueta fixa "Ação → Consequência" em cima de cada item já configurado.
///
/// Tudo some durante o diálogo e com o assistente de configuração aberto.
/// Montado e salvo na cena por Tools > Interação > Montar destaque dos itens.
///
/// Na Interacao_Vida a etiqueta tem duas
/// linhas, fica embaixo das plataformas (em cima é onde o jogador pisa), não
/// sai da tela e fica transparente quando o jogador passa atrás dela.
/// </summary>
public class DestaqueItensInteracao : MonoBehaviour
{
    [System.Serializable]
    public struct Linha
    {
        public Image fundo;
        public Image borda;
        public Image ponto;
        public TMP_Text nome;
        public TMP_Text sub;
        public RectTransform pilula;
    }

    [System.Serializable]
    public struct Etiqueta
    {
        public InteractableItem item;
        public RectTransform raiz;
        public TMP_Text acao;
        public TMP_Text consequencia;
        [Tooltip("Embaixo do item, com a ponta para cima (plataformas da Interacao_Vida).")]
        public bool embaixo;
        public RectTransform ponta;
        public CanvasGroup grupo;
    }

    [Header("Painel do mouse")]
    [SerializeField] private RectTransform caixa;
    [SerializeField] private TMP_Text titulo;
    [SerializeField] private RectTransform tipo;
    [SerializeField] private Image tipoFundo;
    [SerializeField] private Image tipoBorda;
    [SerializeField] private TMP_Text tipoTexto;
    [SerializeField] private Linha linhaAcao;
    [SerializeField] private Linha linhaConsequencia;
    [SerializeField] private TMP_Text rodape;

    [Header("Etiquetas dos itens configurados")]
    [SerializeField] private Etiqueta[] etiquetas;
    [Tooltip("Folga entre o topo do item e a ponta da etiqueta (pixels da tela de referência).")]
    [SerializeField] private float folgaEtiqueta = 10f;
    [Tooltip("Etiqueta de item na borda encosta na borda em vez de sair cortada; item fora da tela não mostra etiqueta.")]
    [SerializeField] private bool manterNaTela;
    [Tooltip("Etiqueta fica transparente enquanto o desenho do jogador está atrás dela.")]
    [SerializeField] private bool clarearAtrasDoJogador;
    [SerializeField, Range(0f, 1f)] private float alfaAtrasDoJogador = 0.3f;
    [SerializeField] private Transform jogador;
    [SerializeField] private float margemEtiqueta = 12f;

    [Header("Posição do painel")]
    [SerializeField] private Vector2 deslocamento = new Vector2(22f, 30f);
    [SerializeField] private float margemTela = 20f;

    [Header("Cores")]
    [SerializeField] private Color cartao = new Color(0.078f, 0.106f, 0.161f);
    [SerializeField] private Color fundoVazio = new Color(0.047f, 0.067f, 0.106f);
    [SerializeField] private Color bordaVazia = new Color(0.180f, 0.196f, 0.235f);
    [SerializeField] private Color pilulaNeutra = new Color(0.118f, 0.125f, 0.149f);
    [SerializeField] private Color texto = new Color(0.902f, 0.914f, 0.937f);
    [SerializeField] private Color textoApagado = new Color(0.541f, 0.576f, 0.651f);
    [SerializeField] private Color textoFraco = new Color(0.361f, 0.392f, 0.459f);
    [SerializeField] private Color corAcao = new Color(0.263f, 0.820f, 0.851f);
    [SerializeField] private Color corConsequencia = new Color(0.961f, 0.769f, 0.259f);
    [SerializeField] private Color corConfigurado = new Color(0.290f, 0.871f, 0.502f);

    private readonly Collider2D[] acertos = new Collider2D[16];
    private Camera cam;
    private Canvas canvas;
    private InteractableItem ultimoAlvo;
    private int ultimoEstado = -1;
    private SpriteRenderer[] desenhosJogador;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (clarearAtrasDoJogador)
        {
            if (jogador == null)
            {
                var controle = FindAnyObjectByType<InteracaoPlayerController>();
                if (controle != null) jogador = controle.transform;
            }

            desenhosJogador = jogador != null ? jogador.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
        }

        if (caixa != null)
        {
            caixa.gameObject.SetActive(false);
        }

        foreach (Etiqueta e in etiquetas)
        {
            if (e.raiz != null) e.raiz.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
        }

        bool liberado = cam != null && !DialogueManager.IsAnyDialogueActive && Time.timeScale > 0f &&
                        (InteractionConfigUI.Instance == null || !InteractionConfigUI.Instance.PainelAberto);

        AtualizarEtiquetas(liberado);
        AtualizarPainel(liberado);
    }

    // ------------------------------------------------------------ etiquetas

    private void AtualizarEtiquetas(bool liberado)
    {
        var pai = (RectTransform)transform;
        Rect tela = pai.rect;
        Rect jogadorNaTela = clarearAtrasDoJogador ? RetanguloDoJogador(pai) : Rect.zero;
        for (int i = 0; i < etiquetas.Length; i++)
        {
            Etiqueta e = etiquetas[i];
            if (e.raiz == null)
            {
                continue;
            }

            InteractableItem item = e.item;
            bool mostrar = liberado && item != null && item.isActiveAndEnabled && item.IsConfigured;
            Vector2 local = Vector2.zero;
            if (mostrar)
            {
                Vector3 ancora = e.embaixo ? BaseDoItem(item) : TopoDoItem(item);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(pai, cam.WorldToScreenPoint(ancora), CameraDaUi(), out local);
                // Com manterNaTela, item fora da tela não deixa etiqueta grudada na borda.
                mostrar = !manterNaTela || (local.x >= tela.xMin && local.x <= tela.xMax);
            }

            if (e.raiz.gameObject.activeSelf != mostrar)
            {
                e.raiz.gameObject.SetActive(mostrar);
            }

            if (!mostrar)
            {
                continue;
            }

            string acao = InteractionConfigUI.NomeAcao(item.ChosenAction);
            string cons = InteractionConfigUI.NomeConsequencia(item.ChosenConsequence);
            if (e.acao.text != acao) e.acao.text = acao;
            if (e.consequencia.text != cons) e.consequencia.text = cons;

            float x = local.x;
            if (manterNaTela)
            {
                float meia = e.raiz.rect.width * 0.5f;
                x = Mathf.Clamp(x, tela.xMin + margemEtiqueta + meia, Mathf.Max(tela.xMin + margemEtiqueta + meia, tela.xMax - margemEtiqueta - meia));
            }

            float y = e.embaixo ? local.y - folgaEtiqueta : local.y + folgaEtiqueta;
            // Item colado no teto (ou no chão): a etiqueta desce (ou sobe) até caber inteira.
            Rect r = e.raiz.rect;
            y = Mathf.Min(y, tela.yMax - margemEtiqueta - r.yMax);
            y = Mathf.Max(y, tela.yMin + margemEtiqueta - r.yMin);
            e.raiz.localPosition = new Vector3(x, y, 0f);

            // A ponta continua apontando pro item quando a etiqueta encosta na borda.
            if (e.ponta != null)
            {
                float limite = Mathf.Max(0f, e.raiz.rect.width * 0.5f - 14f);
                Vector2 p = e.ponta.anchoredPosition;
                p.x = Mathf.Clamp(local.x - x, -limite, limite);
                e.ponta.anchoredPosition = p;
            }

            if (clarearAtrasDoJogador && e.grupo != null)
            {
                var area = new Rect(x + r.xMin, y + r.yMin, r.width, r.height);
                float alvo = jogadorNaTela.width > 0f && area.Overlaps(jogadorNaTela) ? alfaAtrasDoJogador : 1f;
                e.grupo.alpha = Mathf.MoveTowards(e.grupo.alpha, alvo, Time.unscaledDeltaTime * 6f);
            }
        }
    }

    // Retângulo do desenho do jogador no espaço do destaque.
    private Rect RetanguloDoJogador(RectTransform pai)
    {
        bool achou = false;
        Bounds b = default;
        foreach (SpriteRenderer sr in desenhosJogador)
        {
            if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy) continue;
            if (achou) b.Encapsulate(sr.bounds);
            else { b = sr.bounds; achou = true; }
        }

        if (!achou)
        {
            return Rect.zero;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(pai, cam.WorldToScreenPoint(b.min), CameraDaUi(), out Vector2 a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(pai, cam.WorldToScreenPoint(b.max), CameraDaUi(), out Vector2 z);
        return Rect.MinMaxRect(a.x, a.y, z.x, z.y);
    }

    private static Vector3 BaseDoItem(InteractableItem item)
    {
        Collider2D c = item.GetComponent<Collider2D>();
        if (c != null && c.enabled)
        {
            return new Vector3(c.bounds.center.x, c.bounds.min.y, 0f);
        }

        var sr = item.GetComponent<SpriteRenderer>();
        return sr != null ? new Vector3(sr.bounds.center.x, sr.bounds.min.y, 0f) : item.transform.position;
    }

    // Meio do topo do collider do próprio item (a área que recebe o clique);
    // sem collider ligado, o desenho do próprio objeto.
    private static Vector3 TopoDoItem(InteractableItem item)
    {
        Collider2D c = item.GetComponent<Collider2D>();
        if (c != null && c.enabled)
        {
            return new Vector3(c.bounds.center.x, c.bounds.max.y, 0f);
        }

        var sr = item.GetComponent<SpriteRenderer>();
        return sr != null ? new Vector3(sr.bounds.center.x, sr.bounds.max.y, 0f) : item.transform.position;
    }

    // ---------------------------------------------------------------- painel

    private void AtualizarPainel(bool liberado)
    {
        InteractableItem alvo = liberado ? ItemEmbaixoDoMouse(out Vector2 telaMouse) : null;
        if (alvo == null)
        {
            Mostrar(false);
            ultimoAlvo = null;
            return;
        }

        int estado = alvo.IsConfigured ? 1 + (int)alvo.ChosenAction * 100 + (int)alvo.ChosenConsequence : 0;
        if (alvo != ultimoAlvo || estado != ultimoEstado)
        {
            ultimoAlvo = alvo;
            ultimoEstado = estado;
            Preencher(alvo);
        }

        Posicionar(Mouse.current.position.ReadValue());
        Mostrar(true);
    }

    private InteractableItem ItemEmbaixoDoMouse(out Vector2 telaMouse)
    {
        telaMouse = Vector2.zero;
        Mouse mouse = Mouse.current;
        if (mouse == null || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            return null;
        }

        telaMouse = mouse.position.ReadValue();
        Vector2 mundo = cam.ScreenToWorldPoint(telaMouse);
        var filtro = new ContactFilter2D { useTriggers = true };
        bool queriesHitTriggers = Physics2D.queriesHitTriggers;
        int n;
        try
        {
            Physics2D.queriesHitTriggers = true;
            n = Physics2D.OverlapPoint(mundo, filtro, acertos);
        }
        finally
        {
            Physics2D.queriesHitTriggers = queriesHitTriggers;
        }

        // Só o collider do próprio item conta (é ele que recebe o clique); as
        // zonas de proximidade e de topo são filhos bem maiores que o desenho.
        for (int i = 0; i < n; i++)
        {
            var item = acertos[i].GetComponent<InteractableItem>();
            if (item != null && item.isActiveAndEnabled)
            {
                return item;
            }
        }

        return null;
    }

    private void Preencher(InteractableItem item)
    {
        titulo.text = item.DisplayName;
        bool feito = item.IsConfigured;

        string tipoStr = feito ? "configurado" : "configurável";
        tipoTexto.text = tipoStr;
        tipoTexto.color = feito ? corConfigurado : textoApagado;
        tipoFundo.color = feito ? Color.Lerp(cartao, corConfigurado, 0.18f) : pilulaNeutra;
        tipoBorda.color = feito ? ComAlfa(corConfigurado, 0.6f) : bordaVazia;
        float largura = tipoStr.Length * 8.5f + 22f;
        tipo.sizeDelta = new Vector2(largura, tipo.sizeDelta.y);
        tipo.anchoredPosition = new Vector2(caixa.sizeDelta.x - 16f - largura, tipo.anchoredPosition.y);
        // O nome encolhe (autosize) para nunca encostar na pílula.
        RectTransform rtTitulo = titulo.rectTransform;
        rtTitulo.sizeDelta = new Vector2(caixa.sizeDelta.x - 16f - largura - 12f - rtTitulo.anchoredPosition.x, rtTitulo.sizeDelta.y);

        if (feito)
        {
            PintarLinha(linhaAcao, corAcao, InteractionConfigUI.NomeAcao(item.ChosenAction),
                InteractionConfigUI.EquivalenteUnity(item.ChosenAction), true);
            PintarLinha(linhaConsequencia, corConsequencia, InteractionConfigUI.NomeConsequencia(item.ChosenConsequence),
                item.ChosenRepeats ? "repetir: sempre" : "repetir: uma vez", false);
            rodape.text = "Clique para reconfigurar.";
        }
        else
        {
            PintarVazia(linhaAcao, item.AllowedActions.Count);
            PintarVazia(linhaConsequencia, item.AllowedConsequences.Count);
            rodape.text = "Clique para escolher Ação e Consequência.";
        }
    }

    private void PintarLinha(Linha l, Color cor, string nome, string sub, bool subCodigo)
    {
        l.fundo.color = Color.Lerp(cartao, cor, 0.14f);
        l.borda.color = ComAlfa(cor, 0.75f);
        l.ponto.color = cor;
        l.nome.text = nome;
        l.nome.color = texto;
        l.sub.gameObject.SetActive(!string.IsNullOrEmpty(sub));
        l.sub.text = sub;
        l.sub.color = subCodigo ? cor : Color.Lerp(cor, Color.white, 0.35f);
        l.nome.rectTransform.anchoredPosition = new Vector2(l.nome.rectTransform.anchoredPosition.x, string.IsNullOrEmpty(sub) ? -12f : -3f);
        l.pilula.gameObject.SetActive(false);
    }

    private void PintarVazia(Linha l, int opcoes)
    {
        l.fundo.color = fundoVazio;
        l.borda.color = bordaVazia;
        l.ponto.color = ComAlfa(textoFraco, 0.6f);
        l.nome.text = "a escolher";
        l.nome.color = textoApagado;
        l.sub.gameObject.SetActive(true);
        l.sub.text = opcoes == 1 ? "1 opção" : opcoes + " opções";
        l.sub.color = textoFraco;
        l.nome.rectTransform.anchoredPosition = new Vector2(l.nome.rectTransform.anchoredPosition.x, -3f);
        l.pilula.gameObject.SetActive(true);
    }

    private static Color ComAlfa(Color c, float a)
    {
        c.a = a;
        return c;
    }

    private Camera CameraDaUi()
    {
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private void Posicionar(Vector2 telaMouse)
    {
        var pai = (RectTransform)caixa.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(pai, telaMouse, CameraDaUi(), out Vector2 local);

        // Abaixo e à direita do mouse; vira quando encostaria na borda.
        Rect r = pai.rect;
        Vector2 tam = caixa.sizeDelta;
        float x = local.x + deslocamento.x;
        float y = local.y - deslocamento.y;
        if (y - tam.y < r.yMin + margemTela)
        {
            y = local.y + 16f + tam.y;
        }

        if (x + tam.x > r.xMax - margemTela)
        {
            x = local.x - deslocamento.x - tam.x;
        }

        x = Mathf.Clamp(x, r.xMin + margemTela, r.xMax - margemTela - tam.x);
        y = Mathf.Clamp(y, r.yMin + margemTela + tam.y, r.yMax - margemTela);
        caixa.localPosition = new Vector3(x, y, 0f);
    }

    private void Mostrar(bool sim)
    {
        if (caixa != null && caixa.gameObject.activeSelf != sim)
        {
            caixa.gameObject.SetActive(sim);
        }
    }
}
