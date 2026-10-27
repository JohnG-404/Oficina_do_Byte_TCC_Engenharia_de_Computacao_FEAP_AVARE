using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PuzzleControl : MonoBehaviour
{
    private static readonly List<PuzzleControl> Todos = new List<PuzzleControl>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Todos.Clear();

    /// <summary>Algum puzzle de encaixe está na tela (usado pelo <see cref="JogoBloqueado"/>).</summary>
    public static bool AlgumAberto
    {
        get
        {
            foreach (PuzzleControl p in Todos)
            {
                if (p != null && p.puzzle != null && p.puzzle.activeInHierarchy) return true;
            }
            return false;
        }
    }

    public GameFlowManager flowManager;

    [Tooltip("Alternativa ao flowManager pra cenas sem GameFlowManager (ex: IA): qualquer MonoBehaviour que implemente IPuzzleListener.")]
    [SerializeField] private MonoBehaviour listener;

    [Header("Código montado (mostrado ao concluir)")]
    [Tooltip("Título do painel que aparece ao resolver, ex: 'Código montado · Pulo'. Vazio = não mostra o painel.")]
    [SerializeField] private string tituloCodigo;
    [Tooltip("O trecho que as peças montaram. Aceita rich text do TextMeshPro (<color>, <b>).")]
    [TextArea(3, 8)]
    [SerializeField] private string codigoMontado;
    [Tooltip("Não fecha mais o painel: ele fica aberto até Enter ou clique (ver CodigoMontadoUI).")]
    [SerializeField] private float segundosCodigo = 4f;

    [Header("Peça errada")]
    [Tooltip("Quanto tempo a peça errada fica tremendo em cima da lacuna antes do puzzle recomeçar (tempo sem escala).")]
    [SerializeField] private float segundosPecaErrada = 0.6f;
    [Tooltip("Tremida da peça errada, em pixels de uma tela 1080p (para cada lado).")]
    [SerializeField] private float pixelsTremida = 6f;
    [Tooltip("Quanto tempo o painel da Dica mostra o aviso de erro.")]
    [SerializeField] private float segundosAvisoErro = 3f;

    public int totalPieces = 2;
    private int placedPieces = 0;

    private DragPiece[] pieces;
    public GameObject puzzle;

    // Pro registro de aprendizagem (ver RegistroAprendizagem).
    private float startTime;
    private int wrongPieces;

    // Peça errada sendo mostrada (os ~0,6 s antes do reset).
    private Coroutine erroRotina;
    private InfoPanelUI painelDica;

    /// <summary>True enquanto a peça errada treme e o arraste está bloqueado.</summary>
    public bool MostrandoErro => erroRotina != null;

    /// <summary>
    /// Exposto pra SnapZone conseguir achar sozinha qual PuzzleControl é dono
    /// de qual grupo, mesmo com vários puzzles (Puzzle/PuzzleRig/PuzzleCol) na
    /// mesma cena.
    /// </summary>
    public GameObject Puzzle => puzzle;

    /// <summary>Nome do conceito deste puzzle, usado no registro (em vez do nome do GameObject).</summary>
    public string ConceptName
    {
        get
        {
            switch (puzzle != null ? puzzle.name : "")
            {
                case "Puzzle": return "Movimento";
                case "PuzzleRig": return "Rigidbody2D";
                case "PuzzleCol": return "Collider2D";
                case "PuzzleJump": return "Pulo";
                case "PuzzleCam": return "Camera";
                default: return puzzle != null ? puzzle.name : name;
            }
        }
    }

    void Awake()
    {
        Todos.Add(this);
    }

    void OnDestroy()
    {
        Todos.Remove(this);
    }

    void Start()
    {
        // Só as peças DENTRO do grupo deste puzzle — antes usava
        // FindObjectsOfType<DragPiece>(), que pegava peças de TODOS os
        // puzzles da cena misturadas quando existe mais de um grupo
        // (Puzzle/PuzzleRig/PuzzleCol).
        pieces = puzzle.GetComponentsInChildren<DragPiece>(true);
        painelDica = puzzle.GetComponentInChildren<InfoPanelUI>(true);
        puzzle.SetActive(false);
    }

    public void StartPuzzle()
    {
        PararErro();
        placedPieces = 0;
        wrongPieces = 0;
        startTime = Time.time;
        puzzle.SetActive(true);
        ResetPuzzle();
        RegistroAprendizagem.Registrar("puzzle_inicio", ConceptName);
    }

    public void PiecePlaced()
    {
        // Acertou antes do aviso acabar: a Dica volta na hora.
        if (PainelDica != null) PainelDica.EsconderAvisoErro();

        placedPieces++;

        if (placedPieces >= totalPieces)
        {
            PuzzleSolved();
        }
    }

    private InfoPanelUI PainelDica => painelDica != null ? painelDica : InfoPanelUI.Instance;

    /// <summary>
    /// Chamado pela SnapZone quando a peça solta não é a certa: registra o
    /// erro na hora (CSV e som de erro), mostra a peça tremendo em cima da
    /// lacuna vermelha por ~0,6 s com o arraste bloqueado e o aviso no painel
    /// da Dica, e só então reinicia o puzzle (o reset ao errar é intencional).
    /// </summary>
    public void WrongPiece(DragPiece piece, SnapZone zone)
    {
        if (MostrandoErro) return;

        wrongPieces++;
        RegistroAprendizagem.RegistrarErro("puzzle_peca_errada", $"{ConceptName} | encaixe {zone.correctPieceID} | {piece.name}");
        erroRotina = StartCoroutine(MostrarPecaErrada(piece, zone));
    }

    private IEnumerator MostrarPecaErrada(DragPiece piece, SnapZone zone)
    {
        BloquearArraste(true);
        piece.Tremer(zone.CentroVisual, segundosPecaErrada, pixelsTremida * PiscaErro.MundoPorPixel());
        zone.PiscarErro(piece);
        if (PainelDica != null) PainelDica.MostrarAvisoErro(segundosAvisoErro);

        yield return new WaitForSecondsRealtime(segundosPecaErrada);

        erroRotina = null;
        BloquearArraste(false);
        ResetPuzzle();
    }

    // Interrompe uma peça errada em andamento (puzzle reaberto).
    private void PararErro()
    {
        if (erroRotina != null)
        {
            StopCoroutine(erroRotina);
            erroRotina = null;
        }
        BloquearArraste(false);
        if (PainelDica != null) PainelDica.EsconderAvisoErro();
    }

    private void BloquearArraste(bool bloquear)
    {
        if (pieces == null) return;
        foreach (DragPiece p in pieces)
        {
            if (p != null) p.ArrasteBloqueado = bloquear;
        }
    }

    void PuzzleSolved()
    {
        Debug.Log("Puzzle resolvido!");
        RegistroAprendizagem.Registrar("puzzle_concluido", $"{ConceptName} | {Time.time - startTime:0}s | {wrongPieces} erros");

        // Com código configurado, as peças ficam encaixadas na tela enquanto o
        // painel mostra o trecho montado; só depois o puzzle some.
        if (!string.IsNullOrEmpty(codigoMontado))
        {
            CodigoMontadoUI.Show(tituloCodigo, codigoMontado, segundosCodigo, FinishPuzzle);
            return;
        }

        FinishPuzzle();
    }

    private void FinishPuzzle()
    {
        puzzle.SetActive(false);

        // AVISA quem controla a cena, se identificando (o GameFlowManager tem
        // uma etapa diferente pra cada puzzle).
        if (flowManager != null)
        {
            flowManager.OnPuzzleCompleted(this);
        }
        else if (listener is IPuzzleListener l)
        {
            l.OnPuzzleCompleted(this);
        }
        else
        {
            Debug.LogError($"[PuzzleControl] '{name}' foi resolvido, mas não tem flowManager nem listener pra avisar.");
        }
    }

    public void ResetPuzzle()
    {
        placedPieces = 0;

        foreach (DragPiece piece in pieces)
        {
            piece.ResetPiece();
        }

        // Só as zonas DENTRO do grupo deste puzzle — mesmo motivo do
        // pieces acima: antes resetava as zonas de TODOS os puzzles juntas.
        SnapZone[] zones = puzzle.GetComponentsInChildren<SnapZone>(true);

        foreach (SnapZone zone in zones)
        {
            zone.ResetZone();
        }
    }
}
