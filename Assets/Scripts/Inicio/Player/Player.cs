using System;
using UnityEngine;

/// <summary>
/// Player da cena "Fisica": começa parado, flutuando e sem colisão, e cada
/// mecânica só é ligada quando o puzzle correspondente é resolvido (Movimento,
/// Rigidbody2D, Collider2D, Pulo).
///
/// O pulo usa a mesma sensação das cenas Interacao/Aplicacao (ver
/// InteracaoPlayerController): gravidade "assimétrica" (queda mais forte que a
/// subida, pulo curto ao soltar o botão cedo) + coyote time + buffer de pulo,
/// em vez de uma gravidade única e fraca que dá a aparência de flutuar.
/// </summary>
public class Player : MonoBehaviour
{
    [Header("Movimento")]
    public float velocidade = 4f;

    [Header("Física (ativada quando o puzzle de Física é resolvido)")]
    [Tooltip("Posição Y abaixo da qual o personagem é considerado 'caiu da tela'.")]
    [SerializeField] private float fallThresholdY = -10f;

    [Header("Pulo (liberado quando o puzzle de Pulo é resolvido) — mesma sensação da cena Interacao")]
    [SerializeField] private float forcaPulo = 14f;
    [SerializeField] private float gravidadeBase = 4f;
    [Tooltip("Multiplica a gravidade enquanto o personagem está caindo — faz a queda ser mais rápida/seca que a subida.")]
    [SerializeField] private float multiplicadorQueda = 2.5f;
    [Tooltip("Multiplica a gravidade se soltar o botão de pulo ainda subindo — permite pulo curto (bate o botão) ou alto (segura o botão).")]
    [SerializeField] private float multiplicadorPuloCurto = 2f;
    [Tooltip("Segundos após sair da borda em que ainda dá pra pular.")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("Segundos que um pulo apertado um pouco cedo (ainda no ar) fica 'guardado' pra disparar assim que aterrissar.")]
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Limites da sala")]
    [Tooltip("Trava o personagem entre estes X (mínimo, máximo) — as paredes da sala. Útil antes do puzzle de Colisão, quando ele ainda atravessaria tudo.")]
    [SerializeField] private bool usarLimitesX = false;
    [SerializeField] private Vector2 limitesX = new Vector2(-8.4f, 26.6f);

    [HideInInspector]
    public bool podeMover = false;

    /// <summary>Disparado uma única vez, na primeira vez que o jogador realmente se move.</summary>
    public event Action OnFirstMove;

    /// <summary>Disparado uma única vez, no primeiro pulo de verdade do jogador.</summary>
    public event Action OnFirstJump;

    /// <summary>True enquanto o jogador está de fato andando pros lados (usado pra medir "tempo testando o movimento").</summary>
    public bool IsMovingHorizontally => podeMover && !DialogueManager.IsAnyDialogueActive && Mathf.Abs(movimento.x) > 0.01f;

    public bool PodePular { get; private set; }
    public bool IsGrounded => chao.NoChao;

    private Vector2 movimento;
    private bool hasMovedOnce;
    private bool hasJumpedOnce;
    private bool hasPhysics;

    private Rigidbody2D rb;
    private Collider2D col;
    private Vector3 startPosition;

    private readonly ContatosChao chao = new ContatosChao();
    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool jumpConsumedSinceGrounded;

    void Awake()
    {
        startPosition = transform.position;

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        // Começa sempre "flutuando": sem gravidade, até o puzzle de Física ser resolvido.
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        // Personagem que pula rápido demais pode "atravessar" uma plataforma
        // fina num único FixedUpdate com detecção discreta.
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        col = GetComponent<Collider2D>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider2D>();
        }

        // Sem atrito + cantos arredondados: evita "travar" nas emendas entre
        // os blocos do chão ou na quina de uma parede (mesmo ajuste das demais cenas).
        Physics2DTuning.PreventCornerSnag(col);

        // Começa sempre sem colisão, até o puzzle de Colisão ser resolvido.
        col.enabled = false;
    }

    void Update()
    {
        if (!podeMover || DialogueManager.IsAnyDialogueActive || JogoBloqueado.Ativo)
        {
            movimento.x = 0;
        }
        else
        {
            movimento.x = InputManager.Instance.MoveAction.ReadValue<float>();

            if (!hasMovedOnce && Mathf.Abs(movimento.x) > 0.01f)
            {
                hasMovedOnce = true;
                OnFirstMove?.Invoke();
            }
        }

        // Move no eixo X via física (preserva a velocidade Y, que é onde a gravidade age).
        rb.linearVelocity = new Vector2(movimento.x * velocidade, rb.linearVelocity.y);
        ClampToRoom();

        UpdateJumpTimers();
        HandleJump();
        ApplyJumpGravity();
    }

    private void ClampToRoom()
    {
        if (!usarLimitesX)
        {
            return;
        }

        Vector2 p = rb.position;
        float x = Mathf.Clamp(p.x, limitesX.x, limitesX.y);
        if (!Mathf.Approximately(x, p.x))
        {
            rb.position = new Vector2(x, p.y);
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    private void UpdateJumpTimers()
    {
        if (IsGrounded)
        {
            coyoteTimer = coyoteTime;
            // Só "recarrega" o pulo ao tocar o chão de novo: um pulo por aterrissagem, sem pulo duplo.
            jumpConsumedSinceGrounded = false;
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.deltaTime);
        }

        // Janela aberta (pausa, puzzle, painel...): o Espaço não chega no
        // robô e o pulo guardado é apagado, senão ele pula sozinho ao fechar.
        if (JogoBloqueado.Ativo)
        {
            jumpBufferTimer = 0f;
            return;
        }

        bool jumpPressedThisFrame = PodePular && !DialogueManager.IsAnyDialogueActive &&
            InputManager.Instance != null && InputManager.Instance.JumpAction.WasPressedThisFrame();

        jumpBufferTimer = jumpPressedThisFrame ? jumpBufferTime : Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
    }

    private void HandleJump()
    {
        if (jumpConsumedSinceGrounded || jumpBufferTimer <= 0f || coyoteTimer <= 0f)
        {
            return;
        }

        jumpConsumedSinceGrounded = true;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        // Zera na hora, não espera o OnCollisionExit2D: o evento de saída só
        // chega no próximo passo de física, e nesse intervalo IsGrounded ainda
        // leria true — dava pra ganhar um pulo extra "de graça".
        chao.Limpar();
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, forcaPulo);
        AudioManager.TocarEfeito(AudioManager.Efeito.Pulo);

        if (!hasJumpedOnce)
        {
            hasJumpedOnce = true;
            OnFirstJump?.Invoke();
        }
    }

    // Gravidade maior caindo (queda seca) e maior ainda subindo se o botão já
    // foi solto (pulo curto). Só depois que o Rigidbody2D (gravidade) foi ligado.
    private void ApplyJumpGravity()
    {
        if (!hasPhysics)
        {
            return;
        }

        if (rb.linearVelocity.y < 0f)
        {
            rb.gravityScale = gravidadeBase * multiplicadorQueda;
        }
        else if (rb.linearVelocity.y > 0f && !JumpHeld())
        {
            rb.gravityScale = gravidadeBase * multiplicadorPuloCurto;
        }
        else
        {
            rb.gravityScale = gravidadeBase;
        }
    }

    private bool JumpHeld()
    {
        return PodePular && InputManager.Instance != null && InputManager.Instance.JumpAction.IsPressed();
    }

    // Só conta como "chão" um contato cuja normal aponta pra cima — encostar
    // numa parede ou bater a cabeça não libera o pulo.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (chao.Entrar(collision))
        {
            AudioManager.TocarAterrissagem(collision.relativeVelocity.y);
        }
    }

    private void OnCollisionStay2D(Collision2D collision) => chao.Permanecer(collision, rb.linearVelocity.y);

    private void OnCollisionExit2D(Collision2D collision) => chao.Sair(collision);

    public void LiberarMovimento()
    {
        podeMover = true;
    }

    public void BloquearMovimento()
    {
        podeMover = false;
    }

    /// <summary>Liga a gravidade (Rigidbody2D) — chamado quando o puzzle de Física é resolvido.</summary>
    public void AdicionarFisica()
    {
        hasPhysics = true;
        rb.gravityScale = gravidadeBase;
    }

    /// <summary>
    /// Devolve o personagem ao ponto de partida, parado — usado depois que ele
    /// caiu pra fora da tela (sem colisão) e o puzzle de Colisão é resolvido:
    /// sem isso ele continuaria caindo lá embaixo, longe do chão.
    /// </summary>
    public void VoltarAoInicio()
    {
        transform.position = startPosition;
        rb.position = startPosition;
        rb.linearVelocity = Vector2.zero;
        chao.Limpar();
    }

    /// <summary>Liga a colisão (Collider2D) — chamado quando o puzzle de Colisão é resolvido.</summary>
    public void AdicionarColisao()
    {
        if (col != null)
        {
            col.enabled = true;
        }
    }

    /// <summary>Libera o pulo — chamado quando o puzzle de Pulo é resolvido.</summary>
    public void LiberarPulo()
    {
        PodePular = true;
    }

    /// <summary>True quando o personagem já caiu abaixo do limite da tela.</summary>
    public bool HasFallenPastThreshold()
    {
        return transform.position.y < fallThresholdY;
    }
}
