using UnityEngine;

/// <summary>
/// Trecho do terreno da cena "Fisica" que o personagem só passa pulando (um
/// degrau no chão). Ele dá um motivo pro puzzle do Pulo: depois do
/// Collider2D o jogador anda pela sala, esbarra no degrau e não consegue
/// seguir.
///
/// Os blocos do degrau continuam onde estão (no "Chao"); este componente só
/// aponta pros colliders deles em <c>partes</c>. Sem nada ali, usa os
/// colliders do próprio objeto e dos filhos.
///
/// Antes do puzzle de Colisão o personagem não tem Collider2D e atravessa o
/// degrau como atravessa o chão, então o obstáculo só "existe" pra ele a
/// partir daí. O GameFlowManager lê <see cref="JogadorEmpacou"/> (abre o
/// diálogo do pulo) e <see cref="JogadorPassou"/> (fecha a fase).
/// </summary>
public class ObstaculoPulo : MonoBehaviour
{
    [SerializeField] private Player player;

    [Tooltip("Colliders dos blocos que formam o obstáculo. Vazio = os colliders deste objeto e dos filhos.")]
    [SerializeField] private Collider2D[] partes = new Collider2D[0];

    [Tooltip("Segundos empurrando a lateral do obstáculo para contar como 'empacou'. Um esbarrão rápido não conta.")]
    [SerializeField] private float segundosEmpurrando = 0.5f;

    [Tooltip("Quanto o personagem precisa passar da borda direita do obstáculo (e estar no chão) para contar como 'passou'.")]
    [SerializeField] private float folgaDepois = 0.3f;

    /// <summary>True a partir do momento em que o jogador empurrou a lateral do obstáculo por <c>segundosEmpurrando</c>.</summary>
    public bool JogadorEmpacou { get; private set; }

    /// <summary>True enquanto o personagem está no chão, do outro lado do obstáculo.</summary>
    public bool JogadorPassou =>
        playerCol != null && player.IsGrounded &&
        playerCol.bounds.min.x > BordaDireita() + folgaDepois;

    private Collider2D playerCol;
    private float tempoEmpurrando;

    private void Awake()
    {
        if (partes == null || partes.Length == 0)
        {
            partes = GetComponentsInChildren<Collider2D>();
        }
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

    private float BordaDireita()
    {
        float x = float.MinValue;
        foreach (Collider2D parte in partes)
        {
            if (parte != null)
            {
                x = Mathf.Max(x, parte.bounds.max.x);
            }
        }
        return x;
    }

    // Encostado na lateral de algum bloco (não em cima) e andando contra ele.
    // Sem Collider2D ligado no personagem o Distance não é válido, então antes
    // do puzzle de Colisão isso nunca acontece.
    private bool EstaEmpurrando()
    {
        if (!playerCol.enabled || !player.IsMovingHorizontally)
        {
            return false;
        }

        float tecla = InputManager.Instance != null ? InputManager.Instance.MoveAction.ReadValue<float>() : 0f;
        foreach (Collider2D parte in partes)
        {
            if (parte == null)
            {
                continue;
            }

            ColliderDistance2D d = parte.Distance(playerCol);
            if (!d.isValid || d.distance > 0.05f || Mathf.Abs(d.normal.x) < 0.7f)
            {
                continue;
            }

            // A tecla aponta pro bloco (personagem à esquerda apertando pra direita, ou o contrário).
            float ladoDoPersonagem = Mathf.Sign(player.transform.position.x - parte.bounds.center.x);
            if (tecla * ladoDoPersonagem < -0.01f)
            {
                return true;
            }
        }
        return false;
    }
}
