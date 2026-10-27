using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Peça arrastável da cena "Tutorial_Anim" — um frame solto (sprite) que o
/// jogador arrasta da bandeja até um encaixe da linha do tempo. Mesma
/// mecânica de arrastar por mouse já usada em DragPiece (cena Inicio):
/// OnMouseDown/OnMouseDrag/OnMouseUp, com o arrasto travado dentro da câmera.
///
/// Um clique sem arrastar também funciona: na bandeja, manda a peça pro
/// próximo encaixe livre; num encaixe, devolve pra bandeja (ver
/// AnimationSequencePuzzle.NotifyClicked).
///
/// Quem decide se a peça "cola" num encaixe, se substitui outra que já
/// estava lá, ou se volta pra bandeja é o AnimationSequencePuzzle — esta
/// classe só avisa ele quando a peça é solta ou clicada.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SequencePiece : MonoBehaviour
{
    [Tooltip("Posição correta desta peça na sequência (1 a 5). Deixe 0 se ela for uma peça isca, que não faz parte da sequência certa. Preenchido pelo puzzle a cada rodada.")]
    [SerializeField] private int correctPosition;

    [Tooltip("Distância (em unidades de mundo) que o mouse precisa andar pra contar como arrasto, e não clique.")]
    [SerializeField] private float limiteClique = 0.12f;

    public int CorrectPosition => correctPosition;
    public bool IsDragging { get; private set; }

    /// <summary>Peça presa no encaixe 1 (animação em loop): não arrasta nem volta pra bandeja.</summary>
    public bool Travada { get; set; }

    public Sprite Sprite => sr != null ? sr.sprite : null;

    private SpriteRenderer sr;
    private Collider2D col;
    private Vector3 homePosition;
    private Vector3 dragOffset;
    private Vector3 mouseDownPos;
    private bool apertou;
    private Vector2 halfExtents;
    private int ordemBase;
    private float tamanhoBandeja = 1.4f;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        ordemBase = sr != null ? sr.sortingOrder : 0;
        halfExtents = ComputeHalfExtents();
    }

    private void OnEnable()
    {
        homePosition = transform.position;
    }

    /// <summary>
    /// Reaproveita esta peça (mesma posição na bandeja) pra uma nova rodada
    /// do puzzle, trocando o sprite mostrado e a posição certa dela na nova
    /// sequência — ver AnimationSequencePuzzle.StartRound.
    /// </summary>
    public void Configure(Sprite sprite, int newCorrectPosition, float tamanho)
    {
        correctPosition = newCorrectPosition;
        tamanhoBandeja = tamanho;
        Travada = false;

        if (sr != null)
        {
            sr.sprite = sprite;
        }

        AjustarTamanho(tamanho);
    }

    /// <summary>Escala a peça pra que o maior lado do sprite caiba em <paramref name="tamanho"/> unidades.</summary>
    public void AjustarTamanho(float tamanho)
    {
        if (sr == null || sr.sprite == null)
        {
            return;
        }

        Vector2 size = sr.sprite.bounds.size;
        float maior = Mathf.Max(size.x, size.y);
        float escala = maior > 0.0001f ? tamanho / maior : 1f;
        transform.localScale = new Vector3(escala, escala, 1f);

        if (col is BoxCollider2D box)
        {
            box.size = size;
            box.offset = sr.sprite.bounds.center;
        }

        halfExtents = ComputeHalfExtents();
    }

    private bool PodeInteragir()
    {
        // OnMouseDown ignora a UI: sem isso, clicar nos botões do painel de
        // ajuste (FPS/Loop) ou no diálogo do Prof mexeria na peça de trás.
        AnimationSequencePuzzle puzzle = AnimationSequencePuzzle.Instance;
        return !Travada && (puzzle == null || !puzzle.BloqueiaArrasto);
    }

    // Passo de evento: só clique (marca o frame), nada de arrastar.
    private static bool EmEvento => AnimationSequencePuzzle.Instance != null && AnimationSequencePuzzle.Instance.EmEvento;

    private void OnMouseDown()
    {
        apertou = false;
        if (EmEvento)
        {
            apertou = AnimationSequencePuzzle.Instance.AceitaCliqueEvento;
            mouseDownPos = GetMouseWorldPosition();
            return;
        }

        if (!PodeInteragir())
        {
            return;
        }

        apertou = true;
        mouseDownPos = GetMouseWorldPosition();
        dragOffset = transform.position - mouseDownPos;
    }

    private void OnMouseDrag()
    {
        if (!apertou || EmEvento)
        {
            return;
        }

        Vector3 mouse = GetMouseWorldPosition();
        if (!IsDragging && Vector2.Distance(mouse, mouseDownPos) < limiteClique)
        {
            return;
        }

        if (!IsDragging)
        {
            IsDragging = true;
            if (sr != null)
            {
                sr.sortingOrder = ordemBase + 20; // por cima de tudo enquanto arrasta
            }
        }

        transform.position = ClampToScreen(mouse + dragOffset);
        AnimationSequencePuzzle.Instance?.NotifyDragging(this);
    }

    private void OnMouseUp()
    {
        if (!apertou)
        {
            return;
        }

        apertou = false;

        if (!IsDragging)
        {
            AnimationSequencePuzzle.Instance?.NotifyClicked(this);
            return;
        }

        IsDragging = false;
        if (sr != null)
        {
            sr.sortingOrder = ordemBase;
        }
        AnimationSequencePuzzle.Instance?.NotifyDropped(this);
    }

    /// <summary>Volta pro lugar de origem, na bandeja (ver AnimationSequencePuzzle).</summary>
    public void ReturnHome()
    {
        transform.position = homePosition;
        AjustarTamanho(tamanhoBandeja);
    }

    /// <summary>"Cola" a peça numa posição de encaixe, no tamanho do encaixe (ver AnimationSequencePuzzle).</summary>
    public void SnapTo(Vector3 position, float tamanho)
    {
        position.z = homePosition.z;
        transform.position = position;
        AjustarTamanho(tamanho);
    }

    private Vector3 GetMouseWorldPosition()
    {
        // Mouse do Input System (o resto do jogo já lê tudo por ele).
        Vector3 mouse = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
        mouse.z = 10f;
        return Camera.main.ScreenToWorldPoint(mouse);
    }

    // Não deixa a peça (nem a borda dela) sair da área visível da câmera principal.
    private Vector3 ClampToScreen(Vector3 position)
    {
        position.z = homePosition.z;

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

        position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : camPos.x;
        position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : camPos.y;

        return position;
    }

    private Vector2 ComputeHalfExtents()
    {
        if (col != null)
        {
            return col.bounds.extents;
        }

        return sr != null ? (Vector2)sr.bounds.extents : Vector2.zero;
    }
}
