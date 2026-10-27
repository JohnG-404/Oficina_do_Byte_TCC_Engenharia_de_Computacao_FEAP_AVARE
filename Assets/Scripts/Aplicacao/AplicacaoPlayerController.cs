using System;
using UnityEngine;

/// <summary>
/// Player da fase "Aplicacao": começa sem física, colisão, movimento nem
/// pulo — cada um só é ligado quando o jogador arrasta o ícone
/// correspondente do inventário até ele, com a mesma mecânica usada nos
/// demais objetos da fase (ver DraggableToolIcon e IToolReceiver).
///
/// Física liga a gravidade; Colisão torna o collider sólido. Se a Física for
/// ligada antes da Colisão, o personagem cai livremente sem nada pra
/// pará-lo — ao passar do limite inferior da tela, OnFellPastThreshold
/// avisa quem estiver ouvindo (AplicacaoLevelFlow reinicia a fase).
///
/// O pulo usa gravidade "assimétrica" (mais forte caindo, mais forte ainda
/// se soltar o botão subindo) em vez de gravidade constante — é o que dá o
/// arco seco e "com peso" de plataformas tipo Hollow Knight, em vez da
/// aparência de flutuar sem gravidade que uma gravidade única e fraca dá.
/// Coyote time e buffer de pulo deixam o input tolerante o suficiente pra
/// não parecer "duro"/impreciso.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class AplicacaoPlayerController : MonoBehaviour, IToolReceiver
{
    [Header("Movimento (liberado pela ferramenta Mover)")]
    [SerializeField] private float velocidade = 4f;

    [Header("Pulo (liberado pela ferramenta Pular)")]
    [SerializeField] private float forcaPulo = 14f;
    [SerializeField] private float gravidadeBase = 4f;
    [Tooltip("Multiplica a gravidade enquanto o personagem está caindo — faz a queda ser mais rápida/seca que a subida.")]
    [SerializeField] private float multiplicadorQueda = 2.5f;
    [Tooltip("Multiplica a gravidade se soltar o botão de pulo ainda subindo — permite pulo curto (bate o botão) ou alto (segura o botão).")]
    [SerializeField] private float multiplicadorPuloCurto = 2f;
    [Tooltip("Segundos após sair da borda em que ainda dá pra pular — evita a sensação de 'perdi o pulo por um frame'.")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("Segundos que um pulo apertado um pouco cedo (ainda no ar) fica 'guardado' pra disparar assim que aterrissar.")]
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Física")]
    [Tooltip("Posição Y abaixo da qual o personagem é considerado 'caiu da fase'.")]
    [SerializeField] private float fallThresholdY = -10f;

    public bool HasPhysics { get; private set; }
    public bool HasCollision { get; private set; }
    public bool HasMovement { get; private set; }
    public bool HasJump { get; private set; }
    public bool IsGrounded => chao.NoChao;

    /// <summary>
    /// Disparado uma única vez, quando o personagem cai abaixo do limite da
    /// tela com Física ligada (tipicamente por ter caído sem Colisão sólida
    /// pra pará-lo).
    /// </summary>
    public event Action OnFellPastThreshold;

    private Rigidbody2D rb;
    private Collider2D col;
    private readonly ContatosChao chao = new ContatosChao();
    private bool hasFallen;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool jumpConsumedSinceGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        // Personagem que pula rápido demais pode "atravessar" uma plataforma
        // fina num único FixedUpdate com detecção discreta.
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        col = GetComponent<Collider2D>();
        // Começa como trigger: não bloqueia nada, mas continua detectável por
        // Physics2D.OverlapPoint — é assim que o ícone arrastado sabe que
        // soltou em cima do Player, mesmo antes da colisão sólida existir
        // (mesmo truque usado em PhysicsTarget).
        col.isTrigger = true;

        // Sem atrito + cantos arredondados: evita ficar "preso" na quina de
        // uma plataforma ao pular do lado dela.
        Physics2DTuning.PreventCornerSnag(col);

        // Quais ferramentas já pegaram aparece no painel ao passar o mouse
        // em cima do Player (PainelComponentes).
    }

    private void Update()
    {
        HandleMovement();
        UpdateJumpTimers();
        HandleJump();
        ApplyJumpGravity();
        CheckFallThreshold();
    }

    private void HandleMovement()
    {
        float moveInput = 0f;
        if (HasMovement && !DialogueManager.IsAnyDialogueActive && !JogoBloqueado.Ativo && InputManager.Instance != null)
        {
            moveInput = InputManager.Instance.MoveAction.ReadValue<float>();
        }

        // Move no eixo X via física (preserva a velocidade Y, onde a gravidade/pulo agem).
        rb.linearVelocity = new Vector2(moveInput * velocidade, rb.linearVelocity.y);
    }

    private void UpdateJumpTimers()
    {
        if (IsGrounded)
        {
            coyoteTimer = coyoteTime;
            // Só reseta "já pulei" quando de fato toca o chão de novo — impede
            // que apertar o botão várias vezes no ar (ou qualquer folga de um
            // frame entre sair do chão e o collider registrar a saída) gere
            // pulos extras: um pulo só é permitido por aterrissagem.
            jumpConsumedSinceGrounded = false;
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.deltaTime);
        }

        // Janela aberta (pausa, painel...): o Espaço não chega no robô e o
        // pulo guardado é apagado, senão ele pula sozinho ao fechar.
        if (JogoBloqueado.Ativo)
        {
            jumpBufferTimer = 0f;
            return;
        }

        bool jumpPressedThisFrame = HasJump && !DialogueManager.IsAnyDialogueActive && InputManager.Instance != null &&
            InputManager.Instance.JumpAction.WasPressedThisFrame();

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
        // chega no próximo passo de física, e nesse intervalo IsGrounded
        // ainda lia true — dava pra apertar pular de novo bem rápido e ganhar
        // um pulo extra "de graça" antes do chão ser perdido de verdade.
        chao.Limpar();
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, forcaPulo);
        AudioManager.TocarEfeito(AudioManager.Efeito.Pulo);
    }

    // Gravidade maior caindo (queda seca) e maior ainda subindo se o botão já
    // foi solto (pulo curto) — em vez da gravidade única e fraca que dava a
    // aparência de flutuar sem peso.
    private void ApplyJumpGravity()
    {
        if (!HasPhysics)
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
        return HasJump && InputManager.Instance != null && InputManager.Instance.JumpAction.IsPressed();
    }

    private void CheckFallThreshold()
    {
        if (hasFallen || !HasPhysics)
        {
            return;
        }

        if (transform.position.y < fallThresholdY)
        {
            hasFallen = true;
            OnFellPastThreshold?.Invoke();
        }
    }

    /// <summary>O Player aceita as 4 ferramentas.</summary>
    public bool CanReceive(ToolType tool)
    {
        return true;
    }

    public bool HasTool(ToolType tool)
    {
        switch (tool)
        {
            case ToolType.Fisica: return HasPhysics;
            case ToolType.Colisao: return HasCollision;
            case ToolType.Movimento: return HasMovement;
            case ToolType.Pulo: return HasJump;
            default: return false;
        }
    }

    public void ApplyTool(ToolType tool)
    {
        switch (tool)
        {
            case ToolType.Fisica:
                rb.gravityScale = gravidadeBase;
                HasPhysics = true;
                break;

            case ToolType.Colisao:
                col.isTrigger = false;
                HasCollision = true;
                break;

            case ToolType.Movimento:
                HasMovement = true;
                break;

            case ToolType.Pulo:
                HasJump = true;
                break;
        }
    }

    // Só conta como "chão" um contato cuja normal aponta pra cima — evita que
    // encostar numa parede ou bater a cabeça no teto libere o pulo.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (chao.Entrar(collision))
        {
            AudioManager.TocarAterrissagem(collision.relativeVelocity.y);
        }
    }

    private void OnCollisionStay2D(Collision2D collision) => chao.Permanecer(collision, rb.linearVelocity.y);

    private void OnCollisionExit2D(Collision2D collision) => chao.Sair(collision);
}
