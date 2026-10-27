using UnityEngine;

/// <summary>
/// Plataforma que se move em direção a um destino enquanto o jogador está em
/// cima dela, e volta sozinha pro ponto inicial assim que ele sai. Só começa
/// a se mover depois de configurada pelo assistente Ação+Consequência (ver
/// InteractableItem/ConsequenceType.ActivateMovingPlatform) — antes disso
/// fica parada, igual aos outros itens que esperam configuração.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour
{
    [Tooltip("Deslocamento (em unidades de mundo) a partir da posição inicial, aplicado enquanto o jogador está em cima.")]
    [SerializeField] private Vector2 movementOffset = new Vector2(0f, 3f);
    [SerializeField] private float speed = 3f;
    [SerializeField] private float ridingZoneHeight = 0.3f;

    private Rigidbody2D rb;
    private Vector2 startPosition;
    private Vector2 targetPosition;
    private Rigidbody2D rider;

    public bool IsActivated { get; private set; }

    /// <summary>Chamado pela Consequência "Ativar a plataforma" — antes disso ela ignora contato e fica parada.</summary>
    public void Activate()
    {
        IsActivated = true;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        startPosition = rb.position;
        targetPosition = startPosition + movementOffset;

        EnsureSolidCollider();
        EnsureRidingZone();
    }

    private void EnsureSolidCollider()
    {
        if (GetComponent<Collider2D>() != null)
        {
            return;
        }

        var sr = GetComponent<SpriteRenderer>();
        var box = gameObject.AddComponent<BoxCollider2D>();
        if (sr != null && sr.sprite != null)
        {
            box.size = sr.sprite.bounds.size;
        }
        Physics2DTuning.PreventCornerSnag(box);
    }

    // Zona fina no topo, só pra saber se o jogador está em cima (não conta
    // como colisão sólida separada — o collider principal já bloqueia).
    private void EnsureRidingZone()
    {
        if (transform.Find("RidingZone") != null)
        {
            return;
        }

        var sr = GetComponent<SpriteRenderer>();
        Vector2 size = sr != null && sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;

        var zoneGO = new GameObject("RidingZone");
        zoneGO.transform.SetParent(transform, false);
        zoneGO.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);

        var box = zoneGO.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(size.x * 0.9f, ridingZoneHeight);

        zoneGO.AddComponent<MovingPlatformRidingZone>();
    }

    private void FixedUpdate()
    {
        if (!IsActivated)
        {
            return;
        }

        Vector2 previousPosition = rb.position;
        Vector2 destination = rider != null ? targetPosition : startPosition;
        Vector2 newPosition = Vector2.MoveTowards(previousPosition, destination, speed * Time.fixedDeltaTime);
        rb.MovePosition(newPosition);

        // Move o passageiro pelo mesmo delta, no mesmo passo — depender só da
        // física (corpo cinemático empurrando o dinâmico) deixava uma folga
        // entre os dois a cada frame, o suficiente pra RidingZone entrar e
        // sair repetidamente (plataforma vibrando) e, às vezes, o jogador
        // atravessar o collider sólido por completo.
        //
        // Usa atribuição direta em rb.position (não MovePosition): MovePosition
        // define uma posição-alvo absoluta pro passo de física inteiro, o que
        // sobrescrevia/ignorava o deslocamento horizontal que o próprio
        // InteracaoPlayerController já aplica via velocidade no mesmo frame,
        // travando o jogador só no eixo vertical enquanto andava de carona.
        // Atribuir direto soma o empurrão vertical sem brigar com isso.
        if (rider != null)
        {
            Vector2 delta = newPosition - previousPosition;
            rider.position += delta;
        }
    }

    /// <summary>Chamado pela MovingPlatformRidingZone quando o jogador entra/sai de cima.</summary>
    public void SetRider(Rigidbody2D playerRb)
    {
        rider = playerRb;
    }
}

/// <summary>Repassa contato/saída de um pouso em cima pra MovingPlatform dona.</summary>
public class MovingPlatformRidingZone : MonoBehaviour
{
    private MovingPlatform owner;

    // Resolvido sozinho via GetComponentInParent em vez de depender de um
    // Initialize() externo: como "owner" não é [SerializeField], um valor
    // setado por fora não sobrevive a um reload de domínio/cena — e
    // EnsureRidingZone só chamaria isso de novo se o filho não existisse
    // ainda. Assim funciona sempre, mesmo com o child já salvo na cena.
    private void Awake()
    {
        owner = GetComponentInParent<MovingPlatform>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner == null || other.GetComponent<InteracaoPlayerController>() == null)
        {
            return;
        }

        Rigidbody2D playerRb = other.attachedRigidbody;
        if (playerRb != null)
        {
            owner.SetRider(playerRb);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (owner != null && other.GetComponent<InteracaoPlayerController>() != null)
        {
            owner.SetRider(null);
        }
    }
}
