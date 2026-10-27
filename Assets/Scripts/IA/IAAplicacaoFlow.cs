using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Orquestra a fase "IA_Aplicacao": um corredor com um Guarda patrulhando
/// na frente da saída, uma plataforma alta onde o jogador pode se esconder
/// (ela bloqueia o Raycast do guarda) e um Aliado que segue o jogador.
///
/// Clicar no Guarda ou no Aliado (IASeletorNPC) abre o diagrama do cérebro
/// dele (IADiagramaUI). A solução é fazer o aliado DISTRAIR o guarda — o que
/// só funciona se o alvo do guarda incluir a tag Aliado — enquanto o
/// jogador passa escondido.
///
/// Condição errada não reseta nada: o comportamento fica errado e o jogador
/// vê o resultado. Se o guarda pega o jogador, ele perde um coração e volta
/// pro começo; depois de algumas pegas, o Prof dá uma dica que depende da
/// configuração atual. Vida zerada recarrega a fase mantendo as
/// configurações (estáticas). Tudo vai pro RegistroAprendizagem: cada
/// condição encaixada, cada configuração testada e cada pega.
/// </summary>
public class IAAplicacaoFlow : MonoBehaviour, IDialogueListener, IReinicioFase
{
    [Header("Referências")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private InteracaoPlayerController jogador;
    [SerializeField] private PlayerHealth vidaJogador;
    [SerializeField] private GuardaIA guarda;
    [SerializeField] private GuardaCerebroConfig guardaConfig;
    [SerializeField] private AliadoIA aliado;
    [SerializeField] private IASaida saida;
    [SerializeField] private IASeletorNPC seletor;
    [SerializeField] private LevelCompleteUI completeUI;

    [Header("Câmera (a intro mostra o jogador e o guarda juntos)")]
    [SerializeField] private CameraFollow cameraFollow;
    [Tooltip("Mesmos limites do CameraFollow.")]
    [SerializeField] private Vector2 cameraMin = new Vector2(5.9f, 3f);
    [SerializeField] private Vector2 cameraMax = new Vector2(18.1f, 3f);
    [SerializeField] private Vector2 cameraOffset = new Vector2(0f, 0.5f);

    [Header("Posições (voltar depois de ser pego)")]
    [SerializeField] private Transform pontoInicio;
    [SerializeField] private Transform pontoInicioAliado;

    [Header("Próxima cena")]
    [SerializeField] private string nextSceneName = "Questionario";
    [SerializeField] private float delayBeforeNextScene = 1.5f;
    [SerializeField] private float delayAntesDeRecarregar = 1f;

    [Header("Dicas")]
    [Tooltip("A cada tantas pegas, o Prof dá uma dica (baseada na configuração atual).")]
    [SerializeField] private int pegasPorDica = 2;
    [Tooltip("Máximo de dicas por partida.")]
    [SerializeField] private int maximoDeDicas = 3;

    [Header("Diálogos (editáveis, um texto por linha)")]
    [TextArea(2, 4)]
    [SerializeField]
    private string[] introDialogue =
    {
        "Último desafio: chegar até a saída, no fim do corredor. Só que tem um guarda patrulhando bem na frente dela.",
        "Esse robozinho verde é o seu aliado. O cérebro dele tem a mesma estrutura do guarda: estados e regras. Só que ainda está vazio.",
        "Clique no aliado ou no guarda pra abrir o cérebro deles. Arraste condições do inventário pras setas. O jogo pausa enquanto o cérebro está aberto.",
        "Uma coisa importante: o guarda só persegue quem ele vê e tem a tag escolhida no alvo dele.",
        "Se ficar sem saída, segure {tecla} para recomeçar."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] dicaAlvoGuarda =
    {
        "Repara no aliado: ele fica parado na frente do guarda, tentando chamar a atenção, e o guarda nem olha pra ele.",
        "Abra o cérebro do guarda e veja quem ele persegue."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] dicaSeguir =
    {
        "Seu aliado ficou pra trás. Ele só sai do lugar quando uma regra manda.",
        "Abra o cérebro dele: qual condição faz ele ir de Esperar pra Seguir?"
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] dicaDistrair =
    {
        "O guarda até correria atrás do aliado, mas o aliado nunca entra em Distrair.",
        "Pra distrair, ele precisa ver o guarda primeiro. Que condição faz isso?"
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] dicaEsconder =
    {
        "Os dois cérebros estão certos. Agora é esconder: suba na plataforma e fique no meio dela, onde a linha do guarda bate na plataforma.",
        "Quando o aliado levar o guarda embora, desça e corra pra saída."
    };

    private static bool reloading;
    private static int pegas;
    private static int dicasMostradas;
    private static readonly Dictionary<string, string[]> configSalva = new Dictionary<string, string[]>();
    private static readonly HashSet<string> configsTestadas = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        reloading = false;
        ZerarPartida();
    }

    private static void ZerarPartida()
    {
        pegas = 0;
        dicasMostradas = 0;
        configSalva.Clear();
        configsTestadas.Clear();
    }

    private bool isReload;
    private bool introAtiva;
    private bool levelEnded;
    private bool recarregando;
    private string ultimaConfigRegistrada;

    private void Awake()
    {
        isReload = reloading;
        reloading = false;
        if (!isReload)
        {
            ZerarPartida();
        }

        if (dialogueManager == null)
        {
            dialogueManager = FindAnyObjectByType<DialogueManager>(FindObjectsInactive.Include);
        }

        // Painel "Fase Concluída!": a referência pode ter se perdido na cena (a Aplicacao_Fisica estava sem).
        if (completeUI == null)
        {
            completeUI = FindAnyObjectByType<LevelCompleteUI>(FindObjectsInactive.Include);
        }

        if (jogador != null && vidaJogador == null)
        {
            vidaJogador = jogador.GetComponent<PlayerHealth>();
        }

        if (guarda != null && guardaConfig == null)
        {
            guardaConfig = guarda.GetComponent<GuardaCerebroConfig>();
        }
    }

    private void OnEnable()
    {
        ReinicioFase.Registrar(this);
        if (guarda != null) guarda.OnAtaque += HandleAtaque;
        if (vidaJogador != null) vidaJogador.OnDeath += HandleMorte;
        if (saida != null) saida.OnJogadorChegou += HandleSaida;
        IADiagramaUI.OnConfigAlterada += HandleConfigAlterada;
        IADiagramaUI.OnFechou += HandlePainelFechou;
    }

    private void OnDisable()
    {
        ReinicioFase.Remover(this);
        if (guarda != null) guarda.OnAtaque -= HandleAtaque;
        if (vidaJogador != null) vidaJogador.OnDeath -= HandleMorte;
        if (saida != null) saida.OnJogadorChegou -= HandleSaida;
        IADiagramaUI.OnConfigAlterada -= HandleConfigAlterada;
        IADiagramaUI.OnFechou -= HandlePainelFechou;
    }

    private void Start()
    {
        if (dialogueManager == null || jogador == null || guarda == null || aliado == null)
        {
            Debug.LogError("[IAAplicacaoFlow] Faltam referências (DialogueManager, Jogador, Guarda ou Aliado). Rode Tools/IA/Recriar Cena IA_Aplicacao.");
            return;
        }

        dialogueManager.SetListener(this);
        IAVisao.IgnorarColisoesEntre(jogador, guarda, aliado);

        if (isReload)
        {
            RestaurarConfiguracoes();
        }
        else
        {
            introAtiva = true;
            MostrarJogadorEGuarda();
            dialogueManager.StartDialogue(ReinicioFase.ComTecla(introDialogue));
        }

        ultimaConfigRegistrada = ResumoConfig();
        AtualizarObjetivo();
    }

    private void Update()
    {
        if (jogador == null || guarda == null || aliado == null)
        {
            return;
        }

        bool pausado = DialogueManager.IsAnyDialogueActive || IADiagramaUI.EstaAberto || levelEnded;
        guarda.Pausado = pausado;
        aliado.Pausado = pausado;
        jogador.enabled = !IADiagramaUI.EstaAberto && !levelEnded;

        if (seletor != null)
        {
            seletor.Ativo = !levelEnded && !DialogueManager.IsAnyDialogueActive;
        }

        if (levelEnded || recarregando || pausado)
        {
            return;
        }

        AtualizarObjetivo();
    }

    // Reiniciar fase (segurar a tecla da ação Restart ou o botão da pausa): ver ReinicioFase.
    public bool PodeReiniciar => !levelEnded && !recarregando;

    public void Reiniciar(string origem)
    {
        RegistroAprendizagem.RegistrarFalha("reinicio_manual", origem + " | " + ResumoConfig());
        RecarregarFase();
    }

    public void OnDialogueFinished()
    {
        // Intro e dicas só explicam; no fim da intro a câmera volta pro jogador.
        if (introAtiva)
        {
            introAtiva = false;
            VoltarCameraAoJogador();
        }
    }

    private void MostrarJogadorEGuarda()
    {
        if (cameraFollow == null)
        {
            return;
        }

        float meio = (jogador.transform.position.x + guarda.transform.position.x) * 0.5f;
        cameraFollow.FocarEm(new Vector2(Mathf.Clamp(meio, cameraMin.x, cameraMax.x), cameraMin.y));
    }

    private void VoltarCameraAoJogador()
    {
        if (cameraFollow == null)
        {
            return;
        }

        Vector3 p = jogador.transform.position;
        Transform cam = cameraFollow.transform;
        cam.position = new Vector3(
            Mathf.Clamp(p.x + cameraOffset.x, cameraMin.x, cameraMax.x),
            Mathf.Clamp(p.y + cameraOffset.y, cameraMin.y, cameraMax.y),
            cam.position.z);
        cameraFollow.Retomar();
    }

    // ------------------------------------------------------------------
    // Painel do cérebro: salva e registra as configurações
    // ------------------------------------------------------------------

    private void HandleConfigAlterada(ICerebroConfiguravel cerebro, int slot, string opcao)
    {
        SalvarConfiguracao(cerebro);
    }

    private void HandlePainelFechou(ICerebroConfiguravel cerebro)
    {
        string resumo = ResumoConfig();
        if (resumo == ultimaConfigRegistrada)
        {
            return;
        }

        ultimaConfigRegistrada = resumo;
        bool nova = configsTestadas.Add(resumo);
        RegistroAprendizagem.Registrar("ia_config_testada", (nova ? "nova | " : "repetida | ") + resumo);
    }

    private static void SalvarConfiguracao(ICerebroConfiguravel cerebro)
    {
        if (cerebro == null)
        {
            return;
        }

        var valores = new string[cerebro.QuantidadeDeSlots];
        for (int i = 0; i < valores.Length; i++)
        {
            valores[i] = cerebro.GetOpcaoDoSlot(i);
        }

        configSalva[cerebro.IdCerebro] = valores;
    }

    private void RestaurarConfiguracoes()
    {
        Restaurar(aliado);
        if (guardaConfig != null)
        {
            Restaurar(guardaConfig);
        }
    }

    private static void Restaurar(ICerebroConfiguravel cerebro)
    {
        if (!configSalva.TryGetValue(cerebro.IdCerebro, out string[] valores))
        {
            return;
        }

        for (int i = 0; i < valores.Length && i < cerebro.QuantidadeDeSlots; i++)
        {
            cerebro.SetOpcaoDoSlot(i, valores[i]);
        }
    }

    /// <summary>Configuração atual dos dois cérebros, legível no CSV.</summary>
    private string ResumoConfig()
    {
        var sb = new StringBuilder();
        AppendCerebro(sb, aliado);
        if (guardaConfig != null)
        {
            sb.Append(" || ");
            AppendCerebro(sb, guardaConfig);
        }

        return sb.ToString();
    }

    private static void AppendCerebro(StringBuilder sb, ICerebroConfiguravel cerebro)
    {
        if (cerebro == null)
        {
            return;
        }

        IADiagrama d = cerebro.Diagrama;
        sb.Append(cerebro.IdCerebro).Append(": ");
        for (int i = 0; i < cerebro.QuantidadeDeSlots; i++)
        {
            if (i > 0) sb.Append("; ");
            string legenda = i < d.slots.Count ? d.slots[i].legenda : i.ToString();
            IAOpcao opcao = d.BuscarOpcao(cerebro.GetOpcaoDoSlot(i));
            sb.Append(legenda).Append(" = ").Append(opcao != null ? opcao.rotulo : "(vazio)");
        }
    }

    // ------------------------------------------------------------------
    // Guarda pegou / dicas / morte / saída
    // ------------------------------------------------------------------

    private void HandleAtaque(Transform alvo)
    {
        if (levelEnded || alvo == null || !alvo.IsChildOf(jogador.transform))
        {
            return;
        }

        pegas++;
        RegistroAprendizagem.RegistrarFalha("guarda_pegou", $"{pegas}ª | {ResumoConfig()}");

        if (vidaJogador != null && vidaJogador.IsDead)
        {
            return;
        }

        VoltarAoInicio();

        if (pegasPorDica > 0 && pegas % pegasPorDica == 0 && dicasMostradas < maximoDeDicas)
        {
            MostrarDica();
        }
    }

    private void VoltarAoInicio()
    {
        Teleportar(jogador.transform, pontoInicio);
        Teleportar(aliado.transform, pontoInicioAliado);
        aliado.Reiniciar();
        guarda.EsquecerAlvo();
    }

    private static void Teleportar(Transform quem, Transform ponto)
    {
        if (quem == null || ponto == null)
        {
            return;
        }

        Rigidbody2D rb = quem.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = ponto.position;
        }

        quem.position = ponto.position;
    }

    // A dica depende do que está errado agora, na ordem em que o problema aparece.
    private void MostrarDica()
    {
        string[] dica;
        string tipo;

        if (!aliado.SegueCorretamente)
        {
            dica = dicaSeguir;
            tipo = "aliado_seguir";
        }
        else if (!aliado.DistracaoCorreta)
        {
            dica = dicaDistrair;
            tipo = "aliado_distrair";
        }
        else if (guardaConfig != null && !guardaConfig.IncluiAliado)
        {
            dica = dicaAlvoGuarda;
            tipo = "alvo_guarda";
        }
        else
        {
            dica = dicaEsconder;
            tipo = "esconder";
        }

        dicasMostradas++;
        RegistroAprendizagem.Registrar("dica", $"{tipo} | {pegas} pegas");
        dialogueManager.SetListener(this);
        dialogueManager.StartDialogue(dica);
    }

    private void HandleMorte()
    {
        if (recarregando || levelEnded)
        {
            return;
        }

        recarregando = true;
        RegistroAprendizagem.RegistrarFalha("morte", $"guarda | {ResumoConfig()}");
        Invoke(nameof(MostrarGameOver), delayAntesDeRecarregar);
    }

    // Game Over do tema Laboratório; "Reiniciar" recarrega como antes,
    // mantendo as condições encaixadas.
    private void MostrarGameOver()
    {
        IADiagramaUI.Fechar();
        GameOverUI.Mostrar(configSalva.Count > 0 ? "As regras que você encaixou continuam valendo." : "", RecarregarFase);
    }

    private void RecarregarFase()
    {
        recarregando = true;
        IADiagramaUI.Fechar();
        reloading = true;
        TrocaDeCena.Recarregar();
    }

    private void HandleSaida()
    {
        if (levelEnded)
        {
            return;
        }

        levelEnded = true;
        IADiagramaUI.Fechar();
        RegistroAprendizagem.Registrar("fase_concluida", $"{pegas} pegas | {configsTestadas.Count} configs testadas | {ResumoConfig()}");

        if (completeUI != null)
        {
            completeUI.Mostrar(ResumoDaFase(), false, CarregarProximaCena);
            return;
        }

        Invoke(nameof(CarregarProximaCena), delayBeforeNextScene);
    }

    // ------------------------------------------------------------------
    // Faixa de objetivo e resumo
    // ------------------------------------------------------------------

    private const int EtapaSeguir = 0, EtapaDistrair = 1, EtapaAlvoGuarda = 2, EtapaEsconder = 3;

    // Mesma ordem das dicas (ver MostrarDica): a etapa é a primeira coisa que ainda falta.
    private void AtualizarObjetivo()
    {
        if (!aliado.SegueCorretamente) ObjetivoFase.Ir(EtapaSeguir);
        else if (!aliado.DistracaoCorreta) ObjetivoFase.Ir(EtapaDistrair);
        else if (guardaConfig != null && !guardaConfig.IncluiAliado) ObjetivoFase.Ir(EtapaAlvoGuarda);
        else ObjetivoFase.Ir(EtapaEsconder);
    }

    private List<LevelCompleteUI.Item> ResumoDaFase()
    {
        var itens = new List<LevelCompleteUI.Item>();
        if (aliado.SegueCorretamente) itens.Add(new LevelCompleteUI.Item("Aliado: Seguir", "a regra que tira ele de Esperar"));
        if (aliado.DistracaoCorreta) itens.Add(new LevelCompleteUI.Item("Aliado: Distrair", "ele vê o guarda e chama a atenção"));
        if (guardaConfig != null && guardaConfig.IncluiAliado) itens.Add(new LevelCompleteUI.Item("Guarda: alvo", "a tag Aliado entra no que ele persegue"));
        itens.Add(new LevelCompleteUI.Item("Configurações testadas", configsTestadas.Count == 1 ? "1 combinação de regras" : configsTestadas.Count + " combinações de regras"));
        return itens;
    }

    private void CarregarProximaCena()
    {
        TrocaDeCena.Carregar(nextSceneName);
    }
}
