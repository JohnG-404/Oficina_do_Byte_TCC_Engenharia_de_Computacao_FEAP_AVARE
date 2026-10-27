using UnityEngine;

/// <summary>
/// Alavanca da fase "Animacao". Com o personagem perto, a tecla de interagir
/// dispara o Trigger "puxar": Parada → Puxando, e Puxando → Puxada por Has
/// Exit Time. Cada alavanca puxada soma 1 na carga da bateria.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AlavancaFase : MonoBehaviour
{
    public const string ParamPuxar = "puxar";

    [SerializeField] private Animator animator;
    [SerializeField] private BateriaFase bateria;
    [Tooltip("Dica da tecla de interagir (DicaTecla, zona PeloObjeto): o balão aparece com o personagem perto e some depois de puxar.")]
    [SerializeField] private GameObject dica;
    [TextArea(2, 4)]
    [SerializeField] private string[] falas =
    {
        "A alavanca também usa um Trigger, chamado puxar.",
        "Diferente do Bool da lâmpada, o Trigger não fica ligado: dispara a transição uma vez e volta a false sozinho.",
    };

    public bool Puxada { get; private set; }

    private AnimacaoPlayerFisica perto;
    private DicaTecla dicaTecla;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        AnimacaoPlayerFisica p = other.GetComponentInParent<AnimacaoPlayerFisica>();
        if (p != null) perto = p;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (perto != null && other.GetComponentInParent<AnimacaoPlayerFisica>() == perto) perto = null;
    }

    private void Update()
    {
        // Balão da tecla de interagir enquanto dá para puxar.
        if (dicaTecla == null && dica != null) dicaTecla = dica.GetComponent<DicaTecla>();
        // Sem o Animator configurado (ObjetoAnimadoInteracao), a alavanca não puxa.
        bool liberada = ObjetoAnimadoInteracao.EstaLiberado(this);
        if (dicaTecla != null) dicaTecla.Mostrar(liberada && !Puxada && perto != null && !perto.EstaMorto);

        if (!liberada || Puxada || perto == null || perto.EstaMorto || DialogueManager.IsAnyDialogueActive || Time.timeScale <= 0f || JogoBloqueado.Ativo)
        {
            return;
        }

        InputManager input = InputManager.Instance;
        if (input == null || input.InteractAction == null || !input.InteractAction.WasPressedThisFrame())
        {
            return;
        }

        Puxar();
    }

    /// <summary>Puxa a alavanca (também usado pelos testes automáticos).</summary>
    public void Puxar()
    {
        if (Puxada || !ObjetoAnimadoInteracao.EstaLiberado(this))
        {
            return;
        }

        Puxada = true;
        if (animator != null) animator.SetTrigger(ParamPuxar);
        if (dica != null) dica.SetActive(false);
        AudioManager.TocarEfeito(AudioManager.Efeito.Alavanca);
        RegistroAprendizagem.Registrar("alavanca", name);

        bool falou = AnimacaoLevelFlow.Instance != null && AnimacaoLevelFlow.Instance.JaFalouSobre("alavanca");
        if (bateria != null) bateria.Carregar(falou ? null : this);
        else if (AnimacaoLevelFlow.Instance != null) AnimacaoLevelFlow.Instance.FalarSobreObjeto("alavanca", falas);
    }

    /// <summary>Falas da alavanca (a bateria junta com as dela na primeira vez).</summary>
    public string[] Falas => falas;
}
