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

        bool mostrarGrande = grandePendente && podeGrande;
        float passo = Time.unscaledDeltaTime / Mathf.Max(0.01f, segundosFade);
        if (grande != null)
        {
            grande.alpha = Mathf.MoveTowards(grande.alpha, mostrarGrande ? 1f : 0f, passo);
        }

        if (compacta != null)
        {
            compacta.alpha = Mathf.MoveTowards(compacta.alpha, mostrarGrande ? 0f : 1f, passo);
        }
    }
}
