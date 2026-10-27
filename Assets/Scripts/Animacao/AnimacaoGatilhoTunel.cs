using UnityEngine;

/// <summary>
/// Entrada do túnel baixo da fase "Animacao" (trecho C). Quando o personagem
/// entra nesta área, avisa o AnimacaoLevelFlow, que para o jogador e
/// apresenta o agachar (ver AnimacaoLevelFlow.ChegouNoTunel). Usa também
/// OnTriggerStay2D: se o jogador já estiver aqui quando um diálogo ou
/// puzzle terminar, a apresentação começa sem precisar sair e entrar de novo.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AnimacaoGatilhoTunel : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => Avisar(other);

    private void OnTriggerStay2D(Collider2D other) => Avisar(other);

    private static void Avisar(Collider2D other)
    {
        if (AnimacaoLevelFlow.Instance == null || other.GetComponentInParent<AnimacaoPlayerFisica>() == null)
        {
            return;
        }

        AnimacaoLevelFlow.Instance.ChegouNoTunel();
    }
}
