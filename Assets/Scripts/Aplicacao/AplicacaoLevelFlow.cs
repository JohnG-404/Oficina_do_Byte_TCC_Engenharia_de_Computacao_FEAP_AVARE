using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Orquestra a fase "Aplicacao": mostra o diálogo de intro do Prof Byte
/// explicando a fase (inventário some até o diálogo terminar), reinicia a
/// fase se o Player ou um objeto cair da tela (Rigidbody2D ligado sem
/// Collider2D sólido pra pará-lo) e avança pra fase seguinte quando o Player
/// alcança o objetivo (LevelGoal).
///
/// Montagem da cena (visual por conta do autor): cada objeto que recebe
/// ferramentas leva um Collider2D + PhysicsTarget; o objetivo leva um
/// Collider2D + LevelGoal; os ícones do inventário (DraggableToolIcon) podem
/// ter usos limitados. Se o jogador ficar sem saída, R reinicia a fase.
///
/// Referências (player/goal/completeUI/dialogueManager/inventoryPanel) são
/// achadas automaticamente na cena se não forem atribuídas no Inspector —
/// ver AplicacaoSceneBuilder.
/// </summary>
public class AplicacaoLevelFlow : MonoBehaviour, IDialogueListener, IReinicioFase
{
    public static AplicacaoLevelFlow Instance { get; private set; }

    [Tooltip("Nome da cena carregada quando o Player alcança o objetivo (precisa estar em Build Settings).")]
    [SerializeField] private string nextSceneName = "Interacao";

    [Tooltip("Segundos que o painel 'Fase Concluída!' fica visível antes de trocar de cena.")]
    [SerializeField] private float delayBeforeNextScene = 1.5f;

    [Header("Diálogo de intro (Prof Byte)")]
    [SerializeField] private DialogueManager dialogueManager;
    [Tooltip("Escondido até o diálogo de intro terminar, pra não distrair enquanto o Prof Byte explica a fase.")]
    [SerializeField] private GameObject inventoryPanel;

    [TextArea(2, 4)]
    [SerializeField]
    private string[] introDialogue =
    {
        "Hora de colocar em prática o que você montou no laboratório!",
        "Sua missão: atravessar o cenário até o objetivo final.",
        "Só que aqui nada tem Rigidbody2D nem Collider2D, nem você: por enquanto você nem anda nem pula.",
        "Olha o inventário, no canto superior esquerdo: arraste cada mecânica até quem vai recebê-la."
    };

    [Tooltip("Acrescentada ao fim da intro quando algum ícone do inventário tem usos limitados.")]
    [SerializeField] private string limitedUsesLine = "Cada mecânica tem um número de usos, marcado no ícone. Se ficar sem saída, segure {tecla} para recomeçar.";

    [Tooltip("Acrescentada ao fim da intro quando o inventário não tem limite de usos.")]
    [SerializeField] private string restartLine = "Se ficar sem saída, segure {tecla} para recomeçar.";

    [Header("Dica de queda do jogador (uma vez por fase)")]
    [Tooltip("Na queda de número X, mostra a dica abaixo antes de reiniciar.")]
    [SerializeField] private int fallHintThreshold = 2;

    [TextArea(2, 4)]
    [SerializeField]
    private string[] fallHintDialogue =
    {
        "Caiu de novo? Lembra: arraste os ícones do inventário até os objetos pra ligar Rigidbody2D, Collider2D, Movimento ou Pulo neles.",
        "Sem Collider2D embaixo dele, nada segura a queda."
    };

    [Header("Dica quando um objeto cai da fase (uma vez por fase)")]
    [TextArea(2, 4)]
    [SerializeField]
    private string[] objectFallHintDialogue =
    {
        "Opa, esse objeto caiu pra fora da fase. Com Rigidbody2D e sem Collider2D, ele atravessa tudo.",
        "A fase vai recomeçar. Dessa vez, pensa na ordem."
    };

    [Header("Dica quando Movimento/Pulo vão pra um objeto (uma vez por fase)")]
    [SerializeField] private string playerOnlyToolLine = "Movimento e Pulo só funcionam em você mesmo. Arraste até o seu personagem!";

    [SerializeField] private AplicacaoPlayerController player;
    [SerializeField] private LevelGoal goal;
    [SerializeField] private LevelCompleteUI completeUI;

    private bool levelEnded;
    private bool isReload;
    private bool awaitingRestart;
    private PhysicsTarget[] targets;

    // Sobrevivem ao recarregamento da própria fase e são zerados quando o
    // jogador chega nela vindo de outra cena — sem isso a contagem de quedas
    // voltava a 0 a cada queda e a dica nunca aparecia.
    private static bool reloading;
    private static int fallCount;
    private static bool shownObjectFallHint;
    private static bool shownPlayerOnlyHint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        reloading = false;
        ResetCounters();
    }

    private static void ResetCounters()
    {
        fallCount = 0;
        shownObjectFallHint = false;
        shownPlayerOnlyHint = false;
    }

    private void Awake()
    {
        Instance = this;

        isReload = reloading;
        reloading = false;
        if (!isReload)
        {
            ResetCounters();
        }

        if (player == null)
        {
            player = FindAnyObjectByType<AplicacaoPlayerController>();
        }

        if (goal == null)
        {
            goal = FindAnyObjectByType<LevelGoal>();
        }

        if (dialogueManager == null)
        {
            // Inclui inativos: o painel de diálogo pode já ter se escondido no Awake.
            dialogueManager = FindAnyObjectByType<DialogueManager>(FindObjectsInactive.Include);
        }

        // Painel "Fase Concluída!": a referência pode ter se perdido na cena (a Aplicacao_Fisica estava sem).
        if (completeUI == null)
        {
            completeUI = FindAnyObjectByType<LevelCompleteUI>(FindObjectsInactive.Include);
        }

        targets = FindObjectsByType<PhysicsTarget>(FindObjectsInactive.Exclude);

        if (goal == null)
        {
            Debug.LogWarning("[AplicacaoLevelFlow] Nenhum LevelGoal na cena — a fase não tem como ser concluída. Coloque LevelGoal (com Collider2D) no objeto de chegada.");
        }
    }

    private void Start()
    {
        // Sem diálogo configurado, ou recomeçando depois de uma queda: não
        // repete a intro, libera o inventário direto.
        if (dialogueManager == null || isReload)
        {
            AtualizarObjetivo();
            return;
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }

        dialogueManager.SetListener(this);
        dialogueManager.StartDialogue(BuildIntro());
    }

    private string[] BuildIntro()
    {
        bool limited = false;
        foreach (DraggableToolIcon icon in FindObjectsByType<DraggableToolIcon>(FindObjectsInactive.Include))
        {
            limited |= icon.TemLimite;
        }

        var lines = new List<string>(introDialogue);
        string extra = limited ? limitedUsesLine : restartLine;
        if (!string.IsNullOrEmpty(extra))
        {
            lines.Add(ReinicioFase.ComTecla(extra));
        }

        return lines.ToArray();
    }

    // Reiniciar fase (segurar a tecla da ação Restart ou o botão da pausa): ver ReinicioFase.
    public bool PodeReiniciar => !levelEnded && !awaitingRestart && !reloading;

    public void Reiniciar(string origem)
    {
        RegistroAprendizagem.RegistrarFalha("reinicio_manual", origem);
        ReloadScene();
    }

    /// <summary>Chamado pelo DialogueManager ao fim da intro ou de uma dica.</summary>
    public void OnDialogueFinished()
    {
        if (awaitingRestart)
        {
            ReloadScene();
            return;
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
        }

        AtualizarObjetivo();
    }

    // ---------------------------------------------------------------- objetivo e resumo

    private const int EtapaArrastar = 0, EtapaPersonagem = 1, EtapaObjetivo = 2;

    // Ferramenta -> onde foi aplicada (pro painel "Fase Concluída!").
    private readonly Dictionary<ToolType, int> aplicadasEmObjetos = new Dictionary<ToolType, int>();
    private readonly HashSet<ToolType> aplicadasNoJogador = new HashSet<ToolType>();
    private readonly List<ToolType> ordemAplicada = new List<ToolType>();

    /// <summary>Chamado pelo DraggableToolIcon quando uma ferramenta é aplicada de verdade.</summary>
    public void NotifyToolApplied(ToolType tool, IToolReceiver target)
    {
        if (!ordemAplicada.Contains(tool))
        {
            ordemAplicada.Add(tool);
        }

        if (ReferenceEquals(target, player))
        {
            aplicadasNoJogador.Add(tool);
        }
        else
        {
            aplicadasEmObjetos.TryGetValue(tool, out int n);
            aplicadasEmObjetos[tool] = n + 1;
        }

        AtualizarObjetivo();
    }

    private void AtualizarObjetivo()
    {
        if (player != null && player.HasTool(ToolType.Movimento) && player.HasTool(ToolType.Pulo))
        {
            ObjetivoFase.Ir(EtapaObjetivo);
        }
        else if (ordemAplicada.Count > 0)
        {
            ObjetivoFase.Ir(EtapaPersonagem);
        }
        else
        {
            ObjetivoFase.Ir(EtapaArrastar);
        }
    }

    private List<LevelCompleteUI.Item> ResumoDaFase()
    {
        var itens = new List<LevelCompleteUI.Item>();
        foreach (ToolType tool in ordemAplicada)
        {
            string nome, frase;
            switch (tool)
            {
                case ToolType.Fisica: nome = "Rigidbody2D"; frase = "gravidade"; break;
                case ToolType.Colisao: nome = "Collider2D"; frase = "corpo sólido"; break;
                case ToolType.Movimento: nome = "Movimento"; frase = "andar pros lados"; break;
                default: nome = "Pulo"; frase = "sair do chão"; break;
            }

            aplicadasEmObjetos.TryGetValue(tool, out int objetos);
            bool emVoce = aplicadasNoJogador.Contains(tool);
            string onde = emVoce && objetos > 0 ? $"em você e em {Objetos(objetos)}"
                : emVoce ? "em você" : "em " + Objetos(objetos);
            itens.Add(new LevelCompleteUI.Item(nome, frase + " · " + onde));
        }
        return itens;
    }

    private static string Objetos(int n) => n == 1 ? "1 objeto" : n + " objetos";

    private void OnEnable()
    {
        ReinicioFase.Registrar(this);

        if (player != null)
        {
            player.OnFellPastThreshold += HandlePlayerFell;
        }

        if (goal != null)
        {
            goal.OnPlayerReachedGoal += CompleteLevel;
        }

        if (targets != null)
        {
            foreach (PhysicsTarget target in targets)
            {
                target.OnFellOutOfLevel += HandleObjectFell;
            }
        }
    }

    private void OnDisable()
    {
        ReinicioFase.Remover(this);

        if (player != null)
        {
            player.OnFellPastThreshold -= HandlePlayerFell;
        }

        if (goal != null)
        {
            goal.OnPlayerReachedGoal -= CompleteLevel;
        }

        if (targets != null)
        {
            foreach (PhysicsTarget target in targets)
            {
                target.OnFellOutOfLevel -= HandleObjectFell;
            }
        }
    }

    // Jogador caiu (Rigidbody2D ligado sem Collider2D embaixo): reinicia. Na
    // queda de número fallHintThreshold, mostra uma dica antes.
    private void HandlePlayerFell()
    {
        if (levelEnded || awaitingRestart)
        {
            return;
        }

        fallCount++;
        RegistroAprendizagem.RegistrarFalha("queda", "jogador | " + fallCount + "ª");
        RestartWithOptionalHint(fallCount == fallHintThreshold ? fallHintDialogue : null);
    }

    // Um objeto ganhou Rigidbody2D sem Collider2D e caiu pra fora: sem ele a
    // fase pode ficar sem solução, então reinicia (com dica na primeira vez).
    private void HandleObjectFell(PhysicsTarget target)
    {
        if (levelEnded || awaitingRestart)
        {
            return;
        }

        RegistroAprendizagem.RegistrarFalha("objeto_caiu", target.name);

        string[] hint = null;
        if (!shownObjectFallHint)
        {
            shownObjectFallHint = true;
            hint = objectFallHintDialogue;
        }

        RestartWithOptionalHint(hint);
    }

    private void RestartWithOptionalHint(string[] hint)
    {
        if (dialogueManager != null && hint != null && hint.Length > 0)
        {
            awaitingRestart = true;
            dialogueManager.SetListener(this);
            dialogueManager.StartDialogue(hint);
            return;
        }

        ReloadScene();
    }

    /// <summary>Chamado pelo DraggableToolIcon quando o jogador solta Movimento/Pulo num objeto — avisa uma vez por fase.</summary>
    public void ShowToolNotAccepted(ToolType tool)
    {
        if (shownPlayerOnlyHint || dialogueManager == null || DialogueManager.IsAnyDialogueActive)
        {
            return;
        }

        if (tool != ToolType.Movimento && tool != ToolType.Pulo)
        {
            return;
        }

        shownPlayerOnlyHint = true;
        dialogueManager.SetListener(this);
        dialogueManager.StartDialogue(new[] { playerOnlyToolLine });
    }

    /// <summary>Reinicia a fase — pode ser ligado num botão "Reiniciar" da UI.</summary>
    public void ReiniciarFase()
    {
        if (levelEnded)
        {
            return;
        }

        RegistroAprendizagem.RegistrarFalha("reinicio_manual", "botão");
        ReloadScene();
    }

    private static void ReloadScene()
    {
        reloading = true;
        TrocaDeCena.Recarregar();
    }

    private void CompleteLevel()
    {
        if (levelEnded)
        {
            return;
        }

        levelEnded = true;
        Debug.Log("[AplicacaoLevelFlow] Objetivo alcançado — fase concluída.");
        RegistroAprendizagem.Registrar("fase_concluida", fallCount + " quedas");

        if (completeUI != null)
        {
            completeUI.Mostrar(ResumoDaFase(), false, LoadNextScene);
            return;
        }

        Invoke(nameof(LoadNextScene), delayBeforeNextScene);
    }

    private void LoadNextScene()
    {
        TrocaDeCena.Carregar(nextSceneName);
    }
}
