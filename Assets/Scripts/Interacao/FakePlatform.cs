using System.Collections;
using UnityEngine;

/// <summary>
/// Plataforma falsa: tem dois comportamentos possíveis, escolhidos pelo
/// jogador no assistente Ação+Consequência (ver InteractableItem/
/// ConsequenceType.BreakAndRespawn/BounceAndReturn) — só um dos dois é
/// aplicado, conforme a Consequência configurada.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FakePlatform : MonoBehaviour
{
    [Header("Quebrar e voltar depois")]
    [SerializeField] private float breakDelay = 0.3f;
    [SerializeField] private float respawnDelay = 2f;

    [Header("Cair depois de um tempo")]
    [Tooltip("Quanto tempo aguenta o jogador antes de cair.")]
    [SerializeField] private float fallDelay = 0.6f;
    [SerializeField] private float fallDuration = 0.9f;

    [Header("Ficar escorregadia")]
    [SerializeField] private Color slipperyColor = new Color(0.65f, 0.88f, 1f, 1f);

    [Header("Subir e voltar")]
    [SerializeField] private float bounceHeight = 2.5f;
    [SerializeField] private float bounceDuration = 0.4f;

    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;
    private Vector2 startPosition;
    private bool busy;

    /// <summary>Sem atrito: o InteracaoPlayerController desliza em cima (ver ConsequenceType.MakeSlippery).</summary>
    public bool Escorregadia { get; private set; }

    // Avisos pra parte visual (PlataformaFalsaVisual): começou a rachar (com
    // a duração), sumiu e voltou.
    public event System.Action<float> Rachando;
    public event System.Action Sumiu;
    public event System.Action Voltou;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        startPosition = rb.position;
        if (sr != null) corOriginal = sr.color;
    }

    private Color corOriginal = Color.white;

    /// <summary>
    /// Reconfigurar: desfaz "Ficar escorregadia" e "Deixar passar por baixo"
    /// (volta o atrito, desliga o PlatformEffector2D e a cor original).
    /// </summary>
    public void Desfazer()
    {
        Escorregadia = false;
        var effector = GetComponent<PlatformEffector2D>();
        if (effector != null) effector.useOneWay = false;
        if (col != null) col.usedByEffector = false;
        if (sr != null) sr.color = corOriginal;
    }

    /// <summary>Some (para de bloquear e de aparecer) por um tempo, depois volta sozinha.</summary>
    public void BreakAndRespawn()
    {
        if (busy) return;
        StartCoroutine(BreakRoutine());
    }

    private IEnumerator BreakRoutine()
    {
        busy = true;
        Rachando?.Invoke(breakDelay);
        yield return new WaitForSeconds(breakDelay);

        col.enabled = false;
        if (sr != null) sr.enabled = false;
        Sumiu?.Invoke();

        yield return new WaitForSeconds(respawnDelay);

        col.enabled = true;
        if (sr != null) sr.enabled = true;
        Voltou?.Invoke();
        busy = false;
    }

    /// <summary>
    /// Aguenta um instante (tremendo), cai, some e volta pro lugar depois de
    /// um tempo. Usa os mesmos avisos da quebra (Rachando, Sumiu, Voltou),
    /// então o visual treme e reaparece igual.
    /// </summary>
    public void FallAfterDelay()
    {
        if (busy) return;
        StartCoroutine(FallRoutine());
    }

    private IEnumerator FallRoutine()
    {
        busy = true;
        Rachando?.Invoke(fallDelay);
        yield return new WaitForSeconds(fallDelay);

        // Cai acelerando; o collider continua no começo da queda (o jogador
        // cai junto) e é desligado logo depois, pra a placa atravessar o chão.
        float t = 0f;
        float velocidade = 0f;
        while (t < fallDuration)
        {
            t += Time.deltaTime;
            velocidade += 25f * Time.deltaTime;
            rb.MovePosition(rb.position + Vector2.down * velocidade * Time.deltaTime);
            if (t > 0.2f && col.enabled) col.enabled = false;
            yield return null;
        }

        col.enabled = false;
        if (sr != null) sr.enabled = false;
        Sumiu?.Invoke();

        yield return new WaitForSeconds(respawnDelay);

        rb.position = startPosition;
        transform.position = new Vector3(startPosition.x, startPosition.y, transform.position.z);
        col.enabled = true;
        if (sr != null) sr.enabled = true;
        Voltou?.Invoke();
        busy = false;
    }

    /// <summary>Tira o atrito da placa: quem pisa desliza (ver InteracaoPlayerController).</summary>
    public void MakeSlippery()
    {
        Escorregadia = true;
        if (sr != null) sr.color = slipperyColor;
    }

    /// <summary>
    /// Vira plataforma de mão única: o jogador atravessa pulando por baixo e
    /// fica de pé em cima. No Unity é o PlatformEffector2D com "Use One Way"
    /// e o collider marcado com "Used By Effector".
    /// </summary>
    public void MakeOneWay()
    {
        var effector = GetComponent<PlatformEffector2D>();
        if (effector == null)
        {
            effector = gameObject.AddComponent<PlatformEffector2D>();
        }

        effector.useOneWay = true;
        effector.surfaceArc = 160f;
        col.usedByEffector = true;
        if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.75f);
    }

    /// <summary>Sobe, e depois volta sozinha pro ponto inicial.</summary>
    public void BounceAndReturn()
    {
        if (busy) return;
        StartCoroutine(BounceRoutine());
    }

    private IEnumerator BounceRoutine()
    {
        busy = true;
        Vector2 top = startPosition + Vector2.up * bounceHeight;

        yield return MoveOverTime(startPosition, top, bounceDuration);
        yield return MoveOverTime(top, startPosition, bounceDuration);

        busy = false;
    }

    private IEnumerator MoveOverTime(Vector2 from, Vector2 to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            rb.MovePosition(Vector2.Lerp(from, to, t / duration));
            yield return null;
        }
        rb.MovePosition(to);
    }
}
