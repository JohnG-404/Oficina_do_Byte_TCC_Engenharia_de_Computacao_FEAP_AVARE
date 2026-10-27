using UnityEngine;

/// <summary>
/// Moeda da fase "Animacao" (no chão do túnel de agachar). O Animator gira a
/// moeda num estado com Loop ligado; ao ser pega, o Trigger "coletada" leva
/// ao estado Coletada, que toca uma vez e some.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MoedaFase : MonoBehaviour
{
    public const string ParamColetada = "coletada";

    [SerializeField] private Animator animator;
    [Tooltip("Tempo do clipe Coletada antes de a moeda sumir.")]
    [SerializeField] private float tempoSumir = 0.35f;
    [TextArea(2, 4)]
    [SerializeField] private string[] falas =
    {
        "Essa é a moeda que você animou na Oficina. O clipe Girando tem Loop Time ligado, por isso ela gira sem parar.",
        "Quando você pega, um Trigger chamado coletada leva para o estado Coletada, que toca uma vez só.",
    };

    private bool coletada;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => Tentar(other);

    // Se o personagem já estava em cima quando o Animator foi configurado.
    private void OnTriggerStay2D(Collider2D other) => Tentar(other);

    private void Tentar(Collider2D other)
    {
        // Sem o Animator configurado (ObjetoAnimadoInteracao), a moeda não pode ser pega.
        if (coletada || !ObjetoAnimadoInteracao.EstaLiberado(this) || other.GetComponentInParent<AnimacaoPlayerFisica>() == null)
        {
            return;
        }

        coletada = true;
        GetComponent<Collider2D>().enabled = false;
        if (animator != null) animator.SetTrigger(ParamColetada);
        AudioManager.TocarEfeito(AudioManager.Efeito.Coletar);
        RegistroAprendizagem.Registrar("moeda", name);
        Invoke(nameof(Sumir), tempoSumir);
        InvokeRepeating(nameof(Falar), tempoSumir, 0.5f);
    }

    // Enviado por ObjetoAnimadoInteracao quando o Animator da moeda é configurado:
    // se o personagem já está parado em cima, pega agora (parado, o corpo dorme
    // e o OnTriggerStay2D não chega).
    private void AoAnimatorConfigurado()
    {
        var encostados = new System.Collections.Generic.List<Collider2D>();
        GetComponent<Collider2D>().Overlap(ContactFilter2D.noFilter, encostados);
        foreach (Collider2D c in encostados) Tentar(c);
    }

    private void Sumir()
    {
        if (animator != null) animator.gameObject.SetActive(false);
    }

    // Fala assim que a fase estiver livre (o Prof não interrompe o túnel).
    private void Falar()
    {
        if (AnimacaoLevelFlow.Instance == null || AnimacaoLevelFlow.Instance.FalarSobreObjeto("moeda", falas))
        {
            CancelInvoke(nameof(Falar));
        }
    }
}
