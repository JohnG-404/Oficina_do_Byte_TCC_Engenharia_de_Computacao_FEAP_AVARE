using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zona onde uma peça do puzzle deve ser encaixada. Ao soltar a peça certa
/// aqui, ela é "colada" (posição, rotação E tamanho) exatamente no VISUAL
/// desta zona — o filho com o SpriteRenderer de verdade (ex: "Fundo") — em
/// vez de um Transform "SnapPoint" separado.
///
/// Quem decide é a soltura da peça ("ímã com aviso"):
/// - Ao soltar (DragPiece.OnMouseUp → <see cref="LacunaPara"/>), a peça vai
///   pra lacuna livre que ela cobre mais, desde que cubra pelo menos 1/4 do
///   visual dela. Não importa onde fica o centro da peça. Antes, a lacuna só
///   aceitava a peça com o centro dentro do gatilho e "esquecia" a peça
///   solta fora do centro; ajeitar por cima não adiantava.
/// - Enquanto o aluno arrasta (DragPiece.OnMouseDrag → <see cref="AtualizarAviso"/>),
///   a lacuna que vai receber a peça acende em ciano, no estilo da moldura
///   vermelha de erro. Sem moldura, soltar ali não encaixa em nada.
/// Como só a lacuna mais coberta conta, duas lacunas vizinhas nunca disputam
/// a mesma peça.
///
/// O gatilho (BoxCollider2D) continua se realinhando com o sprite visual em
/// Awake() (ver <see cref="ClampSizeToNeighbors"/>), mas não decide mais o
/// encaixe.
/// (Peças também têm seu próprio realinhamento de collider — ver
/// DragPiece.AlignColliderToVisual — pro clique/arrasto bater com o sprite,
/// e sua própria conta de "onde o pivô deve ir" pro sprite acabar exatamente
/// em cima do slot — ver DragPiece.ComputeRootPositionForVisual.)
/// </summary>
public class SnapZone : MonoBehaviour
{
    // Lacunas ativas na cena, pra peça achar a que ela cobre mais.
    private static readonly List<SnapZone> Todas = new List<SnapZone>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Todas.Clear();

    /// <summary>Fração mínima do visual da lacuna que a peça precisa cobrir pra encaixar.</summary>
    public const float CoberturaMinima = 0.25f;

    public int correctPieceID;

    private PuzzleControl puzzle;
    private Transform visualAnchor;
    private bool occupied = false;

    [Header("Encaixe dentro do código (cena Fisica)")]
    [Tooltip("Opcional. Se preenchido, a peça certa some da bandeja e este objeto (a palavra já escrita na linha) aparece no lugar da lacuna. Vazio = comportamento antigo: a peça é colada em cima do encaixe.")]
    [SerializeField] private GameObject preenchido;

    [Tooltip("Encolhe o gatilho pra não encostar no da zona vizinha. Desligado nas lacunas dentro do código, que ficam em linhas e colunas diferentes.")]
    [SerializeField] private bool ajustarAosVizinhos = true;

    [Tooltip("Folga extra do gatilho em volta do visual (unidades de mundo), pra soltar uma peça larga numa lacuna estreita.")]
    [SerializeField] private Vector2 folgaGatilho = Vector2.zero;

    /// <summary>True depois que a peça certa entrou (até o reset).</summary>
    public bool Ocupado => occupied;

    // Texto opcional dentro do encaixe vazio mostrando onde a peça entra na
    // linha de código (ex: "___.GetButtonDown(…)"). Some quando a peça
    // encaixa e volta no reset. Criado pelo FisicaPuzzleLayoutBuilder.
    private GameObject codeHint;

    // Peça escondida ao encaixar (modo "preenchido"); volta no reset.
    private DragPiece pecaEscondida;

    private void Awake()
    {
        visualAnchor = FindVisualAnchor();
        AlignTriggerToVisual();
        if (preenchido != null) preenchido.SetActive(false);

        Transform hint = transform.Find("DicaCodigo");
        codeHint = hint != null ? hint.gameObject : null;
    }

    // Redimensiona/reposiciona o próprio collider de gatilho (trigger) pra
    // bater com o retângulo do sprite visual, automaticamente. Isso evita o
    // problema de um BoxCollider2D "ajustado à mão" ficar desalinhado do
    // sprite sem ninguém perceber (foi exatamente isso que causou peças
    // parecendo não encaixar: o jogador mirava no visual, mas o gatilho de
    // verdade estava em outro lugar). Agora mover/redimensionar só o VISUAL
    // no Editor já ajusta o gatilho sozinho, sempre em Play Mode.
    //
    // O gatilho é encolhido o quanto for preciso pra nunca encostar no
    // gatilho da zona vizinha mais próxima (zonas de puzzle costumam ficar
    // bem juntas numa fileira) — sem isso, uma peça encaixando certinho
    // podia "vazar" pro gatilho da zona ao lado e disparar um reset
    // (pieceID errado ali) bem na hora que devia travar na certa.
    private void AlignTriggerToVisual()
    {
        if (visualAnchor == transform) return;

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        SpriteRenderer sr = visualAnchor.GetComponent<SpriteRenderer>();
        if (box == null || sr == null) return;

        Vector3 worldCenter = sr.bounds.center;
        Vector2 lossy = transform.lossyScale;
        Vector2 visual = (Vector2)sr.bounds.size + folgaGatilho * 2f;
        Vector2 worldSize = ajustarAosVizinhos ? ClampSizeToNeighbors(worldCenter, visual) : visual;

        box.offset = transform.InverseTransformPoint(worldCenter);
        box.size = new Vector2(
            lossy.x != 0f ? worldSize.x / Mathf.Abs(lossy.x) : worldSize.x,
            lossy.y != 0f ? worldSize.y / Mathf.Abs(lossy.y) : worldSize.y);
    }

    // Encolhe cada eixo do tamanho pra nunca passar de 90% da distância até
    // a zona vizinha mais próxima nesse eixo — garante folga sem precisar
    // ajustar nada à mão, não importa o quão apertada seja a fileira.
    private Vector2 ClampSizeToNeighbors(Vector3 worldCenter, Vector2 desiredSize)
    {
        if (transform.parent == null) return desiredSize;

        Vector2 halfSize = desiredSize * 0.5f;
        const float safetyMargin = 0.9f;

        SnapZone[] siblings = transform.parent.GetComponentsInChildren<SnapZone>(true);
        foreach (SnapZone sibling in siblings)
        {
            if (sibling == this) continue;

            Transform siblingAnchor = sibling.FindVisualAnchor();
            Vector3 delta = siblingAnchor.position - worldCenter;

            if (Mathf.Abs(delta.x) > 0.0001f)
            {
                halfSize.x = Mathf.Min(halfSize.x, Mathf.Abs(delta.x) * 0.5f * safetyMargin);
            }

            if (Mathf.Abs(delta.y) > 0.0001f)
            {
                halfSize.y = Mathf.Min(halfSize.y, Mathf.Abs(delta.y) * 0.5f * safetyMargin);
            }
        }

        return halfSize * 2f;
    }

    // Prefere o filho com o sprite de verdade (ex: "Fundo") — é ele que
    // representa onde o slot REALMENTE aparece na tela pro jogador.
    private Transform FindVisualAnchor()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.transform : transform;
    }

    private void Start()
    {
        puzzle = FindOwningPuzzleControl();

        if (puzzle == null)
        {
            Debug.LogError($"[SnapZone] '{name}' não encontrou o PuzzleControl responsável pelo grupo dele — confira se algum PuzzleControl tem o campo 'Puzzle' apontando pra um ancestral deste objeto.");
        }
    }

    // Sobe pela hierarquia (Encaixar -> grupo do puzzle, ex: "PuzzleCol") até
    // achar qual PuzzleControl tem o campo Puzzle apontando pra esse mesmo
    // ancestral — cada grupo de puzzle (Movimento/Física/Colisão) tem o seu.
    private PuzzleControl FindOwningPuzzleControl()
    {
        PuzzleControl[] allControls = FindObjectsByType<PuzzleControl>();

        for (Transform current = transform; current != null; current = current.parent)
        {
            foreach (PuzzleControl control in allControls)
            {
                if (control.Puzzle == current.gameObject)
                {
                    return control;
                }
            }
        }

        return null;
    }

    private void OnEnable()
    {
        Todas.Add(this);
    }

    // ------------------------------------------------------------------
    // Ímã: qual lacuna recebe a peça
    // ------------------------------------------------------------------

    /// <summary>
    /// Lacuna livre do mesmo puzzle que a peça cobre mais, se cobrir pelo
    /// menos <see cref="CoberturaMinima"/> dela; senão null (soltar ali não
    /// encaixa em nada).
    /// </summary>
    public static SnapZone LacunaPara(DragPiece peca)
    {
        SnapZone melhor = null;
        float melhorCobertura = CoberturaMinima;

        foreach (SnapZone zona in Todas)
        {
            if (zona == null || !zona.AceitaPeca(peca)) continue;

            float cobertura = zona.Cobertura(peca);
            if (cobertura >= melhorCobertura)
            {
                melhor = zona;
                melhorCobertura = cobertura;
            }
        }

        return melhor;
    }

    /// <summary>Acende só a lacuna que receberia a peça se ela fosse solta agora.</summary>
    public static void AtualizarAviso(DragPiece peca)
    {
        SnapZone alvo = LacunaPara(peca);
        foreach (SnapZone zona in Todas)
        {
            if (zona == null) continue;
            if (zona == alvo) zona.MostrarAviso(peca);
            else zona.EsconderAviso();
        }
    }

    public static void EsconderAvisos()
    {
        foreach (SnapZone zona in Todas)
        {
            if (zona != null) zona.EsconderAviso();
        }
    }

    private bool AceitaPeca(DragPiece peca)
    {
        if (occupied || puzzle == null || !isActiveAndEnabled) return false;

        // Enquanto a peça errada treme, nenhuma lacuna decide nada.
        if (puzzle.MostrandoErro) return false;

        // Só as lacunas do mesmo grupo de puzzle da peça.
        return puzzle.Puzzle != null && peca.transform.IsChildOf(puzzle.Puzzle.transform);
    }

    /// <summary>Fração do visual da lacuna coberta pelo visual da peça (0 a 1).</summary>
    public float Cobertura(DragPiece peca)
    {
        Bounds lacuna = VisualBounds;
        Bounds p = peca.VisualBounds;

        float largura = Mathf.Min(lacuna.max.x, p.max.x) - Mathf.Max(lacuna.min.x, p.min.x);
        float altura = Mathf.Min(lacuna.max.y, p.max.y) - Mathf.Max(lacuna.min.y, p.min.y);
        float area = lacuna.size.x * lacuna.size.y;
        if (largura <= 0f || altura <= 0f || area <= 0f) return 0f;

        return largura * altura / area;
    }

    private Bounds VisualBounds
    {
        get
        {
            SpriteRenderer sr = visualAnchor != null ? visualAnchor.GetComponent<SpriteRenderer>() : null;
            if (sr != null) return sr.bounds;
            Collider2D col = GetComponent<Collider2D>();
            return col != null ? col.bounds : new Bounds(transform.position, Vector3.one);
        }
    }

    /// <summary>
    /// A peça foi solta nesta lacuna (escolhida por <see cref="LacunaPara"/>):
    /// encaixa se for a certa, senão mostra o erro.
    /// </summary>
    public void Receber(DragPiece peca)
    {
        if (!AceitaPeca(peca)) return;

        EsconderAviso();

        if (peca.pieceID == correctPieceID)
        {
            if (preenchido != null)
            {
                // A palavra já escrita na linha toma o lugar da lacuna; a
                // peça sai da bandeja até o reset.
                preenchido.SetActive(true);
                pecaEscondida = peca;
                peca.gameObject.SetActive(false);
            }
            else
            {
                peca.LockPiece(visualAnchor.position, GetTargetSize(), visualAnchor.rotation);
            }
            occupied = true;
            if (codeHint != null) codeHint.SetActive(false);

            AudioManager.TocarEfeito(AudioManager.Efeito.Encaixe);
            puzzle.PiecePlaced();
        }
        else
        {
            puzzle.WrongPiece(peca, this);
        }
    }

    // Moldura ciano de "vai encaixar aqui" (criada na primeira vez, por código).
    private SpriteRenderer marcaAviso;

    /// <summary>
    /// Moldura ciano em volta da lacuna e da peça arrastada, no mesmo lugar e
    /// estilo da moldura vermelha de erro (logo atrás da peça, com folga).
    /// </summary>
    private void MostrarAviso(DragPiece peca)
    {
        SpriteRenderer fundo = visualAnchor != null ? visualAnchor.GetComponent<SpriteRenderer>() : null;
        Bounds area = VisualBounds;
        area.Encapsulate(peca.VisualBounds);

        if (marcaAviso == null)
        {
            var go = new GameObject("MarcaAviso");
            go.transform.SetParent(transform, false);
            marcaAviso = go.AddComponent<SpriteRenderer>();
            marcaAviso.sprite = PiscaErro.MolduraEncaixe;
        }

        if (fundo != null) marcaAviso.sortingLayerID = fundo.sortingLayerID;
        marcaAviso.sortingOrder = peca.VisualSortingOrder - 1;
        marcaAviso.color = TemaLabUI.Ciano;

        float folga = PiscaErro.MundoPorPixel() * 10f;
        PiscaErro.Cobrir(marcaAviso, area.center, (Vector2)area.size + Vector2.one * folga * 2f);
        marcaAviso.gameObject.SetActive(true);
    }

    private void EsconderAviso()
    {
        if (marcaAviso != null && marcaAviso.gameObject.activeSelf) marcaAviso.gameObject.SetActive(false);
    }

    // Tamanho (em unidades de mundo) que a peça deve assumir ao encaixar —
    // o do próprio sprite da zona, não do collider (pequeno de propósito).
    private Vector2 GetTargetSize()
    {
        SpriteRenderer sr = visualAnchor.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            return sr.bounds.size;
        }

        Collider2D col = GetComponent<Collider2D>();
        return col != null ? (Vector2)col.bounds.size : Vector2.one;
    }

    /// <summary>Centro do visual da lacuna, em mundo.</summary>
    public Vector3 CentroVisual
    {
        get
        {
            SpriteRenderer sr = visualAnchor != null ? visualAnchor.GetComponent<SpriteRenderer>() : null;
            return sr != null ? sr.bounds.center : transform.position;
        }
    }

    // Moldura vermelha da peça errada (criada na primeira vez, por código).
    private SpriteRenderer marcaErro;
    private Coroutine piscaRotina;

    /// <summary>
    /// Peça errada solta aqui: a lacuna pisca em vermelho no ritmo do
    /// PiscaErro. A moldura cobre a lacuna e a peça (com folga pra tremida)
    /// e fica logo atrás da peça — senão a peça, maior que a lacuna nos
    /// puzzles da Física, esconderia o vermelho.
    /// </summary>
    public void PiscarErro(DragPiece peca)
    {
        SpriteRenderer fundo = visualAnchor != null ? visualAnchor.GetComponent<SpriteRenderer>() : null;
        Bounds area = fundo != null ? fundo.bounds : new Bounds(transform.position, Vector3.one);
        Bounds pecaArea = peca.VisualBounds;
        pecaArea.center = area.center;
        area.Encapsulate(pecaArea);

        if (marcaErro == null)
        {
            var go = new GameObject("MarcaErro");
            go.transform.SetParent(transform, false);
            marcaErro = go.AddComponent<SpriteRenderer>();
            marcaErro.sprite = PiscaErro.MolduraLacuna;
        }

        if (fundo != null) marcaErro.sortingLayerID = fundo.sortingLayerID;
        marcaErro.sortingOrder = peca.VisualSortingOrder - 1;
        marcaErro.color = VidaUI.Vermelho;

        // 10 px de folga em volta: a tremida é de ±6 px.
        float folga = PiscaErro.MundoPorPixel() * 10f;
        PiscaErro.Cobrir(marcaErro, area.center, (Vector2)area.size + Vector2.one * folga * 2f);

        PararPisca();
        piscaRotina = StartCoroutine(Piscar());
    }

    private System.Collections.IEnumerator Piscar()
    {
        yield return PiscaErro.Piscar(aceso =>
        {
            if (marcaErro != null) marcaErro.gameObject.SetActive(aceso);
        });
        piscaRotina = null;
    }

    private void PararPisca()
    {
        if (piscaRotina != null)
        {
            StopCoroutine(piscaRotina);
            piscaRotina = null;
        }
        if (marcaErro != null) marcaErro.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        Todas.Remove(this);
        PararPisca();
        EsconderAviso();
    }

    public void ResetZone()
    {
        PararPisca();
        EsconderAviso();
        occupied = false;
        if (codeHint != null) codeHint.SetActive(true);
        if (preenchido != null) preenchido.SetActive(false);
        if (pecaEscondida != null)
        {
            // O PuzzleControl já devolveu a peça pra posição inicial
            // (ResetPiece) antes de chamar ResetZone.
            pecaEscondida.gameObject.SetActive(true);
            pecaEscondida = null;
        }
    }
}
