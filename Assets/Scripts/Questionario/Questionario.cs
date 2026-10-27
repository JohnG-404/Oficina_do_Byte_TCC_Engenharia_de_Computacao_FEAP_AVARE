using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Questionário de múltipla escolha da cena "Questionario", usado como
/// pré-teste (antes da primeira fase) e pós-teste (depois da última). As
/// perguntas são as mesmas nos dois — a comparação das notas é a evidência de
/// aprendizagem, junto com o registro de jogo (ver RegistroAprendizagem).
///
/// O modo é escolhido sozinho: se a cena anterior foi a última fase
/// ("Última Fase", padrão IA_Aplicacao), é pós-teste; senão, pré-teste. Nenhuma
/// resposta é corrigida durante o questionário (corrigir no pré-teste
/// ensinaria a resposta antes do jogo).
///
/// A tela é montada por código (como o PauseMenuUI), então a cena só precisa
/// de câmera, EventSystem e este componente. Perguntas editáveis no Inspector.
/// O visual usa o tema Laboratório (Resources/TemaLaboratorio, ver TemaLabUI).
/// </summary>
public class Questionario : MonoBehaviour
{
    [Serializable]
    public class Pergunta
    {
        [Tooltip("Bloco do jogo a que a pergunta pertence — usado pra somar os acertos por bloco no resultado.")]
        public string bloco;
        [TextArea(2, 4)] public string enunciado;
        public string[] opcoes;
        [Tooltip("Índice da opção certa (0 = primeira).")]
        public int correta;
    }

    private enum Modo { Pre, Pos }

    [Header("Fluxo")]
    [SerializeField] private string cenaDepoisDoPre = "Fisica";
    [SerializeField] private string cenaMenu = "Menu";
    [Tooltip("Quando esta é a cena anterior, o questionário roda como pós-teste.")]
    [SerializeField] private string ultimaFase = "IA_Aplicacao";
    [Tooltip("Atalho do professor (Ctrl+Shift+P) pra pular o questionário. Sem botão na tela: os alunos não pulam sem querer.")]
    [SerializeField] private bool atalhoPularDoProfessor = true;

    [Header("Perguntas (as mesmas no pré e no pós-teste)")]
    [SerializeField]
    private Pergunta[] perguntas =
    {
        new Pergunta
        {
            bloco = "Física e movimento",
            enunciado = "Num jogo 2D no Unity, qual comando lê as setas (ou A/D) que o jogador aperta?",
            opcoes = new[] { "transform.position", "Input.GetAxis(\"Horizontal\")", "Vector3.up", "Time.deltaTime" },
            correta = 1
        },
        new Pergunta
        {
            bloco = "Física e movimento",
            enunciado = "Você colocou um personagem na cena, deu Play, e ele fica parado no ar. O que falta nele pra cair?",
            opcoes = new[] { "Um Collider2D com Is Trigger ligado", "Um SpriteRenderer", "Um Rigidbody2D", "O método Start" },
            correta = 2
        },
        new Pergunta
        {
            bloco = "Física e movimento",
            enunciado = "Agora o personagem cai, mas atravessa o chão. O que resolve?",
            opcoes = new[] { "Collider2D no personagem e no chão, com Is Trigger desligado", "Aumentar o Gravity Scale pra ele pesar mais", "Colocar um Rigidbody2D no chão pra ele segurar o personagem", "Ligar o Is Trigger do chão pra ele perceber o personagem" },
            correta = 0
        },
        new Pergunta
        {
            bloco = "Física e movimento",
            enunciado = "Por que o código do pulo confere se o personagem está no chão (isGrounded)?",
            opcoes = new[] { "Pra ele pular mais alto quando sai do chão", "Pra ele não pular de novo enquanto está no ar", "Pra gravidade puxar ele de volta depois do pulo", "Pra ele andar mais rápido enquanto está no chão" },
            correta = 1
        },
        new Pergunta
        {
            bloco = "Interação e vida",
            enunciado = "Uma porta abre quando o jogador chega perto dela. Nessa interação, \"chegar perto\" é:",
            opcoes = new[] { "A consequência", "A animação", "A ação", "O Rigidbody2D" },
            correta = 2
        },
        new Pergunta
        {
            bloco = "Interação e vida",
            enunciado = "Um espinho tira 1 ponto de vida quando o jogador encosta nele. Qual é a consequência?",
            opcoes = new[] { "Encostar no espinho", "Tirar 1 ponto de vida", "O collider do espinho", "A barra de vida" },
            correta = 1
        },
        new Pergunta
        {
            bloco = "Animação",
            enunciado = "Uma animação de corrida num jogo 2D é feita de:",
            opcoes = new[] { "Uma sequência de sprites (frames) trocados bem rápido", "Um único sprite que estica e encolhe enquanto ele corre", "Um Rigidbody2D com mais velocidade pra mexer as pernas", "Um Collider2D maior que acompanha o movimento das pernas" },
            correta = 0
        },
        new Pergunta
        {
            bloco = "Animação",
            enunciado = "Os frames de uma animação foram colocados fora de ordem. O que o jogador vê?",
            opcoes = new[] { "A animação toca mais devagar, mas na ordem certa", "Nada muda, porque o Unity coloca os frames em ordem sozinho", "O personagem para de colidir com o chão", "Um movimento embaralhado, que não faz sentido" },
            correta = 3
        },
        new Pergunta
        {
            bloco = "IA",
            enunciado = "Um inimigo anda de um lado pro outro e, quando o jogador chega perto, começa a correr atrás dele. O que faz ele trocar de comportamento?",
            opcoes = new[] { "Uma animação diferente que toca quando ele vê o jogador", "Uma regra que confere a distância até o jogador a cada frame", "O Rigidbody2D do inimigo, que empurra ele até o jogador", "O botão que o jogador aperta quando chega perto dele" },
            correta = 1
        },
        new Pergunta
        {
            bloco = "IA",
            enunciado = "O inimigo \"vê\" o jogador mesmo com uma parede no meio. Qual é a melhor correção?",
            opcoes = new[] { "Aumentar o raio de visão pra ele enxergar mais longe", "Usar OnCollisionEnter2D pra saber quando ele encosta no jogador", "Lançar um Raycast até o jogador e ver se ele bate na parede", "Tirar o Collider2D da parede pra ela não atrapalhar a visão" },
            correta = 2
        },
    };

    // Visual: tema Laboratório (ver TemaLabUI). Nenhuma cor aqui indica acerto
    // ou erro: as opções são todas iguais e o progresso só mostra a posição.
    private const float LarguraJanela = 1400f;
    private const float AlturaJanela = 920f;
    private const float MargemX = 80f;

    private Modo modo;
    // Pós-teste aberto pelo modo professor (Ctrl+Shift+U na Seleção) antes de
    // todas as fases concluídas: a abertura avisa e a nota sai marcada no CSV.
    private bool modoProfessor;
    private int fasesConcluidas;
    private RectTransform seloProfessor;
    private TMP_Text seloProfessorTexto;
    private TMP_Text notaProfessor;
    private int atual;
    private int acertos;
    private readonly Dictionary<string, int> acertosPorBloco = new Dictionary<string, int>();
    private readonly List<string> ordemDosBlocos = new List<string>();

    private RectTransform canvasRect;
    private TMP_Text placa;
    private TMP_Text cabecalho;
    private TMP_Text titulo;
    private TMP_Text corpo;
    private RectTransform conteudo;
    private RectTransform pontosRoot;
    private RectTransform opcoesRoot;
    private RectTransform resultadoRoot;
    private RectTransform legenda;
    private Button botaoPrincipal;
    private TMP_Text botaoPrincipalLabel;
    private Image botaoPrincipalIcone;
    private readonly List<Button> opcoesAtuais = new List<Button>();
    private readonly List<GameObject> marcasDasOpcoes = new List<GameObject>();
    private readonly List<TextMeshProUGUI> letrasDasOpcoes = new List<TextMeshProUGUI>();

    // Faixa de confirmação embaixo da pergunta: repete a opção marcada e só
    // registra a resposta no "Confirmar" (um clique sem querer não conta).
    private RectTransform faixa;
    private TMP_Text faixaTexto;
    private Button botaoTrocar;
    private Button botaoConfirmar;
    private int marcada = -1;
    private bool pulando;

    private void Start()
    {
        // Pós-teste: vindo da última fase, ou escolhido no seletor de fases
        // depois de todas concluídas (ver ProgressoJogo). Rejogar a última
        // fase depois do pós-teste já feito volta direto pro Menu.
        bool veioDaUltimaFase = RegistroAprendizagem.CenaAnterior == ultimaFase;
        if (veioDaUltimaFase && ProgressoJogo.PosTesteFeito)
        {
            TrocaDeCena.Carregar(cenaMenu);
            return;
        }

        // Mesma regra da Seleção de fases: o Ctrl+Shift+U também libera o final.
        bool posLiberado = ProgressoJogo.PreTesteFeito && ProgressoJogo.PosTesteDesbloqueado;

        // O pré-teste só é feito uma vez: rodar como inicial de novo gravaria
        // a nota por cima da de antes.
        if (ProgressoJogo.PreTesteFeito && !veioDaUltimaFase && !posLiberado)
        {
            TrocaDeCena.Carregar(cenaMenu);
            return;
        }

        modo = veioDaUltimaFase || posLiberado ? Modo.Pos : Modo.Pre;
        fasesConcluidas = 0;
        foreach (ProgressoJogo.Fase f in ProgressoJogo.Fases)
        {
            if (ProgressoJogo.Concluida(f.cena)) fasesConcluidas++;
        }
        modoProfessor = modo == Modo.Pos && fasesConcluidas < ProgressoJogo.Fases.Length;

        if (modo == Modo.Pre)
        {
            RegistroAprendizagem.NovaSessao();
        }
        else
        {
            // O jogo pode ter sido fechado no meio do caminho: a nota de
            // antes vem do arquivo de progresso.
            RegistroAprendizagem.RestaurarPreTeste(ProgressoJogo.Atual.preTeste.nota, ProgressoJogo.BlocosDoPreTeste());
        }

        foreach (Pergunta p in perguntas)
        {
            if (!ordemDosBlocos.Contains(p.bloco))
            {
                ordemDosBlocos.Add(p.bloco);
            }
        }

        GarantirEventSystem();
        MontarTela();
        MostrarIntro();
    }

    // Teclado/controle: nada vem pré-selecionado nas perguntas (um foco já
    // na opção A poderia puxar a resposta). A primeira seta escolhe a
    // primeira opção; daí em diante a navegação do EventSystem assume.
    private void Update()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado != null && atalhoPularDoProfessor && !pulando && resultadoRoot != null && !resultadoRoot.gameObject.activeSelf)
        {
            bool ctrl = teclado.leftCtrlKey.isPressed || teclado.rightCtrlKey.isPressed;
            bool shift = teclado.leftShiftKey.isPressed || teclado.rightShiftKey.isPressed;
            if (ctrl && shift && teclado.pKey.wasPressedThisFrame)
            {
                Pular();
                return;
            }
        }

        // P abre a pasta dos registros na tela de resultado: ela fica em
        // AppData, que o Windows esconde, então o professor não chega clicando.
        if (teclado != null && resultadoRoot != null && resultadoRoot.gameObject.activeSelf && !JogoBloqueado.PausaPorCima
            && teclado.pKey.wasPressedThisFrame && !teclado.ctrlKey.isPressed && !teclado.shiftKey.isPressed)
        {
            AbrirPastaRegistros();
            return;
        }

        if (opcoesAtuais.Count == 0)
        {
            return;
        }

        // Backspace desmarca (Esc fica com a pausa, que existe em todas as cenas).
        if (marcada >= 0 && teclado != null && teclado.backspaceKey.wasPressedThisFrame)
        {
            Trocar();
            return;
        }

        EventSystem es = EventSystem.current;
        if (es == null || (es.currentSelectedGameObject != null && es.currentSelectedGameObject.activeInHierarchy))
        {
            return;
        }

        Keyboard k = Keyboard.current;
        bool seta = k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame ||
                                  k.wKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame);
        Gamepad g = Gamepad.current;
        bool direcional = g != null && (g.dpad.up.wasPressedThisFrame || g.dpad.down.wasPressedThisFrame);

        if (seta || direcional)
        {
            es.SetSelectedGameObject(opcoesAtuais[0].gameObject);
        }
    }

    // ------------------------------------------------------------------
    // Telas
    // ------------------------------------------------------------------

    private void MostrarIntro()
    {
        RegistroAprendizagem.Registrar(Prefixo + "_inicio");

        placa.text = modo == Modo.Pre ? "Questionário inicial" : "Questionário final";
        cabecalho.text = modo == Modo.Pre ? "QUESTIONÁRIO · ANTES DO JOGO" : "QUESTIONÁRIO · DEPOIS DO JOGO";
        titulo.text = modo == Modo.Pre ? "Antes de começar" : "Última parada";
        titulo.fontSize = 76f;
        corpo.text = modo == Modo.Pre
            ? $"São {perguntas.Length} perguntas rápidas sobre o que você vai ver no jogo. Não vale nota: elas servem só pra comparar com o que você sabe no final."
            : $"As mesmas {perguntas.Length} perguntas do começo. Responde de novo e vê o quanto mudou.";

        seloProfessor.gameObject.SetActive(modoProfessor);
        notaProfessor.gameObject.SetActive(modoProfessor);
        if (modoProfessor)
        {
            seloProfessorTexto.text = $"Modo professor · {fasesConcluidas} de {ProgressoJogo.Fases.Length} fases concluídas";
            seloProfessor.sizeDelta = new Vector2(seloProfessorTexto.GetPreferredValues(seloProfessorTexto.text).x + 56f, 52f);
        }

        LimparOpcoes();
        corpo.gameObject.SetActive(true);
        resultadoRoot.gameObject.SetActive(false);
        MostrarPontos(-1);
        Posicionar(pontosRoot, new Vector2(0f, 1f), new Vector2(0f, -420f), new Vector2(600f, 32f));
        ((HorizontalLayoutGroup)pontosRoot.GetComponent<HorizontalLayoutGroup>()).childAlignment = TextAnchor.MiddleLeft;

        faixa.gameObject.SetActive(false);
        ConfigurarBotaoPrincipal("Começar", TemaLabUI.Tema != null ? TemaLabUI.Tema.iconeJogar : null, () =>
        {
            atual = 0;
            acertos = 0;
            acertosPorBloco.Clear();
            MostrarPergunta();
        });
        Selecionar(botaoPrincipal);
        TrocarLegenda("Enter", "Começar");
    }

    private void MostrarPergunta()
    {
        Pergunta p = perguntas[atual];

        cabecalho.text = $"PERGUNTA {atual + 1} DE {perguntas.Length} · {p.bloco.ToUpperInvariant()}";
        titulo.text = p.enunciado;
        titulo.fontSize = 50f;
        corpo.gameObject.SetActive(false);
        botaoPrincipal.gameObject.SetActive(false);

        // Progresso no canto de cima, na linha do cabeçalho.
        MostrarPontos(atual);
        Posicionar(pontosRoot, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(600f, 30f));
        ((HorizontalLayoutGroup)pontosRoot.GetComponent<HorizontalLayoutGroup>()).childAlignment = TextAnchor.MiddleRight;

        LimparOpcoes();
        for (int i = 0; i < p.opcoes.Length; i++)
        {
            int escolha = i;
            CriarOpcao((char)('A' + i), p.opcoes[i], () => Marcar(escolha));
        }

        marcada = -1;
        faixa.gameObject.SetActive(true);
        AtualizarFaixa();

        // Ninguém fica com foco herdado da pergunta anterior.
        EventSystem.current?.SetSelectedGameObject(null);
        TrocarLegenda("↑↓", "Escolher", "Enter", "Marcar");
    }

    // Clique (ou Enter) numa opção só marca. A cor da marca é a mesma pra
    // qualquer opção: não indica acerto.
    private void Marcar(int escolha)
    {
        marcada = escolha;
        AtualizarFaixa();
        Selecionar(botaoConfirmar);
        TrocarLegenda("↑↓", "Escolher", "Enter", "Confirmar", "Backspace", "Trocar");
    }

    private void Trocar()
    {
        int antes = marcada;
        marcada = -1;
        AtualizarFaixa();
        if (opcoesAtuais.Count > 0)
        {
            Selecionar(opcoesAtuais[Mathf.Clamp(antes, 0, opcoesAtuais.Count - 1)]);
        }
        TrocarLegenda("↑↓", "Escolher", "Enter", "Marcar");
    }

    private void Confirmar()
    {
        if (marcada < 0)
        {
            return;
        }

        int escolha = marcada;
        marcada = -1;
        Responder(escolha);
    }

    private void AtualizarFaixa()
    {
        for (int i = 0; i < marcasDasOpcoes.Count; i++)
        {
            bool esta = i == marcada;
            marcasDasOpcoes[i].SetActive(esta);
            letrasDasOpcoes[i].color = esta ? TemaLabUI.Ciano : TemaLabUI.CorTexto;
        }

        bool tem = marcada >= 0 && marcada < perguntas[atual].opcoes.Length;
        faixaTexto.text = tem
            ? $"Sua resposta: <color=#E8EEF9>{(char)('A' + marcada)} · <noparse>{perguntas[atual].opcoes[marcada]}</noparse></color>"
            : "Escolha uma opção e depois confirme.";
        botaoConfirmar.interactable = tem;
        botaoTrocar.gameObject.SetActive(tem);
    }

    private void Responder(int escolha)
    {
        Pergunta p = perguntas[atual];
        bool certa = escolha == p.correta;

        if (certa)
        {
            acertos++;
            acertosPorBloco.TryGetValue(p.bloco, out int n);
            acertosPorBloco[p.bloco] = n + 1;
        }

        RegistroAprendizagem.Registrar(Prefixo + "_resposta",
            $"Q{atual + 1} | {p.bloco} | {p.opcoes[escolha]} | {(certa ? "certa" : "errada")}");

        atual++;
        if (atual < perguntas.Length)
        {
            MostrarPergunta();
            return;
        }

        RegistroAprendizagem.RegistrarNota(modo == Modo.Pos, acertos, perguntas.Length, acertosPorBloco,
            modoProfessor ? $"modo_professor ({fasesConcluidas} de {ProgressoJogo.Fases.Length} fases)" : null);
        Concluir();
    }

    private void Pular()
    {
        pulando = true;
        RegistroAprendizagem.Registrar(Prefixo + "_pulado");
        Concluir();
    }

    private void Concluir()
    {
        if (modo == Modo.Pre)
        {
            TrocaDeCena.Carregar(cenaDepoisDoPre);
        }
        else
        {
            MostrarResultado();
        }
    }

    private void MostrarResultado()
    {
        placa.text = "Questionário final";
        cabecalho.text = modoProfessor ? "RESULTADO · MODO PROFESSOR" : "FIM DO JOGO";
        titulo.text = "Resultado";
        titulo.fontSize = 64f;
        corpo.gameObject.SetActive(false);
        if (modoProfessor)
        {
            // O selo é filho do corpo, que some aqui: passa pro conteúdo, à direita do título.
            seloProfessor.SetParent(conteudo, false);
            Posicionar(seloProfessor, new Vector2(1f, 1f), new Vector2(0f, -66f), seloProfessor.sizeDelta);
            seloProfessor.gameObject.SetActive(true);
        }
        LimparOpcoes();
        pontosRoot.gameObject.SetActive(false);
        faixa.gameObject.SetActive(false);

        MontarResultado();
        resultadoRoot.gameObject.SetActive(true);

        ConfigurarBotaoPrincipal("Voltar ao menu", TemaLabUI.Tema != null ? TemaLabUI.Tema.iconeVoltar : null, () =>
        {
            RegistroAprendizagem.Registrar("sessao_fim");
            TrocaDeCena.Carregar(cenaMenu);
        });
        ((RectTransform)botaoPrincipal.transform).sizeDelta = new Vector2(420f, 108f);
        Selecionar(botaoPrincipal);
        if (string.IsNullOrEmpty(RegistroAprendizagem.CaminhoArquivo))
        {
            TrocarLegenda("Enter", "Voltar ao menu");
        }
        else
        {
            TrocarLegenda("P", "Abrir pasta", "Enter", "Voltar ao menu");
        }
    }

    private string Prefixo => modo == Modo.Pre ? "questionario_pre" : "questionario_pos";

    private static string Acertos(Dictionary<string, int> porBloco, int nota, string bloco)
    {
        if (nota < 0)
        {
            return "–";
        }

        return porBloco.TryGetValue(bloco, out int n) ? n.ToString() : "0";
    }

    // ------------------------------------------------------------------
    // Montagem da UI
    // ------------------------------------------------------------------

    private static void GarantirEventSystem()
    {
        if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private void MontarTela()
    {
        TemaLaboratorio tema = TemaLabUI.Tema;

        var canvasGO = new GameObject("QuestionarioCanvas", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);
        canvasRect = (RectTransform)canvasGO.transform;

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // cabe inteira em qualquer proporção
        canvasGO.AddComponent<GraphicRaycaster>();

        // Fundo calmo do kit, cobrindo a tela sem distorcer.
        Image fundo = TemaLabUI.Imagem(canvasRect, "Fundo", tema != null ? tema.fundoCalmo : null, TemaLabUI.Fundo);
        TemaLabUI.Esticar(fundo.rectTransform);
        if (fundo.sprite != null)
        {
            var fitter = fundo.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = fundo.sprite.rect.width / fundo.sprite.rect.height;
        }

        // Janela central + placa de título por cima da borda de cima.
        Image janela = TemaLabUI.Imagem(canvasRect, "Janela", tema != null ? tema.painelJanela : null, TemaLabUI.Painel);
        TemaLabUI.Centro(janela.rectTransform, new Vector2(0f, 0f), new Vector2(LarguraJanela, AlturaJanela));
        placa = TemaLabUI.Placa(canvasRect, "Questionário inicial", new Vector2(0f, AlturaJanela * 0.5f + 8f), new Vector2(640f, 104f), 50f);

        conteudo = TemaLabUI.Novo(janela.transform, "Conteudo");
        TemaLabUI.Esticar(conteudo);
        conteudo.offsetMin = new Vector2(MargemX, 56f);
        conteudo.offsetMax = new Vector2(-MargemX, -96f);

        cabecalho = TemaLabUI.Rotulo(conteudo, "Cabecalho", "");
        Faixa(cabecalho.rectTransform, 0f, 30f);

        titulo = TemaLabUI.Texto(conteudo, "Titulo", "", 50f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Display, TextAlignmentOptions.TopLeft);
        titulo.lineSpacing = -24f;
        Faixa(titulo.rectTransform, 50f, 136f);

        corpo = TemaLabUI.Texto(conteudo, "Corpo", "", 33f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.Corpo, TextAlignmentOptions.TopLeft);
        corpo.lineSpacing = 14f;
        Faixa(corpo.rectTransform, 170f, 220f);

        // Selo do modo professor, filho do corpo: some junto com ele nas perguntas.
        RectTransform seloMiolo = TemaLabUI.CaixaArredondada(corpo.transform, "SeloProfessor", TemaLabUI.Dourado, new Color32(0x34, 0x30, 0x28, 0xFF), 2f);
        seloProfessor = (RectTransform)seloMiolo.parent;
        Posicionar(seloProfessor, new Vector2(0f, 1f), new Vector2(0f, -94f), new Vector2(620f, 52f));
        seloProfessorTexto = TemaLabUI.Rotulo(seloProfessor, "Texto", "", 22f, TemaLabUI.Dourado);
        seloProfessorTexto.characterSpacing = 4f;
        seloProfessorTexto.alignment = TextAlignmentOptions.Center;
        TemaLabUI.Esticar(seloProfessorTexto.rectTransform);
        notaProfessor = TemaLabUI.Texto(corpo.transform, "NotaProfessor", "A nota entra no registro marcada como \"modo professor\", pra não misturar com a dos alunos.",
            26f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.Corpo, TextAlignmentOptions.TopLeft);
        Faixa(notaProfessor.rectTransform, 168f, 40f);

        // Progresso: bolinhas neutras (nenhuma cor de acerto).
        pontosRoot = TemaLabUI.Novo(conteudo, "Progresso");
        var pontos = pontosRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        pontos.spacing = 10f;
        pontos.childAlignment = TextAnchor.MiddleRight;
        pontos.childControlWidth = pontos.childControlHeight = true;
        pontos.childForceExpandWidth = pontos.childForceExpandHeight = false;

        // Opções (empilhadas)
        opcoesRoot = TemaLabUI.Novo(conteudo, "Opcoes");
        opcoesRoot.anchorMin = new Vector2(0f, 0f);
        opcoesRoot.anchorMax = new Vector2(1f, 1f);
        opcoesRoot.offsetMin = new Vector2(0f, 136f);
        opcoesRoot.offsetMax = new Vector2(0f, -200f);
        var layout = opcoesRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        resultadoRoot = TemaLabUI.Novo(conteudo, "Resultado");
        TemaLabUI.Esticar(resultadoRoot);
        resultadoRoot.gameObject.SetActive(false);

        botaoPrincipal = TemaLabUI.Botao(conteudo, "BotaoPrincipal", "Começar",
            tema != null ? tema.botaoPrimario : null, TemaLabUI.Azul, out TextMeshProUGUI principalLabel,
            tema != null ? tema.iconeJogar : null, 36f, Color.white);
        botaoPrincipalLabel = principalLabel;
        botaoPrincipalIcone = botaoPrincipal.transform.Find("Conteudo/Icone")?.GetComponent<Image>();
        Posicionar((RectTransform)botaoPrincipal.transform, new Vector2(1f, 0f), Vector2.zero, new Vector2(380f, 108f));

        MontarFaixa(tema);
    }

    // Faixa embaixo das opções: "Sua resposta: B · ..." + Trocar + Confirmar.
    private void MontarFaixa(TemaLaboratorio tema)
    {
        RectTransform miolo = TemaLabUI.CaixaArredondada(conteudo, "FaixaResposta", TemaLabUI.Contorno, TemaLabUI.Poco, 3f);
        faixa = (RectTransform)miolo.parent;
        faixa.anchorMin = new Vector2(0f, 0f);
        faixa.anchorMax = new Vector2(1f, 0f);
        faixa.pivot = new Vector2(0.5f, 0f);
        faixa.anchoredPosition = new Vector2(0f, -8f);
        faixa.sizeDelta = new Vector2(0f, 120f);

        TextMeshProUGUI txt = TemaLabUI.Texto(faixa, "Texto", "", 28f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.CorpoForte, TextAlignmentOptions.MidlineLeft);
        txt.enableAutoSizing = true;
        txt.fontSizeMin = 20f;
        txt.fontSizeMax = 28f;
        RectTransform tr = TemaLabUI.Esticar(txt.rectTransform);
        tr.offsetMin = new Vector2(32f, 8f);
        tr.offsetMax = new Vector2(-600f, -8f);
        faixaTexto = txt;

        botaoConfirmar = TemaLabUI.Botao(faixa, "BotaoConfirmar", "Confirmar",
            tema != null ? tema.botaoPrimario : null, TemaLabUI.Azul, out _, tema != null ? tema.iconeCheck : null, 32f, Color.white);
        Posicionar((RectTransform)botaoConfirmar.transform, new Vector2(1f, 0.5f), new Vector2(-16f, 4f), new Vector2(320f, 92f));
        botaoConfirmar.onClick.AddListener(Confirmar);

        botaoTrocar = TemaLabUI.Botao(faixa, "BotaoTrocar", "Trocar",
            tema != null ? tema.botaoSecundario : null, TemaLabUI.PainelElevado, out _, null, 30f);
        Posicionar((RectTransform)botaoTrocar.transform, new Vector2(1f, 0.5f), new Vector2(-352f, 4f), new Vector2(220f, 92f));
        botaoTrocar.onClick.AddListener(Trocar);

        faixa.gameObject.SetActive(false);
    }

    // Faixa horizontal que ocupa a largura do conteúdo, a partir do topo.
    private static void Faixa(RectTransform rect, float topo, float altura)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(0f, -topo - altura);
        rect.offsetMax = new Vector2(0f, -topo);
    }

    private static void Posicionar(RectTransform rect, Vector2 ancora, Vector2 posicao, Vector2 tamanho)
    {
        rect.anchorMin = rect.anchorMax = ancora;
        rect.pivot = ancora;
        rect.anchoredPosition = posicao;
        rect.sizeDelta = tamanho;
    }

    private void MostrarPontos(int atualIndice)
    {
        pontosRoot.gameObject.SetActive(true);
        for (int i = pontosRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(pontosRoot.GetChild(i).gameObject);
        }

        TemaLaboratorio tema = TemaLabUI.Tema;
        for (int i = 0; i < perguntas.Length; i++)
        {
            bool corrente = i == atualIndice || (atualIndice < 0 && i == 0);
            Sprite s = tema != null ? (corrente ? tema.paginaPontoAtivo : tema.paginaPonto) : null;
            Image ponto = TemaLabUI.Imagem(pontosRoot, "Ponto", s, TemaLabUI.TextoSuave);
            var le = ponto.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 26f;
            le.preferredWidth = corrente ? 52f : 26f;
            // Já respondida = um pouco mais clara, igual para qualquer resposta.
            float alfa = corrente ? 1f : (atualIndice >= 0 && i < atualIndice ? 0.95f : 0.45f);
            ponto.color = new Color(ponto.color.r, ponto.color.g, ponto.color.b, alfa);
        }
    }

    private void CriarOpcao(char letra, string texto, Action aoClicar)
    {
        Button button = TemaLabUI.Linha(opcoesRoot, "Opcao");
        var element = button.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 96f;
        element.preferredHeight = 96f;

        var linha = button.gameObject.AddComponent<HorizontalLayoutGroup>();
        linha.padding = new RectOffset(28, 32, 10, 10);
        linha.spacing = 28f;
        linha.childAlignment = TextAnchor.MiddleLeft;
        linha.childControlWidth = linha.childControlHeight = true;
        linha.childForceExpandWidth = false;
        linha.childForceExpandHeight = false;

        // Letra numa "tecla" (mesma cara em todas as opções).
        RectTransform tecla = TemaLabUI.CaixaArredondada(button.transform, "Letra", TemaLabUI.Contorno, TemaLabUI.Poco, 3f);
        var teclaLE = tecla.parent.gameObject.AddComponent<LayoutElement>();
        teclaLE.preferredWidth = teclaLE.preferredHeight = 60f;
        teclaLE.flexibleWidth = 0f;
        TextMeshProUGUI letraTxt = TemaLabUI.Texto(tecla, "L", letra.ToString(), 32f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Display, TextAlignmentOptions.Center);
        TemaLabUI.Esticar(letraTxt.rectTransform).offsetMin = new Vector2(0f, 3f);

        // Contorno da opção marcada, atrás do conteúdo e fora do layout.
        RectTransform mioloMarca = TemaLabUI.CaixaArredondada(button.transform, "Marca", TemaLabUI.Ciano, new Color32(0x1F, 0x3A, 0x4A, 0xFF), 4f);
        GameObject marca = mioloMarca.parent.gameObject;
        marca.AddComponent<LayoutElement>().ignoreLayout = true;
        TemaLabUI.Esticar((RectTransform)marca.transform);
        marca.transform.SetAsFirstSibling();
        marca.SetActive(false);
        marcasDasOpcoes.Add(marca);
        letrasDasOpcoes.Add(letraTxt);

        TextMeshProUGUI label = TemaLabUI.Texto(button.transform, "Label", texto, 31f, TemaLabUI.CorTexto, TemaLabUI.Fonte.CorpoForte);
        // Sem rich text: opções como Input.GetAxis("Horizontal") aparecem como estão.
        label.richText = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 22f;
        label.fontSizeMax = 31f;
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        button.onClick.AddListener(() => aoClicar());
        opcoesAtuais.Add(button);
    }

    private void ConfigurarBotaoPrincipal(string rotulo, Sprite icone, Action aoClicar)
    {
        botaoPrincipal.gameObject.SetActive(true);
        botaoPrincipalLabel.text = rotulo;
        if (botaoPrincipalIcone != null)
        {
            botaoPrincipalIcone.sprite = icone;
            botaoPrincipalIcone.gameObject.SetActive(icone != null);
        }
        botaoPrincipal.onClick.RemoveAllListeners();
        botaoPrincipal.onClick.AddListener(() => aoClicar());
    }

    private static void Selecionar(Selectable alvo)
    {
        if (EventSystem.current != null && alvo != null)
        {
            EventSystem.current.SetSelectedGameObject(alvo.gameObject);
        }
    }

    private void TrocarLegenda(params string[] teclaAcao)
    {
        if (legenda != null)
        {
            Destroy(legenda.gameObject);
        }
        legenda = TemaLabUI.LegendaTeclas(canvasRect, teclaAcao);
    }

    private void LimparOpcoes()
    {
        opcoesAtuais.Clear();
        marcasDasOpcoes.Clear();
        letrasDasOpcoes.Clear();
        for (int i = opcoesRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(opcoesRoot.GetChild(i).gameObject);
        }
    }

    // ------------------------------------------------------------------
    // Resultado (só no pós-teste)
    // ------------------------------------------------------------------

    private void MontarResultado()
    {
        for (int i = resultadoRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(resultadoRoot.GetChild(i).gameObject);
        }

        TemaLaboratorio tema = TemaLabUI.Tema;
        Sprite linha = tema != null ? tema.painelLinha : null;
        float largura = LarguraJanela - MargemX * 2f;

        // Notas de antes e depois, grandes, à esquerda.
        CaixaNota("Antes do jogo", RegistroAprendizagem.NotaPre, TemaLabUI.CorTexto, 150f);
        CaixaNota("Depois do jogo", RegistroAprendizagem.NotaPos, TemaLabUI.Azul, 316f);

        // Tabela por bloco, à direita.
        float xTabela = 600f;
        float wTabela = largura - xTabela;
        RectTransform cab = TemaLabUI.Novo(resultadoRoot, "CabecalhoTabela");
        Caixa(cab, xTabela, 150f, wTabela, 40f);
        LinhaTabela(cab, "BLOCO", "ANTES", "DEPOIS", "DE", true);

        float y = 196f;
        foreach (string bloco in ordemDosBlocos)
        {
            int total = 0;
            foreach (Pergunta p in perguntas)
            {
                if (p.bloco == bloco) total++;
            }

            Image fundoLinha = TemaLabUI.Imagem(resultadoRoot, "Bloco", linha, TemaLabUI.Painel);
            Caixa(fundoLinha.rectTransform, xTabela, y, wTabela, 60f);
            LinhaTabela(fundoLinha.rectTransform, bloco,
                Acertos(RegistroAprendizagem.AcertosPre, RegistroAprendizagem.NotaPre, bloco),
                Acertos(RegistroAprendizagem.AcertosPos, RegistroAprendizagem.NotaPos, bloco),
                total.ToString(), false);
            y += 66f;
        }

        // Registro de jogo em três caixas: soma de todas as aulas, pelo save
        // (os contadores do RegistroAprendizagem zeram a cada abertura do jogo).
        ProgressoJogo.Totais(out float segundosNasFases, out int erros, out int falhas);
        int minutos = Mathf.RoundToInt(segundosNasFases / 60f);
        string[,] dados =
        {
            { "Erros nos quebra-cabeças", erros.ToString() },
            { "Quedas e reinícios", falhas.ToString() },
            { "Tempo nas fases", $"{minutos} min" },
        };
        float wCaixa = (largura - 32f) / 3f;
        for (int i = 0; i < 3; i++)
        {
            Image c = TemaLabUI.Imagem(resultadoRoot, "Registro", linha, TemaLabUI.Painel);
            Caixa(c.rectTransform, i * (wCaixa + 16f), 496f, wCaixa, 84f);
            TextMeshProUGUI r = TemaLabUI.Texto(c.transform, "Rotulo", dados[i, 0], 23f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.CorpoForte, TextAlignmentOptions.MidlineLeft);
            r.textWrappingMode = TextWrappingModes.NoWrap;
            r.enableAutoSizing = true;
            r.fontSizeMin = 16f;
            r.fontSizeMax = 23f;
            TemaLabUI.Esticar(r.rectTransform).offsetMin = new Vector2(24f, 0f);
            r.rectTransform.offsetMax = new Vector2(-110f, 0f);
            TextMeshProUGUI v = TemaLabUI.Texto(c.transform, "Valor", dados[i, 1], 34f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Display, TextAlignmentOptions.MidlineRight);
            v.textWrappingMode = TextWrappingModes.NoWrap;
            TemaLabUI.Esticar(v.rectTransform).offsetMax = new Vector2(-24f, 0f);
        }

        TextMeshProUGUI legenda = TemaLabUI.Texto(resultadoRoot, "LegendaRegistro",
            "Soma de todas as aulas. O tempo conta só as fases, sem o Menu, as pausas e os questionários.",
            20f, TemaLabUI.Ciano, TemaLabUI.Fonte.CorpoForte);
        legenda.textWrappingMode = TextWrappingModes.NoWrap;
        legenda.enableAutoSizing = true;
        legenda.fontSizeMin = 16f;
        legenda.fontSizeMax = 20f;
        Caixa(legenda.rectTransform, 0f, 592f, largura, 30f);

        if (!string.IsNullOrEmpty(RegistroAprendizagem.CaminhoArquivo))
        {
            // Pasta e arquivo em linhas separadas: o caminho inteiro não cabe
            // numa linha e quebrava no meio do nome do arquivo.
            string arquivo = RegistroAprendizagem.CaminhoArquivo;
            LinhaCaminho("Pasta", System.IO.Path.GetDirectoryName(arquivo), 628f, largura - 450f);
            LinhaCaminho("Arquivo", System.IO.Path.GetFileName(arquivo), 656f, largura - 450f);

            TextMeshProUGUI dica = TemaLabUI.Texto(resultadoRoot, "DicaPasta",
                "Aperte P para abrir essa pasta no Explorador de Arquivos.", 18f, TemaLabUI.Ciano, TemaLabUI.Fonte.CorpoForte, TextAlignmentOptions.MidlineLeft);
            dica.textWrappingMode = TextWrappingModes.NoWrap;
            Caixa(dica.rectTransform, 0f, 690f, largura - 450f, 26f);
        }
    }

    private void LinhaCaminho(string rotulo, string valor, float topo, float largura)
    {
        TextMeshProUGUI r = TemaLabUI.Texto(resultadoRoot, "Rotulo" + rotulo, rotulo, 18f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.CorpoForte, TextAlignmentOptions.MidlineLeft);
        r.textWrappingMode = TextWrappingModes.NoWrap;
        Caixa(r.rectTransform, 0f, topo, 86f, 26f);

        // parseCtrlCharacters desligado: com ele, o "\t" de "\testTCC" virava tabulação.
        TextMeshProUGUI v = TemaLabUI.Texto(resultadoRoot, "Caminho" + rotulo, valor, 18f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Mono, TextAlignmentOptions.MidlineLeft);
        v.richText = false;
        v.parseCtrlCharacters = false;
        v.textWrappingMode = TextWrappingModes.NoWrap;
        v.enableAutoSizing = true;
        v.fontSizeMin = 14f;
        v.fontSizeMax = 18f;
        v.overflowMode = TextOverflowModes.Ellipsis;
        Caixa(v.rectTransform, 86f, topo, largura - 86f, 26f);
    }

    private static void AbrirPastaRegistros()
    {
        string arquivo = RegistroAprendizagem.CaminhoArquivo;
        string pasta = string.IsNullOrEmpty(arquivo) ? null : System.IO.Path.GetDirectoryName(arquivo);
        if (string.IsNullOrEmpty(pasta) || !System.IO.Directory.Exists(pasta))
        {
            return;
        }
        Application.OpenURL("file:///" + pasta.Replace('\\', '/'));
    }

    private void CaixaNota(string rotulo, int nota, Color corNumero, float topo)
    {
        TemaLaboratorio tema = TemaLabUI.Tema;
        Image c = TemaLabUI.Imagem(resultadoRoot, "Nota", tema != null ? tema.painelLinha : null, TemaLabUI.Painel);
        Caixa(c.rectTransform, 0f, topo, 560f, 150f);

        TextMeshProUGUI r = TemaLabUI.Texto(c.transform, "Rotulo", rotulo, 24f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.CorpoForte);
        Caixa(r.rectTransform, 30f, 24f, 500f, 32f);

        string valor = nota < 0
            ? "<size=40%><color=#9AA8C4>não respondido</color></size>"
            : $"{nota} <size=50%><color=#9AA8C4>de {perguntas.Length}</color></size>";
        TextMeshProUGUI v = TemaLabUI.Texto(c.transform, "Valor", valor, 68f, corNumero, TemaLabUI.Fonte.Display);
        v.textWrappingMode = TextWrappingModes.NoWrap;
        Caixa(v.rectTransform, 30f, 58f, 500f, 76f);
    }

    private static void LinhaTabela(RectTransform pai, string bloco, string antes, string depois, string total, bool cabecalhoTabela)
    {
        TemaLabUI.Fonte fonte = cabecalhoTabela ? TemaLabUI.Fonte.MonoForte : TemaLabUI.Fonte.CorpoForte;
        float tamanho = cabecalhoTabela ? 18f : 25f;
        Color cor = cabecalhoTabela ? TemaLabUI.TextoSuave : TemaLabUI.CorTexto;

        void Coluna(string nome, string texto, float x, float largura, TextAlignmentOptions al, Color c)
        {
            TextMeshProUGUI t = TemaLabUI.Texto(pai, nome, texto, tamanho, c, fonte, al);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = !cabecalhoTabela;
            t.fontSizeMin = 16f;
            t.fontSizeMax = tamanho;
            if (cabecalhoTabela) t.characterSpacing = 6f;
            t.rectTransform.anchorMin = new Vector2(0f, 0f);
            t.rectTransform.anchorMax = new Vector2(0f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            t.rectTransform.anchoredPosition = new Vector2(x, 0f);
            t.rectTransform.sizeDelta = new Vector2(largura, 0f);
        }

        Coluna("Bloco", bloco, 20f, 300f, TextAlignmentOptions.MidlineLeft, cor);
        Coluna("Antes", antes, 330f, 80f, TextAlignmentOptions.Center, cor);
        Coluna("Seta", cabecalhoTabela ? "" : "→", 410f, 40f, TextAlignmentOptions.Center, TemaLabUI.TextoSuave);
        Coluna("Depois", depois, 450f, 90f, TextAlignmentOptions.Center, cor);
        Coluna("Total", total, 540f, 80f, TextAlignmentOptions.Center, cabecalhoTabela ? cor : TemaLabUI.TextoSuave);
    }

    // Caixa posicionada a partir do canto superior esquerdo do pai.
    private static void Caixa(RectTransform rect, float x, float topo, float largura, float altura)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -topo);
        rect.sizeDelta = new Vector2(largura, altura);
    }
}
