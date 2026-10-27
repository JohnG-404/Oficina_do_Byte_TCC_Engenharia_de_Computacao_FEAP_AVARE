using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Mini-lição de HUD da fase "Interacao_Vida": mostra que a tela só exibe
/// um dado e precisa ser AVISADA quando ele muda.
///
/// Falha proposital: a barra de quadrados (HeartHealthUI) começa
/// "desconectada" do PlayerHealth. Ela desenha o valor inicial e depois não
/// muda mais. No primeiro dano a fase trava, aparece "vidaAtual = N" em cima
/// do jogador, o Prof Byte explica o problema e abre um puzzle curto de 3
/// encaixes (dado, evento, tela — ver HudPuzzleUI). Ao montar, a barra é
/// conectada e passa a mostrar a vida de verdade na hora.
///
/// A lição acontece uma vez só: depois de um Game Over (ver GameOverUI) a
/// fase recomeça com a barra já conectada. O estado é estático e sobrevive
/// ao recarregamento da própria cena; é zerado quando o jogador chega nela
/// vindo de outra (mesmo padrão das dicas do InteracaoLevelFlow).
/// </summary>
public class HudLicao : MonoBehaviour, IDialogueListener
{
    [Header("Referências (auto-encontradas se vazias)")]
    [SerializeField] private HeartHealthUI barraDeVida;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private DialogueManager dialogueManager;

    [Header("Falas do Prof Byte")]
    [Tooltip("Ditas logo depois do primeiro dano. {vida} vira o valor atual da vida.")]
    [TextArea(2, 4)]
    [SerializeField]
    private string[] falasDano =
    {
        "Opa, você tomou dano. A vida caiu pra {vida}...",
        "...mas a barra continua cheia. A vida mudou, mas ninguém avisou a tela.",
        "Num jogo, a tela só mostra um dado. Ela precisa ser avisada toda vez que esse dado muda. Monte esse aviso."
    };

    [Tooltip("Ditas depois que o puzzle é montado e a barra é conectada.")]
    [TextArea(2, 4)]
    [SerializeField]
    private string[] falasFechamento =
    {
        "Pronto. Agora toda vez que a vida muda, o evento avisa a barra e ela se redesenha sozinha."
    };

    [Header("Avisos na tela")]
    [Tooltip("Aparece em cima do jogador no primeiro dano. {vida} vira o valor atual.")]
    [SerializeField] private string textoVidaAtual = "vidaAtual = {vida}";
    [Tooltip("Aparece embaixo da barra enquanto ela está desconectada. {barra} vira o valor que ela ainda mostra.")]
    [SerializeField] private string textoBarraParada = "a barra continua mostrando {barra}";
    [Tooltip("Aparece embaixo da barra quando ela é conectada.")]
    [SerializeField] private string textoBarraConectada = "a barra acompanha a vida";

    [Header("Puzzle (dado, evento, tela)")]
    [SerializeField]
    private HudPuzzleUI.Encaixe[] encaixes =
    {
        new HudPuzzleUI.Encaixe { pecaCorreta = "vidaAtual", dicaCodigo = "int ___ = 3;", legenda = "PlayerHealth", bloco = "o dado e o evento" },
        new HudPuzzleUI.Encaixe { pecaCorreta = "OnHealthChanged", dicaCodigo = "event Action<int, int> ___;", legenda = "PlayerHealth", bloco = "o dado e o evento" },
        new HudPuzzleUI.Encaixe { pecaCorreta = "AtualizarQuadrados", dicaCodigo = "void ___(int vida, int max)", legenda = "HUD", bloco = "a tela" },
    };

    [Tooltip("Ordem em que as peças aparecem na bandeja (já embaralhadas). Ícone, moldura e categoria dos cartões: Tools > Fases > Puzzle da HUD na moldura (item 6).")]
    [SerializeField]
    private HudPuzzleUI.Peca[] pecas =
    {
        new HudPuzzleUI.Peca { nome = "AtualizarQuadrados", descricao = "A tela ouvindo o evento. Recebe a vida nova e redesenha os quadrados." },
        new HudPuzzleUI.Peca { nome = "Start", descricao = "Roda uma vez só, quando a fase começa." },
        new HudPuzzleUI.Peca { nome = "vidaAtual", descricao = "O dado. Um número guardado no PlayerHealth." },
        new HudPuzzleUI.Peca { nome = "Destroy(barra)", descricao = "Apaga o objeto da barra de vida da cena." },
        new HudPuzzleUI.Peca { nome = "OnHealthChanged", descricao = "O evento. Quando a vida muda, ele avisa quem estiver ouvindo." },
    };

    [SerializeField] private HudPuzzleUI.Textos textosPuzzle = new HudPuzzleUI.Textos();

    [Header("Código montado (mostrado ao concluir)")]
    [SerializeField] private string tituloCodigo = "Código montado · HUD";
    [TextArea(4, 10)]
    [SerializeField]
    private string codigoMontado =
        "<color=#8B95A7>// PlayerHealth: o dado muda e o evento avisa</color>\n" +
        "<color=#EF4B4B>vidaAtual</color> -= dano;\n" +
        "<color=#EF4B4B>OnHealthChanged</color>?.Invoke(<color=#EF4B4B>vidaAtual</color>, vidaMax);\n\n" +
        "<color=#8B95A7>// HUD: a tela ouve o evento</color>\n" +
        "playerHealth.<color=#EF4B4B>OnHealthChanged</color> += <color=#EF4B4B>AtualizarQuadrados</color>;";
    [Tooltip("Não fecha mais o painel: ele fica aberto até Enter ou clique (ver CodigoMontadoUI).")]
    [SerializeField] private float segundosCodigo = 8f;

    [Header("Ordem de desenho dos Canvas runtime (abaixo do menu de pausa = 100)")]
    [SerializeField] private int ordemAvisos = 60;
    [SerializeField] private int ordemPuzzle = 70;

    private const string Conceito = "HUD";

    private enum Etapa { Nenhuma, FalaDano, Fechamento }

    // Sobrevive ao recarregamento da própria cena (Game Over), zerada ao
    // chegar vindo de outra cena (ver Start) ou ao iniciar o Play.
    private static bool licaoConcluida;

    /// <summary>True depois que a barra foi conectada (o painel de fim de fase lista a lição).</summary>
    public static bool LicaoConcluida => licaoConcluida;

    [Header("Faixa de objetivo durante a lição")]
    [SerializeField] private string objetivoLicao = "Monte o aviso: dado, evento, tela";
    [SerializeField] private string dicaObjetivoLicao = "A barra não sabe que a vida mudou.";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        licaoConcluida = false;
    }

    private Etapa etapa;
    private bool emAndamento;
    private bool pausouTempo;
    private int ultimaVida = -1;

    private Canvas avisos;
    private RectTransform bolhaVida;
    private RectTransform bolhaBarra;
    private HudPuzzleUI puzzle;
    private Canvas faixaEscondida;
    private Rigidbody2D rbJogador;

    private void Awake()
    {
        if (barraDeVida == null)
        {
            barraDeVida = FindAnyObjectByType<HeartHealthUI>(FindObjectsInactive.Include);
        }

        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        if (dialogueManager == null)
        {
            // Inclui inativos: o painel de diálogo pode já ter se escondido no Awake.
            dialogueManager = FindAnyObjectByType<DialogueManager>(FindObjectsInactive.Include);
        }
    }

    private void Start()
    {
        // Start roda depois de todos os Awake: o InteracaoLevelFlow já sabe
        // se esta carga é um reinício da própria fase ou uma chegada nova.
        bool reinicio = InteracaoLevelFlow.Instance != null && InteracaoLevelFlow.Instance.IsReload;
        if (!reinicio)
        {
            licaoConcluida = false;
        }

        if (licaoConcluida || barraDeVida == null || playerHealth == null)
        {
            // Barra fica conectada (comportamento padrão).
            return;
        }

        // A dica rápida "Fica de olho nos quadrados" do InteracaoLevelFlow
        // contradiz a falha proposital; a lição ocupa esse lugar.
        InteracaoLevelFlow.Instance?.SuppressDamageHint();

        barraDeVida.Desconectar();
        rbJogador = playerHealth.GetComponent<Rigidbody2D>();
        ultimaVida = playerHealth.CurrentHealth;
        playerHealth.OnHealthChanged += OnVidaMudou;
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= OnVidaMudou;
        }

        if (pausouTempo)
        {
            Time.timeScale = 1f;
        }
    }

    private void LateUpdate()
    {
        // O aviso "vidaAtual = N" acompanha o jogador na tela.
        if (bolhaVida != null && playerHealth != null)
        {
            bolhaVida.anchoredPosition = PosicaoAcimaDoJogador();
        }
    }

    private void OnVidaMudou(int atual, int max)
    {
        int anterior = ultimaVida;
        ultimaVida = atual;

        if (licaoConcluida || emAndamento || anterior < 0)
        {
            return;
        }

        // Só o primeiro DANO que não mata. Se o primeiro dano já zerar a
        // vida, vale o Game Over, e a lição fica pra depois do reinício.
        if (atual < anterior && atual > 0)
        {
            StartCoroutine(RodarLicao(atual));
        }
    }

    // ---------------------------------------------------------------- etapas

    private IEnumerator RodarLicao(int vida)
    {
        emAndamento = true;
        // Nenhum dano extra enquanto a lição estiver na tela.
        playerHealth.Invulneravel = true;

        CriarAvisos(vida);

        // Se algum outro diálogo estiver na tela, espera ele acabar.
        while (DialogueManager.IsAnyDialogueActive)
        {
            yield return null;
        }

        if (dialogueManager == null)
        {
            AbrirPuzzle();
            yield break;
        }

        etapa = Etapa.FalaDano;
        ObjetivoFase.Extra(objetivoLicao, dicaObjetivoLicao);
        dialogueManager.SetListener(this);
        dialogueManager.StartDialogue(Formatar(falasDano, "{vida}", vida));
    }

    /// <summary>Chamado pelo DialogueManager ao fim de cada fala da lição.</summary>
    public void OnDialogueFinished()
    {
        Etapa atual = etapa;
        etapa = Etapa.Nenhuma;

        switch (atual)
        {
            case Etapa.FalaDano:
                AbrirPuzzle();
                break;
            case Etapa.Fechamento:
                Finalizar();
                break;
        }
    }

    private void AbrirPuzzle()
    {
        if (bolhaVida != null)
        {
            Destroy(bolhaVida.gameObject);
            bolhaVida = null;
        }

        // Diálogo já não trava mais o movimento aqui: congela a fase inteira
        // enquanto o puzzle está aberto (a UI usa tempo sem escala).
        Time.timeScale = 0f;
        pausouTempo = true;

        EsconderFaixaEAvisos(true);
        puzzle = HudPuzzleUI.Abrir(Conceito, encaixes, pecas, textosPuzzle, ordemPuzzle,
            playerHealth.CurrentHealth, barraDeVida.ValorMostrado, playerHealth.MaxHealth, OnPuzzleConcluido);
    }

    /// <summary>
    /// A faixa de objetivo e o aviso "a barra continua
    /// mostrando N" saem da tela enquanto o puzzle está aberto (apareciam
    /// cortados atrás dele). A Prévia do puzzle mostra a mesma coisa.
    /// </summary>
    private void EsconderFaixaEAvisos(bool esconder)
    {
        if (avisos != null)
        {
            avisos.enabled = !esconder;
        }

        if (esconder)
        {
            // A faixa anima o próprio CanvasGroup; desligar o Canvas não briga com isso.
            Canvas faixa = ObjetivoFase.Instance != null ? ObjetivoFase.Instance.GetComponentInParent<Canvas>() : null;
            if (faixa != null && faixa.rootCanvas.enabled)
            {
                faixaEscondida = faixa.rootCanvas;
                faixaEscondida.enabled = false;
            }
            return;
        }

        if (faixaEscondida != null)
        {
            faixaEscondida.enabled = true;
        }
        faixaEscondida = null;
    }

    private void OnPuzzleConcluido()
    {
        if (!string.IsNullOrEmpty(codigoMontado))
        {
            CodigoMontadoUI.Show(tituloCodigo, codigoMontado, segundosCodigo, ConectarBarra);
            return;
        }

        ConectarBarra();
    }

    private void ConectarBarra()
    {
        if (puzzle != null)
        {
            puzzle.Fechar();
            puzzle = null;
        }
        EsconderFaixaEAvisos(false);

        // Zera a velocidade antes de soltar a física: um pulo "apertado"
        // durante o congelamento não deve sair sozinho agora.
        if (rbJogador != null)
        {
            rbJogador.linearVelocity = Vector2.zero;
        }

        Time.timeScale = 1f;
        pausouTempo = false;

        licaoConcluida = true;
        barraDeVida.Conectar();
        MostrarBolhaBarra(textoBarraConectada, VidaUI.Verde);

        StartCoroutine(FalarFechamento());
    }

    private IEnumerator FalarFechamento()
    {
        // Espera um instante: o Enter que fechou o painel de código não pode
        // pular a primeira fala, e o jogador vê a barra mudar antes do Prof falar.
        yield return new WaitForSecondsRealtime(0.6f);

        if (dialogueManager == null || falasFechamento == null || falasFechamento.Length == 0)
        {
            Finalizar();
            yield break;
        }

        etapa = Etapa.Fechamento;
        dialogueManager.SetListener(this);
        dialogueManager.StartDialogue(falasFechamento);
    }

    private void Finalizar()
    {
        playerHealth.Invulneravel = false;
        emAndamento = false;
        ObjetivoFase.FimExtra();

        // Devolve o diálogo pro dono da fase.
        if (dialogueManager != null && InteracaoLevelFlow.Instance != null)
        {
            dialogueManager.SetListener(InteracaoLevelFlow.Instance);
        }

        if (avisos != null)
        {
            Destroy(avisos.gameObject, 1.5f);
            avisos = null;
        }
    }

    // ---------------------------------------------------------------- avisos

    private void CriarAvisos(int vida)
    {
        avisos = VidaUI.CriarCanvas("HudLicaoAvisos", ordemAvisos);
        avisos.transform.SetParent(transform, false);
        var raiz = (RectTransform)avisos.transform;

        // Invisível, mas bloqueia cliques no cenário (InteractableItem ignora
        // cliques com o ponteiro em cima de UI) enquanto a lição roda.
        VidaUI.Esticar(VidaUI.NovaImagem(raiz, "BloquearCliques", new Color(0f, 0f, 0f, 0f), raycast: true).rectTransform);

        bolhaVida = CriarBolha(raiz, "VidaAtual", textoVidaAtual.Replace("{vida}", vida.ToString()), VidaUI.Vermelho, new Vector2(300f, 60f), 30);
        TextMeshProUGUI codigo = bolhaVida.GetComponentInChildren<TextMeshProUGUI>();
        if (codigo != null) codigo.font = TemaLabUI.FonteDe(TemaLabUI.Fonte.MonoForte);
        bolhaVida.anchoredPosition = PosicaoAcimaDoJogador();
        StartCoroutine(Pular(bolhaVida));

        MostrarBolhaBarra(textoBarraParada.Replace("{barra}", barraDeVida.ValorMostrado.ToString()), VidaUI.TextoApagado);
    }

    private void MostrarBolhaBarra(string texto, Color cor)
    {
        if (avisos == null)
        {
            return;
        }

        if (bolhaBarra != null)
        {
            Destroy(bolhaBarra.gameObject);
        }

        var raiz = (RectTransform)avisos.transform;
        bolhaBarra = CriarBolha(raiz, "Barra", texto, cor, new Vector2(460f, 48f), 24);
        bolhaBarra.pivot = new Vector2(0f, 1f);
        bolhaBarra.anchoredPosition = PosicaoAbaixoDaBarra(raiz);
        StartCoroutine(Pular(bolhaBarra));
    }

    private static RectTransform CriarBolha(RectTransform raiz, string nome, string texto, Color cor, Vector2 tamanho, float fonte)
    {
        RectTransform fundo = VidaUI.CaixaComBorda(raiz, nome, Vector2.zero, tamanho, cor, VidaUI.Fundo, 3f);
        TextMeshProUGUI tmp = VidaUI.NovoTexto(fundo, "Texto", texto, fonte, cor, TextAlignmentOptions.Center, FontStyles.Bold);
        tmp.richText = false;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 14f;
        tmp.fontSizeMax = fonte;
        VidaUI.Esticar(tmp.rectTransform, 6f);
        return (RectTransform)fundo.parent;
    }

    private Vector2 PosicaoAcimaDoJogador()
    {
        Camera cam = Camera.main;
        if (cam == null || avisos == null)
        {
            return new Vector2(0f, 120f);
        }

        Vector3 mundo = playerHealth.transform.position + Vector3.up * 1.4f;
        Vector2 tela = cam.WorldToScreenPoint(mundo);
        return VidaUI.TelaParaLocal((RectTransform)avisos.transform, tela);
    }

    // Logo abaixo do canto inferior esquerdo da fileira de quadrados, em
    // qualquer modo de Canvas que o HUD use.
    private Vector2 PosicaoAbaixoDaBarra(RectTransform raiz)
    {
        var barraRect = barraDeVida.transform as RectTransform;
        if (barraRect == null)
        {
            return new Vector2(-900f, 380f);
        }

        Canvas canvasBarra = barraDeVida.GetComponentInParent<Canvas>();
        Camera cam = canvasBarra != null && canvasBarra.renderMode != RenderMode.ScreenSpaceOverlay ? canvasBarra.worldCamera : null;

        var cantos = new Vector3[4];
        barraRect.GetWorldCorners(cantos);
        Vector2 tela = RectTransformUtility.WorldToScreenPoint(cam, cantos[0]);
        return VidaUI.TelaParaLocal(raiz, tela) + new Vector2(0f, -10f);
    }

    // Pequeno "pop" de entrada (tempo sem escala).
    private static IEnumerator Pular(RectTransform alvo)
    {
        const float duracao = 0.25f;
        for (float t = 0f; t < duracao && alvo != null; t += Time.unscaledDeltaTime)
        {
            float k = t / duracao;
            float s = k < 0.7f ? Mathf.Lerp(0f, 1.12f, k / 0.7f) : Mathf.Lerp(1.12f, 1f, (k - 0.7f) / 0.3f);
            alvo.localScale = Vector3.one * s;
            yield return null;
        }

        if (alvo != null)
        {
            alvo.localScale = Vector3.one;
        }
    }

    private static string[] Formatar(string[] linhas, string marcador, int valor)
    {
        var saida = new string[linhas.Length];
        for (int i = 0; i < linhas.Length; i++)
        {
            saida[i] = linhas[i].Replace(marcador, valor.ToString());
        }

        return saida;
    }
}
