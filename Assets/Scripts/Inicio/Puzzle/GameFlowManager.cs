using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orquestra a sequência narrativa da cena "Fisica": diálogo do Prof Byte e os
/// 4 puzzles, incluindo as "falhas propositais" que ensinam os conceitos de
/// movimento, física (gravidade), colisão e pulo.
///
/// Sequência:
/// 1. Diálogo de intro -> Puzzle de Movimento.
/// 2. Puzzle de Movimento resolvido -> diálogo "bom trabalho, tente se mover"
///    -> ao terminar de ler, libera o movimento (o personagem flutua, ainda
///    sem gravidade). A sala é mais larga que a tela: quando o personagem sai
///    dela, o jogo trava, o Prof Byte fala da câmera e abre o Puzzle da
///    Câmera. Resolvido, a câmera passa a seguir o personagem (CameraFollow)
///    e o jogo ESPERA o jogador andar de fato por um tempinho.
/// 3. Jogador anda o suficiente -> diálogo "acho que não devia ser assim..."
///    -> ao terminar de ler, abre o Puzzle de Física (Rigidbody2D).
/// 4. Puzzle de Física resolvido -> ativa a gravidade no Player -> ele cai e
///    sai da tela (chão e personagem ainda sem colisão).
/// 5. Jogador cai além do limite da tela -> diálogo "passou pelo bloco que
///    deveria ser o chão..." -> Puzzle de Colisão (Collider2D).
/// 6. Puzzle de Colisão resolvido -> ativa a colisão no Player e no chão ->
///    o personagem para de cair -> diálogo convidando a testar o movimento.
/// 7. Depois de o jogador andar por um tempo (ou de um tempo máximo de
///    espera) -> diálogo "vamos incrementar a movimentação" -> Puzzle de Pulo.
/// 8. Puzzle de Pulo resolvido -> libera o pulo no Player -> diálogo de como
///    pular -> primeiro pulo de verdade -> diálogo final -> painel "Fase
///    Concluída!" (LevelCompleteUI, espera Enter) -> próxima cena
///    (Aplicacao_Fisica). Cada etapa avisa a faixa de objetivo (ObjetivoFase).
/// </summary>
public class GameFlowManager : MonoBehaviour, IDialogueListener
{
    private enum FlowStep
    {
        Intro,
        MovementPuzzle,
        MovementSolvedDialogue,  // lendo "Muito bem! Agora tente se mover!"
        WaitingFirstMove,        // movimento liberado, esperando o jogador sair da tela (câmera) e depois andar um tempinho
        CameraExitDialogue,      // lendo "ele saiu da tela, a câmera não foi atrás"
        CameraPuzzle,
        CameraSolvedDialogue,    // lendo "agora a câmera segue o personagem"
        FirstMoveDialogue,       // lendo "acho que não devia ser assim..."
        PhysicsPuzzle,
        WaitingFall,
        CollisionPuzzle,
        CollisionSolvedDialogue, // lendo "agora sim, teste o movimento"
        TestingMovement,         // jogador testando andar com física + colisão
        JumpIntroDialogue,       // lendo "vamos incrementar a movimentação"
        JumpPuzzle,
        JumpSolvedDialogue,      // lendo "agora é só apertar pular"
        WaitingFirstJump,        // pulo liberado, esperando o jogador pular de fato
        TestingJump,             // jogador testando o pulo por um tempo
        FinalDialogue,           // lendo o fechamento da cena
        Done
    }

    [Header("Referências")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private Player player;

    [Header("Puzzles (cada um chama OnPuzzleCompleted ao ser resolvido)")]
    [SerializeField] private PuzzleControl movementPuzzle;
    [SerializeField] private PuzzleControl physicsPuzzle;
    [SerializeField] private PuzzleControl collisionPuzzle;
    [SerializeField] private PuzzleControl jumpPuzzle;
    [Tooltip("Opcional — puzzle da câmera, aberto quando o personagem sai da tela depois do puzzle de Movimento.")]
    [SerializeField] private PuzzleControl cameraPuzzle;

    [Header("Câmera")]
    [Tooltip("CameraFollow da Main Camera — começa desligado e é ligado quando o puzzle da câmera é resolvido.")]
    [SerializeField] private CameraFollow cameraFollow;
    [Tooltip("Onde a câmera fica enquanto um puzzle está aberto (os puzzles são montados no centro da sala).")]
    [SerializeField] private Vector2 posicaoDosPuzzles = Vector2.zero;

    [Header("Timings — quanto o jogador precisa testar cada mecânica antes do próximo diálogo")]
    [Tooltip("Segundos de movimento de verdade que o jogador precisa acumular depois do puzzle de Movimento, antes do diálogo que introduz a Física (Rigidbody2D).")]
    [SerializeField] private float firstMoveTestSeconds = 2.5f;
    [Tooltip("Segundos de movimento de verdade (jogador andando) que o jogador precisa acumular testando a física + colisão antes do diálogo do pulo.")]
    [SerializeField] private float movementTestSeconds = 9f;
    [Tooltip("Tempo máximo de espera (em segundos) — se o jogador não andar o bastante, o diálogo do pulo aparece mesmo assim.")]
    [SerializeField] private float movementTestMaxWait = 30f;
    [Tooltip("Segundos que o jogador tem pra testar o pulo (contados a partir do primeiro pulo) antes do diálogo final.")]
    [SerializeField] private float jumpTestSeconds = 12f;

    [Header("Próxima cena (depois do painel 'Fase Concluída!')")]
    [SerializeField] private string nextSceneName = "Aplicacao_Fisica";
    [Tooltip("Só usado sem painel de fim de fase na cena.")]
    [SerializeField] private float delayBeforeNextScene = 1.2f;
    [Tooltip("Painel 'Fase Concluída!' (cartão com o resumo). Auto-encontrado se vazio.")]
    [SerializeField] private LevelCompleteUI completeUI;

    [Header("Diálogos (editáveis, um texto por linha)")]
    [TextArea(2, 4)]
    [SerializeField]
    private string[] introDialogue =
    {
        "Fala, futuro dev! Eu sou o Prof Byte e vou te guiar pelo laboratório.",
        "Aqui a gente monta as mecânicas de um jogo 2D uma por vez. Primeira da lista: fazer o personagem andar.",
        "Só que ele está travado. Nenhuma tecla faz nada. Cadê o código do movimento?",
        "Vou abrir um puzzle: arraste cada peça pro espaço certo e monte esse código. Passe o mouse nas peças se precisar de uma dica."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] afterMovementPuzzleDialogue =
    {
        "Isso aí! Seu código lê a tecla apertada e desloca o personagem na direção dela.",
        "Testa: setas ou {andar}."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] afterFirstMoveDialogue =
    {
        "Ele anda, mas parece um balão: flutua, sem peso nenhum.",
        "Falta gravidade. No Unity, quem cuida disso é o Rigidbody2D.",
        "Monte o próximo puzzle e vamos ver o que acontece."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] afterFallDialogue =
    {
        "Opa! Ele atravessou o chão e sumiu da tela.",
        "O chão está lá, mas nada avisa que ele é sólido. Pra objetos esbarrarem uns nos outros, precisa de Collider2D.",
        "Monte esse puzzle e vamos trazer ele de volta."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] afterCollisionDialogue =
    {
        "Agora sim! Ele pisa no chão e não atravessa mais nada.",
        "Um detalhe: com Rigidbody2D, quem move o corpo é a física. Por isso o Movimento troca o transform.position por rb.linearVelocity, a velocidade do corpo.",
        "Dá uma voltinha e sente como ficou."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] jumpIntroDialogue =
    {
        "Percebeu? Ele só anda pros lados.",
        "Personagem de plataforma sem pulo é meio rasteiro, né? Vamos incrementar a movimentação!",
        "O próximo puzzle é o do Pulo. Bora montar?"
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] afterJumpDialogue =
    {
        "Pronto! Aperta {pular} e vê ele sair do chão.",
        "Só dá pra pular apoiado no chão. O isGrounded no if barra o pulo no ar. Sem ele, dava pra pular sem parar."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] cameraExitDialogue =
    {
        "Opa, ele saiu da tela!",
        "O personagem continua andando lá fora, mas a câmera ficou parada onde foi colocada. Ela não sabe que precisa ir atrás dele.",
        "No Unity, quem resolve isso é um script na Main Camera. Vamos montar esse script."
    };

    [TextArea(2, 4)]
    [SerializeField]
    private string[] afterCameraDialogue =
    {
        "Achou! Agora a câmera segue o personagem, chega nele sem tranco e para nas bordas da sala.",
        "Anda mais um pouco pra sentir."
    };

    [Tooltip("Fechamento da cena — mostrado depois do primeiro pulo de verdade, antes de ir pra Aplicacao_Fisica.")]
    [TextArea(2, 4)]
    [SerializeField]
    private string[] finalDialogue =
    {
        "Movimento, câmera, gravidade, colisão e pulo. Com esses cinco puzzles você montou o coração de um jogo de plataforma.",
        "Agora vem a parte boa: usar essas mecânicas pra passar de fase."
    };

    private FlowStep step;
    private float firstMoveElapsed;
    private float jumpTestElapsed;
    private float testElapsed;
    private float testMovingTime;
    private bool cameraDone;
    private Camera mainCamera;

    private void Start()
    {
        if (dialogueManager == null)
        {
            Debug.LogError("[GameFlowManager] Campo 'Dialogue Manager' não atribuído no Inspector — nenhum diálogo vai aparecer.");
            return;
        }

        if (player == null)
        {
            Debug.LogError("[GameFlowManager] Campo 'Player' não atribuído no Inspector.");
        }

        if (movementPuzzle == null)
        {
            Debug.LogError("[GameFlowManager] Campo 'Movement Puzzle' não atribuído no Inspector — o puzzle de movimento não vai iniciar mesmo depois do diálogo de intro.");
        }

        mainCamera = cameraFollow != null ? cameraFollow.GetComponent<Camera>() : Camera.main;
        cameraDone = cameraPuzzle == null;
        if (cameraFollow != null)
        {
            cameraFollow.FocarEm(posicaoDosPuzzles);
        }

        if (completeUI == null)
        {
            completeUI = FindAnyObjectByType<LevelCompleteUI>(FindObjectsInactive.Include);
        }

        step = FlowStep.Intro;
        ObjetivoFase.Ir(EtapaMovimento);
        dialogueManager.StartDialogue(introDialogue);
    }

    // Etapas da faixa de objetivo (ver ObjetivoFase na cena).
    private const int EtapaMovimento = 0, EtapaAndar = 1, EtapaCamera = 2, EtapaAndarComCamera = 3,
        EtapaRigidbody = 4, EtapaCollider = 5, EtapaTestar = 6, EtapaPulo = 7, EtapaPular = 8;

    private void Update()
    {
        if (step == FlowStep.WaitingFirstMove)
        {
            UpdateFirstMoveTest();
        }
        else if (step == FlowStep.WaitingFall && player.HasFallenPastThreshold())
        {
            // Só dispara uma vez: já muda de etapa aqui, o diálogo acontece em seguida.
            step = FlowStep.CollisionPuzzle;
            dialogueManager.StartDialogue(afterFallDialogue);
        }
        else if (step == FlowStep.TestingMovement)
        {
            UpdateMovementTest();
        }
        else if (step == FlowStep.TestingJump)
        {
            UpdateJumpTest();
        }
    }

    // Deixa o jogador brincar com o pulo por um tempo (a partir do primeiro
    // pulo) antes do fechamento da cena.
    private void UpdateJumpTest()
    {
        jumpTestElapsed += Time.deltaTime;

        if (jumpTestElapsed >= jumpTestSeconds)
        {
            step = FlowStep.FinalDialogue;
            dialogueManager.StartDialogue(finalDialogue);
        }
    }

    // Espera o jogador andar de verdade por um tempinho (não só apertar a
    // tecla uma vez) antes de puxar o diálogo que introduz a Física — assim
    // ele tem tempo de sentir o personagem "flutuando" antes de ouvir a
    // explicação.
    private void UpdateFirstMoveTest()
    {
        // Antes da câmera: só espera o personagem sair da tela.
        if (!cameraDone)
        {
            if (player != null && IsPlayerOffScreen())
            {
                step = FlowStep.CameraExitDialogue;
                player.BloquearMovimento();
                RegistroAprendizagem.Registrar("saiu_da_tela");
                dialogueManager.StartDialogue(cameraExitDialogue);
            }
            return;
        }

        if (player != null && player.IsMovingHorizontally)
        {
            firstMoveElapsed += Time.deltaTime;
        }

        if (firstMoveElapsed >= firstMoveTestSeconds)
        {
            step = FlowStep.FirstMoveDialogue;
            dialogueManager.StartDialogue(afterFirstMoveDialogue);
        }
    }

    // Fora da tela = o sprite inteiro passou da borda esquerda ou direita da câmera.
    private bool IsPlayerOffScreen()
    {
        if (mainCamera == null)
        {
            return false;
        }

        var sr = player.GetComponent<SpriteRenderer>();
        Bounds b = sr != null ? sr.bounds : new Bounds(player.transform.position, Vector3.one);
        float halfWidth = mainCamera.orthographicSize * mainCamera.aspect;
        float camX = mainCamera.transform.position.x;
        return b.min.x > camX + halfWidth || b.max.x < camX - halfWidth;
    }

    // Conta quanto tempo o jogador de fato andou (ou o tempo total de espera,
    // como plano B) antes de puxar o diálogo do pulo.
    private void UpdateMovementTest()
    {
        if (DialogueManager.IsAnyDialogueActive)
        {
            return;
        }

        testElapsed += Time.deltaTime;
        if (player != null && player.IsMovingHorizontally)
        {
            testMovingTime += Time.deltaTime;
        }

        if (testMovingTime >= movementTestSeconds || testElapsed >= movementTestMaxWait)
        {
            step = FlowStep.JumpIntroDialogue;
            dialogueManager.StartDialogue(jumpIntroDialogue);
        }
    }

    // Chamado pelo DialogueManager quando o jogador termina de ler um diálogo.
    public void OnDialogueFinished()
    {
        switch (step)
        {
            case FlowStep.Intro:
                step = FlowStep.MovementPuzzle;
                StartPuzzleOrLogError(movementPuzzle, "Movement Puzzle");
                break;

            case FlowStep.MovementSolvedDialogue:
                // "Tente se mover" acabou de ser lido: SÓ AGORA libera o
                // movimento e espera o jogador andar de fato por um tempinho
                // — não abre o próximo puzzle ainda (isso só acontece em
                // FirstMoveDialogue).
                step = FlowStep.WaitingFirstMove;
                firstMoveElapsed = 0f;
                player.LiberarMovimento();
                ObjetivoFase.Ir(EtapaAndar);
                break;

            case FlowStep.CameraExitDialogue:
                step = FlowStep.CameraPuzzle;
                ObjetivoFase.Ir(EtapaCamera);
                StartPuzzleOrLogError(cameraPuzzle, "Camera Puzzle");
                break;

            case FlowStep.CameraSolvedDialogue:
                step = FlowStep.WaitingFirstMove;
                firstMoveElapsed = 0f;
                player.LiberarMovimento();
                ObjetivoFase.Ir(EtapaAndarComCamera);
                break;

            case FlowStep.FirstMoveDialogue:
                step = FlowStep.PhysicsPuzzle;
                ObjetivoFase.Ir(EtapaRigidbody);
                StartPuzzleOrLogError(physicsPuzzle, "Physics Puzzle");
                break;

            case FlowStep.CollisionPuzzle:
                ObjetivoFase.Ir(EtapaCollider);
                StartPuzzleOrLogError(collisionPuzzle, "Collision Puzzle");
                break;

            case FlowStep.CollisionSolvedDialogue:
                step = FlowStep.TestingMovement;
                testElapsed = 0f;
                testMovingTime = 0f;
                ObjetivoFase.Ir(EtapaTestar);
                break;

            case FlowStep.JumpIntroDialogue:
                step = FlowStep.JumpPuzzle;
                ObjetivoFase.Ir(EtapaPulo);
                StartPuzzleOrLogError(jumpPuzzle, "Jump Puzzle");
                break;

            case FlowStep.JumpSolvedDialogue:
                // "Aperta Espaço" acabou de ser lido: espera o jogador pular
                // de fato antes do fechamento da cena.
                step = FlowStep.WaitingFirstJump;
                player.OnFirstJump += HandleFirstJump;
                ObjetivoFase.Ir(EtapaPular);
                break;

            case FlowStep.FinalDialogue:
                step = FlowStep.Done;
                RegistroAprendizagem.Registrar("fase_concluida");
                if (completeUI != null)
                {
                    player.BloquearMovimento();
                    completeUI.Mostrar(ResumoDaFase(), true, LoadNextScene);
                }
                else
                {
                    Invoke(nameof(LoadNextScene), delayBeforeNextScene);
                }
                break;
        }
    }

    private void StartPuzzleOrLogError(PuzzleControl puzzle, string fieldName)
    {
        if (puzzle == null)
        {
            Debug.LogError($"[GameFlowManager] Campo '{fieldName}' não atribuído no Inspector — o diálogo terminou, mas não há puzzle pra abrir.");
            return;
        }

        // Os puzzles ficam no centro da sala: a câmera para de seguir e vai
        // pra lá enquanto um deles está aberto.
        if (cameraFollow != null)
        {
            cameraFollow.FocarEm(posicaoDosPuzzles);
        }

        puzzle.StartPuzzle();
    }

    private void ResumeCameraFollow()
    {
        if (cameraFollow != null && cameraDone)
        {
            cameraFollow.Retomar();
        }
    }

    // Chamado por PuzzleControl.PuzzleSolved() -- cada instância de puzzle se identifica.
    public void OnPuzzleCompleted(PuzzleControl completedPuzzle)
    {
        if (player == null)
        {
            Debug.LogError("[GameFlowManager] Campo 'Player' não atribuído no Inspector — não é possível reagir ao puzzle concluído.");
            return;
        }

        if (cameraPuzzle != null && completedPuzzle == cameraPuzzle)
        {
            cameraDone = true;
        }

        ResumeCameraFollow();

        if (cameraPuzzle != null && completedPuzzle == cameraPuzzle)
        {
            step = FlowStep.CameraSolvedDialogue;
            dialogueManager.StartDialogue(afterCameraDialogue);
        }
        else if (completedPuzzle == movementPuzzle)
        {
            // Só mostra o diálogo "tente se mover" aqui — o movimento em si só
            // é liberado quando ESSE diálogo terminar (ver OnDialogueFinished),
            // pra não deixar o jogador se mexer no meio do texto.
            step = FlowStep.MovementSolvedDialogue;
            dialogueManager.StartDialogue(afterMovementPuzzleDialogue);
        }
        else if (completedPuzzle == physicsPuzzle)
        {
            player.AdicionarFisica();
            step = FlowStep.WaitingFall;
            // A partir daqui, Update() fica de olho em HasFallenPastThreshold().
        }
        else if (completedPuzzle == collisionPuzzle)
        {
            // O personagem caiu pra fora da tela no puzzle de Física: traz ele
            // de volta ao ponto de partida antes de ligar a colisão, senão ele
            // ficaria caindo lá embaixo, longe do chão.
            player.VoltarAoInicio();
            player.AdicionarColisao();

            step = FlowStep.CollisionSolvedDialogue;
            dialogueManager.StartDialogue(afterCollisionDialogue);
        }
        else if (jumpPuzzle != null && completedPuzzle == jumpPuzzle)
        {
            player.LiberarPulo();
            step = FlowStep.JumpSolvedDialogue;
            dialogueManager.StartDialogue(afterJumpDialogue);
        }
        else
        {
            Debug.LogWarning($"[GameFlowManager] O puzzle '{completedPuzzle.name}' avisou que terminou, mas não é o Movement/Physics/Collision/Jump Puzzle configurado no Inspector — verifique se ele está atribuído em algum desses campos.");
        }
    }

    // Disparado pelo Player no primeiro pulo de verdade — começa a contar o
    // tempo de teste do pulo (ver UpdateJumpTest) antes do fechamento da cena.
    private void HandleFirstJump()
    {
        player.OnFirstJump -= HandleFirstJump;
        step = FlowStep.TestingJump;
        jumpTestElapsed = 0f;
    }

    // O que foi montado nesta fase, para o painel "Fase Concluída!".
    private List<LevelCompleteUI.Item> ResumoDaFase()
    {
        var itens = new List<LevelCompleteUI.Item>
        {
            new LevelCompleteUI.Item("Movimento", "Input lê a tecla e move o personagem"),
        };
        if (cameraPuzzle != null)
        {
            itens.Add(new LevelCompleteUI.Item("Câmera", "CameraFollow vai atrás do personagem"));
        }
        itens.Add(new LevelCompleteUI.Item("Rigidbody2D", "Gravidade: ele cai e tem peso"));
        itens.Add(new LevelCompleteUI.Item("Collider2D", "Pisa no chão e esbarra nas coisas"));
        if (jumpPuzzle != null)
        {
            itens.Add(new LevelCompleteUI.Item("Pulo", "Sobe com linearVelocity, só apoiado no chão"));
        }
        return itens;
    }

    private void LoadNextScene()
    {
        TrocaDeCena.Carregar(nextSceneName);
    }
}
