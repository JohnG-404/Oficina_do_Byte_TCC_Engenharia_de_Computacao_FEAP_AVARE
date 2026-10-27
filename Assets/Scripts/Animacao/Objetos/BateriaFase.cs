using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bateria da fase "Animacao" (na parede, ao lado da porta de saída). O
/// Animator tem um parâmetro Int "carga": cada alavanca puxada soma 1, e
/// cada estado (Vazia, Metade, Cheia) tem uma seta com carga == valor. A
/// PortaSaida desta fase só abre com a bateria cheia.
/// </summary>
public class BateriaFase : MonoBehaviour
{
    public const string ParamCarga = "carga";

    [SerializeField] private Animator animator;
    [Tooltip("Quantas alavancas enchem a bateria.")]
    [SerializeField] private int cargaMaxima = 2;
    [Tooltip("Porta de saída, usada para o aviso de porta trancada.")]
    [SerializeField] private Transform porta;
    [SerializeField] private float distanciaAviso = 3f;

    [TextArea(2, 4)]
    [SerializeField] private string[] falasPrimeiraCarga =
    {
        "A bateria ao lado da porta tem um parâmetro Int chamado carga. Cada alavanca soma 1.",
        "No Animator, cada seta testa um valor: carga == 1 mostra meia carga, carga == 2 mostra a bateria cheia.",
    };
    [TextArea(2, 4)]
    [SerializeField] private string[] falasCheia =
    {
        "Bateria cheia: carga == 2, e a porta de saída foi destrancada.",
        "A porta vai de Fechada para Abrindo e passa para Aberta quando o clipe termina, sem condição nenhuma. Isso é Has Exit Time.",
    };
    [TextArea(2, 4)]
    [SerializeField] private string[] falasTrancada =
    {
        "A porta está trancada. Ela só abre com a bateria cheia: puxe as duas alavancas da fase.",
        "Uma fica em cima da laje (use o jumppad) e a outra no fundo do poço.",
    };

    public int Carga { get; private set; }
    /// <summary>As alavancas já deram toda a carga.</summary>
    public bool CargaCompleta => Carga >= cargaMaxima;
    /// <summary>Carga completa e Animator da bateria configurado: só assim a porta abre.</summary>
    public bool Cheia => CargaCompleta && ObjetoAnimadoInteracao.EstaLiberado(this);

    private readonly Queue<(string chave, string[] falas)> pendentes = new Queue<(string, string[])>();
    private Transform jogador;
    private bool avisouCheia;

    private void Start()
    {
        if (animator != null && animator.isActiveAndEnabled) animator.SetInteger(ParamCarga, 0);
        var p = FindAnyObjectByType<AnimacaoPlayerFisica>();
        if (p != null) jogador = p.transform;
    }

    /// <summary>Soma 1 na carga. <paramref name="alavanca"/>, se vier, junta as falas dela às da bateria.</summary>
    public void Carregar(AlavancaFase alavanca)
    {
        if (CargaCompleta)
        {
            return;
        }

        Carga++;
        if (animator != null && animator.isActiveAndEnabled) animator.SetInteger(ParamCarga, Carga);

        if (alavanca != null) pendentes.Enqueue(("alavanca", Juntar(alavanca.Falas, Carga == 1 && !CargaCompleta ? falasPrimeiraCarga : null)));
        else if (Carga == 1 && !CargaCompleta) pendentes.Enqueue(("bateria", falasPrimeiraCarga));
    }

    // Enviado por ObjetoAnimadoInteracao quando o Animator da bateria é configurado.
    private void AoAnimatorConfigurado()
    {
        if (animator != null && animator.isActiveAndEnabled) animator.SetInteger(ParamCarga, Carga);
    }

    private static string[] Juntar(string[] a, string[] b)
    {
        if (b == null) return a;
        var l = new List<string>(a);
        l.AddRange(b);
        return l.ToArray();
    }

    private void Update()
    {
        AnimacaoLevelFlow flow = AnimacaoLevelFlow.Instance;
        if (flow == null)
        {
            return;
        }

        if (pendentes.Count > 0)
        {
            var f = pendentes.Peek();
            if (flow.FalarSobreObjeto(f.chave, f.falas)) pendentes.Dequeue();
            return;
        }

        if (Cheia && !avisouCheia)
        {
            avisouCheia = true;
            pendentes.Enqueue(("bateria_cheia", falasCheia));
            return;
        }

        if (!Cheia && porta != null && jogador != null
            && Mathf.Abs(jogador.position.x - porta.position.x) < distanciaAviso
            && Mathf.Abs(jogador.position.y - porta.position.y) < 2f)
        {
            flow.FalarSobreObjeto("porta_trancada", falasTrancada);
        }
    }
}
