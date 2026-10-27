using UnityEngine;

/// <summary>
/// Objeto (ou bloco inteiro) do mundo que pode receber Física (Rigidbody2D)
/// e/ou Colisão (Collider2D) quando o jogador arrasta o ícone correspondente
/// do inventário até ele. Começa sem nenhum dos dois — o jogador precisa
/// aplicar manualmente pra resolver a fase.
///
/// Funciona de dois jeitos:
/// - Objeto simples: um Collider2D no próprio GameObject.
/// - Bloco (ex: "Plataforma1" com vários tiles filhos): cada tile tem seu
///   BoxCollider2D em modo Merge, e um CompositeCollider2D no bloco junta
///   todos num collider só (sem emendas entre os tiles). Aplicar a Colisão no
///   bloco liga a colisão de todos os filhos de uma vez; aplicar a Física faz
///   o bloco inteiro cair como uma peça só. Ver Tools > Aplicação > Configurar
///   Blocos do Cenário.
///
/// O collider começa como Trigger (não bloqueia nada, mas continua
/// "detectável" pelo ícone arrastado) e vira sólido quando a Colisão é
/// aplicada.
///
/// Feedback visual: fica em PainelComponentes (painel ao passar o mouse com
/// o que o objeto já tem e o que ainda pode receber). O objeto em si não
/// muda de aparência.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PhysicsTarget : MonoBehaviour, IToolReceiver
{
    [Header("Estado inicial")]
    [Tooltip("Marcado: o bloco já começa sólido (Collider2D ligado), como parte fixa do cenário — o jogador não precisa (nem consegue) aplicar Collider2D nele. Ainda aceita Rigidbody2D.")]
    [SerializeField] private bool comecaComColisao = false;

    [Header("O que este objeto precisa pra ser considerado resolvido")]
    [SerializeField] private bool requiresPhysics = true;
    [SerializeField] private bool requiresCollision = true;

    [Header("Física (quando o Rigidbody2D é aplicado)")]
    [Tooltip("Gravidade do objeto depois de receber o Rigidbody2D (o Player usa 4).")]
    [SerializeField] private float gravityScale = 3f;

    [Header("Queda")]
    [Tooltip("Quando o objeto inteiro fica abaixo deste Y (fora da tela), conta como 'caiu da fase'.")]
    [SerializeField] private float fallThresholdY = -8f;

    public bool HasPhysics { get; private set; }
    public bool HasCollision { get; private set; }
    /// <summary>Se a colisão já veio com o cenário (não foi o jogador que aplicou).</summary>
    public bool ComecaComColisao => comecaComColisao;
    public bool IsSolved => (!requiresPhysics || HasPhysics) && (!requiresCollision || HasCollision);

    /// <summary>Disparado toda vez que uma ferramenta é aplicada aqui (acertando ou repetindo).</summary>
    public event System.Action OnToolApplied;

    /// <summary>Disparado uma vez, quando o objeto cai abaixo do limite da fase (AplicacaoLevelFlow reinicia).</summary>
    public event System.Action<PhysicsTarget> OnFellOutOfLevel;

    private Rigidbody2D rb;
    private Collider2D col;
    private bool hasFallen;

    private void Awake()
    {
        // Num bloco, é o CompositeCollider2D (o único Collider2D do próprio
        // GameObject — os dos tiles estão nos filhos).
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        // Atrito de verdade (não o "SemAtrito" do Player) — este objeto pode
        // virar degrau de outro ou sustentar o Player em cima; sem atrito,
        // o impacto de pousar nele já bastava pra empurrá-lo pro lado.
        Physics2DTuning.PreventCornerSnagWithGrip(col);

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        // Sem isso, um bloco apoiado só parcialmente em cima de outro podia
        // tombar/girar com o peso do Player em vez de ficar deitado.
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        // Bloco caindo rápido pode atravessar uma plataforma fina (0,8) num
        // único passo de física; Continuous evita isso.
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // Sólido desde o início: a colisão já veio com o cenário (o painel
        // de componentes mostra isso como "CENÁRIO").
        if (comecaComColisao)
        {
            col.isTrigger = false;
            HasCollision = true;
        }
    }

    private void Update()
    {
        if (hasFallen || !HasPhysics || col.bounds.max.y >= fallThresholdY)
        {
            return;
        }

        hasFallen = true;
        OnFellOutOfLevel?.Invoke(this);
    }

    /// <summary>Objetos comuns só recebem Rigidbody2D e Collider2D — Movimento e Pulo são do Player.</summary>
    public bool CanReceive(ToolType tool)
    {
        return tool == ToolType.Fisica || tool == ToolType.Colisao;
    }

    public bool HasTool(ToolType tool)
    {
        return (tool == ToolType.Fisica && HasPhysics) || (tool == ToolType.Colisao && HasCollision);
    }

    /// <summary>Chamado por DraggableToolIcon quando o jogador solta uma ferramenta aqui.</summary>
    public void ApplyTool(ToolType tool)
    {
        if (tool == ToolType.Colisao)
        {
            col.isTrigger = false;
            HasCollision = true;
        }
        else if (tool == ToolType.Fisica)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = gravityScale;
            HasPhysics = true;
        }

        OnToolApplied?.Invoke();
    }
}
