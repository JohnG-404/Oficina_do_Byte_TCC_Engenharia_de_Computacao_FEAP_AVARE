using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orquestra a fase "Interacao": mostra o diálogo de intro do Prof Byte
/// explicando o assistente de Ação+Consequência, e conclui a fase quando o
/// item de objetivo é ativado (ver InteractableItem/ConsequenceType.
/// CompleteLevel). Reaproveita o mesmo LevelCompleteUI usado na fase
/// Aplicacao (painel genérico "Fase Concluída!", sem nada específico desta
/// cena).
///
/// Diferente de Aplicacao/Fisica, aqui não há nada pra "esconder" até o
/// diálogo terminar — o jogador já anda e pula desde o início (ver
/// InteracaoPlayerController) e pode continuar se movendo enquanto lê.
/// </summary>
public class InteracaoLevelFlow : MonoBehaviour, IDialogueListener, IReinicioFase, ILembreteReinicio
{
    public static InteracaoLevelFlow Instance { get; private set; }

    [Tooltip("Nome da cena carregada ao concluir a fase (precisa estar em Build Settings).")]
    [SerializeField] private string nextSceneName = "Menu";

    [Tooltip("Só usado sem o cartão 'Fase Concluída!' na cena (o cartão espera Enter).")]
    [SerializeField] private float delayBeforeNextScene = 1.5f;

    [Header("Diálogo de intro (Prof Byte)")]
    [SerializeField] private DialogueManager dialogueManager;

    [TextArea(2, 4)]
    [SerializeField]
    private string[] introDialogue =
    {
        "Esta sala está cheia de objetos, mas nenhum faz nada. Ainda.",
        "Muita coisa num jogo funciona assim: algo detecta você (um collider) e responde com uma consequência. Aqui, quem decide isso é você.",
        "Clique num objeto e configure duas coisas: a AÇÃO, como você interage (apertar um botão, chegar perto, ter pegado um cartão...), e a CONSEQUÊNCIA, o que acontece (abrir uma porta, ativar uma plataforma...).",
        "Explore a sala, teste combinações e descubra qual delas abre o caminho até a saída."
    };

    [Header("Reações rápidas (aparecem uma vez cada, no painel de dica)")]
    [SerializeField] private string firstConfigHint = "Boa! Agora vai lá testar.";
    [SerializeField] private string firstDoorHint = "Show! Essa combinação funcionou.";
    [SerializeField] private string firstRestartHint = "Não foi dessa vez. Vamos tentar de novo!";
    [Tooltip("Só se aplica em fases com vida (ver PlayerHealth) — mostrado na primeira vez que o jogador toma dano.")]
    [SerializeField] private string firstDamageHint = "Ai! Cada armadilha tira vida. Fica de olho nos quadrados.";
    [Tooltip("Só se aplica em fases com vida — mostrado na primeira vez que o jogador é curado.")]
    [SerializeField] private string firstHealHint = "Vida recuperada!";
    [Tooltip("Segundos de espera depois da dica de reinício, antes de recarregar a fase.")]
    [SerializeField] private float restartHintDelay = 0.8f;

    [SerializeField] private LevelCompleteUI completeUI;

    [Tooltip("Opcional — se a fase tiver vida (ver PlayerHealth), reinicia sozinha ao chegar a 0. Auto-encontrado se deixar vazio.")]
    [SerializeField] private PlayerHealth playerHealth;

    [Tooltip("Opcional — só na Interacao_Vida: se houver um GameOverUI, a morte mostra o painel 'Game Over' em vez de recarregar direto. Auto-encontrado se deixar vazio; sem ele (fase Interacao), recarrega como sempre.")]
    [SerializeField] private GameOverUI gameOverUI;

    /// <summary>Quantos cartões de acesso o jogador já coletou (ver ConsequenceType.CollectCard/ActionType.RequiresOneCard/RequiresTwoCards).</summary>
    public int CollectedCards { get; private set; }

    /// <summary>True se esta carga da cena é um reinício da própria fase (morte/Game Over/consequência "Reiniciar"), não uma chegada vinda de outra cena.</summary>
    public bool IsReload => isReload;

    private bool levelEnded;
    private bool isReload;
    private bool recarregando;
    private int lastKnownHealth = -1;

    // As dicas rápidas aparecem uma vez por fase — não uma vez a cada
    // reinício. Por isso sobrevivem ao recarregamento da própria cena e só
    // são zeradas quando o jogador chega nela vindo de outra cena.
    private static bool reloading;
    private static bool hasShownConfigHint;
    private static bool hasShownDoorHint;
    private static bool hasShownRestartHint;
    private static bool hasShownDamageHint;
    private static bool hasShownHealHint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        reloading = false;
        ResetHints();
    }

    private static void ResetHints()
    {
        hasShownConfigHint = false;
        hasShownDoorHint = false;
        hasShownRestartHint = false;
        hasShownDamageHint = false;
        hasShownHealHint = false;
    }

    private void Awake()
    {
        Instance = this;

        isReload = reloading;
        reloading = false;
        if (!isReload)
        {
            ResetHints();
        }

        ReinicioFase.Registrar(this);

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

        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        if (gameOverUI == null)
        {
            gameOverUI = FindAnyObjectByType<GameOverUI>();
        }
    }

    private void Start()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDeath += HandleDeath;
            playerHealth.OnHealthChanged += HandleHealthChanged;
        }

        ObjetivoFase.Ir(EtapaConfigurar);

        // Depois de um reinício a fase recomeça direto — a intro só aparece
        // na primeira vez.
        if (dialogueManager == null || isReload)
        {
            return;
        }

        dialogueManager.SetListener(this);
        dialogueManager.StartDialogue(introDialogue);
    }

    private void OnDestroy()
    {
        ReinicioFase.Remover(this);

        if (playerHealth != null)
        {
            playerHealth.OnDeath -= HandleDeath;
            playerHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }

    private void HandleDeath()
    {
        RegistroAprendizagem.RegistrarFalha("morte", "vida zerou");

        // Interacao_Vida: estado Game Over (o próprio painel recarrega via
        // ReloadAfterGameOver). Sem GameOverUI na cena: reinício direto.
        if (gameOverUI != null)
        {
            gameOverUI.Show();
            return;
        }

        Restart();
    }

    // Só reage a partir da segunda notificação: a primeira é o valor inicial
    // de vida (disparado pelo próprio PlayerHealth.Start()), não um dano/cura
    // de verdade. Cada dica aparece só uma vez por fase.
    private void HandleHealthChanged(int current, int max)
    {
        if (lastKnownHealth < 0)
        {
            lastKnownHealth = current;
            return;
        }

        if (current < lastKnownHealth)
        {
            RegistroAprendizagem.Registrar("dano", $"{lastKnownHealth} -> {current}");
            if (!hasShownDamageHint)
            {
                hasShownDamageHint = true;
                InteractionConfigUI.Instance?.ShowHint(firstDamageHint);
            }
        }
        else if (current > lastKnownHealth)
        {
            RegistroAprendizagem.Registrar("cura", $"{lastKnownHealth} -> {current}");
            if (!hasShownHealHint)
            {
                hasShownHealHint = true;
                InteractionConfigUI.Instance?.ShowHint(firstHealHint);
            }
        }

        lastKnownHealth = current;
    }

    /// <summary>Chamado pelo DialogueManager quando o jogador termina de ler o diálogo de intro.</summary>
    public void OnDialogueFinished()
    {
        // Nada pra liberar aqui além da leitura em si — movimento/pulo e os
        // itens da sala já estão disponíveis desde o início.
    }

    /// <summary>Chamado pelo InteractableItem com a consequência CollectCard.</summary>
    public void CollectCard()
    {
        CollectedCards++;
        AudioManager.TocarEfeito(AudioManager.Efeito.Cartao);
    }

    /// <summary>Chamado pelo InteractableItem assim que o jogador termina de configurar QUALQUER item — a dica só aparece na primeira vez.</summary>
    public void NotifyItemConfigured(InteractableItem item)
    {
        RegistroAprendizagem.Registrar("item_configurado", $"{item.DisplayName} | {item.ChosenAction} | {item.ChosenConsequence} | {(item.ChosenRepeats ? "Sempre" : "UmaVez")}");

        if (ObjetivoFase.Instance != null && ObjetivoFase.Instance.EtapaAtual < EtapaTestar)
        {
            ObjetivoFase.Ir(EtapaTestar);
        }

        if (hasShownConfigHint)
        {
            return;
        }

        hasShownConfigHint = true;
        InteractionConfigUI.Instance?.ShowHint(firstConfigHint);
    }

    /// <summary>Chamado pela Door assim que ela abre — só reage na primeira vez.</summary>
    public void NotifyDoorOpened()
    {
        ObjetivoFase.Ir(EtapaSaida);

        if (hasShownDoorHint)
        {
            return;
        }

        hasShownDoorHint = true;
        InteractionConfigUI.Instance?.ShowHint(firstDoorHint);
    }

    /// <summary>Recarrega a fase do zero (ver ConsequenceType.RestartLevel). Na primeira vez, mostra uma dica antes de recarregar.</summary>
    public void RestartLevel()
    {
        RegistroAprendizagem.RegistrarFalha("reinicio", "consequência 'Reiniciar a fase'");
        Restart();
    }

    private void Restart()
    {
        if (!hasShownRestartHint && InteractionConfigUI.Instance != null)
        {
            hasShownRestartHint = true;
            InteractionConfigUI.Instance.ShowHint(firstRestartHint);
            Invoke(nameof(ReloadScene), restartHintDelay);
            return;
        }

        ReloadScene();
    }

    /// <summary>Chamado pelo GameOverUI ao clicar em "Reiniciar": recarrega a fase como um reinício (a intro não repete).</summary>
    public void ReloadAfterGameOver()
    {
        ReloadScene();
    }

    /// <summary>
    /// Marca a dica do primeiro dano como já mostrada — usado pela mini-lição
    /// de HUD (ver HudLicao), que ocupa o lugar dessa dica na Interacao_Vida.
    /// </summary>
    public void SuppressDamageHint()
    {
        hasShownDamageHint = true;
    }

    private void ReloadScene()
    {
        reloading = true;
        recarregando = true;
        TrocaDeCena.Recarregar();
    }

    // "Reiniciar fase" volta como depois do
    // Game Over: sem a intro e com a lição da HUD feita. Os itens voltam sem
    // configuração, como numa morte.
    public bool PodeReiniciar => !levelEnded && !recarregando && (playerHealth == null || !playerHealth.IsDead);

    public void Reiniciar(string origem)
    {
        RegistroAprendizagem.RegistrarFalha("reinicio_manual", origem);
        ReloadScene();
    }

    /// <summary>Linha verde da confirmação de reinício (só na Interacao_Vida, depois da lição da HUD).</summary>
    public string LembreteReinicio =>
        HudLicao.LicaoConcluida && FindAnyObjectByType<HudLicao>() != null ? "Continua feita: a lição da HUD." : "";

    public void CompleteLevel()
    {
        if (levelEnded)
        {
            return;
        }

        levelEnded = true;
        Debug.Log("[InteracaoLevelFlow] Fase concluída.");
        RegistroAprendizagem.Registrar("fase_concluida");

        if (completeUI != null)
        {
            completeUI.Mostrar(ResumoDaFase(), false, LoadNextScene);
            return;
        }

        Invoke(nameof(LoadNextScene), delayBeforeNextScene);
    }

    // ---------------------------------------------------------------- objetivo e resumo

    private const int EtapaConfigurar = 0, EtapaTestar = 1, EtapaCaminho = 2, EtapaSaida = 3;

    private float proximaChecagem;

    // Etapa "teste a combinação" -> "abra o caminho": algum item configurado
    // já disparou. Checado de tempos em tempos (são poucos itens).
    private void Update()
    {
        if (levelEnded || Time.time < proximaChecagem || ObjetivoFase.Instance == null
            || ObjetivoFase.Instance.EtapaAtual != EtapaTestar)
        {
            return;
        }

        proximaChecagem = Time.time + 0.4f;
        foreach (InteractableItem item in FindObjectsByType<InteractableItem>(FindObjectsSortMode.None))
        {
            if (item.IsConfigured && item.WasActivated)
            {
                ObjetivoFase.Ir(EtapaCaminho);
                return;
            }
        }
    }

    // As combinações que estão valendo no fim da fase, pro painel "Fase Concluída!".
    private List<LevelCompleteUI.Item> ResumoDaFase()
    {
        var itens = new List<LevelCompleteUI.Item>();
        if (HudLicao.LicaoConcluida)
        {
            itens.Add(new LevelCompleteUI.Item("HUD de vida", "OnHealthChanged avisa a barra quando a vida muda"));
        }

        InteractableItem[] todos = FindObjectsByType<InteractableItem>(FindObjectsSortMode.None);
        System.Array.Sort(todos, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        foreach (InteractableItem item in todos)
        {
            if (!item.IsConfigured)
            {
                continue;
            }

            string unity = InteractionConfigUI.EquivalenteUnity(item.ChosenAction);
            itens.Add(new LevelCompleteUI.Item(
                InteractionConfigUI.NomeAcao(item.ChosenAction) + " → " + InteractionConfigUI.NomeConsequencia(item.ChosenConsequence),
                string.IsNullOrEmpty(unity) ? item.DisplayName : item.DisplayName + " · " + unity));
        }
        return itens;
    }

    private void LoadNextScene()
    {
        TrocaDeCena.Carregar(nextSceneName);
    }
}
