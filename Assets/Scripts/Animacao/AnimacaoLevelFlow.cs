using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orquestra a fase "Animacao" (versão Animator):
///
/// 1. Intro do Prof Byte: o personagem agora anda com a física montada na
///    fase Fisica (AnimacaoPlayerFisica).
/// 2. Falha proposital: ele anda e pula, mas o desenho fica congelado no
///    Idle (o AnimatorController tem as transições, só que todas exigem
///    regrasMontadas == true, e isso ainda está false). Depois de alguns
///    segundos andando e um pulo, o Prof explica que falta alguém decidir
///    QUAL animação tocar: o Animator. Se o jogador fica parado (ou não pula)
///    até o tempo máximo, o Prof só lembra o que falta e continua esperando.
/// 3. Puzzle do Animator (AnimatorPuzzleUI): encaixar as condições nas setas.
/// 4. CodigoMontadoUI com o resumo (parâmetros + transições); a partir daí
///    regrasMontadas = true, o personagem anima sozinho e o painel de canto
///    (AnimatorEstadoHUD) acende o estado ativo em tempo real.
/// 5. Resto da fase: subir a escada e pular a vala com espinhos.
/// 6. Túnel (AnimacaoGatilhoTunel na entrada): o Prof mostra que a passagem é
///    baixa e pede pro jogador segurar a tecla de agachar. O corpo (collider)
///    abaixa, mas o desenho fica em pé, porque o Animator ainda não recebe o
///    parâmetro agachado (AnimacaoPlayerFisica.AgacharMontado). Mini-puzzle de
///    2 setas (o mesmo AnimatorPuzzleUI, em modo agachar), resumo do código,
///    e só então aparece a dica da tecla no túnel.
/// 7. Atravessar o túnel e chegar à porta no fim do caminho (PortaSaida +
///    AnimacaoObjetivo no vão).
///
/// Morte (espinhos, ver AnimacaoPerigo) recarrega a fase com a flag
/// estática "reloading": a intro não repete, e se o Animator já foi montado
/// o puzzle também não (o personagem já volta animando). Morrer antes da
/// explicação do Animator não mostra a dica dos espinhos: a fase volta e o
/// Prof explica o Animator na hora.
/// </summary>
public class AnimacaoLevelFlow : MonoBehaviour, IDialogueListener, IReinicioFase, ILembreteReinicio
{
    private enum Etapa
    {
        Intro,
        SemAnimacao,
        ExplicandoAnimator,
        Puzzle,
        CodigoMontado,
        PosPuzzle,
        Livre,
        TunelAviso,
        TesteAgachar,
        ExplicandoAgachar,
        PuzzleAgachar,
        CodigoAgachar,
        PosAgachar,
        Concluida,
        PainelObjeto
    }

    public static AnimacaoLevelFlow Instance { get; private set; }

    [Tooltip("Nome da cena carregada ao concluir a fase (precisa estar em Build Settings).")]
    [SerializeField] private string nextSceneName = "Questionario";

    [Tooltip("Só usado sem o cartão 'Fase Concluída!' na cena (o cartão espera Enter).")]
    [SerializeField] private float delayBeforeNextScene = 1.5f;

    [Header("Referências (auto-encontradas se vazias)")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private AnimacaoPlayerFisica player;
    [SerializeField] private AnimatorPuzzleUI puzzle;
    [SerializeField] private LevelCompleteUI completeUI;

    [Tooltip("Opcional — reinicia a fase sozinha ao chegar a 0. Auto-encontrado se deixar vazio.")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Diálogo de intro (Prof Byte)")]
    [TextArea(2, 4)]
    [SerializeField] private string[] introDialogue = (string[])IntroPadrao.Clone();

    [Header("Falha proposital: anda sem animar")]
    [Tooltip("Segundos andando (no chão, com velocidade) antes do Prof comentar.")]
    [SerializeField] private float segundosAndandoParaFalha = 1.5f;
    [Tooltip("Se marcado, também espera o jogador pular pelo menos uma vez.")]
    [SerializeField] private bool exigirPuloParaFalha = true;
    [Tooltip("Sem as condições acima depois deste tempo, o Prof lembra o que falta (andar ou pular) e volta a esperar.")]
    [SerializeField] private float tempoMaximoFalha = 12f;

    [TextArea(2, 4)]
    [SerializeField] private string[] falhaDialogue = (string[])FalhaPadrao.Clone();
    [Tooltip("Lembrete quando o jogador ainda não andou.")]
    [TextArea(2, 4)]
    [SerializeField] private string[] lembreteAndarDialogue = (string[])LembreteAndarPadrao.Clone();
    [Tooltip("Lembrete quando o jogador já andou, mas ainda não pulou.")]
    [TextArea(2, 4)]
    [SerializeField] private string[] lembretePularDialogue = (string[])LembretePularPadrao.Clone();

    [Header("Depois do puzzle")]
    [SerializeField] private string tituloCodigo = "Animator montado: parâmetros + transições";
    [TextArea(4, 12)]
    [SerializeField] private string codigoMontado = CodigoPadrao;
    [Tooltip("Não fecha mais o painel: ele fica aberto até Enter ou clique (ver CodigoMontadoUI).")]
    [SerializeField] private float segundosCodigo = 16f;
    [SerializeField] private string tituloPainelEstado = "Animator do Player";

    [TextArea(2, 4)]
    [SerializeField] private string[] posPuzzleDialogue = (string[])PosPuzzlePadrao.Clone();

    [Header("Túnel: apresentar o agachar")]
    [Tooltip("Objeto AgacharPuzzle da cena (AnimatorPuzzleUI com Modo Agachar ligado).")]
    [SerializeField] private AnimatorPuzzleUI puzzleAgachar;
    [Tooltip("Dica da tecla de agachar no túnel (DicaTecla). Fica desligada até o mini-puzzle terminar.")]
    [SerializeField] private GameObject dicaAgachar;
    [Tooltip("Segundos segurando a tecla de agachar antes do Prof comentar.")]
    [SerializeField] private float segundosTesteAgachar = 0.8f;
    [Tooltip("Se o jogador não apertar a tecla, o Prof comenta depois deste tempo.")]
    [SerializeField] private float tempoMaximoTesteAgachar = 8f;
    [Tooltip("Contorno do collider do player (ContornoCollider, filho do Player). Aparece no teste do agachar, já que o collider é invisível no jogo.")]
    [SerializeField] private GameObject contornoCollider;

    [Tooltip("{C} vira a tecla de agachar configurada.")]
    [TextArea(2, 4)]
    [SerializeField] private string[] tunelDialogue = (string[])TunelPadrao.Clone();
    [TextArea(2, 4)]
    [SerializeField] private string[] falhaAgacharDialogue = (string[])FalhaAgacharPadrao.Clone();
    [SerializeField] private string tituloCodigoAgachar = "Animator: estados de agachar";
    [TextArea(4, 12)]
    [SerializeField] private string codigoAgachar = CodigoAgacharPadrao;
    [Tooltip("Não fecha mais o painel: ele fica aberto até Enter ou clique (ver CodigoMontadoUI).")]
    [SerializeField] private float segundosCodigoAgachar = 14f;
    [TextArea(2, 4)]
    [SerializeField] private string[] posAgacharDialogue = (string[])PosAgacharPadrao.Clone();

    [Header("Dica ao morrer (mostrada só nas primeiras vezes)")]
    [SerializeField] private int maxDeathHints = 2;

    [TextArea(2, 4)]
    [SerializeField]
    private string[] deathHintDialogue = (string[])DicaMortePadrao.Clone();

    // ---------------------------------------------------------------- textos padrão
    // Públicos pro AnimacaoSceneBuilder conseguir trocar os textos antigos
    // (da versão "arraste a animação") que ficaram salvos na cena.

    public static readonly string[] IntroPadrao =
    {
        "Lembra da física que você montou? Agora o personagem anda com ela: setas ou {andar} pra andar, {pular} pra pular.",
        "Até aqui o personagem já chegava animado em todas as fases. Nesta sala a gente vai ver o que faz isso funcionar.",
        "Dá uma volta por aí e pula um pouco. Presta atenção no desenho do personagem enquanto ele se mexe."
    };

    public static readonly string[] FalhaPadrao =
    {
        "Percebeu? Ele anda e pula, mas o desenho fica parado no mesmo quadro. Parece que está deslizando.",
        "As animações de corrida e de pulo existem. Falta alguém decidir QUAL delas tocar em cada momento.",
        "No Unity, quem faz isso é o Animator. Ele é uma máquina de estados: cada caixa é uma animação (Idle, Corrida, Pulo) e cada seta é uma regra pra trocar de uma pra outra.",
        "Quem move o personagem continua sendo a física. O Animator não vê a física direto: todo quadro o script copia a velocidade pro parâmetro velocidadeX e o isGrounded pro parâmetro noChao, e as setas leem esses parâmetros.",
        "Encaixe em cada seta a condição que faz a troca acontecer. A caixa Any State vale pra qualquer estado, igual no Unity."
    };

    public static readonly string[] LembreteAndarPadrao =
    {
        "Ainda parado? Anda com as setas ou {andar} e pula com {pular}. Fica de olho no desenho do personagem enquanto ele se mexe."
    };

    public static readonly string[] LembretePularPadrao =
    {
        "Agora aperta {pular} pra dar um pulo. Repara no desenho enquanto ele está no ar."
    };

    public static readonly string[] PosPuzzlePadrao =
    {
        "Agora a animação troca sozinha! Ninguém manda tocar a corrida: o Animator olha a física e escolhe.",
        "No Animator do Unity é assim: o script atualiza os parâmetros (velocidadeX e noChao) todo quadro, e as transições conferem esses parâmetros.",
        "Repara no painel do canto: a caixa acesa é o estado em que ele está agora. Guarda essa ideia de estados e setas, que a próxima fase usa de novo.",
        "Agora leve o personagem até a porta no fim do caminho. Os espinhos pedem um pulo lá do alto da escada."
    };

    public static readonly string[] TunelPadrao =
    {
        "Opa, uma passagem baixa! Em pé o personagem não cabe aí.",
        "O código de agachar já está pronto: segurando {C}, o corpo (o collider) fica mais baixo.",
        "Só que o collider é invisível no jogo. Pra você enxergar, eu acendi o contorno dele em verde. Segure {C} e compare o contorno com o desenho."
    };

    public static readonly string[] FalhaAgacharPadrao =
    {
        "Viu? O contorno verde (o collider) abaixou, mas o desenho continua em pé.",
        "Num jogo de verdade ninguém vê o collider, só o desenho. Sem a animação, o personagem entraria no túnel em pé, com a cabeça atravessando o teto.",
        "É a mesma falha do começo: o código mexe na física, mas o Animator não tem nenhuma seta que leve pro estado Agachado.",
        "Vamos usar um parâmetro novo, o Bool agachado, que o script liga enquanto a tecla está apertada. Encaixe as duas setas que faltam."
    };

    public static readonly string[] PosAgacharPadrao =
    {
        "Agora sim! O script liga o agachado e o Animator troca o desenho sozinho. Com o desenho certo, o contorno verde não precisa mais aparecer.",
        "Segure {C} pra passar pelo túnel. Andando agachado, ele vai mais devagar. Depois é só seguir até a porta."
    };

    public const string CodigoAgacharPadrao =
        "<color=#8B95A7>// Player: o script também passa o agachar pro Animator</color>\n" +
        "animator.SetBool(<color=#F5C542>\"agachado\"</color>, agachar.Agachado);\n" +
        "<color=#8B95A7>// Transições novas (as setas que você montou)</color>\n" +
        "Idle -> Agachado:  <color=#F5C542>agachado == true</color>\n" +
        "Agachado -> AndarAgachado:  <color=#F5C542><noparse>velocidadeX > 0.1</noparse></color>\n" +
        "<color=#8B95A7>// E as de volta, que já vêm prontas</color>\n" +
        "Agachado -> Idle:  <color=#F5C542>agachado == false</color>\n" +
        "AndarAgachado -> Agachado:  <color=#F5C542><noparse>velocidadeX < 0.1</noparse></color>";

    public static readonly string[] DicaMortePadrao =
    {
        "Faltou distância! Pule lá do alto da escada e segure {pular} pra ir mais longe. Olha o painel: no ar, o Animator troca pra Pulo sozinho."
    };

    // "<" solto quebraria o rich text do TMP; por isso as comparações com
    // menor ficam dentro de <noparse>.
    public const string CodigoPadrao =
        "<color=#8B95A7>// Player: todo quadro o script passa a física pro Animator</color>\n" +
        "animator.SetFloat(<color=#F5C542>\"velocidadeX\"</color>, Mathf.Abs(rb.linearVelocity.x));\n" +
        "animator.SetBool(<color=#F5C542>\"noChao\"</color>, isGrounded);\n" +
        "<color=#8B95A7>// Transições (as setas que você montou)</color>\n" +
        "Idle -> Corrida:  <color=#F5C542><noparse>velocidadeX > 0.1</noparse></color>\n" +
        "Corrida -> Idle:  <color=#F5C542><noparse>velocidadeX < 0.1</noparse></color>\n" +
        "Any State -> Pulo:  <color=#F5C542>noChao == false</color>\n" +
        "<color=#8B95A7>// Can Transition To Self desligado: no ar, o Pulo não recomeça</color>\n" +
        "Pulo -> Idle:  <color=#F5C542>noChao == true</color>";

    // ---------------------------------------------------------------- estado

    /// <summary>Nome do último perigo que matou o jogador (preenchido por AnimacaoPerigo, vai pro registro).</summary>
    public string UltimoPerigo { get; set; } = "espinho";

    /// <summary>A fase só termina depois do puzzle do Animator.</summary>
    public bool PodeConcluir => animatorMontado && !levelEnded && (etapa == Etapa.Livre || etapa == Etapa.PosPuzzle || etapa == Etapa.PosAgachar);

    private Etapa etapa = Etapa.Intro;
    private bool levelEnded;
    // Estática (como o resto do progresso abaixo): depois de uma morte ou de um
    // reinício o Prof não repete o que já falou sobre os objetos.
    private static readonly System.Collections.Generic.HashSet<string> falasDeObjetoDitas = new System.Collections.Generic.HashSet<string>();
    private bool awaitingDeathHintRestart;
    private bool isReload;
    private AnimatorEstadoHUD hud;

    // Etapa "sem animação"
    private float tempoNaEtapa;
    private float tempoAndando;
    private float tempoParadoNoChao;
    private int pulosNoInicio;

    // Sobrevivem ao recarregamento da própria fase (morte) e são zerados
    // quando o jogador chega nela vindo de outra cena.
    private static bool reloading;
    private static int deathCount;
    private static int mortesComAnimator;
    private static bool animatorMontado;
    // Morreu antes do Prof explicar o Animator: na volta, a explicação vem
    // na hora (senão quem corre direto pros espinhos nunca chega no puzzle).
    private static bool falhaPendente;
    private static bool agacharMontado;
    // Objetos com o Animator configurado (moeda, jumppad, bateria, alavanca,
    // lampada): voltam configurados depois de uma morte ou de um reinício.
    private static readonly System.Collections.Generic.HashSet<string> objetosMontados = new System.Collections.Generic.HashSet<string>();

    // Etapa "teste do agachar" no túnel
    private float tempoTeste;
    private float tempoSegurandoAgachar;
    private PlayerAgachar agachar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        reloading = false;
        agacharMontado = false;
        deathCount = 0;
        mortesComAnimator = 0;
        animatorMontado = false;
        falhaPendente = false;
        objetosMontados.Clear();
        falasDeObjetoDitas.Clear();
    }

    private void Awake()
    {
        Instance = this;

        isReload = reloading;
        reloading = false;
        if (!isReload)
        {
            deathCount = 0;
            mortesComAnimator = 0;
            animatorMontado = false;
            falhaPendente = false;
            agacharMontado = false;
            objetosMontados.Clear();
            falasDeObjetoDitas.Clear();
        }

        ReinicioFase.Registrar(this);

        if (dialogueManager == null)
        {
            // Inclui inativos: o painel de diálogo pode já ter se escondido no Awake.
            dialogueManager = FindAnyObjectByType<DialogueManager>(FindObjectsInactive.Include);
        }

        if (player == null)
        {
            player = FindAnyObjectByType<AnimacaoPlayerFisica>();
        }

        if (playerHealth == null)
        {
            playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
            if (playerHealth == null)
            {
                playerHealth = FindAnyObjectByType<PlayerHealth>();
            }
        }

        if (puzzle == null)
        {
            puzzle = FindAnyObjectByType<AnimatorPuzzleUI>(FindObjectsInactive.Include);
            if (puzzle == null)
            {
                // Sem o objeto da cena (builder não rodado): funciona igual,
                // só sem os sprites da prévia.
                puzzle = gameObject.AddComponent<AnimatorPuzzleUI>();
            }
        }

        if (completeUI == null)
        {
            completeUI = FindAnyObjectByType<LevelCompleteUI>(FindObjectsInactive.Include);
        }

        agachar = player != null ? player.GetComponent<PlayerAgachar>() : null;
        if (player != null)
        {
            player.AgacharMontado = agacharMontado;
        }

        if (dicaAgachar != null)
        {
            dicaAgachar.SetActive(agacharMontado);
        }
    }

    private void Start()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDeath += RestartLevel;
        }

        if (player == null)
        {
            Debug.LogError("[AnimacaoLevelFlow] Nenhum AnimacaoPlayerFisica na cena — rode Tools > Animação > Montar Cena de Animação.");
        }

        // Voltou de uma morte depois de montar o Animator: tudo já pronto.
        if (animatorMontado)
        {
            LigarAnimator();
            etapa = Etapa.Livre;
            ObjetivoFase.Ir(agacharMontado ? EtapaTunel : EtapaEscada);
            return;
        }

        if (player != null)
        {
            player.DefinirRegrasMontadas(false);
        }

        // Depois de uma morte, a intro não repete.
        if (isReload || dialogueManager == null)
        {
            IniciarSemAnimacao();
            if (falhaPendente)
            {
                falhaPendente = false;
                Invoke(nameof(FalhaAposMorte), 0.6f);
            }
            return;
        }

        etapa = Etapa.Intro;
        dialogueManager.SetListener(this);
        dialogueManager.StartDialogue(introDialogue);
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= RestartLevel;
        }

        if (Instance == this)
        {
            Instance = null;
        }

        ReinicioFase.Remover(this);
    }

    private void Update()
    {
        if (etapa == Etapa.SemAnimacao)
        {
            VerificarFalhaProposital();
        }
        else if (etapa == Etapa.TesteAgachar)
        {
            VerificarTesteAgachar();
        }
    }

    // ---------------------------------------------------------------- etapa 2: anda sem animar

    private void IniciarSemAnimacao()
    {
        etapa = Etapa.SemAnimacao;
        ObjetivoFase.Ir(EtapaAndar);
        tempoNaEtapa = 0f;
        tempoAndando = 0f;
        tempoParadoNoChao = 0f;
        pulosNoInicio = player != null ? player.PulosDados : 0;

        if (player != null)
        {
            player.MovimentoLiberado = true;
        }
    }

    private void VerificarFalhaProposital()
    {
        if (player == null || player.EstaMorto || DialogueManager.IsAnyDialogueActive)
        {
            return;
        }

        tempoNaEtapa += Time.deltaTime;

        bool andando = player.IsGrounded && Mathf.Abs(player.Velocidade.x) > AnimacaoPlayerFisica.LimiarParado;
        if (andando)
        {
            tempoAndando += Time.deltaTime;
        }

        bool pulou = player.PulosDados > pulosNoInicio;
        bool andou = tempoAndando >= segundosAndandoParaFalha;
        bool cumpriu = andou && (pulou || !exigirPuloParaFalha);

        // O tempo máximo não dispara mais a falha (o Prof
        // diria "ele anda e pula" com o jogador parado). Só lembra o que falta
        // e a contagem recomeça.
        if (!cumpriu && tempoNaEtapa >= tempoMaximoFalha)
        {
            tempoNaEtapa = 0f;
            LembrarFalha(andou);
            return;
        }

        // Espera ele pousar (e ficar um instante no chão) pra falha ficar à
        // vista: o pulo inteiro no mesmo quadro Idle.
        if (cumpriu && player.IsGrounded)
        {
            tempoParadoNoChao += Time.deltaTime;
            if (tempoParadoNoChao >= 0.35f)
            {
                DispararFalha(false);
            }
        }
        else
        {
            tempoParadoNoChao = 0f;
        }
    }

    private void LembrarFalha(bool andou)
    {
        RegistroAprendizagem.Registrar("dica", andou ? "Animator | falta pular" : "Animator | falta andar");
        string[] falas = andou ? lembretePularDialogue : lembreteAndarDialogue;
        if (dialogueManager == null || falas == null || falas.Length == 0)
        {
            return;
        }

        // Etapa continua SemAnimacao: OnDialogueFinished não faz nada e a
        // espera segue de onde parou.
        dialogueManager.SetListener(this);
        dialogueManager.StartDialogue(falas);
    }

    private void FalhaAposMorte()
    {
        if (etapa == Etapa.SemAnimacao)
        {
            DispararFalha(false);
        }
    }

    private void DispararFalha(bool porTempo)
    {
        etapa = Etapa.ExplicandoAnimator;
        RegistroAprendizagem.Registrar("falha_proposital", $"Animator | anda sem animar | {tempoNaEtapa:0}s{(porTempo ? " (tempo máximo)" : "")}");

        if (dialogueManager == null)
        {
            AbrirPuzzle();
            return;
        }

        dialogueManager.SetListener(this);
        dialogueManager.StartDialogue(falhaDialogue);
    }

    // ---------------------------------------------------------------- etapa 3: puzzle

    private void AbrirPuzzle()
    {
        etapa = Etapa.Puzzle;
        ObjetivoFase.Ir(EtapaAnimator);
        if (player != null)
        {
            player.MovimentoLiberado = false;
        }

        puzzle.Abrir(OnPuzzleConcluido);
    }

    private void OnPuzzleConcluido()
    {
        etapa = Etapa.CodigoMontado;
        animatorMontado = true;

        // Um instante pra ver o diagrama completo antes do painel de código.
        Invoke(nameof(MostrarCodigoMontado), 0.8f);
    }

    private void MostrarCodigoMontado()
    {
        CodigoMontadoUI.Show(tituloCodigo, codigoMontado, segundosCodigo, AoFecharCodigo);
    }

    // ---------------------------------------------------------------- etapa 4: anima sozinho

    private void AoFecharCodigo()
    {
        puzzle.Fechar();
        LigarAnimator();
        ObjetivoFase.Ir(EtapaEscada);

        if (dialogueManager == null)
        {
            etapa = Etapa.Livre;
            return;
        }

        etapa = Etapa.PosPuzzle;
        dialogueManager.SetListener(this);
        dialogueManager.StartDialogue(posPuzzleDialogue);
    }

    private void LigarAnimator()
    {
        if (player == null)
        {
            return;
        }

        player.DefinirRegrasMontadas(true);
        player.MovimentoLiberado = true;
        hud = AnimatorEstadoHUD.Mostrar(player, tituloPainelEstado, agacharMontado);
    }

    // ---------------------------------------------------------------- túnel: agachar

    /// <summary>Chamado pelo AnimacaoGatilhoTunel quando o player chega na entrada do túnel.</summary>
    public void ChegouNoTunel()
    {
        if (levelEnded || player == null || player.EstaMorto)
        {
            return;
        }

        // Correu direto até o túnel sem ver a falha do começo: explica o
        // Animator aqui mesmo (senão passaria agachado sem nunca montar nada).
        if (etapa == Etapa.SemAnimacao)
        {
            DispararFalha(false);
            return;
        }

        if (etapa != Etapa.Livre || agacharMontado || DialogueManager.IsAnyDialogueActive)
        {
            return;
        }

        etapa = Etapa.TunelAviso;
        player.MovimentoLiberado = false;
        RegistroAprendizagem.Registrar("agachar_tunel", "chegou no túnel");

        if (dialogueManager == null)
        {
            IniciarTesteAgachar();
            return;
        }

        MostrarContorno(true);
        dialogueManager.SetListener(this);
        dialogueManager.StartDialogue(ComTecla(tunelDialogue));
    }

    private void IniciarTesteAgachar()
    {
        etapa = Etapa.TesteAgachar;
        ObjetivoFase.Ir(EtapaTesteAgachar);
        tempoTeste = 0f;
        tempoSegurandoAgachar = 0f;
        MostrarContorno(true);
        // Continua parado: dá pra agachar (o PlayerAgachar não depende do
        // movimento), mas não dá pra entrar no túnel antes do puzzle.
        player.MovimentoLiberado = false;
    }

    private void VerificarTesteAgachar()
    {
        if (DialogueManager.IsAnyDialogueActive)
        {
            return;
        }

        tempoTeste += Time.deltaTime;
        if (agachar != null && agachar.Agachado)
        {
            tempoSegurandoAgachar += Time.deltaTime;
        }

        bool viu = tempoSegurandoAgachar >= segundosTesteAgachar;
        if (viu || tempoTeste >= tempoMaximoTesteAgachar)
        {
            etapa = Etapa.ExplicandoAgachar;
            RegistroAprendizagem.Registrar("falha_proposital", $"Agachar | corpo abaixa e desenho em pé | {(viu ? "apertou" : "tempo máximo")}");
            if (dialogueManager == null)
            {
                AbrirPuzzleAgachar();
                return;
            }

            dialogueManager.SetListener(this);
            dialogueManager.StartDialogue(ComTecla(falhaAgacharDialogue));
        }
    }

    private void AbrirPuzzleAgachar()
    {
        if (puzzleAgachar == null)
        {
            Debug.LogWarning("[AnimacaoLevelFlow] Sem o objeto AgacharPuzzle na cena: o agachar é liberado sem o mini-puzzle.");
            OnPuzzleAgacharConcluido();
            AoFecharCodigoAgachar();
            return;
        }

        etapa = Etapa.PuzzleAgachar;
        ObjetivoFase.Ir(EtapaPuzzleAgachar);
        MostrarContorno(false);
        player.MovimentoLiberado = false;
        puzzleAgachar.Abrir(OnPuzzleAgacharConcluido);
    }

    private void OnPuzzleAgacharConcluido()
    {
        etapa = Etapa.CodigoAgachar;
        agacharMontado = true;
        if (puzzleAgachar != null)
        {
            Invoke(nameof(MostrarCodigoAgachar), 0.8f);
        }
    }

    private void MostrarCodigoAgachar()
    {
        CodigoMontadoUI.Show(tituloCodigoAgachar, codigoAgachar, segundosCodigoAgachar, AoFecharCodigoAgachar);
    }

    private void AoFecharCodigoAgachar()
    {
        if (puzzleAgachar != null)
        {
            puzzleAgachar.Fechar();
        }

        player.AgacharMontado = true;
        player.MovimentoLiberado = true;
        ObjetivoFase.Ir(EtapaTunel);
        // Animator configurado: o desenho já mostra o agachar, o contorno sai.
        MostrarContorno(false);
        hud = AnimatorEstadoHUD.Mostrar(player, tituloPainelEstado, true);
        if (dicaAgachar != null)
        {
            dicaAgachar.SetActive(true);
        }

        if (dialogueManager == null)
        {
            etapa = Etapa.Livre;
            return;
        }

        etapa = Etapa.PosAgachar;
        dialogueManager.SetListener(this);
        dialogueManager.StartDialogue(ComTecla(posAgacharDialogue));
    }

    private void MostrarContorno(bool mostrar)
    {
        if (contornoCollider != null)
        {
            contornoCollider.SetActive(mostrar);
        }
    }

    private static string[] ComTecla(string[] falas)
    {
        string tecla = InputManager.Instance != null ? InputManager.Instance.GetBindingName("Crouch") : "C";
        if (string.IsNullOrEmpty(tecla))
        {
            tecla = "C";
        }

        var saida = new string[falas.Length];
        for (int i = 0; i < falas.Length; i++)
        {
            saida[i] = falas[i].Replace("{C}", tecla);
        }

        return saida;
    }

    // ---------------------------------------------------------------- diálogos

    /// <summary>Chamado pelo DialogueManager ao fim de cada diálogo desta fase.</summary>
    public void OnDialogueFinished()
    {
        if (awaitingDeathHintRestart)
        {
            awaitingDeathHintRestart = false;
            ReloadScene();
            return;
        }

        switch (etapa)
        {
            case Etapa.Intro:
                IniciarSemAnimacao();
                break;
            case Etapa.ExplicandoAnimator:
                AbrirPuzzle();
                break;
            case Etapa.PosPuzzle:
                etapa = Etapa.Livre;
                break;
            case Etapa.TunelAviso:
                IniciarTesteAgachar();
                break;
            case Etapa.ExplicandoAgachar:
                AbrirPuzzleAgachar();
                break;
            case Etapa.PosAgachar:
                etapa = Etapa.Livre;
                break;
        }
    }

    /// <summary>
    /// Fala do Prof Byte sobre um objeto animado da fase (lâmpada, alavanca,
    /// bateria...). Só fala com a fase livre (fora de puzzle e de outro
    /// diálogo) e uma vez por <paramref name="chave"/>; devolve false se não
    /// deu para falar agora, para o objeto tentar de novo depois.
    /// </summary>
    public bool FalarSobreObjeto(string chave, string[] falas)
    {
        if (falas == null || falas.Length == 0 || levelEnded || etapa != Etapa.Livre
            || dialogueManager == null || DialogueManager.IsAnyDialogueActive)
        {
            return false;
        }

        if (!falasDeObjetoDitas.Add(chave))
        {
            return true;
        }

        dialogueManager.SetListener(this);
        dialogueManager.StartDialogue(falas);
        return true;
    }

    /// <summary>
    /// True quando dá pra abrir o painel de um objeto da fase (ex.: o Animator
    /// da lâmpada): fase livre, sem diálogo nem outro puzzle na tela.
    /// </summary>
    public bool PodeAbrirPainelDeObjeto => !levelEnded && etapa == Etapa.Livre && player != null
        && !DialogueManager.IsAnyDialogueActive;

    /// <summary>Trava o personagem enquanto o painel de um objeto está aberto. Devolve false se agora não dá.</summary>
    public bool AbrirPainelDeObjeto()
    {
        if (!PodeAbrirPainelDeObjeto)
        {
            return false;
        }

        etapa = Etapa.PainelObjeto;
        player.MovimentoLiberado = false;
        return true;
    }

    /// <summary>Painel do objeto fechado: a fase volta a ficar livre.</summary>
    public void FecharPainelDeObjeto()
    {
        if (etapa != Etapa.PainelObjeto)
        {
            return;
        }

        etapa = Etapa.Livre;
        if (player != null)
        {
            player.MovimentoLiberado = true;
        }
    }

    /// <summary>True se o Prof já falou sobre <paramref name="chave"/> nesta tentativa.</summary>
    public bool JaFalouSobre(string chave) => falasDeObjetoDitas.Contains(chave);

    // ---------------------------------------------------------------- reiniciar fase

    // "Reiniciar fase" (botão da pausa ou tecla
    // segurada) volta como depois de uma morte: sem a intro e com o Animator
    // do Player, o agachar e os objetos que já foram montados.
    public bool PodeReiniciar => !levelEnded && (playerHealth == null || !playerHealth.IsDead);

    public void Reiniciar(string origem)
    {
        RegistroAprendizagem.RegistrarFalha("reinicio_manual", origem + " | etapa " + etapa);

        // Reiniciou com o Prof explicando o Animator (ou com o puzzle aberto):
        // na volta a explicação vem na hora, como depois de uma morte.
        if (!animatorMontado && etapa >= Etapa.ExplicandoAnimator)
        {
            falhaPendente = true;
        }

        ReloadScene();
    }

    /// <summary>Linha verde da confirmação de reinício: o que continua montado.</summary>
    public string LembreteReinicio
    {
        get
        {
            var partes = new System.Collections.Generic.List<string>();
            if (animatorMontado) partes.Add("o Animator do Player");
            if (agacharMontado) partes.Add("o agachar");
            if (objetosMontados.Count > 0) partes.Add(objetosMontados.Count == 1 ? "1 objeto" : objetosMontados.Count + " objetos");
            if (partes.Count == 0) return "";

            string lista = partes.Count == 1
                ? partes[0]
                : string.Join(", ", partes.GetRange(0, partes.Count - 1)) + " e " + partes[partes.Count - 1];
            return "Continua montado: " + lista + ".";
        }
    }

    /// <summary>O Animator do objeto deste tipo já foi configurado nesta visita à fase.</summary>
    public static bool ObjetoMontado(string tipo) => !string.IsNullOrEmpty(tipo) && objetosMontados.Contains(tipo);

    /// <summary>Chamado pelos painéis dos objetos ao configurar o Animator.</summary>
    public static void MarcarObjetoMontado(string tipo)
    {
        if (!string.IsNullOrEmpty(tipo))
        {
            objetosMontados.Add(tipo);
        }
    }

    // ---------------------------------------------------------------- morte e fim

    // Recarrega a fase do zero — chamado quando a vida chega a 0 (espinhos).
    // Nas primeiras vezes, mostra uma dica antes de recarregar; depois disso,
    // recarrega direto pra não repetir sempre.
    public void RestartLevel()
    {
        if (levelEnded)
        {
            return;
        }

        deathCount++;
        RegistroAprendizagem.RegistrarFalha("morte", UltimoPerigo + " | " + deathCount + "ª");

        // Antes do Animator a dica dos espinhos não serve (o problema é outro):
        // volta direto e o Prof já explica o Animator.
        if (!animatorMontado)
        {
            falhaPendente = true;
            Invoke(nameof(Recarregar), 0.4f);
            return;
        }

        mortesComAnimator++;
        if (dialogueManager != null && mortesComAnimator <= maxDeathHints && deathHintDialogue.Length > 0)
        {
            awaitingDeathHintRestart = true;
            dialogueManager.SetListener(this);
            dialogueManager.StartDialogue(deathHintDialogue);
            return;
        }

        // Depois das dicas: Game Over do tema Laboratório, e "Reiniciar"
        // recarrega como antes (o Animator montado continua montado).
        GameOverUI.Mostrar(animatorMontado ? "O Animator continua montado." : "", ReloadScene);
    }

    private void Recarregar() => ReloadScene();

    private static void ReloadScene()
    {
        reloading = true;
        TrocaDeCena.Recarregar();
    }

    public void CompleteLevel()
    {
        if (levelEnded)
        {
            return;
        }

        levelEnded = true;
        etapa = Etapa.Concluida;
        Debug.Log("[AnimacaoLevelFlow] Fase concluída.");
        RegistroAprendizagem.Registrar("fase_concluida", deathCount + " mortes");

        if (player != null)
        {
            player.MovimentoLiberado = false;
        }

        if (hud != null)
        {
            hud.Esconder();
        }

        if (completeUI != null)
        {
            completeUI.Mostrar(ResumoDaFase(), false, LoadNextScene);
            return;
        }

        Invoke(nameof(LoadNextScene), delayBeforeNextScene);
    }

    // ---------------------------------------------------------------- objetivo e resumo

    private const int EtapaAndar = 0, EtapaAnimator = 1, EtapaEscada = 2, EtapaTesteAgachar = 3,
        EtapaPuzzleAgachar = 4, EtapaTunel = 5;

    private List<LevelCompleteUI.Item> ResumoDaFase()
    {
        var itens = new List<LevelCompleteUI.Item>();
        if (animatorMontado)
        {
            itens.Add(new LevelCompleteUI.Item("Animator", "transições Idle, Corrida e Pulo por parâmetros"));
        }
        if (agacharMontado)
        {
            itens.Add(new LevelCompleteUI.Item("Parâmetro agachado", "o desenho abaixa junto com o collider"));
        }
        if (falasDeObjetoDitas.Count > 0)
        {
            itens.Add(new LevelCompleteUI.Item("Objetos animados", "cada tipo de objeto tem o seu Animator Controller"));
        }
        return itens;
    }

    private void LoadNextScene()
    {
        TrocaDeCena.Carregar(nextSceneName);
    }
}
