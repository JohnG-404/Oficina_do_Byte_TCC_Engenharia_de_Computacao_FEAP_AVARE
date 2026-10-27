using UnityEngine;

/// <summary>
/// Agachar de verdade: enquanto a tecla de agachar (InputManager.CrouchAction)
/// estiver pressionada e o player estiver no chão, o BoxCollider2D fica mais
/// baixo (mesma altura do sprite agachado), então dá pra passar por baixo de
/// dutos, lasers e tetos baixos. Andando agachado a velocidade cai pela metade.
///
/// Ao soltar a tecla, só levanta se houver espaço acima; embaixo de um teto
/// baixo continua agachado até sair dele.
///
/// Funciona com qualquer controlador da fase (AnimacaoPlayerFisica,
/// InteracaoPlayerController, AplicacaoPlayerController, Player): eles
/// definem rb.linearVelocity.x no Update e este script só reduz no
/// LateUpdate, antes do passo de física. Quem mostra a animação é o
/// PlayerVisorAnimator ou, na fase Animacao, o parâmetro "agachado" do
/// AnimacaoController (ver AnimacaoPlayerFisica).
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAgachar : MonoBehaviour
{
    [Tooltip("Altura do collider agachado em relação à altura em pé (0.62 = altura do sprite agachado).")]
    [SerializeField, Range(0.3f, 0.9f)] private float alturaAgachado = 0.62f;
    [Tooltip("Multiplica a velocidade horizontal enquanto anda agachado.")]
    [SerializeField, Range(0.1f, 1f)] private float fatorVelocidade = 0.5f;
    [Tooltip("Folga lateral no teste de 'tem espaço pra levantar' (evita travar encostado numa parede).")]
    [SerializeField] private float folgaLateral = 0.04f;

    /// <summary>True enquanto está agachado (collider baixo).</summary>
    public bool Agachado { get; private set; }

    /// <summary>Time.time da última vez que o collider mudou de tamanho (agachou ou levantou).</summary>
    public float UltimaTroca { get; private set; } = -1f;

    // Ao trocar o tamanho do collider o contato com o chão some por um passo
    // de física; sem essa folga o player abaixava e levantava a cada frame.
    private const float FolgaChaoAposTroca = 0.15f;

    private BoxCollider2D box;
    private Rigidbody2D rb;
    private Vector2 tamanhoEmPe;
    private Vector2 offsetEmPe;
    private System.Func<bool> noChao;
    private readonly Collider2D[] resultados = new Collider2D[8];

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // Lê o tamanho só no Start: no Awake os controladores ainda podem
        // arredondar/encolher o collider (Physics2DTuning.PreventCornerSnag).
        tamanhoEmPe = box.size;
        offsetEmPe = box.offset;
        DefinirTesteDeChao();
    }

    private void DefinirTesteDeChao()
    {
        // Cada controlador da fase já sabe se está no chão; reaproveita.
        if (TryGetComponent(out AnimacaoPlayerFisica a)) noChao = () => a.IsGrounded;
        else if (TryGetComponent(out InteracaoPlayerController i)) noChao = () => i.IsGrounded;
        else if (TryGetComponent(out AplicacaoPlayerController ap)) noChao = () => ap.IsGrounded;
        else if (TryGetComponent(out Player p)) noChao = () => p.IsGrounded;
        else noChao = () => Mathf.Abs(rb.linearVelocity.y) < 0.01f;
    }

    private void Update()
    {
        // Com cenas somadas no Editor o Update chegou a rodar sem o Start ter
        // definido o teste de chão (NullReferenceException na linha abaixo).
        if (noChao == null) DefinirTesteDeChao();
        bool chao = noChao() || (Agachado && Time.time - UltimaTroca < FolgaChaoAposTroca);
        bool quer = !DialogueManager.IsAnyDialogueActive && !JogoBloqueado.Ativo
            && InputManager.Instance != null && InputManager.Instance.CrouchAction.IsPressed()
            && chao;

        if (quer && !Agachado) Abaixar();
        else if (!quer && Agachado && TemEspacoParaLevantar()) Levantar();
    }

    private void LateUpdate()
    {
        if (Agachado)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * fatorVelocidade, rb.linearVelocity.y);
        }
    }

    private void OnDisable()
    {
        if (Agachado) Levantar();
    }

    private void Abaixar()
    {
        Agachado = true;
        UltimaTroca = Time.time;
        // Altura total (com o edgeRadius dos cantos arredondados) vezes a proporção agachada.
        float borda = box.edgeRadius * 2f;
        float h = Mathf.Max(0.05f, (tamanhoEmPe.y + borda) * alturaAgachado - borda);
        box.size = new Vector2(tamanhoEmPe.x, h);
        // Mantém a base do collider no mesmo lugar (os pés não saem do chão).
        box.offset = new Vector2(offsetEmPe.x, offsetEmPe.y - (tamanhoEmPe.y - h) * 0.5f);
    }

    private void Levantar()
    {
        Agachado = false;
        UltimaTroca = Time.time;
        box.size = tamanhoEmPe;
        box.offset = offsetEmPe;
    }

    private bool TemEspacoParaLevantar()
    {
        Vector2 escala = transform.lossyScale;
        // Sobe um pouquinho e encolhe a altura pra não contar o chão em que está pisando.
        Vector2 centro = (Vector2)transform.position + Vector2.Scale(offsetEmPe, escala) + Vector2.up * 0.03f;
        float borda = box.edgeRadius * 2f;
        Vector2 tamanho = new Vector2(tamanhoEmPe.x * Mathf.Abs(escala.x) + borda - folgaLateral * 2f,
                                      tamanhoEmPe.y * Mathf.Abs(escala.y) + borda - 0.06f);
        var filtro = new ContactFilter2D { useTriggers = false };
        filtro.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        int n = Physics2D.OverlapBox(centro, tamanho, 0f, filtro, resultados);
        for (int k = 0; k < n; k++)
        {
            if (resultados[k] != box && !resultados[k].transform.IsChildOf(transform))
            {
                return false;
            }
        }
        return true;
    }
}
