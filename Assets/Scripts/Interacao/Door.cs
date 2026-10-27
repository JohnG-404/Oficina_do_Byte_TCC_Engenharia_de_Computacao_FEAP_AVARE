using System.Collections;
using UnityEngine;

/// <summary>
/// Porta simples: bloqueia a passagem (collider sólido) até ser aberta pela
/// Consequência "Abrir uma porta" (ver InteractableItem/ConsequenceType.OpenDoor).
///
/// Porta de enrolar (padrão): a parte de cima (porta_cima) fica fixa e o
/// painel vira lâminas (sprite portaLamina do TemaLaboratorio, em modo Tiled)
/// que vão do fundo da porta_cima até o chão. Ao abrir, as lâminas se
/// recolhem para dentro da porta_cima, de baixo para cima, e as luzes dela
/// passam de vermelho para verde. Nada aparece por cima da porta.
///
/// Sem a porta_cima ou sem o sprite da lâmina, usa o jeito antigo: desliza o
/// painel para cima.
/// </summary>
public class Door : MonoBehaviour
{
    [SerializeField] private Collider2D blockingCollider;

    [Tooltip("Parte visual que se mexe ao abrir (o painel). Se vazio, usa o próprio transform.")]
    [SerializeField] private Transform panelToSlide;

    [Tooltip("Parte de cima, fixa (porta_cima). Se vazio, procura o outro SpriteRenderer filho.")]
    [SerializeField] private SpriteRenderer caixaDeCima;

    [Tooltip("Desligado = comportamento antigo (o painel desliza para cima).")]
    [SerializeField] private bool portaDeEnrolar = true;

    [Tooltip("Largura das lâminas em relação à porta_cima (1 = mesma largura). Menor = parece que entram na caixa.")]
    [Range(0.3f, 1f)]
    [SerializeField] private float larguraLaminas = LarguraInternaPx / 144f;

    [SerializeField] private float openMoveDistance = 3f;
    [SerializeField] private float openDuration = 0.6f;

    [Header("Luzes da porta_cima (porta de enrolar)")]
    [SerializeField] private Color corFechada = new Color32(0xE5, 0x64, 0x6A, 0xFF);
    [SerializeField] private Color corAbrindo = new Color32(0xF5, 0xC5, 0x42, 0xFF);
    [SerializeField] private Color corAberta = new Color32(0x52, 0xC9, 0x8A, 0xFF);
    [Tooltip("Sprite das luzes (porta_luz). Vazio = desenha um círculo na hora.")]
    [SerializeField] private Sprite luzSprite;

    // Onde ficam as duas luzes no sprite porta_cima (144 x 176 px, a partir do canto de cima à esquerda).
    private static readonly Vector2[] LuzesPx = { new Vector2(40f, 42f), new Vector2(103f, 42f) };
    private static readonly Vector2 TamanhoCaixaPx = new Vector2(144f, 176f);
    private const float LarguraInternaPx = 132f;
    private const float RecolheDentroDaCaixa = 0.12f;

    private SpriteRenderer painel;
    private SpriteRenderer[] luzes;
    private float alturaFechada;
    private float topoPainel;
    private float escalaLaminaY = 1.5f;
    private bool enrolando;

    private static Sprite spriteLuz;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (blockingCollider == null)
        {
            blockingCollider = GetComponent<Collider2D>();
        }

        if (panelToSlide == null)
        {
            panelToSlide = transform;
        }

        PrepararPortaDeEnrolar();
        if (blockingCollider != null && blockingCollider.enabled)
        {
            areaPassagem = blockingCollider.bounds;
        }
    }

    /// <summary>Monta a porta de enrolar (chamado no Awake; público para capturas de conferência no Editor).</summary>
    public void PrepararPortaDeEnrolar()
    {
        if (portaDeEnrolar && !enrolando)
        {
            enrolando = MontarPortaDeEnrolar();
        }
    }

    /// <summary>Desenha a porta aberta pela fração <paramref name="fracao"/> (0 fechada, 1 aberta), sem animar. Para conferência no Editor.</summary>
    public void MostrarAbertura(float fracao)
    {
        if (!enrolando)
        {
            return;
        }

        fracao = Mathf.Clamp01(fracao);
        DesenharPainel(alturaFechada * (1f - fracao), caixaDeCima.bounds.center.x);
        PintarLuzes(fracao <= 0f ? corFechada : fracao >= 1f ? corAberta : corAbrindo);
    }

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;

        if (blockingCollider != null)
        {
            blockingCollider.enabled = false;
        }

        InteracaoLevelFlow.Instance?.NotifyDoorOpened();
        AudioManager.TocarEfeito(AudioManager.Efeito.PortaAbrir);
        StartCoroutine(enrolando ? Enrolar() : SlideUp());
    }

    /// <summary>
    /// Consequência "Abrir por 5 segundos": abre e fecha sozinha depois do
    /// tempo. Se o jogador estiver embaixo da porta na hora, espera ele sair;
    /// se ele entrar enquanto ela desce, ela volta a abrir (ver Close).
    /// </summary>
    public void OpenFor(float segundos)
    {
        if (fecharDepois != null)
        {
            StopCoroutine(fecharDepois);
            fecharDepois = null;
        }

        if (fracaoFechada > 0f)
        {
            // Foi acionada de novo no meio da descida: sobe de volta.
            StartCoroutine(Reabrir());
        }

        Open();
        fecharDepois = StartCoroutine(FecharDepois(segundos));
    }

    private Coroutine fecharDepois;

    // Quanto a porta já desceu (0 aberta, 1 fechada) enquanto Close anima.
    private float fracaoFechada;

    // Área do collider fechado: com a porta aberta o collider fica desligado.
    private Bounds areaPassagem;

    private IEnumerator FecharDepois(float segundos)
    {
        // Luzes amarelas no último segundo: aviso de que vai fechar.
        yield return new WaitForSeconds(Mathf.Max(0f, segundos - 1f));
        PintarLuzes(corAbrindo);
        yield return new WaitForSeconds(Mathf.Min(1f, segundos));

        while (JogadorNaPassagem())
        {
            yield return null;
        }

        yield return Close();
        fecharDepois = null;
    }

    private bool JogadorNaPassagem()
    {
        if (blockingCollider == null)
        {
            return false;
        }

        Bounds area = blockingCollider.enabled ? blockingCollider.bounds : areaPassagem;
        foreach (Collider2D c in Physics2D.OverlapBoxAll(area.center, area.size, 0f))
        {
            if (c != blockingCollider && InteractableItem.IsPlayer(c) && !c.isTrigger)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Fecha a porta de novo (animação inversa) e volta a bloquear. Se o
    /// jogador entrar embaixo durante a descida, a porta sobe de novo, como
    /// porta de elevador, e só tenta fechar quando a passagem estiver livre.
    /// </summary>
    public IEnumerator Close()
    {
        if (!IsOpen)
        {
            yield break;
        }

        Vector3 inicio = panelToSlide.position - Vector3.down * openMoveDistance * Mathf.SmoothStep(0f, 1f, fracaoFechada);
        AudioManager.TocarEfeito(AudioManager.Efeito.PortaFechar);
        PintarLuzes(corAbrindo);
        while (fracaoFechada < 1f)
        {
            if (JogadorNaPassagem())
            {
                AudioManager.TocarEfeito(AudioManager.Efeito.PortaAbrir);
                while (fracaoFechada > 0f)
                {
                    fracaoFechada = Mathf.Max(0f, fracaoFechada - Time.deltaTime / openDuration);
                    DesenharFechamento(inicio);
                    yield return null;
                }
                while (JogadorNaPassagem())
                {
                    yield return null;
                }
                AudioManager.TocarEfeito(AudioManager.Efeito.PortaFechar);
            }

            fracaoFechada = Mathf.Min(1f, fracaoFechada + Time.deltaTime / openDuration);
            DesenharFechamento(inicio);
            yield return null;
        }

        fracaoFechada = 0f;
        if (enrolando)
        {
            PintarLuzes(corFechada);
        }
        if (blockingCollider != null)
        {
            blockingCollider.enabled = true;
        }
        IsOpen = false;
    }

    // Desenha a porta descida pela fração fracaoFechada. inicio = posição do
    // painel aberto (só no jeito antigo, que desliza o painel).
    private void DesenharFechamento(Vector3 inicio)
    {
        float k = Mathf.SmoothStep(0f, 1f, fracaoFechada);
        if (enrolando)
        {
            DesenharPainel(alturaFechada * k, caixaDeCima.bounds.center.x);
        }
        else
        {
            panelToSlide.position = inicio + Vector3.down * openMoveDistance * k;
        }
    }

    // Sobe a porta que parou no meio da descida (acionada de novo).
    private IEnumerator Reabrir()
    {
        Vector3 inicio = panelToSlide.position - Vector3.down * openMoveDistance * Mathf.SmoothStep(0f, 1f, fracaoFechada);
        while (fracaoFechada > 0f)
        {
            fracaoFechada = Mathf.Max(0f, fracaoFechada - Time.deltaTime / openDuration);
            DesenharFechamento(inicio);
            yield return null;
        }
        if (enrolando)
        {
            PintarLuzes(corAberta);
        }
    }

    // ---------------------------------------------------------------- enrolar

    private bool MontarPortaDeEnrolar()
    {
        Sprite lamina = TemaLaboratorio.Atual != null ? TemaLaboratorio.Atual.portaLamina : null;
        painel = panelToSlide != null ? panelToSlide.GetComponent<SpriteRenderer>() : null;

        if (caixaDeCima == null)
        {
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr != painel && sr.sprite != null)
                {
                    caixaDeCima = sr;
                    break;
                }
            }
        }

        if (lamina == null || painel == null || caixaDeCima == null || caixaDeCima.sprite == null)
        {
            return false;
        }

        Bounds caixa = caixaDeCima.bounds;
        float chao = blockingCollider != null ? blockingCollider.bounds.min.y : caixa.min.y - 2f;
        topoPainel = caixa.min.y + RecolheDentroDaCaixa;
        alturaFechada = Mathf.Max(0.1f, topoPainel - chao);
        float largura = caixa.size.x * larguraLaminas;

        painel.sprite = lamina;
        painel.drawMode = SpriteDrawMode.Tiled;
        painel.tileMode = SpriteTileMode.Continuous;
        painel.color = Color.white;
        painel.sortingLayerID = caixaDeCima.sortingLayerID;
        painel.sortingOrder = caixaDeCima.sortingOrder - 1;

        float larguraSprite = lamina.bounds.size.x;
        Transform t = painel.transform;
        Vector3 escalaPai = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localRotation = Quaternion.identity;
        t.localScale = new Vector3(largura / larguraSprite / escalaPai.x, escalaLaminaY / escalaPai.y, 1f);
        DesenharPainel(alturaFechada, caixa.center.x);

        CriarLuzes(caixa);
        PintarLuzes(corFechada);
        return true;
    }

    private void DesenharPainel(float altura, float centroX)
    {
        Transform t = painel.transform;
        float escalaY = t.lossyScale.y;
        painel.size = new Vector2(painel.sprite.bounds.size.x, Mathf.Max(0f, altura) / escalaY);
        painel.enabled = altura > 0.001f;
        t.position = new Vector3(centroX, topoPainel - altura * 0.5f, t.position.z);
    }

    private IEnumerator Enrolar()
    {
        float centroX = caixaDeCima.bounds.center.x;
        PintarLuzes(corAbrindo);
        float t = 0f;
        while (t < openDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / openDuration);
            DesenharPainel(alturaFechada * (1f - k), centroX);
            yield return null;
        }

        DesenharPainel(0f, centroX);
        PintarLuzes(corAberta);
    }

    private void CriarLuzes(Bounds caixa)
    {
        Sprite luz = luzSprite;
        if (luz == null)
        {
            if (spriteLuz == null)
            {
                spriteLuz = CriarSpriteCirculo(32);
            }
            luz = spriteLuz;
        }

        luzes = new SpriteRenderer[LuzesPx.Length];
        float pxParaMundo = caixa.size.x / TamanhoCaixaPx.x;
        for (int i = 0; i < LuzesPx.Length; i++)
        {
            // Luzes já salvas na cena (montadas no Editor) são reaproveitadas.
            Transform existente = caixaDeCima.transform.Find("Luz" + (i + 1));
            var go = existente != null ? existente.gameObject : new GameObject("Luz" + (i + 1));
            go.transform.SetParent(caixaDeCima.transform, true);
            go.transform.position = new Vector3(
                caixa.min.x + LuzesPx[i].x * pxParaMundo,
                caixa.max.y - LuzesPx[i].y * (caixa.size.y / TamanhoCaixaPx.y),
                caixaDeCima.transform.position.z);
            float diametro = 16f * pxParaMundo;
            Vector3 escala = caixaDeCima.transform.lossyScale;
            go.transform.localScale = new Vector3(diametro / luz.bounds.size.x / escala.x, diametro / luz.bounds.size.y / escala.y, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = luz;
            sr.sortingLayerID = caixaDeCima.sortingLayerID;
            sr.sortingOrder = caixaDeCima.sortingOrder + 1;
            luzes[i] = sr;
        }
    }

    private void PintarLuzes(Color cor)
    {
        if (luzes == null)
        {
            return;
        }

        foreach (SpriteRenderer sr in luzes)
        {
            if (sr != null)
            {
                sr.color = cor;
            }
        }
    }

    // Círculo com contorno escuro, como as luzes desenhadas do pack Tecno.
    public static Sprite CriarSpriteCirculo(int tamanho)
    {
        var tex = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var contorno = new Color32(0x1B, 0x1E, 0x25, 0xFF);
        float r = tamanho * 0.5f;
        for (int y = 0; y < tamanho; y++)
        {
            for (int x = 0; x < tamanho; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d);
                Color c = d < r - 3.5f ? Color.white : (Color)contorno;
                c.a = a;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tamanho, tamanho), new Vector2(0.5f, 0.5f), 100f);
    }

    // ---------------------------------------------------------------- antigo

    private IEnumerator SlideUp()
    {
        Vector3 start = panelToSlide.position;
        Vector3 end = start + Vector3.up * openMoveDistance;
        float t = 0f;

        while (t < openDuration)
        {
            t += Time.deltaTime;
            panelToSlide.position = Vector3.Lerp(start, end, t / openDuration);
            yield return null;
        }

        panelToSlide.position = end;
    }
}
