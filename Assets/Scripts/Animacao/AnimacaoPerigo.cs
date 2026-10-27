using UnityEngine;

/// <summary>
/// Perigo da fase "Animacao" (ex: espinhos): encostou, morreu. Tira a vida
/// inteira do PlayerHealth, e quem ouve PlayerHealth.OnDeath (o
/// AnimacaoLevelFlow) mostra a dica e recarrega a fase.
///
/// Fica num filho gerado pelo AnimacaoSceneBuilder ("Perigo (gerado)"), com
/// um BoxCollider2D trigger um pouco menor que o desenho dos espinhos: raspar
/// a ponta no fim de um pulo não conta.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AnimacaoPerigo : MonoBehaviour
{
    [Tooltip("Nome gravado no RegistroAprendizagem quando o jogador morre aqui.")]
    [SerializeField] private string nomeDoPerigo = "espinho";

    public string NomeDoPerigo => nomeDoPerigo;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => Atingir(other);

    private void OnTriggerStay2D(Collider2D other) => Atingir(other);

    private void Atingir(Collider2D other)
    {
        AnimacaoPlayerFisica player = other.GetComponentInParent<AnimacaoPlayerFisica>();
        if (player == null || player.EstaMorto)
        {
            return;
        }

        player.Congelar();

        if (AnimacaoLevelFlow.Instance != null)
        {
            AnimacaoLevelFlow.Instance.UltimoPerigo = nomeDoPerigo;
        }

        PlayerHealth vida = player.GetComponent<PlayerHealth>();
        if (vida != null)
        {
            vida.TakeDamage(vida.MaxHealth);
        }
        else if (AnimacaoLevelFlow.Instance != null)
        {
            AnimacaoLevelFlow.Instance.RestartLevel();
        }
    }
}
