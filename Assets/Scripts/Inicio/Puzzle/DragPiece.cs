using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class DragPiece : MonoBehaviour
{
    public int pieceID;

    [Tooltip("Duração da animação de encaixe no slot, em segundos.")]
    [SerializeField] private float snapDuration = 0.22f;

    private Vector3 startPosition;
    private Vector3 startScale;

    private Vector3 offset;
    private bool dragging = false;

    public bool locked = false;

    /// <summary>
    /// Ligado pelo PuzzleControl enquanto mostra uma peça errada (~0,6 s):
    /// nenhuma peça pode ser pega nesse meio-tempo.
    /// </summary>
    public bool ArrasteBloqueado { get; set; }

    // Metade do tamanho da própria peça (sprite/collider), usada pra
    // travar o arrasto sem deixar nem a borda da peça sair da câmera.
    private Vector2 halfExtents;

    private Collider2D col;
    private SpriteRenderer visualRenderer;
    private Coroutine snapRoutine;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        // O sprite visível (filho "Fundo") é quem define o que o jogador
        // realmente vê — usado pra calcular o encaixe de tamanho, já que o
        // collider (no root) pode ser um pouco menor/maior que o sprite.
        visualRenderer = GetComponentInChildren<SpriteRenderer>();
        AlignColliderToVisual();
        halfExtents = ComputeHalfExtents();
    }

    // Realinha o próprio collider da peça (usado tanto pra arrastar quanto
    // pra detectar o encaixe no SnapZone) com o sprite visível dela. Peças
    // hand-autoradas podem ter um BoxCollider2D com offset/tamanho torto —
    // isso fazia a peça "errar" o gatilho da zona mesmo quando soltada
    // exatamente em cima do slot visível (o collider de verdade estava em
    // outro lugar). Mesma ideia já aplicada no SnapZone.
    private void AlignColliderToVisual()
    {
        if (visualRenderer == null || visualRenderer.transform == transform) return;

        BoxCollider2D box = col as BoxCollider2D;
        if (box == null) return;

        Vector3 localCenter = transform.InverseTransformPoint(visualRenderer.bounds.center);
        box.offset = localCenter;

        Vector2 lossy = transform.lossyScale;
        Vector2 worldSize = visualRenderer.bounds.size;
        box.size = new Vector2(
            lossy.x != 0f ? worldSize.x / Mathf.Abs(lossy.x) : worldSize.x,
            lossy.y != 0f ? worldSize.y / Mathf.Abs(lossy.y) : worldSize.y);
    }

    void OnEnable()
    {
        startPosition = transform.position;
        startScale = transform.localScale;
    }

    void OnMouseDown()
    {
        if (locked || ArrasteBloqueado) return;

        offset = transform.position - GetMouseWorldPosition();
        dragging = true;
    }

    void OnMouseDrag()
    {
        if (dragging && !locked)
        {
            Vector3 target = GetMouseWorldPosition() + offset;
            transform.position = ClampToScreen(target);

            // A lacuna que vai receber a peça acende em ciano (ver SnapZone).
            SnapZone.AtualizarAviso(this);
        }
    }

    void OnMouseUp()
    {
        if (!dragging) return;
        dragging = false;

        // Ímã: a peça vai pra lacuna que ela cobre mais (ver SnapZone.LacunaPara).
        SnapZone.EsconderAvisos();
        SnapZone alvo = SnapZone.LacunaPara(this);
        if (alvo != null) alvo.Receber(this);
    }

    void OnDisable()
    {
        // Puzzle fechado no meio do arrasto: a moldura ciano não pode ficar acesa.
        if (dragging)
        {
            dragging = false;
            SnapZone.EsconderAvisos();
        }
    }

    public bool IsDragging()
    {
        return dragging;
    }

    /// <summary>
    /// Encaixa a peça no slot: anima posição, rotação E tamanho até baterem
    /// exatamente com o visual da zona (ver SnapZone) — em vez de
    /// teleportar direto pra um ponto que podia ficar desalinhado do que
    /// aparece na tela.
    ///
    /// "snapPosition" é onde o SPRITE da peça deve aparecer — não
    /// necessariamente onde o transform (pivô) dela deve ir. Várias peças
    /// têm o sprite ("Fundo") deslocado do próprio pivô (autoria manual);
    /// sem corrigir isso, a peça "encaixava" com o pivô certinho mas
    /// aparecia visualmente fora do slot. <see cref="ComputeRootPositionForVisual"/>
    /// faz essa conta pra trás.
    /// </summary>
    public void LockPiece(Vector3 snapPosition, Vector2 targetWorldSize, Quaternion targetRotation)
    {
        locked = true;
        dragging = false;

        Vector3 targetScale = ComputeScaleToFit(targetWorldSize);
        Vector3 targetRootPosition = ComputeRootPositionForVisual(snapPosition, targetScale, targetRotation);

        if (snapRoutine != null)
        {
            StopCoroutine(snapRoutine);
        }
        snapRoutine = StartCoroutine(AnimateSnap(targetRootPosition, targetScale, targetRotation));
    }

    // O sprite visível pode estar deslocado do pivô da peça (localPosition
    // do filho "Fundo" não é zero). Pra fazer o SPRITE terminar exatamente
    // em cima de "desiredVisualCenter", calculamos pra trás onde o PIVÔ
    // precisa estar, dado o deslocamento do filho e a escala/rotação finais.
    private Vector3 ComputeRootPositionForVisual(Vector3 desiredVisualCenter, Vector3 rootLocalScale, Quaternion rootWorldRotation)
    {
        if (visualRenderer == null) return desiredVisualCenter;

        // TransformPoint usa a escala de MUNDO (lossy), não a local — se o
        // pai desta peça também tiver escala, precisa entrar na conta.
        Vector3 parentLossyScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        Vector3 futureLossyScale = Vector3.Scale(parentLossyScale, rootLocalScale);

        Vector3 childLocalPos = visualRenderer.transform.localPosition;
        Vector3 childOffsetAtTargetTransform = rootWorldRotation * Vector3.Scale(futureLossyScale, childLocalPos);

        return desiredVisualCenter - childOffsetAtTargetTransform;
    }

    /// <summary>Compatibilidade: encaixa só na posição, sem ajustar tamanho/rotação.</summary>
    public void LockPiece(Vector3 snapPosition)
    {
        LockPiece(snapPosition, halfExtents * 2f, transform.rotation);
    }

    /// <summary>Retângulo do sprite visível da peça, em mundo.</summary>
    public Bounds VisualBounds => visualRenderer != null
        ? visualRenderer.bounds
        : (col != null ? col.bounds : new Bounds(transform.position, halfExtents * 2f));

    /// <summary>Ordem de desenho do sprite da peça (pra pôr coisas logo atrás dela).</summary>
    public int VisualSortingOrder => visualRenderer != null ? visualRenderer.sortingOrder : 0;

    /// <summary>
    /// Peça errada: centraliza o sprite em "centroVisual" e treme de leve na
    /// horizontal (±amplitude, em tempo sem escala) por "segundos". O
    /// ResetPiece que vem depois interrompe e devolve a peça pra bandeja.
    /// </summary>
    public void Tremer(Vector3 centroVisual, float segundos, float amplitude)
    {
        dragging = false;
        if (snapRoutine != null)
        {
            StopCoroutine(snapRoutine);
        }
        snapRoutine = StartCoroutine(AnimarTremida(centroVisual, segundos, amplitude));
    }

    private IEnumerator AnimarTremida(Vector3 centroVisual, float segundos, float amplitude)
    {
        Vector3 centro = ComputeRootPositionForVisual(centroVisual, transform.localScale, transform.rotation);
        centro.z = transform.position.z;

        // ~11 idas e vindas por segundo, perdendo força no fim.
        const float frequencia = 11f;
        float t = 0f;
        while (t < segundos)
        {
            float queda = 1f - 0.6f * (t / segundos);
            float dx = Mathf.Sin(t * frequencia * Mathf.PI * 2f) * amplitude * queda;
            transform.position = centro + new Vector3(dx, 0f, 0f);
            yield return null;
            t += Time.unscaledDeltaTime;
        }

        transform.position = centro;
        snapRoutine = null;
    }

    public void ResetPiece()
    {
        if (snapRoutine != null)
        {
            StopCoroutine(snapRoutine);
            snapRoutine = null;
        }

        transform.position = startPosition;
        transform.localScale = startScale;
        locked = false;
        halfExtents = ComputeHalfExtents();
    }

    // Calcula a escala que faz o tamanho ATUAL da peça (medido pelo sprite
    // visível, não pelo collider — o collider pode ter uma folga
    // proposital pra facilitar pegar a peça) bater exatamente com o
    // tamanho do slot, em unidades de mundo.
    private Vector3 ComputeScaleToFit(Vector2 targetWorldSize)
    {
        Vector2 currentWorldSize = visualRenderer != null
            ? (Vector2)visualRenderer.bounds.size
            : (col != null ? (Vector2)col.bounds.size : halfExtents * 2f);
        Vector3 currentScale = transform.localScale;

        float scaleX = currentWorldSize.x > 0.0001f ? targetWorldSize.x / currentWorldSize.x : 1f;
        float scaleY = currentWorldSize.y > 0.0001f ? targetWorldSize.y / currentWorldSize.y : 1f;

        return new Vector3(currentScale.x * scaleX, currentScale.y * scaleY, currentScale.z);
    }

    private IEnumerator AnimateSnap(Vector3 targetPosition, Vector3 targetScale, Quaternion targetRotation)
    {
        Vector3 fromPosition = transform.position;
        Vector3 fromScale = transform.localScale;
        Quaternion fromRotation = transform.rotation;

        float t = 0f;
        while (t < snapDuration)
        {
            t += Time.deltaTime;
            float linear = Mathf.Clamp01(t / snapDuration);
            float eased = EaseOutBack(linear);

            transform.position = Vector3.LerpUnclamped(fromPosition, targetPosition, eased);
            transform.localScale = Vector3.LerpUnclamped(fromScale, targetScale, eased);
            transform.rotation = Quaternion.Slerp(fromRotation, targetRotation, linear);
            yield return null;
        }

        transform.position = targetPosition;
        transform.localScale = targetScale;
        transform.rotation = targetRotation;
        halfExtents = ComputeHalfExtents();
        snapRoutine = null;
    }

    // "Back ease-out" com overshoot reduzido (1.2 em vez do clássico
    // 1.70158) — dá a sensação de "click" no lugar sem exagerar numa peça
    // de puzzle educativo.
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.2f;
        const float c3 = c1 + 1f;
        float m = x - 1f;
        return 1f + c3 * (m * m * m) + c1 * (m * m);
    }

    Vector3 GetMouseWorldPosition()
    {
        // Mouse do Input System (o resto do jogo já lê tudo por ele).
        Vector3 mouse = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
        mouse.z = 10f;
        return Camera.main.ScreenToWorldPoint(mouse);
    }

    // Não deixa a peça (nem a borda dela) sair da área visível da câmera principal.
    Vector3 ClampToScreen(Vector3 position)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            return position;
        }

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Vector3 camPos = cam.transform.position;

        float minX = camPos.x - halfWidth + halfExtents.x;
        float maxX = camPos.x + halfWidth - halfExtents.x;
        float minY = camPos.y - halfHeight + halfExtents.y;
        float maxY = camPos.y + halfHeight - halfExtents.y;

        // Se a peça for maior que a tela nesse eixo, trava no centro em vez de bugar.
        position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : camPos.x;
        position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : camPos.y;

        return position;
    }

    Vector2 ComputeHalfExtents()
    {
        if (col != null)
        {
            return col.bounds.extents;
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            return sr.bounds.extents;
        }

        return Vector2.zero;
    }
}
