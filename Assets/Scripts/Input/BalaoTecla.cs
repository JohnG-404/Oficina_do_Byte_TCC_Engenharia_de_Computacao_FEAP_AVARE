using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Balão do tema Laboratório sobre a cabeça do jogador, com a tecla de uma
/// ação e o que ela faz (ex.: [E] puxar · alavanca da bateria). É o mesmo
/// balão para todas as interações do jogo: agachar, alavancas, lâmpada e
/// itens configurados como "Pressionar um botão".
///
/// O visual é o prefab Resources/BalaoTecla (montado por Tools > Fases >
/// Balão de tecla > Montar prefab e colocar nas cenas). Cada cena com
/// interação tem uma instância salva; numa cena sem ela, o prefab é criado
/// na primeira vez que alguém pede o balão.
///
/// Quem quer o balão chama <see cref="Pedir"/> enquanto o jogador pode
/// interagir e <see cref="Soltar"/> quando não pode mais. Se dois pedirem ao
/// mesmo tempo, vale o mais recente. A tecla vem sempre do InputManager e é
/// trocada na hora se o jogador mudar o atalho nas configurações.
/// </summary>
public class BalaoTecla : MonoBehaviour
{
    private const float TempoFade = 0.15f;

    [SerializeField] private RectTransform balao;
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private TMP_Text letraTecla;
    [SerializeField] private TMP_Text acaoTxt;
    [SerializeField] private TMP_Text detalheTxt;
    [Tooltip("Distância (em unidades do mundo) entre o topo do jogador e a ponta do balão.")]
    [SerializeField] private float folgaCabeca = 0.2f;

    private class Pedido
    {
        public Object dono;
        public string acao;
        public string rotulo;
        public string detalhe;
    }

    private static BalaoTecla instancia;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instancia = null;

    // Nome de tecla comprido ("Espaço", "Shift"): a tecla alarga para a direita,
    // empurrando os textos e o balão (valores do prefab guardados no Awake).
    private const float TamanhoLetraLonga = 30f;
    private const float MargemTecla = 28f;
    private RectTransform caixaTecla;
    private float larguraTecla, larguraBalao;
    private RectTransform acaoRt, detalheRt;
    private float acaoX, detalheX;

    private readonly List<Pedido> pedidos = new List<Pedido>();
    private Pedido mostrado;
    private Transform jogador;
    private Collider2D colisorJogador;
    private AnimacaoPlayerFisica fisica;
    private InputManager inscritoEm;

    /// <summary>Balão da cena (procura o salvo na cena ou cria do prefab).</summary>
    public static BalaoTecla Instance
    {
        get
        {
            if (instancia == null)
            {
                instancia = FindAnyObjectByType<BalaoTecla>(FindObjectsInactive.Include);
            }

            if (instancia == null)
            {
                var prefab = Resources.Load<BalaoTecla>("BalaoTecla");
                if (prefab != null)
                {
                    instancia = Instantiate(prefab);
                    instancia.name = prefab.name;
                }
            }

            return instancia;
        }
    }

    /// <summary>Mostra o balão com a tecla da ação (nome da ação no mapa Player, ex.: Interact).</summary>
    public static void Pedir(Object dono, string acao, string rotulo, string detalhe)
    {
        if (dono == null || !Application.isPlaying) return;
        BalaoTecla b = Instance;
        if (b != null) b.Adicionar(dono, acao, rotulo, detalhe);
    }

    /// <summary>Tira o pedido desse dono (o balão some se ninguém mais pedir).</summary>
    public static void Soltar(Object dono)
    {
        if (instancia != null) instancia.Remover(dono);
    }

    /// <summary>Atalho: pede se <paramref name="mostrar"/> for true, solta se não.</summary>
    public static void Definir(Object dono, bool mostrar, string acao, string rotulo, string detalhe)
    {
        if (mostrar) Pedir(dono, acao, rotulo, detalhe);
        else Soltar(dono);
    }

    /// <summary>Balão visível agora (usado pelos testes).</summary>
    public bool Visivel => mostrado != null && grupo != null && grupo.alpha > 0f;

    /// <summary>Texto da tecla mostrada agora (usado pelos testes).</summary>
    public string TeclaMostrada => letraTecla != null ? letraTecla.text : "";

    /// <summary>Texto da ação mostrada agora (usado pelos testes).</summary>
    public string RotuloMostrado => acaoTxt != null ? acaoTxt.text : "";

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        if (grupo != null) grupo.alpha = 0f;

        // Letra -> Fundo -> Tecla (a caixa posicionada no painel).
        caixaTecla = letraTecla != null && letraTecla.transform.parent != null
            ? letraTecla.transform.parent.parent as RectTransform : null;
        if (caixaTecla != null) larguraTecla = caixaTecla.sizeDelta.x;
        if (balao != null) larguraBalao = balao.sizeDelta.x;
        if (acaoTxt != null) { acaoRt = acaoTxt.rectTransform; acaoX = acaoRt.anchoredPosition.x; }
        if (detalheTxt != null) { detalheRt = detalheTxt.rectTransform; detalheX = detalheRt.anchoredPosition.x; }
    }

    private void OnDestroy()
    {
        if (inscritoEm != null) inscritoEm.OnBindingsChanged -= AtualizarTecla;
        if (instancia == this) instancia = null;
    }

    private void Adicionar(Object dono, string acao, string rotulo, string detalhe)
    {
        Pedido p = pedidos.Find(x => x.dono == dono);
        if (p == null)
        {
            p = new Pedido { dono = dono };
            pedidos.Add(p);
        }
        else if (p.acao == acao && p.rotulo == rotulo && p.detalhe == detalhe)
        {
            return;
        }

        p.acao = acao;
        p.rotulo = rotulo;
        p.detalhe = detalhe;
        if (mostrado == null || mostrado == p) Exibir(p);
        else if (pedidos[pedidos.Count - 1] == p) Exibir(p);
    }

    private void Remover(Object dono)
    {
        int i = pedidos.FindIndex(x => x.dono == dono);
        if (i < 0) return;
        bool eraMostrado = pedidos[i] == mostrado;
        pedidos.RemoveAt(i);
        if (eraMostrado && pedidos.Count > 0) Exibir(pedidos[pedidos.Count - 1]);
    }

    private void Exibir(Pedido p)
    {
        mostrado = p;
        if (acaoTxt != null) acaoTxt.text = p.rotulo;
        if (detalheTxt != null)
        {
            detalheTxt.text = p.detalhe;
            detalheTxt.gameObject.SetActive(!string.IsNullOrEmpty(p.detalhe));
        }
        AtualizarTecla();
    }

    private void AtualizarTecla()
    {
        if (mostrado == null || letraTecla == null) return;
        string tecla = InputManager.Instance != null ? InputManager.Instance.GetBindingName(mostrado.acao) : "";
        letraTecla.text = string.IsNullOrEmpty(tecla) ? "?" : tecla;
        AjustarLargura();
    }

    private void AjustarLargura()
    {
        if (caixaTecla == null || larguraTecla <= 0f) return;

        float extra = 0f;
        if (letraTecla.text.Length > 2)
        {
            float texto = letraTecla.GetPreferredValues(letraTecla.text, float.PositiveInfinity, float.PositiveInfinity).x;
            texto *= TamanhoLetraLonga / Mathf.Max(1f, letraTecla.fontSize);
            extra = Mathf.Max(0f, texto + MargemTecla - larguraTecla);
        }

        caixaTecla.sizeDelta = new Vector2(larguraTecla + extra, caixaTecla.sizeDelta.y);
        if (balao != null) balao.sizeDelta = new Vector2(larguraBalao + extra, balao.sizeDelta.y);
        if (acaoRt != null) acaoRt.anchoredPosition = new Vector2(acaoX + extra, acaoRt.anchoredPosition.y);
        if (detalheRt != null) detalheRt.anchoredPosition = new Vector2(detalheX + extra, detalheRt.anchoredPosition.y);
    }

    private void Inscrever()
    {
        InputManager im = InputManager.Instance;
        if (im == inscritoEm) return;
        if (inscritoEm != null) inscritoEm.OnBindingsChanged -= AtualizarTecla;
        inscritoEm = im;
        if (im != null) im.OnBindingsChanged += AtualizarTecla;
        AtualizarTecla();
    }

    private void ProcurarJogador()
    {
        if (jogador != null) return;

        GameObject p = null;
        var agachar = FindAnyObjectByType<PlayerAgachar>();
        if (agachar != null) p = agachar.gameObject;
        if (p == null)
        {
            var corpo = FindAnyObjectByType<AnimacaoPlayerFisica>();
            if (corpo != null) p = corpo.gameObject;
        }
        if (p == null)
        {
            var interacao = FindAnyObjectByType<InteracaoPlayerController>();
            if (interacao != null) p = interacao.gameObject;
        }
        if (p == null) p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;

        jogador = p.transform;
        colisorJogador = p.GetComponent<Collider2D>();
        fisica = p.GetComponent<AnimacaoPlayerFisica>();
    }

    private void LateUpdate()
    {
        // Donos destruídos (troca de cena, item coletado) saem da lista.
        pedidos.RemoveAll(x => x.dono == null);
        if (mostrado != null && !pedidos.Contains(mostrado))
        {
            if (pedidos.Count > 0) Exibir(pedidos[pedidos.Count - 1]);
            else if (grupo == null || grupo.alpha <= 0f) mostrado = null;
        }

        Inscrever();
        ProcurarJogador();

        bool mostrar = ConfiguracoesJogo.DicasTecla // Configurações > Jogo > Dicas de teclas
            && pedidos.Count > 0 && jogador != null && jogador.gameObject.activeInHierarchy
            && !DialogueManager.IsAnyDialogueActive && Time.timeScale > 0f
            && (PauseMenuUI.Instance == null || !PauseMenuUI.Instance.PausaAberta)
            && (SettingsPanelUI.Instance == null || !SettingsPanelUI.Instance.IsVisible)
            // Na Animacao, com um painel aberto (Animator do Player, lâmpada) ou morto, nada de balão.
            && (fisica == null || (fisica.MovimentoLiberado && !fisica.EstaMorto));

        if (grupo == null) return;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, mostrar ? 1f : 0f, Time.unscaledDeltaTime / TempoFade);
        if (grupo.alpha > 0f && jogador != null) Posicionar();
    }

    private void Posicionar()
    {
        Camera cam = Camera.main;
        if (cam == null || balao == null) return;

        float topo = colisorJogador != null ? colisorJogador.bounds.max.y : jogador.position.y + 0.5f;
        Vector3 tela = cam.WorldToScreenPoint(new Vector3(jogador.position.x, topo + folgaCabeca, jogador.position.z));
        var raiz = (RectTransform)transform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(raiz, tela, null, out Vector2 local))
        {
            balao.anchoredPosition = local;
        }
    }
}
