using UnityEngine;

/// <summary>
/// Player da fase "Animacao" (versão Animator). Quem move o personagem é a
/// física, igual à fase Fisica: Rigidbody2D.linearVelocity pra andar e pular,
/// isGrounded pra saber se está no chão. A animação NÃO move nada: este
/// script só copia a física pros parâmetros do Animator a cada quadro, e o
/// AnimatorController (ver AnimationClipBuilder) decide sozinho qual
/// animação tocar pelas transições.
///
/// Parâmetros do Animator (os nomes batem com o resumo mostrado ao fim do
/// puzzle e com o controller gerado pelo AnimationClipBuilder):
/// - velocidadeX (float) = |linearVelocity.x|
/// - noChao (bool) = isGrounded
/// - regrasMontadas (bool): toda transição exige ele. Fica false até o
///   jogador montar o puzzle do Animator, e é isso que produz a "falha
///   proposital" do começo da fase (anda, mas fica congelado no Idle).
///
/// Sensação de movimento (gravidade assimétrica, coyote time, buffer de
/// pulo) copiada de InteracaoPlayerController, que é o padrão do jogo. A
/// detecção de chão é diferente: usa Rigidbody2D.IsTouching com filtro de
/// ângulo da normal (só contatos "de baixo"), que também funciona ao sair
/// andando de uma beirada (OnCollisionExit2D não traz os pontos de contato).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Animator))]
public class AnimacaoPlayerFisica : MonoBehaviour
{
    // Nomes usados no AnimatorController. O AnimationClipBuilder (Editor) lê
    // estas mesmas constantes, então código e controller nunca divergem.
    public const string ParamVelocidadeX = "velocidadeX";
    public const string ParamNoChao = "noChao";
    public const string ParamRegrasMontadas = "regrasMontadas";
    public const string EstadoIdle = "Idle";
    public const string EstadoCorrida = "Corrida";
    public const string EstadoPulo = "Pulo";
    // Agachar (PlayerAgachar): parâmetro e estados extras, fora do puzzle.
    public const string ParamAgachado = "agachado";
    public const string EstadoAgachado = "Agachado";
    public const string EstadoAndarAgachado = "AndarAgachado";

    /// <summary>Abaixo disso a velocidade conta como "parado" (float nunca é exatamente 0 com segurança).</summary>
    public const float LimiarParado = 0.1f;

    [Header("Movimento")]
    [SerializeField] private float velocidade = 4f;

    [Header("Pulo")]
    [SerializeField] private float forcaPulo = 14f;
    [SerializeField] private float gravidadeBase = 4f;
    [SerializeField] private float multiplicadorQueda = 2.5f;
    [SerializeField] private float multiplicadorPuloCurto = 2f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Tooltip("Depois de um diálogo/puzzle fechar, espera este tempo antes de aceitar pulo: o Enter/Espaço que fechou o diálogo não vira pulo.")]
    [SerializeField] private float atrasoAposBloqueio = 0.15f;

    [Tooltip("Logo depois de pular, ignora o contato com o chão por este tempo (o contato só some no próximo passo de física).")]
    [SerializeField] private float travaChaoAposPulo = 0.1f;

    [Header("Visual")]
    [Tooltip("Vira o sprite pro lado em que está andando (SpriteRenderer.flipX). As animações só trocam o sprite, então não brigam com isso.")]
    [SerializeField] private bool virarSprite = true;

    /// <summary>isGrounded: true com os pés em alguma superfície sólida.</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>Rigidbody2D.linearVelocity atual.</summary>
    public Vector2 Velocidade => rb != null ? rb.linearVelocity : Vector2.zero;

    /// <summary>Desligado pelo AnimacaoLevelFlow enquanto o puzzle está aberto (o diálogo já trava sozinho).</summary>
    public bool MovimentoLiberado { get; set; } = true;

    /// <summary>Quantos pulos o jogador já deu nesta cena (a falha proposital espera pelo menos um).</summary>
    public int PulosDados { get; private set; }

    public bool RegrasMontadas { get; private set; }

    /// <summary>
    /// Falso até o mini-puzzle do túnel: o PlayerAgachar já abaixa o corpo
    /// (collider), mas o Animator não recebe agachado == true, então o desenho
    /// continua em pé. É a falha que o Prof mostra antes do puzzle.
    /// </summary>
    public bool AgacharMontado { get; set; } = true;

    public bool EstaMorto { get; private set; }

    /// <summary>Estado em que o Animator está agora: "Idle", "Corrida", "Pulo", "Agachado" ou "AndarAgachado".</summary>
    public string EstadoAtual
    {
        get
        {
            if (!animatorPronto || !animator.isActiveAndEnabled)
            {
                return EstadoIdle;
            }

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(EstadoCorrida))
            {
                return EstadoCorrida;
            }

            if (info.IsName(EstadoPulo))
            {
                return EstadoPulo;
            }

            if (info.IsName(EstadoAgachado))
            {
                return EstadoAgachado;
            }

            if (info.IsName(EstadoAndarAgachado))
            {
                return EstadoAndarAgachado;
            }

            return EstadoIdle;
        }
    }

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private ContactFilter2D filtroChao;

    private bool animatorPreparado;
    private bool animatorPronto;
    private bool temVelocidadeX;
    private bool temNoChao;
    private bool temRegras;
    private bool temAgachado;
    private int hashAgachado;
    private PlayerAgachar agachar;
    private int hashVelocidadeX;
    private int hashNoChao;
    private int hashRegras;

    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool jumpConsumedSinceGrounded;
    private float ignorarChaoAte;
    private float ultimoBloqueio = -999f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = gravidadeBase;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = false;
        Physics2DTuning.PreventCornerSnag(col);

        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        // Só contatos sólidos vindos de baixo (normal entre 45° e 135°, ou
        // seja, apontando pra cima): chão e topo de degrau contam, parede não.
        filtroChao = new ContactFilter2D();
        filtroChao.useTriggers = false;
        filtroChao.SetNormalAngle(45f, 135f);
    }

    // Confere uma vez quais parâmetros o controller tem — evita um aviso
    // por quadro no Console se o builder ainda não tiver sido rodado. Roda
    // no primeiro Update (e não no Awake) porque o Animator só termina de
    // carregar o controller depois de habilitado.
    private void PrepararAnimator()
    {
        animatorPreparado = true;
        animatorPronto = animator != null && animator.runtimeAnimatorController != null;
        if (!animatorPronto)
        {
            Debug.LogWarning("[AnimacaoPlayerFisica] Sem Animator/Controller — rode Tools > Animação > Montar Cena de Animação.");
            return;
        }

        hashVelocidadeX = Animator.StringToHash(ParamVelocidadeX);
        hashNoChao = Animator.StringToHash(ParamNoChao);
        hashRegras = Animator.StringToHash(ParamRegrasMontadas);
        hashAgachado = Animator.StringToHash(ParamAgachado);
        agachar = GetComponent<PlayerAgachar>();

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == hashVelocidadeX) temVelocidadeX = true;
            else if (p.nameHash == hashNoChao) temNoChao = true;
            else if (p.nameHash == hashRegras) temRegras = true;
            else if (p.nameHash == hashAgachado) temAgachado = true;
        }

        if (!temVelocidadeX || !temNoChao || !temRegras)
        {
            Debug.LogWarning("[AnimacaoPlayerFisica] O AnimatorController não tem os parâmetros velocidadeX/noChao/regrasMontadas — rode Tools > Animação > Montar Clipes de Animação.");
        }
    }

    /// <summary>Liga/desliga as transições do Animator (todas exigem regrasMontadas == true).</summary>
    public void DefinirRegrasMontadas(bool montadas)
    {
        // Só guarda: o Update empurra pro Animator no próximo quadro (pode
        // ser chamado no Start de outro script, antes do Animator estar pronto).
        RegrasMontadas = montadas;
    }

    /// <summary>Para o personagem na hora (morte): sem input, sem velocidade.</summary>
    public void Congelar()
    {
        EstaMorto = true;
        MovimentoLiberado = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void Update()
    {
        bool bloqueado = EstaMorto || !MovimentoLiberado || DialogueManager.IsAnyDialogueActive || JogoBloqueado.Ativo;
        if (bloqueado)
        {
            ultimoBloqueio = Time.time;
        }

        AtualizarChao();
        HandleMovement(bloqueado);
        UpdateJumpTimers(bloqueado);
        HandleJump();
        ApplyJumpGravity();
        AtualizarVisual();
        AtualizarAnimator();
    }

    private void AtualizarChao()
    {
        bool estavaNoChao = IsGrounded;
        bool tocando = rb.IsTouching(filtroChao);
        // Agachar/levantar troca o tamanho do collider e o contato com o chão
        // some por um passo de física: não conta isso como pulo.
        if (!tocando && estavaNoChao && agachar != null && Time.time - agachar.UltimaTroca < 0.15f)
        {
            tocando = true;
        }
        IsGrounded = Time.time >= ignorarChaoAte && tocando;

        // Velocidade do frame anterior: no frame do contato a física já zerou a queda.
        if (IsGrounded && !estavaNoChao)
        {
            AudioManager.TocarAterrissagem(velocidadeYAnterior);
        }
        velocidadeYAnterior = rb.linearVelocity.y;
    }

    private float velocidadeYAnterior;

    private void HandleMovement(bool bloqueado)
    {
        float moveInput = !bloqueado && InputManager.Instance != null
            ? InputManager.Instance.MoveAction.ReadValue<float>()
            : 0f;
        rb.linearVelocity = new Vector2(moveInput * velocidade, rb.linearVelocity.y);
    }

    private void UpdateJumpTimers(bool bloqueado)
    {
        if (IsGrounded)
        {
            coyoteTimer = coyoteTime;
            jumpConsumedSinceGrounded = false;
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.deltaTime);
        }

        if (bloqueado)
        {
            // Pulo apertado antes da janela abrir não fica guardado para depois.
            jumpBufferTimer = 0f;
        }

        bool podeLerPulo = !bloqueado && Time.time - ultimoBloqueio >= atrasoAposBloqueio;
        bool jumpPressedThisFrame = podeLerPulo &&
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
        ignorarChaoAte = Time.time + travaChaoAposPulo;
        IsGrounded = false;
        PulosDados++;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, forcaPulo);
        AudioManager.TocarEfeito(AudioManager.Efeito.Pulo);
    }

    /// <summary>
    /// Lança o personagem para cima até <paramref name="altura"/> unidades
    /// (jumppad). Enquanto sobe, a gravidade de "pulo curto" fica desligada,
    /// então o impulso não depende de segurar o botão de pulo.
    /// </summary>
    public void Impulsionar(float altura)
    {
        if (rb == null || EstaMorto)
        {
            return;
        }

        float g = Mathf.Abs(Physics2D.gravity.y) * gravidadeBase;
        ignorarChaoAte = Time.time + travaChaoAposPulo;
        IsGrounded = false;
        impulsoAtivo = true;
        jumpConsumedSinceGrounded = true;
        rb.gravityScale = gravidadeBase;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Sqrt(2f * g * Mathf.Max(0f, altura)));
    }

    private bool impulsoAtivo;

    private void ApplyJumpGravity()
    {
        if (rb.linearVelocity.y <= 0f)
        {
            impulsoAtivo = false;
        }

        if (rb.linearVelocity.y < 0f)
        {
            rb.gravityScale = gravidadeBase * multiplicadorQueda;
        }
        else if (rb.linearVelocity.y > 0f && !JumpHeld() && !impulsoAtivo)
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
        return InputManager.Instance != null && InputManager.Instance.JumpAction.IsPressed();
    }

    private void AtualizarVisual()
    {
        if (!virarSprite || spriteRenderer == null)
        {
            return;
        }

        float vx = rb.linearVelocity.x;
        if (vx > LimiarParado)
        {
            spriteRenderer.flipX = false;
        }
        else if (vx < -LimiarParado)
        {
            spriteRenderer.flipX = true;
        }
    }

    // A ponte entre física e animação: todo quadro o script copia o que a
    // física está fazendo pros parâmetros; as transições do controller
    // fazem o resto.
    private void AtualizarAnimator()
    {
        if (!animatorPreparado)
        {
            PrepararAnimator();
        }

        if (!animatorPronto || !animator.isActiveAndEnabled)
        {
            return;
        }

        if (temVelocidadeX)
        {
            animator.SetFloat(hashVelocidadeX, Mathf.Abs(rb.linearVelocity.x));
        }

        if (temNoChao)
        {
            animator.SetBool(hashNoChao, IsGrounded);
        }

        if (temRegras)
        {
            animator.SetBool(hashRegras, RegrasMontadas);
        }

        if (temAgachado)
        {
            animator.SetBool(hashAgachado, AgacharMontado && agachar != null && agachar.Agachado);
        }
    }
}
