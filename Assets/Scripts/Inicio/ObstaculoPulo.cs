using UnityEngine;

/// <summary>
/// Pilha de caixas no chão da cena "Fisica" que o personagem só passa
/// pulando. Ela dá um motivo pro puzzle do Pulo: depois do Collider2D o
/// jogador anda pela sala, esbarra nas caixas e não consegue seguir.
///
/// Antes do puzzle de Colisão o personagem não tem Collider2D e atravessa as
/// caixas como atravessa o chão, então o obstáculo só "existe" pra ele a
/// partir daí. O GameFlowManager lê <see cref="JogadorEmpacou"/> (abre o
/// diálogo do pulo) e <see cref="JogadorPassou"/> (fecha a fase).
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class ObstaculoPulo : MonoBehaviour
{
    [SerializeField] private Player player;

    [Tooltip("Segundos empurrando a lateral das caixas para contar como 'empacou'. Um esbarrão rápido não conta.")]
    [SerializeField] private float segundosEmpurrando = 0.5f;

    [Tooltip("Quanto o personagem precisa passar da borda direita das caixas (e estar no chão) para contar como 'passou'.")]
    [SerializeField] private float folgaDepois = 0.3f;

    /// <summary>True a partir do momento em que o jogador empurrou a lateral das caixas por <c>segundosEmpurrando</c>.</summary>
    public bool JogadorEmpacou { get; private set; }

    /// <summary>True enquanto o personagem está no chão, do outro lado das caixas.</summary>
    public bool JogadorPassou =>
        playerCol != null && player.IsGrounded &&
        playerCol.bounds.min.x > col.bounds.max.x + folgaDepois;

    private BoxCollider2D col;
    private Collider2D playerCol;
    private float tempoEmpurrando;

    private void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
        }
        if (player != null)
        {
            playerCol = player.GetComponent<Collider2D>();
        }
    }

    private void Update()
    {
        if (JogadorEmpacou || playerCol == null)
        {
            return;
        }

        tempoEmpurrando = EstaEmpurrando() ? tempoEmpurrando + Time.deltaTime : 0f;
        if (tempoEmpurrando >= segundosEmpurrando)
        {
            JogadorEmpacou = true;
        }
    }

    // Encostado na lateral (não em cima) e andando contra as caixas. Sem
    // Collider2D ligado no personagem o Distance não é válido, então antes do
    // puzzle de Colisão isso nunca acontece.
    private bool EstaEmpurrando()
    {
        if (!playerCol.enabled || !player.IsMovingHorizontally)
        {
            return false;
        }

        ColliderDistance2D d = col.Distance(playerCol);
        if (!d.isValid || d.distance > 0.05f || Mathf.Abs(d.normal.x) < 0.7f)
        {
            return false;
        }

        // A tecla aponta pras caixas (personagem à esquerda apertando pra direita, ou o contrário).
        float ladoDoPersonagem = Mathf.Sign(player.transform.position.x - transform.position.x);
        float tecla = InputManager.Instance != null ? InputManager.Instance.MoveAction.ReadValue<float>() : 0f;
        return tecla * ladoDoPersonagem < -0.01f;
    }
}
