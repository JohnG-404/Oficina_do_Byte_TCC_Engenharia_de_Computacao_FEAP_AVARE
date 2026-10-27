using UnityEngine;

/// <summary>
/// Jumppad da fase "Animacao" (chão da sala do pulo, antes da laje). Quando
/// o personagem cai em cima, o Trigger "pular" toca a animação Impulso uma
/// vez; a volta para Repouso usa Has Exit Time (espera o clipe acabar). O
/// impulso leva o personagem até a laje, alta demais para o pulo normal.
/// </summary>
public class JumppadFase : MonoBehaviour
{
    public const string ParamPular = "pular";

    [SerializeField] private Animator animator;
    [Tooltip("Altura (unidades) que o personagem sobe a partir do topo do jumppad.")]
    [SerializeField] private float altura = 4f;
    [TextArea(2, 4)]
    [SerializeField] private string[] falas =
    {
        "O jumppad usa um Trigger chamado pular: ele dispara a animação uma vez e volta a false sozinho.",
        "Cuidado com o nome: esse Trigger é um tipo de parâmetro do Animator. Não tem nada a ver com o Is Trigger do collider.",
        "A seta de volta para Repouso não tem condição. Ela usa Has Exit Time: espera o clipe terminar e volta.",
    };

    private float proximoImpulso;

    private void OnCollisionEnter2D(Collision2D c) => Tentar(c);

    private void OnCollisionStay2D(Collision2D c) => Tentar(c);

    private void Tentar(Collision2D c)
    {
        AnimacaoPlayerFisica player = c.collider.GetComponentInParent<AnimacaoPlayerFisica>();
        // Sem o Animator configurado (ObjetoAnimadoInteracao), o jumppad não impulsiona.
        if (player == null || player.EstaMorto || Time.time < proximoImpulso || !ObjetoAnimadoInteracao.EstaLiberado(this))
        {
            return;
        }

        // Só quando o personagem está em cima (o contato aponta para baixo nele).
        bool emCima = false;
        for (int i = 0; i < c.contactCount; i++)
        {
            if (c.GetContact(i).normal.y < -0.5f)
            {
                emCima = true;
                break;
            }
        }

        if (!emCima || player.Velocidade.y > 0.5f)
        {
            return;
        }

        proximoImpulso = Time.time + 0.4f;
        if (animator != null) animator.SetTrigger(ParamPular);
        AudioManager.TocarEfeito(AudioManager.Efeito.JumpPad);
        player.Impulsionar(altura);

        if (AnimacaoLevelFlow.Instance != null)
        {
            Invoke(nameof(Falar), 1.2f);
        }
    }

    private void Falar()
    {
        if (AnimacaoLevelFlow.Instance != null && !AnimacaoLevelFlow.Instance.FalarSobreObjeto("jumppad", falas))
        {
            Invoke(nameof(Falar), 0.5f);
        }
    }
}
