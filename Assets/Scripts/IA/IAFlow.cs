using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Orquestra a fase "IA" (construção): o jogador monta o cérebro do Guarda
/// em 3 puzzles, e depois de cada um vê o resultado e a próxima falha — o
/// mesmo formato da Fisica.
///
/// Sequência:
/// 1. Intro: o guarda parado ("...", sem cérebro) → Puzzle 1 (Estados).
/// 2. Estados montados: ele patrulha entre A e B, mas ignora o jogador. O
///    jogador passa na frente dele → "falta a regra que diz QUANDO trocar"
///    → Puzzle 2 (Transições).
/// 3. Transições montadas: persegue e ataca, mas só por distância. O jogador
///    é levado pra trás da parede; quando o guarda persegue alguém que ele
///    não tem como ver (a linha até o jogador bate na parede), o Prof aponta
///    a falha → Puzzle 3 (Depuração: trocar a regra 1 por Physics2D.Raycast;
///    a regra errada fica vermelha depois de 2 erros).
/// 4. Raycast: a saída abre. O jogador passa pelo guarda funcionando (pela
///    passarela, que bloqueia a linha de visão) até a saída → IA_Aplicacao.
///
/// Se o guarda pega o jogador, ele perde um coração e volta pra trás da
/// parede. Se a vida zera, a fase recarrega SEM repetir os puzzles já feitos
/// (progresso guardado em estático, como o "reloading" das outras fases).
/// </summary>
public class IAFlow : MonoBehaviour, IDialogueListener, IPuzzleListener, IReinicioFase, ILembreteReinicio
{
    private enum Etapa
    {
        Intro,
        PuzzleEstados,
        AposEstadosDialogo,
        TestandoEstados,
        FalhaEstadosDialogo,
        PuzzleTransicoes,
        AposTransicoesDialogo,
        TestandoParede,
        ConducaoParedeDialogo,
        FalhaParedeDialogo,
        PuzzleDepuracao,
        AposRaycastDialogo,
        Final,
        FinalDialogo,
        Concluido
    }

    [Header("Referências")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private GuardaIA guarda;
    [SerializeField] private InteracaoPlayerController jogador;
    [SerializeField] private PlayerHealth vidaJogador;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private IASaida saida;
    [SerializeField] private LevelCompleteUI completeUI;
    [Tooltip("HUD de vida — escondido enquanto um puzzle está aberto.")]
    [SerializeField] private GameObject hudVida;

    [Header("Puzzles (o campo 'listener' de cada PuzzleControl aponta pra este IAFlow)")]
    [SerializeField] private PuzzleControl puzzleEstados;
    [SerializeField] private PuzzleControl puzzleTransicoes;
    [SerializeField] private PuzzleControl puzzleDepuracao;
    [Tooltip("A peça 'Distance < raioVisao' já encaixada na regra 1 do puzzle de Depuração.")]
    [SerializeField] private IAPecaFixa regraErrada;
    [Tooltip("Erros no puzzle de Depuração antes de destacar a regra errada em vermelho.")]
    [SerializeField] private int errosParaDestacar = 2;

    [Header("Posições")]
    [SerializeField] private Transform pontoInicio;
    [Tooltip("Atrás da parede, do lado oposto ao guarda (também é o ponto de volta quando o guarda pega o jogador).")]
    [SerializeField] private Transform pontoAtrasDaParede;

    [Header("Câmera")]
    [Tooltip("Onde a câmera fica enquanto um puzzle está aberto (os puzzles ficam montados longe do cenário).")]
    [SerializeField] private Vector2 posicaoDosPuzzles = new Vector2(0f, 60f);
    [Tooltip("Mesmos limites do CameraFollow — usados pra câmera voltar direto pro jogador depois de um puzzle.")]
    [SerializeField] private Vector2 cameraMin = new Vector2(0.9f, 3f);
    [SerializeField] private Vector2 cameraMax = new Vector2(25f, 3f);
    [SerializeField] private Vector2 cameraOffset = new Vector2(0f, 0.5f);

    [Header("Testes entre os puzzles")]
    [Tooltip("Distância horizontal pra considerar que o jogador 'passou na frente' do guarda.")]
    [SerializeField] private float distanciaPassouNaFrente = 1.5f;
    [Tooltip("Segundos depois de passar na frente do guarda até o Prof comentar.")]
    [SerializeField] private float esperaDepoisDePassar = 1.5f;
    [Tooltip("Plano B: segundos de teste até o Prof comentar mesmo sem o jogador passar na frente.")]
    [SerializeField] private float tempoMaximoTesteEstados = 35f;
    [Tooltip("Segundos seguidos sendo perseguido 'através' de algo sólido até o Prof comentar.")]
    [SerializeField] private float segundosVistoPelaParede = 1f;
    [Tooltip("Plano B (como o do primeiro teste): segundos de teste até o Prof levar o jogador pra trás da parede e travar o movimento até o guarda passar perto.")]
    [FormerlySerializedAs("lembreteParedeSegundos")]
    [SerializeField] private float tempoMaximoTesteParede = 25f;

    [Header("Próxima cena")]
    [SerializeField] private string nextSceneName = "IA_Aplicacao";
    [SerializeField] private float delayBeforeNextScene = 1.5f;
    [SerializeField] private float delayAntesDeRecarregar = 1f;

    [Header("Diálogos (editáveis, um texto por linha)")]
    [TextArea(2, 4)]
    [SerializeField]
    private string[] introDialogue =
    {
        "Bem-vindo à última parte do laboratório: inteligência artificial de jogos.",
        "Esse é o Guarda. Ele não faz nada, nem pisca. Vamos dar um cérebro pra ele.",
        "IA de jogo é um conjunto pequeno de regras que decide o que o personagem faz a cada momento.",
        "Primeiro: o que ele faz em cada estado. Arraste o comportamento certo pra cada estado do cérebro dele."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] aposEstadosDialogue =
    {
        "Agora cada estado tem um comportamento. Olha lá: ele já patrulha entre A e B.",
        "Pula a parede e passa na frente dele pra ver o que acontece."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] falhaEstadosDialogue =
    {
        "Ele sabe andar, mas nunca muda de ideia. Falta a regra que diz QUANDO trocar de estado.",
        "No diagrama, as setas ligam um estado ao outro. Cada seta precisa de uma condição, checada a cada frame.",
        "Monte as condições das quatro setas."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] aposTransicoesDialogue =
    {
        "Agora ele troca de estado sozinho: vê, persegue e ataca. Cuidado com a vida!",
        "Te coloquei atrás da parede. Fica aí e observa o guarda."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] conducaoParedeDialogue =
    {
        "Acabou o tempo de explorar. Te trouxe pra trás da parede: fica parado e observa o guarda."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] falhaParedeDialogue =
    {
        "Ele te viu através da parede? Distância não é visão.",
        "O cérebro dele só mede o quanto você está longe. Ele não sabe que existe uma parede no meio.",
        "Descubra qual regra está errada e troque a peça."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] aposRaycastDialogue =
    {
        "Agora ele só persegue quando a linha até você não bate em nada.",
        "A saída abriu. Passe pelo guarda até lá. A parede e a passarela lá em cima bloqueiam a visão dele."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] finalDialogue =
    {
        "Conseguiu! Estados dizem o que ele faz, transições dizem quando ele troca, e a percepção decide o que ele enxerga.",
        "Com essas três ideias dá pra montar quase todo inimigo de jogo 2D. E um aliado também. É o próximo desafio."
    };

    [Tooltip("Dica mostrada no painel Descrição quando a regra errada fica vermelha.")]
    [TextArea(2, 4)]
    [SerializeField]
    private string dicaRegraErrada = "A regra 1 está em vermelho. Compare o que ela mede com o que aconteceu quando você estava atrás da parede.";

    // Progresso sobrevive ao recarregamento da própria fase (vida zerou) e é
    // zerado quando o jogador chega vindo de outra cena.
    private static bool reloading;
    private static int progresso;   // 0 nada, 1 estados, 2 transições, 3 raycast

    private const string TextoObjetivoFinal = "Chegue à saída sem o guarda te ver.";
    private static int pegas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        reloading = false;
        progresso = 0;
        pegas = 0;
    }

    private Etapa etapa;
    private bool isReload;
    private bool puzzleAberto;
    private bool levelEnded;
    private bool recarregando;

    private float tempoTeste;
    private bool passouNaFrente;
    private float tempoDesdeQuePassou;
    private float tempoVistoPelaParede;
    private float tempoSemVerPelaParede;
    private bool conduzidoAtrasDaParede;
    private int errosAntesDaDepuracao;
    private bool regraDestacada;

    private void Awake()
    {
        isReload = reloading;
        reloading = false;
        if (!isReload)
        {
            progresso = 0;
            pegas = 0;
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
    }

    private void OnEnable()
    {
        ReinicioFase.Registrar(this);
        if (guarda != null) guarda.OnAtaque += HandleAtaque;
        if (vidaJogador != null) vidaJogador.OnDeath += HandleMorte;
        if (saida != null) saida.OnJogadorChegou += HandleSaida;
    }

    private void OnDisable()
    {
        ReinicioFase.Remover(this);
        if (guarda != null) guarda.OnAtaque -= HandleAtaque;
        if (vidaJogador != null) vidaJogador.OnDeath -= HandleMorte;
        if (saida != null) saida.OnJogadorChegou -= HandleSaida;
    }

    private void Start()
    {
        if (dialogueManager == null || guarda == null || jogador == null)
        {
            Debug.LogError("[IAFlow] Faltam referências (DialogueManager, Guarda ou Jogador). Rode Tools/IA/Recriar Cena IA.");
            return;
        }

        dialogueManager.SetListener(this);
        AplicarProgressoNoGuarda();

        if (saida != null)
        {
            saida.Aberta = progresso >= 3;
        }

        if (!isReload || progresso == 0)
        {
            MoverJogador(pontoInicio);
            MostrarJogadorEGuarda();
            etapa = Etapa.Intro;
            ObjetivoFase.Ir(EtapaEstados);
            dialogueManager.StartDialogue(introDialogue);
            return;
        }

        // Recomeçando depois de a vida zerar: volta pro ponto da etapa, sem
        // repetir diálogos nem puzzles.
        switch (progresso)
        {
            case 1:
                MoverJogador(pontoInicio);
                IniciarTesteEstados();
                break;
            case 2:
                MoverJogador(pontoAtrasDaParede);
                IniciarTesteParede();
                break;
            default:
                MoverJogador(pontoAtrasDaParede);
                etapa = Etapa.Final;
                ObjetivoFase.Ir(EtapaSaida);
                break;
        }

        VoltarCameraAoJogador();
    }

    private void AplicarProgressoNoGuarda()
    {
        guarda.MontarEstados(progresso >= 1);
        guarda.MontarTransicoes(progresso >= 2);
        guarda.SetPercepcao(progresso >= 3 ? GuardaIA.Percepcao.Raycast : GuardaIA.Percepcao.Distancia);
    }

    private void Update()
    {
        if (guarda == null || jogador == null || dialogueManager == null)
        {
            return;
        }

        // Sem transições ele não ataca: pode continuar patrulhando durante as
        // falas ("olha lá, ele já patrulha"). Depois disso, pausa pra ninguém
        // tomar dano lendo.
        bool pausarNoDialogo = DialogueManager.IsAnyDialogueActive && guarda.TransicoesMontadas;
        guarda.Pausado = puzzleAberto || levelEnded || pausarNoDialogo;

        if (puzzleAberto || levelEnded || DialogueManager.IsAnyDialogueActive)
        {
            if (etapa == Etapa.PuzzleDepuracao)
            {
                AtualizarDestaqueDaRegraErrada();
            }
            return;
        }

        switch (etapa)
        {
            case Etapa.TestandoEstados:
                AtualizarTesteEstados();
                break;
            case Etapa.TestandoParede:
                AtualizarTesteParede();
                break;
        }
    }

    // ------------------------------------------------------------------
    // Testes entre os puzzles
    // ------------------------------------------------------------------

    private void IniciarTesteEstados()
    {
        etapa = Etapa.TestandoEstados;
        ObjetivoFase.Ir(EtapaPassar);
        tempoTeste = 0f;
        passouNaFrente = false;
        tempoDesdeQuePassou = 0f;
    }

    // Espera o jogador passar na frente do guarda (e ser ignorado).
    private void AtualizarTesteEstados()
    {
        tempoTeste += Time.deltaTime;

        Vector2 pj = IAVisao.Centro(jogador.transform);
        Vector2 pg = IAVisao.Centro(guarda.transform);
        if (!passouNaFrente && Mathf.Abs(pj.x - pg.x) < distanciaPassouNaFrente && Mathf.Abs(pj.y - pg.y) < 1.5f)
        {
            passouNaFrente = true;
            RegistroAprendizagem.Registrar("guarda_ignorou_jogador", "estados sem transições");
        }

        if (passouNaFrente)
        {
            tempoDesdeQuePassou += Time.deltaTime;
        }

        if ((passouNaFrente && tempoDesdeQuePassou >= esperaDepoisDePassar) || tempoTeste >= tempoMaximoTesteEstados)
        {
            etapa = Etapa.FalhaEstadosDialogo;
            dialogueManager.StartDialogue(falhaEstadosDialogue);
        }
    }

    private void IniciarTesteParede()
    {
        etapa = Etapa.TestandoParede;
        ObjetivoFase.Ir(EtapaParede);
        tempoVistoPelaParede = 0f;
        tempoSemVerPelaParede = 0f;
    }

    // Espera o guarda perseguir o jogador sem ter linha de visão até ele.
    private void AtualizarTesteParede()
    {
        bool perseguindoJogador = guarda.EstadoAtual != GuardaIA.Estado.Patrulhar &&
                                  guarda.AlvoAtual != null &&
                                  guarda.AlvoAtual.IsChildOf(jogador.transform);

        if (perseguindoJogador && !guarda.TemLinhaDeVisao(jogador.transform))
        {
            tempoVistoPelaParede += Time.deltaTime;
        }
        else
        {
            tempoVistoPelaParede = Mathf.Max(0f, tempoVistoPelaParede - Time.deltaTime);
        }

        if (tempoVistoPelaParede >= segundosVistoPelaParede)
        {
            MostrarFalhaParede("guarda_viu_pela_parede");
            return;
        }

        tempoSemVerPelaParede += Time.deltaTime;
        if (!conduzidoAtrasDaParede && tempoSemVerPelaParede >= tempoMaximoTesteParede)
        {
            // Tempo máximo, como no primeiro teste: o Prof leva o jogador pra
            // trás da parede e trava o movimento até o guarda passar perto.
            conduzidoAtrasDaParede = true;
            RegistroAprendizagem.Registrar("conduzido_atras_da_parede", "tempo máximo do teste da parede");
            MoverJogador(pontoAtrasDaParede);
            TravarJogador(true);
            etapa = Etapa.ConducaoParedeDialogo;
            dialogueManager.StartDialogue(conducaoParedeDialogue);
        }
        else if (conduzidoAtrasDaParede && tempoSemVerPelaParede >= tempoMaximoTesteParede)
        {
            // Rede de segurança: travado atrás da parede e o guarda ainda não
            // passou perto. A falha acontece mesmo assim pra fase não parar.
            MostrarFalhaParede("guarda_viu_pela_parede_forcado");
        }
    }

    private void MostrarFalhaParede(string evento)
    {
        if (conduzidoAtrasDaParede)
        {
            TravarJogador(false);
        }

        RegistroAprendizagem.Registrar(evento);
        etapa = Etapa.FalhaParedeDialogo;
        dialogueManager.StartDialogue(falhaParedeDialogue);
    }

    // Depuração: depois de N erros, a regra errada fica vermelha.
    private void AtualizarDestaqueDaRegraErrada()
    {
        if (regraDestacada || regraErrada == null)
        {
            return;
        }

        if (RegistroAprendizagem.ErrosDePuzzle - errosAntesDaDepuracao >= errosParaDestacar)
        {
            regraDestacada = true;
            regraErrada.Destacar(true);
            RegistroAprendizagem.Registrar("regra_errada_destacada", "depuração");
            if (InfoPanelUI.Instance != null)
            {
                // Vira o texto padrão do painel (e não só o atual): assim a dica
                // sobrevive ao aviso de peça errada e ao mouse saindo das peças.
                InfoPanelUI.Instance.DefinirPadrao("Regra 1", dicaRegraErrada);
            }
        }
    }

    // ------------------------------------------------------------------
    // Diálogos e puzzles
    // ------------------------------------------------------------------

    public void OnDialogueFinished()
    {
        switch (etapa)
        {
            case Etapa.Intro:
                etapa = Etapa.PuzzleEstados;
                AbrirPuzzle(puzzleEstados);
                break;

            case Etapa.AposEstadosDialogo:
                IniciarTesteEstados();
                break;

            case Etapa.FalhaEstadosDialogo:
                etapa = Etapa.PuzzleTransicoes;
                ObjetivoFase.Ir(EtapaTransicoes);
                AbrirPuzzle(puzzleTransicoes);
                break;

            case Etapa.AposTransicoesDialogo:
            case Etapa.ConducaoParedeDialogo:
                IniciarTesteParede();
                break;

            case Etapa.FalhaParedeDialogo:
                etapa = Etapa.PuzzleDepuracao;
                ObjetivoFase.Ir(EtapaRaycast);
                errosAntesDaDepuracao = RegistroAprendizagem.ErrosDePuzzle;
                regraDestacada = false;
                AbrirPuzzle(puzzleDepuracao);
                break;

            case Etapa.AposRaycastDialogo:
                etapa = Etapa.Final;
                ObjetivoFase.Ir(EtapaSaida);
                break;

            case Etapa.FinalDialogo:
                Concluir();
                break;
        }
    }

    private void AbrirPuzzle(PuzzleControl puzzle)
    {
        if (puzzle == null)
        {
            Debug.LogError("[IAFlow] Puzzle não atribuído no Inspector — rode Tools/IA/Recriar Cena IA.");
            return;
        }

        puzzleAberto = true;
        TravarJogador(true);
        MostrarHudVida(false);
        if (cameraFollow != null) cameraFollow.FocarEm(posicaoDosPuzzles);
        puzzle.StartPuzzle();
    }

    /// <summary>
    /// Mostra/esconde a vida. Com o tema, os quadrados ficam dentro da placa
    /// "VIDA" (TemaLabFases.EstilizarVida); aí a placa inteira some junto,
    /// senão ela ficava vazia por cima do puzzle.
    /// </summary>
    private void MostrarHudVida(bool mostrar)
    {
        if (hudVida == null) return;
        Transform pai = hudVida.transform.parent;
        GameObject alvo = pai != null && pai.name == "PlacaVida" ? pai.gameObject : hudVida;
        alvo.SetActive(mostrar);
        if (alvo != hudVida) hudVida.SetActive(true);
    }

    public void OnPuzzleCompleted(PuzzleControl completedPuzzle)
    {
        puzzleAberto = false;
        TravarJogador(false);
        MostrarHudVida(true);

        if (completedPuzzle == puzzleEstados)
        {
            progresso = Mathf.Max(progresso, 1);
            guarda.MontarEstados(true);
            VoltarCameraAoJogador();
            etapa = Etapa.AposEstadosDialogo;
            dialogueManager.StartDialogue(aposEstadosDialogue);
        }
        else if (completedPuzzle == puzzleTransicoes)
        {
            progresso = Mathf.Max(progresso, 2);
            guarda.MontarTransicoes(true);
            guarda.SetPercepcao(GuardaIA.Percepcao.Distancia);
            MoverJogador(pontoAtrasDaParede);
            VoltarCameraAoJogador();
            etapa = Etapa.AposTransicoesDialogo;
            dialogueManager.StartDialogue(aposTransicoesDialogue);
        }
        else if (completedPuzzle == puzzleDepuracao)
        {
            progresso = Mathf.Max(progresso, 3);
            guarda.SetPercepcao(GuardaIA.Percepcao.Raycast);
            guarda.EsquecerAlvo();
            if (saida != null) saida.Aberta = true;
            VoltarCameraAoJogador();
            etapa = Etapa.AposRaycastDialogo;
            dialogueManager.StartDialogue(aposRaycastDialogue);
        }
        else
        {
            VoltarCameraAoJogador();
            Debug.LogWarning($"[IAFlow] O puzzle '{completedPuzzle.name}' terminou, mas não é nenhum dos três configurados.");
        }
    }

    // ------------------------------------------------------------------
    // Guarda pegou / morte / saída
    // ------------------------------------------------------------------

    private void HandleAtaque(Transform alvo)
    {
        if (levelEnded || alvo == null || !alvo.IsChildOf(jogador.transform))
        {
            return;
        }

        pegas++;
        RegistroAprendizagem.RegistrarFalha("guarda_pegou", $"{pegas}ª | etapa {etapa}");

        if (vidaJogador != null && vidaJogador.IsDead)
        {
            return;
        }

        // Volta pra trás da parede (a vida já caiu pelo TakeDamage do guarda).
        MoverJogador(progresso >= 2 ? pontoAtrasDaParede : pontoInicio);
        if (progresso >= 3)
        {
            guarda.EsquecerAlvo();
        }
    }

    private void HandleMorte()
    {
        if (recarregando || levelEnded)
        {
            return;
        }

        recarregando = true;
        RegistroAprendizagem.RegistrarFalha("morte", $"guarda | etapa {etapa}");
        Invoke(nameof(MostrarGameOver), delayAntesDeRecarregar);
    }

    // Game Over do tema Laboratório; "Reiniciar" recarrega como antes,
    // mantendo os puzzles já montados.
    private void MostrarGameOver()
    {
        string lembrete = progresso > 0 ? "O cérebro do guarda continua montado." : "";
        GameOverUI.Mostrar(lembrete, RecarregarFase);
    }

    private void RecarregarFase()
    {
        reloading = true;
        TrocaDeCena.Recarregar();
    }

    // "Reiniciar fase" volta como depois de a
    // vida zerar, com os puzzles do cérebro do guarda que já foram montados.
    public bool PodeReiniciar => !levelEnded && !recarregando;

    public void Reiniciar(string origem)
    {
        recarregando = true;
        RegistroAprendizagem.RegistrarFalha("reinicio_manual", $"{origem} | etapa {etapa}");
        RecarregarFase();
    }

    /// <summary>Linha verde da confirmação de reinício.</summary>
    public string LembreteReinicio => progresso > 0
        ? $"Continua montado: o cérebro do guarda ({progresso} de 3 puzzles)."
        : "";

    private void HandleSaida()
    {
        if (levelEnded || etapa != Etapa.Final)
        {
            return;
        }

        TravarJogador(true);
        etapa = Etapa.FinalDialogo;
        dialogueManager.StartDialogue(finalDialogue);
    }

    private void Concluir()
    {
        if (levelEnded)
        {
            return;
        }

        levelEnded = true;
        etapa = Etapa.Concluido;
        RegistroAprendizagem.Registrar("fase_concluida", $"{pegas} pegas");

        if (completeUI != null)
        {
            completeUI.Mostrar(ResumoDaFase(), true, CarregarProximaCena);
            return;
        }

        Invoke(nameof(CarregarProximaCena), delayBeforeNextScene);
    }

    // ------------------------------------------------------------------
    // Faixa de objetivo e resumo
    // ------------------------------------------------------------------

    private const int EtapaEstados = 0, EtapaPassar = 1, EtapaTransicoes = 2, EtapaParede = 3,
        EtapaRaycast = 4, EtapaSaida = 5;

    private static List<LevelCompleteUI.Item> ResumoDaFase()
    {
        var itens = new List<LevelCompleteUI.Item>();
        if (progresso >= 1) itens.Add(new LevelCompleteUI.Item("Estados", "Patrulhar, Perseguir e Atacar"));
        if (progresso >= 2) itens.Add(new LevelCompleteUI.Item("Transições", "as regras que dizem quando trocar de estado"));
        if (progresso >= 3) itens.Add(new LevelCompleteUI.Item("Physics2D.Raycast", "o guarda só vê o que a linha alcança"));
        return itens;
    }

    private void CarregarProximaCena()
    {
        TrocaDeCena.Carregar(nextSceneName);
    }

    // ------------------------------------------------------------------
    // Utilitários
    // ------------------------------------------------------------------

    private void TravarJogador(bool travar)
    {
        if (jogador == null)
        {
            return;
        }

        jogador.enabled = !travar;
        if (travar)
        {
            Rigidbody2D rb = jogador.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
    }

    private void MoverJogador(Transform ponto)
    {
        if (ponto == null || jogador == null)
        {
            return;
        }

        Rigidbody2D rb = jogador.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = ponto.position;
        }

        jogador.transform.position = ponto.position;
    }

    // Intro: câmera parada no meio do caminho entre o jogador e o guarda,
    // pra "Esse é o Guarda" mostrar os dois.
    private void MostrarJogadorEGuarda()
    {
        if (cameraFollow == null)
        {
            return;
        }

        float meio = (jogador.transform.position.x + guarda.transform.position.x) * 0.5f;
        cameraFollow.FocarEm(new Vector2(Mathf.Clamp(meio, cameraMin.x, cameraMax.x), cameraMin.y));
    }

    // Volta a câmera direto pro jogador (sem atravessar o mapa até os
    // puzzles) e religa o CameraFollow.
    private void VoltarCameraAoJogador()
    {
        if (cameraFollow == null || jogador == null)
        {
            return;
        }

        Vector3 p = jogador.transform.position;
        float x = Mathf.Clamp(p.x + cameraOffset.x, cameraMin.x, cameraMax.x);
        float y = Mathf.Clamp(p.y + cameraOffset.y, cameraMin.y, cameraMax.y);
        Transform cam = cameraFollow.transform;
        cam.position = new Vector3(x, y, cam.position.z);
        cameraFollow.Retomar();
    }
}
