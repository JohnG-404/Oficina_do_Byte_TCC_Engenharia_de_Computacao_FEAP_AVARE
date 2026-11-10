using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Faixa de objetivo no topo central: uma faixa compacta que
/// fica sempre visível com o objetivo da etapa atual ("Monte o Rigidbody2D
/// 4/9") e, quando a etapa muda, aparece grande por alguns segundos (título,
/// dica e bolinhas de progresso) antes de recolher.
///
/// O visual fica salvo na cena (Tools > Fases > Faixa de objetivo e fim de
/// fase); os textos de cada etapa ficam aqui, no Inspector. Os flows de cada
/// fase só avisam a etapa: <c>ObjetivoFase.Ir(3)</c>. Sem a faixa na cena,
/// as chamadas não fazem nada.
///
/// "{reiniciar}" nos textos vira a tecla atual da ação "Reiniciar fase", e
/// "{andar}"/"{pular}" as teclas de mover e pular (TextoTeclas); trocam junto
/// com a aba Controles.
///
/// A faixa grande espera o diálogo acabar para aparecer (não disputa
/// atenção com o Prof Byte). Tudo some com o painel de fim de fase, com o
/// menu de pausa ou as Configurações abertos (o tempo da faixa grande fica
/// parado) e enquanto um quebra-cabeça (PuzzleControl) está na tela; nesse
/// caso a faixa grande pendente é descartada, porque o puzzle já mostra o
/// próprio título. Com o painel "Configurar item" aberto a faixa some na hora
/// (sem fade, para não cobrir a aba com o nome do item) e a faixa grande
/// pendente espera o painel fechar. Com um puzzle abrindo também: o fade deixava a faixa um instante sobre o título do puzzle.
/// O visual roda no LateUpdate para ver o puzzle já aberto no mesmo frame.
/// Enquanto a caixa de diálogo estiver no canto de cima
/// (DialogueManager.CaixaNoTopo) a faixa também some na
/// hora, senão cobre a placa com o nome do Prof Byte.
/// Quando o robô passa por baixo (o elevador da Interação chega no topo, a
/// escada da Aplicação), a faixa grande recolhe na hora e a compacta fica
/// translúcida, para não esconder a ação.
/// </summary>
public class ObjetivoFase : MonoBehaviour
{
    [System.Serializable]
    public class Etapa
    {
        public string titulo;
        [TextArea(1, 3)] public string dica;
    }

    public static ObjetivoFase Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    [Header("Etapas (na ordem em que acontecem)")]
    [SerializeField] private Etapa[] etapas = new Etapa[0];

    [Header("Tempo da faixa grande")]
    [SerializeField] private float segundosGrande = 3.5f;
    [SerializeField] private float segundosFade = 0.25f;

    [Header("Robô por baixo da faixa")]
    [Tooltip("Opacidade da faixa compacta enquanto o robô está embaixo dela.")]
    [SerializeField, Range(0f, 1f)] private float alphaComRoboEmbaixo = 0.3f;
    [Tooltip("Folga em pixels (1080p) ao redor do robô.")]
    [SerializeField] private float folgaRobo = 24f;

    [Header("Referências (montadas pelo builder)")]
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private CanvasGroup compacta;
    [SerializeField] private TMP_Text textoCompacto;
    [SerializeField] private TMP_Text contador;
    [SerializeField] private CanvasGroup grande;
    [SerializeField] private TMP_Text rotuloGrande;
    [SerializeField] private TMP_Text tituloGrande;
    [SerializeField] private TMP_Text dicaGrande;
    [SerializeField] private Image[] pontos = new Image[0];
    [SerializeField] private Sprite pontoFeito;
    [SerializeField] private Sprite pontoAtual;
    [SerializeField] private Sprite pontoFalta;

    private int atual = -1;
    private PuzzleControl[] puzzles = new PuzzleControl[0];
    private bool emExtra;
    private string extraTitulo;
    private string extraDica;
    private bool grandePendente;
    private float grandeRestante;
    private Transform robo;
    private Renderer[] rendRobo = new Renderer[0];
    private float proximaBuscaRobo;
    private readonly Vector3[] cantos = new Vector3[4];

    /// <summary>
    /// A faixa grande ("Novo objetivo · etapa N de M") está na tela. O painel
    /// "Animator do Player" (AnimatorEstadoHUD) some nesse tempo: a faixa grande
    /// é larga e passava por cima dele.
    /// </summary>
    public static bool GrandeVisivel => Instance != null && Instance.grande != null
        && Instance.grande.alpha > 0.01f && (Instance.grupo == null || Instance.grupo.alpha > 0.01f)
        && Instance.isActiveAndEnabled;

    public int EtapaAtual => atual;
    public int QuantidadeDeEtapas => etapas != null ? etapas.Length : 0;

    private void Awake()
    {
        Instance = this;
        if (grupo != null) grupo.alpha = 0f;
        if (grande != null) grande.alpha = 0f;
    }

    private InputManager inscrito;

    private void Start()
    {
        puzzles = FindObjectsByType<PuzzleControl>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (InputManager.Instance != null)
        {
            inscrito = InputManager.Instance;
            inscrito.OnBindingsChanged += AoTrocarTecla;
        }
    }

    private void AoTrocarTecla()
    {
        // O RotuloTeclaUI da faixa pode ter ligado antes do InputManager existir
        // (aí não se inscreveu): religar faz ele ler a tecla de novo.
        foreach (RotuloTeclaUI r in GetComponentsInChildren<RotuloTeclaUI>(true))
        {
            if (r.isActiveAndEnabled)
            {
                r.enabled = false;
                r.enabled = true;
            }
        }

        if (emExtra || atual >= 0)
        {
            Atualizar(false);
        }
    }

    private const string TokenReiniciar = "{reiniciar}";

    private static string Teclas(string texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return texto;
        }

        if (texto.Contains(TokenReiniciar))
        {
            string tecla = InputManager.Instance != null ? InputManager.Instance.GetBindingName(InputManager.RestartActionName) : "";
            texto = texto.Replace(TokenReiniciar, string.IsNullOrEmpty(tecla) ? "R" : tecla);
        }

        return TextoTeclas.Trocar(texto);
    }

    private static bool PausaOuConfiguracoes =>
        (PauseMenuUI.Instance != null && PauseMenuUI.Instance.PausaAberta)
        || (SettingsPanelUI.Instance != null && SettingsPanelUI.Instance.IsVisible);

    private static bool ConfigurarItemAberto =>
        InteractionConfigUI.Instance != null && InteractionConfigUI.Instance.PainelAberto;

    private bool PuzzleAberto()
    {
        foreach (PuzzleControl p in puzzles)
        {
            if (p != null && p.Puzzle != null && p.Puzzle.activeInHierarchy)
            {
                return true;
            }
        }
        return false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (inscrito != null)
        {
            inscrito.OnBindingsChanged -= AoTrocarTecla;
            inscrito = null;
        }
    }

    // ---------------------------------------------------------------- API

    /// <summary>Vai para a etapa <paramref name="indice"/>. Mesma etapa de novo não faz nada.</summary>
    public static void Ir(int indice, bool mostrarGrande = true)
    {
        if (Instance != null)
        {
            Instance.IrPara(indice, mostrarGrande);
        }
    }

    /// <summary>
    /// Vai para a etapa e mostra a faixa grande, mesmo se a faixa já estava nela
    /// (ex.: a faixa trocou em silêncio ao fechar o puzzle e o destaque vem
    /// quando o diálogo seguinte termina).
    /// </summary>
    public static void Destacar(int indice)
    {
        if (Instance == null)
        {
            return;
        }

        int antes = Instance.atual;
        Instance.IrPara(indice, true);
        if (Instance.atual == antes && antes >= 0 && !Instance.emExtra && !Instance.grandePendente)
        {
            Instance.grandePendente = true;
            Instance.grandeRestante = Instance.segundosGrande;
        }
    }

    /// <summary>
    /// Objetivo de fora da lista (ex.: a lição de HUD da Interacao_Vida), sem
    /// número. <see cref="FimExtra"/> volta para a etapa em que estava.
    /// </summary>
    public static void Extra(string titulo, string dica = "")
    {
        if (Instance == null || string.IsNullOrEmpty(titulo))
        {
            return;
        }

        Instance.emExtra = true;
        Instance.extraTitulo = titulo;
        Instance.extraDica = dica;
        Instance.Atualizar(true);
    }

    public static void FimExtra()
    {
        if (Instance == null || !Instance.emExtra)
        {
            return;
        }

        Instance.emExtra = false;
        Instance.Atualizar(false);
    }

    public void IrPara(int indice, bool mostrarGrande = true)
    {
        if (etapas == null || etapas.Length == 0)
        {
            return;
        }

        indice = Mathf.Clamp(indice, 0, etapas.Length - 1);
        if (indice == atual)
        {
            return;
        }

        // Só avançar chama atenção; voltar (ex.: configuração desfeita na IA) só troca o texto.
        bool avancou = indice > atual;
        atual = indice;
        if (!emExtra)
        {
            Atualizar(mostrarGrande && avancou);
        }
    }

    // ---------------------------------------------------------------- visual

    private void Atualizar(bool chamarAtencao)
    {
        string titulo = Teclas(emExtra ? extraTitulo : (atual >= 0 ? etapas[atual].titulo : ""));
        string dica = Teclas(emExtra ? extraDica : (atual >= 0 ? etapas[atual].dica : ""));
        string numero = emExtra || atual < 0 ? "!" : (atual + 1) + "/" + etapas.Length;

        if (textoCompacto != null) textoCompacto.text = titulo;
        if (contador != null) contador.text = numero;
        if (tituloGrande != null) tituloGrande.text = titulo;
        if (dicaGrande != null) dicaGrande.text = dica;
        if (rotuloGrande != null)
        {
            rotuloGrande.text = emExtra ? "Novo objetivo" : $"Novo objetivo · etapa {atual + 1} de {etapas.Length}";
        }

        for (int i = 0; i < pontos.Length; i++)
        {
            Image p = pontos[i];
            if (p == null) continue;
            bool existe = !emExtra && i < etapas.Length;
            p.gameObject.SetActive(existe);
            if (!existe) continue;

            bool feito = i < atual;
            bool agora = i == atual;
            p.sprite = feito ? pontoFeito : (agora ? pontoAtual : pontoFalta);
            p.color = feito ? TemaLabUI.Verde : Color.white;
            float t = agora ? 22f : 18f;
            p.rectTransform.sizeDelta = new Vector2(t, t);
        }

        if (chamarAtencao)
        {
            grandePendente = true;
            grandeRestante = segundosGrande;
        }
    }

    private void LateUpdate()
    {
        bool fimDeFase = LevelCompleteUI.Aberto;
        bool pausa = PausaOuConfiguracoes;
        bool puzzle = PuzzleAberto();
        bool configurar = ConfigurarItemAberto;
        bool dialogoNoTopo = DialogueManager.IsAnyDialogueActive && DialogueManager.CaixaNoTopo;
        if (puzzle)
        {
            grandePendente = false;
        }

        bool temObjetivo = emExtra || atual >= 0;
        float alvoGrupo = temObjetivo && !fimDeFase && !pausa && !puzzle && !configurar && !dialogoNoTopo ? 1f : 0f;
        if (grupo != null)
        {
            // O painel, o puzzle e a caixa de diálogo no topo aparecem num frame só;
            // com fade a faixa ficaria um instante sobre a aba do painel, o título
            // do puzzle ou a placa "Prof Byte".
            grupo.alpha = configurar || puzzle || dialogoNoTopo ? 0f : Mathf.MoveTowards(grupo.alpha, alvoGrupo, Time.unscaledDeltaTime / Mathf.Max(0.01f, segundosFade));
        }

        // A faixa grande espera o diálogo do Prof acabar.
        bool podeGrande = !DialogueManager.IsAnyDialogueActive && !fimDeFase && !pausa && !configurar;
        if (grandePendente && podeGrande)
        {
            grandeRestante -= Time.unscaledDeltaTime;
            if (grandeRestante <= 0f)
            {
                grandePendente = false;
            }
        }

        // Robô embaixo da faixa grande: ela recolhe na hora (vira a compacta).
        Rect? telaRobo = RetanguloDoRobo();
        if (grandePendente && grande != null && grande.alpha > 0.01f && Sobrepoe(grande.transform as RectTransform, telaRobo))
        {
            grandePendente = false;
        }

        bool mostrarGrande = grandePendente && podeGrande;
        float passo = Time.unscaledDeltaTime / Mathf.Max(0.01f, segundosFade);
        if (grande != null)
        {
            grande.alpha = Mathf.MoveTowards(grande.alpha, mostrarGrande ? 1f : 0f, passo);
        }

        if (compacta != null)
        {
            float alvoCompacta = mostrarGrande ? 0f : (Sobrepoe(compacta.transform as RectTransform, telaRobo) ? alphaComRoboEmbaixo : 1f);
            compacta.alpha = Mathf.MoveTowards(compacta.alpha, alvoCompacta, passo);
        }
    }

    // ---------------------------------------------------------------- robô por baixo

    /// <summary>Retângulo do robô na tela (pixels), com folga; null sem robô ou câmera.</summary>
    private Rect? RetanguloDoRobo()
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        if (robo == null)
        {
            if (Time.unscaledTime < proximaBuscaRobo) return null;
            proximaBuscaRobo = Time.unscaledTime + 1f;
            GameObject go = GameObject.FindWithTag("Player");
            if (go == null) return null;
            robo = go.transform;
            rendRobo = go.GetComponentsInChildren<Renderer>();
        }

        bool achou = false;
        Bounds b = default;
        foreach (Renderer r in rendRobo)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!achou) { b = r.bounds; achou = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!achou) return null;

        Vector3 a = cam.WorldToScreenPoint(b.min);
        Vector3 c = cam.WorldToScreenPoint(b.max);
        float folga = folgaRobo * Screen.height / 1080f;
        return Rect.MinMaxRect(Mathf.Min(a.x, c.x) - folga, Mathf.Min(a.y, c.y) - folga,
                               Mathf.Max(a.x, c.x) + folga, Mathf.Max(a.y, c.y) + folga);
    }

    private bool Sobrepoe(RectTransform rt, Rect? tela)
    {
        if (rt == null || tela == null) return false;
        Canvas canvas = rt.GetComponentInParent<Canvas>();
        Camera camUI = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        rt.GetWorldCorners(cantos);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camUI, cantos[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camUI, cantos[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y).Overlaps(tela.Value);
    }
}
