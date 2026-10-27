using UnityEngine;

/// <summary>
/// Fim do caminho da fase "Animacao" (o cartão de acesso flutuando). Quando
/// o personagem encosta, avisa o AnimacaoLevelFlow, que só conclui a fase
/// depois do puzzle do Animator (ver AnimacaoLevelFlow.PodeConcluir). Usa
/// também OnTriggerStay2D: se o jogador já estiver parado em cima dele
/// quando o puzzle terminar, conclui sem precisar sair e entrar de novo.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AnimacaoObjetivo : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => Tentar(other);

    private void OnTriggerStay2D(Collider2D other) => Tentar(other);

    private static void Tentar(Collider2D other)
    {
        AnimacaoLevelFlow flow = AnimacaoLevelFlow.Instance;
        if (flow == null || !flow.PodeConcluir)
        {
            return;
        }

        AnimacaoPlayerFisica player = other.GetComponentInParent<AnimacaoPlayerFisica>();
        if (player != null && !player.EstaMorto)
        {
            flow.CompleteLevel();
        }
    }
}
