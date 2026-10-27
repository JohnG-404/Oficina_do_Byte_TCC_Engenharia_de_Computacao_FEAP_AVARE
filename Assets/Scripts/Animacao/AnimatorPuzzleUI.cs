using System.Globalization;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puzzle do Animator da fase "Animacao": um diagrama de estados (Idle,
/// Corrida, Pulo e Any State) com 4 setas vazias, e o jogador encaixa em
/// cada seta a condição que dispara aquela troca de animação. As condições
/// usam o que a fase Fisica ensinou (linearVelocity.x, isGrounded); ao
/// encaixar, a peça mostra também como a condição fica escrita no Animator
/// (parâmetros velocidadeX/noChao, os mesmos do AnimatorController real).
///
/// Mesmas regras dos outros puzzles de encaixe do jogo: passar o mouse
/// mostra a descrição, peça errada recomeça o puzzle inteiro (de propósito)
/// e conta como erro no RegistroAprendizagem.
///
/// Uma prévia ao lado roda uma pequena simulação (anda, pula correndo, para,
/// pula parado) e mostra qual animação o Animator escolheria SÓ com as setas
/// já preenchidas. Com nada encaixado, o boneco fica no Idle deslizando, a
/// mesma falha que o jogador acabou de ver; cada seta nova "conserta" um
/// pedaço. O estado ativo acende no diagrama.
///
/// Este componente fica na cena (o AnimacaoSceneBuilder cria o objeto
/// "AnimatorPuzzle" e liga os sprites da prévia); textos e peças são
/// editáveis no Inspector. A UI em si é UGUI montada em runtime, com Canvas
/// próprio (mesma paleta dos painéis do jogo, ver VidaUI).
///
/// Modo agachar (<see cref="modoAgachar"/>): o mesmo puzzle vira o mini-puzzle
/// do túnel. O diagrama ganha a linha Agachado / AndarAgachado, as 4 setas
/// do primeiro puzzle já aparecem prontas, e só as 2 setas novas têm
/// encaixe. A prévia mostra o personagem agachando e andando agachado. A
/// cena tem um segundo objeto ("AgacharPuzzle") com este modo ligado.
///
/// A janela usa a moldura dos puzzles da Física e da
/// IA (MolduraPuzzleUI): cabeçalho "Fase Animação  Puzzle N de 2", janela
/// Player com o diagrama, Objetivo com a prévia, peças em cartões com ícone
/// e categoria, e a Dica embaixo (dica da seta da vez; a descrição da peça
/// ao passar o mouse). Enquanto o puzzle está aberto, a placa VIDA
/// e a faixa de objetivo ficam escondidas.
/// </summary>
public class AnimatorPuzzleUI : MonoBehaviour, IDonoPecaAnimator
{
    [Serializable]
    public class Peca
    {
        [Tooltip("Texto da peça (a condição, com os nomes da física do Unity).")]
        public string texto;

        [Tooltip("Como essa condição fica escrita no Animator (mostrado depois de encaixar). Vazio nas iscas.")]
        public string textoAnimator;

        [Tooltip("Mostrado no painel Descrição ao passar o mouse.")]
        [TextArea(2, 5)] public string descricao;

        [Tooltip("Seta onde ela entra: 0 = Idle -> Corrida, 1 = Corrida -> Idle, 2 = Any State -> Pulo, 3 = Pulo -> Idle, 4 = Idle -> Agachado, 5 = Agachado -> AndarAgachado (modo agachar), -1 = isca (não entra em lugar nenhum).")]
        public int setaCorreta = -1;

        [Header("Cartão (moldura da Física)")]
        public Sprite icone;
        [Tooltip("Moldura da cor do tipo (Assets/Images/EditorKit/pecas).")]
        public Sprite moldura;
        [Tooltip("Etiqueta à direita do cartão (CONDIÇÃO, ENTRADA, MÉTODO, PARÂMETRO...).")]
        public string categoria;
        public Color corCategoria = Color.white;
        [Tooltip("Linha menor embaixo do nome (de onde vem o valor).")]
        public string subtitulo;
    }

    private class Encaixe
    {
        public int seta;
        public RectTransform rect;
        public Image fundo;
        public Image vazio;
        public GameObject destaque;
        public AnimatorPuzzlePeca ocupante;
    }

    private const string Conceito = "Animator";
    private static readonly string[] NomesSeta = { "Idle->Corrida", "Corrida->Idle", "AnyState->Pulo", "Pulo->Idle", "Idle->Agachado", "Agachado->AndarAgachado" };

    [Header("Peças (condições)")]
    [SerializeField] private Peca[] pecas = PecasPadrao();

    [Header("Moldura (padrão da Física)")]
    [SerializeField] private string fase = "Fase Animação";
    [Tooltip("Posição deste puzzle na lista de etapas (1 = primeiro).")]
    [SerializeField] private int numeroPuzzle = 1;
    [SerializeField] private string[] etapas = { "Animator do Player", "Agachar" };
    [SerializeField] private string abaJanela = "Player";
    [SerializeField] private string legendaDiagrama = "ANIMATOR DO PLAYER · BASE LAYER";
    [SerializeField] private string objetivo = "a animação acompanha a física";
    [Tooltip("Uma dica por seta com encaixe, na ordem em que o jogador monta (a dica da primeira seta vazia aparece no painel Dica).")]
    [TextArea(2, 4)]
    [SerializeField] private string[] dicasSetas =
    {
        "Seta 1, de Idle para Corrida: o que muda no Rigidbody2D quando o personagem começa a andar?",
        "Seta 2, de Corrida para Idle: o que fica igual a zero quando o personagem para?",
        "Seta 3, de Any State para Pulo: o que deixa de ser verdade quando os pés saem do chão?",
        "Seta 4, de Pulo para Idle: o que avisa que o personagem voltou a pisar em alguma coisa?",
    };
    [SerializeField] private string rodapeDica = "Passe o mouse numa peça pra ler aqui o que ela faz.";
    [Tooltip("Modo agachar: ícone de cada seta que já vem pronta, na ordem 0 a 3.")]
    [SerializeField] private Sprite[] iconesProntas;

    [Header("Textos")]
    [SerializeField] private string erroTitulo = "Essa condição não serve nessa seta";
    [TextArea(2, 5)]
    [SerializeField] private string erroTexto = "O Animator foi zerado. Leia as peças de novo e pense: o que precisa ser verdade pra trocar de uma animação pra outra?";
    [SerializeField] private string concluidoTitulo = "Animator montado!";
    [TextArea(2, 5)]
    [SerializeField] private string concluidoTexto = "As quatro setas têm condição. Olhe a prévia: agora a animação acompanha a física sozinha.";

    [Header("Prévia do personagem")]
    [Tooltip("Sprite do Idle (o mesmo do clipe Idle). O builder preenche.")]
    [SerializeField] private Sprite spriteIdle;
    [Tooltip("Altura dos pés dentro do sprite (0 = borda de baixo, 1 = borda de cima). Os sprites do visor têm espaço vazio embaixo: os pés ficam em ~0.04.")]
    [SerializeField, Range(0f, 0.5f)] private float pesNoSprite = 0.04f;
    [SerializeField] private Sprite[] framesCorrida;
    [SerializeField] private Sprite[] framesPulo;
    [SerializeField] private float framesPorSegundo = 12f;

    [Header("Modo agachar (mini-puzzle do túnel)")]
    [Tooltip("Liga o diagrama com Agachado/AndarAgachado: as 4 setas do primeiro puzzle vêm prontas e só as setas 4 e 5 têm encaixe.")]
    [SerializeField] private bool modoAgachar;
    [Tooltip("Condição mostrada (fixa) em cima de cada seta já montada no primeiro puzzle, na ordem 0 a 3.")]
    [SerializeField] private string[] condicoesProntas = { "velocidadeX > 0.1", "velocidadeX < 0.1", "noChao == false", "noChao == true" };
    [SerializeField] private Sprite[] framesAgachado;
    [SerializeField] private Sprite[] framesAndarAgachado;

    [Tooltip("Ordem do Canvas do puzzle: acima do HUD/diálogo da cena, abaixo do CodigoMontadoUI (500).")]
    [SerializeField] private int sortingOrder = 400;

    public bool Aberto => canvasGO != null;
    public bool Concluido { get; private set; }

    private readonly List<Encaixe> encaixes = new List<Encaixe>();
    private readonly List<AnimatorPuzzlePeca> pecasUI = new List<AnimatorPuzzlePeca>();

    private GameObject canvasGO;
    private RectTransform camadaArraste;
    private TextMeshProUGUI descTitulo;
    private TextMeshProUGUI descTexto;
    private AnimatorDiagramaUI diagrama;
    private Action aoConcluir;
    private float inicio;
    private int erros;

    // Moldura
    private Image[] pontosProgresso;
    private TextMeshProUGUI textoProgresso;
    private int setaDaVez = -1;
    private readonly List<GameObject> hudEscondido = new List<GameObject>();
    private Canvas canvasObjetivoEscondido;

    // Prévia
    private RectTransform previaBoneco;
    private Image previaImagem;
    private TextMeshProUGUI previaLeitura1;
    private TextMeshProUGUI previaLeitura2;
    private string[] chipsEstados;
    private Image[] chipsFundo;
    private TextMeshProUGUI[] chipsTexto;
    private float previaTempo;
    private float previaX;
    private string previaEstadoAtual = AnimacaoPlayerFisica.EstadoIdle;
    private float previaTempoNoEstado;
    private int setaAcesa = -1;
    private float setaAcesaAte;

    private const float PreviaCiclo = 9f;
    private const float PreviaVelocidade = 4f;
    private const float PreviaXInicial = -120f;
    private const float PreviaPixelsPorUnidade = 14f;
    private const float PreviaAlturaPulo = 50f;
    // Caixa Objetivo (pixels da tela de referência, canto de cima à esquerda).
    private const float ObjX = 955f, ObjY = 140f, ObjW = 333f, ObjH = 653f;
    private const float PreviaCentroX = ObjX + ObjW * 0.5f;
    private const float PreviaChaoTopo = 640f;
    // Pixels de UI por unidade de mundo: cada sprite aparece no mesmo
    // tamanho relativo que tem no jogo (os quadros de corrida/pulo têm
    // espaço vazio em volta, o Idle não).
    private const float PreviaEscala = 65f;

    // Roteiro da prévia no modo agachar: parado, agacha, anda agachado, para, levanta.
    private const float PreviaCicloAgachar = 7f;
    private const float PreviaVelocidadeAgachado = 2f;
    private bool previaAgachado;

    private float EscalaDiagrama => modoAgachar ? 0.7f : 0.78f;
    private int[] SetasDoPuzzle => modoAgachar
        ? new[] { AnimatorDiagramaUI.SetaIdleAgachado, AnimatorDiagramaUI.SetaAgachadoAndar }
        : new[] { AnimatorDiagramaUI.SetaIdleCorrida, AnimatorDiagramaUI.SetaCorridaIdle, AnimatorDiagramaUI.SetaAnyPulo, AnimatorDiagramaUI.SetaPuloIdle };

    /// <summary>Monta e abre o puzzle. <paramref name="aoConcluir"/> é chamado quando as 4 setas estiverem certas (o puzzle continua na tela até alguém chamar <see cref="Fechar"/>).</summary>
    public void Abrir(Action aoConcluir)
    {
        if (Aberto)
        {
            return;
        }

        this.aoConcluir = aoConcluir;
        Concluido = false;
        erros = 0;
        encaixes.Clear();
        pecasUI.Clear();

        previaTempo = 0f;
        previaX = PreviaXInicial;
        previaEstadoAtual = AnimacaoPlayerFisica.EstadoIdle;
        previaTempoNoEstado = 0f;
        previaAgachado = false;
        setaAcesa = -1;

        Canvas canvas = VidaUI.CriarCanvas("AnimatorPuzzleUI", sortingOrder);
        canvasGO = canvas.gameObject;
        JogoBloqueado.Abrir(this);
        Montar((RectTransform)canvas.transform);
        EsconderHud(true);

        inicio = Time.unscaledTime;
        RegistroAprendizagem.Registrar("puzzle_inicio", Conceito);
    }

    public void Fechar()
    {
        if (canvasGO != null)
        {
            Destroy(canvasGO);
        }

        canvasGO = null;
        JogoBloqueado.Fechar(this);
        diagrama = null;
        encaixes.Clear();
        pecasUI.Clear();
        EsconderHud(false);
    }

    /// <summary>
    /// A placa VIDA e a faixa de objetivo saem da tela enquanto o
    /// puzzle está aberto (a IA faz o mesmo com a placa, ver IAFlow.MostrarHudVida).
    /// </summary>
    private void EsconderHud(bool esconder)
    {
        if (esconder)
        {
            // A cena tem mais de um Canvas com placa (TemaLabFases.EstilizarVida): some com todas as ativas.
            foreach (RectTransform rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude))
            {
                if (rt.name == "PlacaVida")
                {
                    rt.gameObject.SetActive(false);
                    hudEscondido.Add(rt.gameObject);
                }
            }

            // A faixa anima o próprio CanvasGroup; desligar o Canvas não briga com isso.
            Canvas faixa = ObjetivoFase.Instance != null ? ObjetivoFase.Instance.GetComponentInParent<Canvas>() : null;
            if (faixa != null && faixa.rootCanvas.enabled)
            {
                canvasObjetivoEscondido = faixa.rootCanvas;
                canvasObjetivoEscondido.enabled = false;
            }
            return;
        }

        foreach (GameObject go in hudEscondido)
        {
            if (go != null) go.SetActive(true);
        }
        hudEscondido.Clear();
        if (canvasObjetivoEscondido != null) canvasObjetivoEscondido.enabled = true;
        canvasObjetivoEscondido = null;
    }

    private void OnDestroy()
    {
        Fechar();
    }

    // ---------------------------------------------------------------- montagem

    private void Montar(RectTransform raiz)
    {
        // Fundo opaco da moldura: também bloqueia cliques no cenário por trás.
        RectTransform tela = MolduraPuzzleUI.Tela(raiz);
        MolduraPuzzleUI.Cabecalho(tela, fase, numeroPuzzle, etapas);

        // Janela Player: diagrama (esquerda) + Objetivo com a prévia (direita).
        MolduraPuzzleUI.Janela(tela, "JanelaPlayer", 38, 80, 1282, 743, abaJanela);
        RectTransform areaDiagrama = MolduraPuzzleUI.Area(tela, "AreaDiagrama", 70, 140, 860, 653);
        MolduraPuzzleUI.Legenda(tela, legendaDiagrama, 90, 167);
        if (spriteIdle != null)
        {
            Image retrato = VidaUI.NovaImagem(tela, "Retrato", Color.white);
            retrato.sprite = spriteIdle;
            retrato.preserveAspect = true;
            MolduraPuzzleUI.TL(retrato.rectTransform, 830, 136, 96, 96);
        }

        diagrama = AnimatorDiagramaUI.Criar(areaDiagrama, new Vector2(0f, modoAgachar ? -28f : -20f), EscalaDiagrama, true, modoAgachar);

        if (modoAgachar)
        {
            // As setas do primeiro puzzle já estão montadas: aparecem como peças encaixadas.
            for (int i = 0; i < AnimatorDiagramaUI.TotalSetas; i++)
            {
                CriarCondicaoPronta(diagrama.Raiz, i);
            }
        }

        foreach (int seta in SetasDoPuzzle)
        {
            encaixes.Add(CriarEncaixe(diagrama.Raiz, seta));
        }

        MontarPrevia(tela);

        // Peças (direita)
        MolduraPuzzleUI.Janela(tela, "JanelaPecas", 1350, 80, 543, 743, "Peças");
        var bandejaGO = new GameObject("Pecas", typeof(RectTransform));
        RectTransform bandeja = MolduraPuzzleUI.TL((RectTransform)bandejaGO.transform, 0, 0, 543, 743);
        bandeja.SetParent(tela, false);
        MolduraPuzzleUI.TL(bandeja, 1350, 80, 543, 743);
        int qtd = pecas != null ? Mathf.Min(pecas.Length, 8) : 0;
        float primeiro = 41f * (qtd - 1);   // cartões de 62 px a cada 82 px, centrados na janela
        for (int i = 0; i < qtd; i++)
        {
            pecasUI.Add(CriarPeca(bandeja, pecas[i], new Vector2(18.5f, primeiro - 82f * i - 17.5f)));
        }

        // Dica (embaixo)
        MolduraPuzzleUI.PainelDica dica = MolduraPuzzleUI.Dica(tela, rodapeDica);
        descTitulo = dica.titulo;
        descTexto = dica.texto;

        // Por último = desenhada por cima de tudo: a peça sendo arrastada vem pra cá.
        var arrasteGO = new GameObject("Arraste", typeof(RectTransform));
        camadaArraste = (RectTransform)arrasteGO.transform;
        camadaArraste.SetParent(raiz, false);
        VidaUI.Esticar(camadaArraste);

        diagrama.DestacarEstado(previaEstadoAtual);
        AtualizarChips();
        AtualizarSetaDaVez();
        MostrarPadrao();
    }

    private void CriarCondicaoPronta(RectTransform raizDiagrama, int seta)
    {
        string texto = condicoesProntas != null && seta < condicoesProntas.Length ? condicoesProntas[seta] : "";
        Sprite icone = iconesProntas != null && seta < iconesProntas.Length ? iconesProntas[seta] : null;
        Vector2 tam = AnimatorDiagramaUI.TamanhoEncaixe * EscalaDiagrama;
        MolduraPuzzleUI.Cartao c = MolduraPuzzleUI.NovoCartao(raizDiagrama, "Pronta_" + NomesSeta[seta], null, icone, TextoSeguro(texto), "", "", MolduraPuzzleUI.Dourado);
        c.moldura.raycastTarget = false;
        VidaUI.Centro(c.moldura.rectTransform, diagrama.CentroEncaixe(seta), tam);
        MolduraPuzzleUI.EstilizarCartao(c, true, null);
    }

    private Encaixe CriarEncaixe(RectTransform raizDiagrama, int seta)
    {
        TemaLaboratorio tema = TemaLaboratorio.Atual;
        Vector2 tam = AnimatorDiagramaUI.TamanhoEncaixe * EscalaDiagrama;
        Vector2 centro = diagrama.CentroEncaixe(seta);

        // Fundo escuro (a seta não aparece por dentro) e o encaixe tracejado da Física por cima.
        Image fundo = VidaUI.NovaImagem(raizDiagrama, "Encaixe_" + NomesSeta[seta], MolduraPuzzleUI.FundoEncaixe);
        RectTransform rect = VidaUI.Centro(fundo.rectTransform, centro, tam);
        Image miolo = VidaUI.NovaImagem(rect, "Miolo", Color.clear);
        VidaUI.Esticar(miolo.rectTransform);
        fundo.color = Color.clear;
        miolo.color = MolduraPuzzleUI.FundoEncaixe;
        miolo.rectTransform.offsetMin = new Vector2(11f, 4f);
        miolo.rectTransform.offsetMax = new Vector2(-3f, -4f);

        Image vazio = VidaUI.NovaImagem(rect, "Tracejado", MolduraPuzzleUI.TintaEncaixe);
        vazio.sprite = tema != null ? tema.puzzleEncaixeVazio : null;
        VidaUI.Esticar(vazio.rectTransform);
        if (vazio.sprite == null) VidaUI.BordaTracejada(rect, tam, VidaUI.Tracejado);

        // Brilho da seta da vez (como a lacuna acesa da Física).
        Image destaque = VidaUI.NovaImagem(rect, "Destaque", new Color(1f, 1f, 1f, 0.95f));
        destaque.sprite = tema != null ? tema.puzzleContornoSelecao : null;
        destaque.type = Image.Type.Sliced;
        destaque.pixelsPerUnitMultiplier = 2f;
        VidaUI.Esticar(destaque.rectTransform, -6f);
        destaque.gameObject.SetActive(false);

        return new Encaixe { seta = seta, rect = rect, fundo = miolo, vazio = vazio, destaque = destaque.gameObject };
    }

    private AnimatorPuzzlePeca CriarPeca(RectTransform bandeja, Peca dados, Vector2 casa)
    {
        MolduraPuzzleUI.Cartao c = MolduraPuzzleUI.NovoCartao(bandeja, "Peca_" + dados.texto, dados.moldura, dados.icone,
            TextoSeguro(dados.texto), dados.subtitulo, dados.categoria, dados.corCategoria);
        VidaUI.Centro(c.moldura.rectTransform, casa, MolduraPuzzleUI.TamanhoCartao);
        MolduraPuzzleUI.EstilizarCartao(c, false, null);

        var peca = c.moldura.gameObject.AddComponent<AnimatorPuzzlePeca>();
        peca.Iniciar(this, dados, c.nome);
        peca.aoMudarDesenho = (encaixada, texto) => MolduraPuzzleUI.EstilizarCartao(c, encaixada, texto);
        return peca;
    }

    private void MontarPrevia(RectTransform tela)
    {
        TemaLaboratorio tema = TemaLaboratorio.Atual;
        MolduraPuzzleUI.Area(tela, "Objetivo", ObjX, ObjY, ObjW, ObjH);
        MolduraPuzzleUI.Txt(tela, "ObjetivoTitulo", "<b>Objetivo</b>", 975, 170, 22, MolduraPuzzleUI.CorpoForte, MolduraPuzzleUI.TituloAzul);
        MolduraPuzzleUI.Txt(tela, "ObjetivoFrase", objetivo, 975, 211, 16, MolduraPuzzleUI.Corpo, new Color32(0xC9, 0xD6, 0xEA, 0xFF));
        MolduraPuzzleUI.Divisoria(tela, 973, 242, 297);

        // Caixas dos estados: a do estado ativo acende (como Patrulhar/Perseguir/Atacar na IA).
        chipsEstados = modoAgachar
            ? new[] { AnimacaoPlayerFisica.EstadoIdle, AnimacaoPlayerFisica.EstadoCorrida, AnimacaoPlayerFisica.EstadoAgachado, AnimacaoPlayerFisica.EstadoAndarAgachado }
            : new[] { AnimacaoPlayerFisica.EstadoIdle, AnimacaoPlayerFisica.EstadoCorrida, AnimacaoPlayerFisica.EstadoPulo };
        int n = chipsEstados.Length;
        int porLinha = n > 3 ? 2 : 3;
        float gap = 6f;
        float w = (297f - gap * (porLinha - 1)) / porLinha;
        float h = n > 3 ? 30f : 38f;
        chipsFundo = new Image[n];
        chipsTexto = new TextMeshProUGUI[n];
        for (int i = 0; i < n; i++)
        {
            int col = i % porLinha, lin = i / porLinha;
            Image ch = VidaUI.NovaImagem(tela, "Estado_" + chipsEstados[i], MolduraPuzzleUI.CaixaApagada);
            ch.sprite = tema != null ? tema.puzzleCaixa : null;
            MolduraPuzzleUI.TL(ch.rectTransform, 973 + col * (w + gap), 257 + lin * (h + gap), w, h);
            TextMeshProUGUI t = VidaUI.NovoTexto(ch.transform, "Texto", chipsEstados[i], 18, Color.white, TextAlignmentOptions.Center);
            t.font = MolduraPuzzleUI.CorpoForte;
            t.fontStyle = FontStyles.Normal;
            t.enableAutoSizing = true;
            t.fontSizeMin = 14f;
            t.fontSizeMax = 18f;
            VidaUI.Esticar(t.rectTransform, 4f);
            chipsFundo[i] = ch;
            chipsTexto[i] = t;
        }

        float yLeitura = n > 3 ? 345f : 330f;
        Color cinza = new Color32(0x8B, 0x95, 0xA7, 0xFF);
        previaLeitura1 = MolduraPuzzleUI.Txt(tela, "Leitura1", "", 975, yLeitura, 15, MolduraPuzzleUI.Mono, cinza);
        previaLeitura2 = MolduraPuzzleUI.Txt(tela, "Leitura2", "", 975, yLeitura + 24f, 15, MolduraPuzzleUI.Mono, cinza);

        // Chão da prévia
        Sprite tile = tema != null ? tema.puzzleChaoPrevia : null;
        for (float x = ObjX + 2f; x < ObjX + ObjW - 2f; x += 41f)
        {
            Image t = VidaUI.NovaImagem(tela, "Chao", tile != null ? Color.white : VidaUI.Moldura);
            t.sprite = tile;
            MolduraPuzzleUI.TL(t.rectTransform, x, PreviaChaoTopo, Mathf.Min(41f, ObjX + ObjW - 2f - x), 41f);
        }

        previaImagem = VidaUI.NovaImagem(tela, "Boneco", Color.white);
        previaImagem.preserveAspect = true;
        previaBoneco = previaImagem.rectTransform;
        previaBoneco.anchorMin = previaBoneco.anchorMax = new Vector2(0f, 1f);
        // Ponto de apoio nos pés: o personagem fica em cima da linha do chão.
        previaBoneco.pivot = new Vector2(0.5f, pesNoSprite);
        previaBoneco.anchoredPosition = new Vector2(PreviaCentroX + PreviaXInicial, -PreviaChaoTopo);
        AplicarSpriteDaPrevia(SpriteDaPrevia(AnimacaoPlayerFisica.EstadoIdle, 0f));

        // Progresso: uma barra por seta com encaixe.
        int m = SetasDoPuzzle.Length;
        float larg = 293f, folga = 8f, wb = (larg - folga * (m - 1)) / m;
        pontosProgresso = new Image[m];
        for (int i = 0; i < m; i++)
        {
            Image b = VidaUI.NovaImagem(tela, "Ponto" + (i + 1), new Color32(0x22, 0x30, 0x4A, 0xFF));
            b.sprite = tema != null ? tema.puzzleCaixa : null;
            MolduraPuzzleUI.TL(b.rectTransform, 975 + i * (wb + folga), 706, wb, 7);
            pontosProgresso[i] = b;
        }
        textoProgresso = MolduraPuzzleUI.Txt(tela, "Progresso", "", 975, 740, 19, MolduraPuzzleUI.Corpo, MolduraPuzzleUI.Rotulo);
        MolduraPuzzleUI.Txt(tela, "ProgressoLegenda", "A prévia repete o objetivo em loop.", 975, 767, 15, MolduraPuzzleUI.Corpo, MolduraPuzzleUI.Escuro);
    }

    private void AtualizarChips()
    {
        if (chipsFundo == null) return;
        for (int i = 0; i < chipsFundo.Length; i++)
        {
            bool ativo = chipsEstados[i] == previaEstadoAtual;
            chipsFundo[i].color = ativo ? MolduraPuzzleUI.Dourado : MolduraPuzzleUI.CaixaApagada;
            chipsTexto[i].color = ativo ? (Color)new Color32(0x1A, 0x22, 0x33, 0xFF) : new Color(0.55f, 0.6f, 0.71f, 1f);
        }
    }

    /// <summary>Acende a primeira seta vazia (brilho no encaixe + seta amarela) e atualiza o progresso.</summary>
    private void AtualizarSetaDaVez()
    {
        Encaixe vez = null;
        int feitos = 0;
        foreach (Encaixe e in encaixes)
        {
            if (e.ocupante != null) feitos++;
            else if (vez == null) vez = e;
        }

        int nova = vez != null ? vez.seta : -1;
        if (setaDaVez >= 0 && setaDaVez != nova && diagrama != null) diagrama.DestacarSeta(setaDaVez, false);
        setaDaVez = nova;
        if (setaDaVez >= 0 && diagrama != null) diagrama.DestacarSeta(setaDaVez, true);
        foreach (Encaixe e in encaixes)
        {
            if (e.destaque != null) e.destaque.SetActive(e == vez);
        }

        if (pontosProgresso != null)
        {
            for (int i = 0; i < pontosProgresso.Length; i++)
            {
                pontosProgresso[i].color = i < feitos ? MolduraPuzzleUI.Dourado : (Color)new Color32(0x22, 0x30, 0x4A, 0xFF);
            }
        }
        if (textoProgresso != null) textoProgresso.text = $"<b>{feitos} de {encaixes.Count} encaixe{(encaixes.Count == 1 ? "" : "s")}.</b>";
    }

    private string DicaDaVez()
    {
        for (int i = 0; i < encaixes.Count; i++)
        {
            if (encaixes[i].ocupante == null)
            {
                return dicasSetas != null && i < dicasSetas.Length ? dicasSetas[i] : "";
            }
        }
        return "";
    }

    // ---------------------------------------------------------------- lógica

    internal RectTransform CamadaArraste => camadaArraste;

    RectTransform IDonoPecaAnimator.CamadaArraste => camadaArraste;
    void IDonoPecaAnimator.Soltar(AnimatorPuzzlePeca peca, Vector2 pontoTela) => Soltar(peca, pontoTela);
    void IDonoPecaAnimator.MostrarDescricao(Peca dados) => MostrarDescricao(dados);
    void IDonoPecaAnimator.MostrarPadrao() => MostrarPadrao();

    /// <summary>Texto sem interpretação de rich text (as condições têm "&lt;", "&gt;" e "!=").</summary>
    public static string TextoSeguro(string s)
    {
        return "<noparse>" + (s ?? "") + "</noparse>";
    }

    internal void MostrarDescricao(Peca dados)
    {
        if (descTitulo == null)
        {
            return;
        }

        descTitulo.color = Color.white;
        descTitulo.text = TextoSeguro(dados.texto);
        descTexto.text = dados.descricao;
    }

    /// <summary>Painel Dica: a dica da seta da vez (ou o texto de concluído).</summary>
    internal void MostrarPadrao()
    {
        if (descTitulo == null)
        {
            return;
        }

        descTitulo.color = Concluido ? VidaUI.Verde : MolduraPuzzleUI.TituloAzul;
        descTitulo.text = Concluido ? concluidoTitulo : "Dica";
        descTexto.text = Concluido ? concluidoTexto : DicaDaVez();
    }

    /// <summary>Chamado pela peça ao ser solta.</summary>
    internal void Soltar(AnimatorPuzzlePeca peca, Vector2 pontoTela)
    {
        if (Concluido)
        {
            peca.VoltarParaCasa();
            return;
        }

        Encaixe alvo = EncaixeEm(pontoTela) ?? EncaixeEm(peca.CentroNaTela());
        if (alvo == null || alvo.ocupante != null)
        {
            peca.VoltarParaCasa();
            return;
        }

        if (peca.Dados.setaCorreta == alvo.seta)
        {
            Encaixar(peca, alvo);
            return;
        }

        // Peça errada: registra e recomeça o puzzle inteiro (intencional,
        // igual aos outros puzzles de encaixe do jogo).
        erros++;
        RegistroAprendizagem.RegistrarErro("puzzle_peca_errada", $"{Conceito} | seta {NomesSeta[alvo.seta]} | {peca.Dados.texto}");
        Resetar();
        descTitulo.text = erroTitulo;
        descTitulo.color = VidaUI.Vermelho;
        descTexto.text = erroTexto;
        StartCoroutine(PiscarErro(alvo));
    }

    private Encaixe EncaixeEm(Vector2 pontoTela)
    {
        foreach (Encaixe e in encaixes)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(e.rect, pontoTela, null))
            {
                return e;
            }
        }

        return null;
    }

    private void Encaixar(AnimatorPuzzlePeca peca, Encaixe encaixe)
    {
        encaixe.ocupante = peca;
        encaixe.vazio.enabled = false;

        string textoAnimator = string.IsNullOrEmpty(peca.Dados.textoAnimator)
            ? ""
            : $"\n<size=72%><color=#9FC2FF>Animator: {TextoSeguro(peca.Dados.textoAnimator)}</color></size>";
        peca.Travar(encaixe.rect, TextoSeguro(peca.Dados.texto) + textoAnimator);
        AudioManager.TocarEfeito(AudioManager.Efeito.Encaixe);
        AtualizarSetaDaVez();
        MostrarPadrao();

        foreach (Encaixe e in encaixes)
        {
            if (e.ocupante == null)
            {
                return;
            }
        }

        Concluido = true;
        RegistroAprendizagem.Registrar("puzzle_concluido", $"{Conceito} | {Time.unscaledTime - inicio:0}s | {erros} erros");
        MostrarPadrao();
        aoConcluir?.Invoke();
    }

    private void Resetar()
    {
        foreach (Encaixe e in encaixes)
        {
            e.ocupante = null;
            e.vazio.enabled = true;
        }

        foreach (AnimatorPuzzlePeca p in pecasUI)
        {
            p.VoltarParaCasa();
        }
        AtualizarSetaDaVez();
    }

    private IEnumerator PiscarErro(Encaixe encaixe)
    {
        for (int i = 0; i < 2; i++)
        {
            if (encaixe.fundo == null)
            {
                yield break;
            }

            encaixe.fundo.color = new Color(VidaUI.Vermelho.r, VidaUI.Vermelho.g, VidaUI.Vermelho.b, 0.45f);
            yield return new WaitForSecondsRealtime(0.15f);
            if (encaixe.fundo == null)
            {
                yield break;
            }

            encaixe.fundo.color = MolduraPuzzleUI.FundoEncaixe;
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }

    private bool SetaPreenchida(int seta)
    {
        // No modo agachar as setas do primeiro puzzle já vêm montadas.
        if (modoAgachar && seta >= 0 && seta < AnimatorDiagramaUI.TotalSetas)
        {
            return true;
        }

        foreach (Encaixe e in encaixes)
        {
            if (e.seta == seta)
            {
                return e.ocupante != null;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------- prévia

    private void Update()
    {
        if (!Aberto || diagrama == null)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        float ciclo = modoAgachar ? PreviaCicloAgachar : PreviaCiclo;
        float antes = previaTempo % ciclo;
        previaTempo += dt;
        float u = previaTempo % ciclo;
        if (u < antes)
        {
            previaX = PreviaXInicial; // recomeçou o ciclo: volta pra esquerda
        }

        float vx;
        float altura;
        if (modoAgachar)
        {
            AmostraDaPreviaAgachar(u, out vx, out previaAgachado);
            altura = 0f;
        }
        else
        {
            AmostraDaPrevia(u, out vx, out altura);
        }
        bool noChao = altura <= 0f;
        previaX += vx * PreviaPixelsPorUnidade * dt;

        // A mesma máquina de estados do AnimatorController, mas usando só as
        // setas que o jogador já preencheu. Any State é conferido primeiro,
        // como no Unity.
        string novo = previaEstadoAtual;
        bool parado = Mathf.Abs(vx) < AnimacaoPlayerFisica.LimiarParado;
        string agachar = modoAgachar ? EstadoAgacharDaPrevia(parado) : null;
        if (agachar != null)
        {
            novo = agachar;
        }
        else if (previaEstadoAtual != AnimacaoPlayerFisica.EstadoPulo && !noChao && SetaPreenchida(AnimatorDiagramaUI.SetaAnyPulo))
        {
            novo = AnimacaoPlayerFisica.EstadoPulo;
        }
        else if (previaEstadoAtual == AnimacaoPlayerFisica.EstadoIdle && !parado && SetaPreenchida(AnimatorDiagramaUI.SetaIdleCorrida))
        {
            novo = AnimacaoPlayerFisica.EstadoCorrida;
        }
        else if (previaEstadoAtual == AnimacaoPlayerFisica.EstadoCorrida && parado && SetaPreenchida(AnimatorDiagramaUI.SetaCorridaIdle))
        {
            novo = AnimacaoPlayerFisica.EstadoIdle;
        }
        else if (previaEstadoAtual == AnimacaoPlayerFisica.EstadoPulo && noChao && SetaPreenchida(AnimatorDiagramaUI.SetaPuloIdle))
        {
            novo = AnimacaoPlayerFisica.EstadoIdle;
        }

        if (novo != previaEstadoAtual)
        {
            AcenderSeta(AnimatorDiagramaUI.SetaEntre(previaEstadoAtual, novo));
            previaEstadoAtual = novo;
            previaTempoNoEstado = 0f;
            diagrama.DestacarEstado(novo);
            AtualizarChips();
        }
        else
        {
            previaTempoNoEstado += dt;
        }

        if (setaAcesa >= 0 && Time.unscaledTime >= setaAcesaAte)
        {
            ApagarSeta(setaAcesa);
            setaAcesa = -1;
        }

        previaBoneco.anchoredPosition = new Vector2(PreviaCentroX + previaX, -PreviaChaoTopo + altura * PreviaAlturaPulo);
        AplicarSpriteDaPrevia(SpriteDaPrevia(previaEstadoAtual, previaTempoNoEstado));
        if (modoAgachar)
        {
            previaLeitura1.text = $"agachado = {(previaAgachado ? "true" : "false")}";
            previaLeitura2.text = $"velocidadeX = {Mathf.Abs(vx).ToString("0.0", CultureInfo.InvariantCulture)}";
        }
        else
        {
            previaLeitura1.text = $"velocidadeX = {Mathf.Abs(vx).ToString("0.0", CultureInfo.InvariantCulture)}";
            previaLeitura2.text = $"noChao = {(noChao ? "true" : "false")}";
        }
    }

    // A seta da vez fica acesa: a prévia só apaga as que ela mesma acendeu.
    private void ApagarSeta(int seta)
    {
        if (seta != setaDaVez)
        {
            diagrama.DestacarSeta(seta, false);
        }
    }

    private void AcenderSeta(int seta)
    {
        if (setaAcesa >= 0)
        {
            ApagarSeta(setaAcesa);
        }

        setaAcesa = seta;
        setaAcesaAte = Time.unscaledTime + 0.6f;
        diagrama.DestacarSeta(seta, true);
    }

    // Roteiro da prévia (ciclo de 9 s, como a simulação do mockup): parado,
    // corre, pula correndo, corre, para, pula parado, parado.
    private static void AmostraDaPrevia(float u, out float vx, out float altura)
    {
        vx = 0f;
        altura = 0f;

        if (u < 1.2f) return;
        if (u < 3.2f) { vx = PreviaVelocidade; return; }
        if (u < 4.0f) { float k = (u - 3.2f) / 0.8f; vx = PreviaVelocidade; altura = 4f * k * (1f - k); return; }
        if (u < 5.2f) { vx = PreviaVelocidade; return; }
        if (u < 6.4f) return;
        if (u < 7.2f) { float k = (u - 6.4f) / 0.8f; altura = 4f * k * (1f - k); }
    }

    // Agachar na prévia: as setas de volta (Agachado -> Idle, AndarAgachado ->
    // Agachado) existem no controller e não fazem parte do puzzle. Sem a seta
    // Idle -> Agachado, o personagem anda agachado desenhado em pé (Corrida):
    // a mesma falha que o jogador acabou de ver no túnel.
    private string EstadoAgacharDaPrevia(bool parado)
    {
        string atual = previaEstadoAtual;
        if (atual == AnimacaoPlayerFisica.EstadoIdle && previaAgachado && SetaPreenchida(AnimatorDiagramaUI.SetaIdleAgachado))
            return AnimacaoPlayerFisica.EstadoAgachado;
        if (atual == AnimacaoPlayerFisica.EstadoAgachado && !previaAgachado)
            return AnimacaoPlayerFisica.EstadoIdle;
        if (atual == AnimacaoPlayerFisica.EstadoAgachado && !parado && SetaPreenchida(AnimatorDiagramaUI.SetaAgachadoAndar))
            return AnimacaoPlayerFisica.EstadoAndarAgachado;
        if (atual == AnimacaoPlayerFisica.EstadoAndarAgachado && parado)
            return AnimacaoPlayerFisica.EstadoAgachado;
        if (atual == AnimacaoPlayerFisica.EstadoAndarAgachado || atual == AnimacaoPlayerFisica.EstadoAgachado)
            return atual;
        return null; // Idle/Corrida seguem as setas do primeiro puzzle
    }

    private static void AmostraDaPreviaAgachar(float u, out float vx, out bool agachado)
    {
        vx = 0f;
        agachado = u >= 1.0f && u < 6.0f;
        if (u >= 2.0f && u < 4.6f) vx = PreviaVelocidadeAgachado;
    }

    private void AplicarSpriteDaPrevia(Sprite sprite)
    {
        if (previaImagem == null || previaImagem.sprite == sprite)
        {
            return;
        }

        previaImagem.sprite = sprite;
        previaBoneco.sizeDelta = sprite != null
            ? sprite.rect.size / Mathf.Max(1f, sprite.pixelsPerUnit) * PreviaEscala
            : Vector2.one * PreviaEscala;
    }

    private Sprite SpriteDaPrevia(string estado, float tempoNoEstado)
    {
        Sprite[] frames = estado == AnimacaoPlayerFisica.EstadoCorrida ? framesCorrida
            : estado == AnimacaoPlayerFisica.EstadoPulo ? framesPulo
            : estado == AnimacaoPlayerFisica.EstadoAgachado ? framesAgachado
            : estado == AnimacaoPlayerFisica.EstadoAndarAgachado ? framesAndarAgachado
            : null;

        if (frames == null || frames.Length == 0)
        {
            if (spriteIdle != null) return spriteIdle;
            return framesCorrida != null && framesCorrida.Length > 0 ? framesCorrida[0] : null;
        }

        int indice = Mathf.FloorToInt(tempoNoEstado * framesPorSegundo);
        // Corrida em loop; Pulo toca uma vez e segura o último quadro (igual aos clipes).
        bool emLoop = estado == AnimacaoPlayerFisica.EstadoCorrida || estado == AnimacaoPlayerFisica.EstadoAndarAgachado;
        indice = emLoop ? indice % frames.Length : Mathf.Min(indice, frames.Length - 1);
        return frames[indice];
    }

    // ---------------------------------------------------------------- padrão

    private static Peca[] PecasPadrao()
    {
        return new[]
        {
            new Peca
            {
                texto = "isGrounded == true",
                textoAnimator = "noChao == true",
                setaCorreta = AnimatorDiagramaUI.SetaPuloIdle,
                descricao = "Verdadeiro quando o personagem está com os pés em alguma superfície: o chão ou o topo de um degrau."
            },
            new Peca
            {
                texto = "Input.GetKey(Space)",
                setaCorreta = -1,
                descricao = "Verdadeiro enquanto a tecla de pular está apertada."
            },
            new Peca
            {
                texto = "linearVelocity.x != 0",
                textoAnimator = "velocidadeX > 0.1",
                setaCorreta = AnimatorDiagramaUI.SetaIdleCorrida,
                descricao = "A velocidade horizontal do Rigidbody2D é diferente de zero: o personagem está se movendo pra esquerda ou pra direita."
            },
            new Peca
            {
                texto = "Start()",
                setaCorreta = -1,
                descricao = "Método que o Unity chama uma única vez, quando o objeto começa."
            },
            new Peca
            {
                texto = "isGrounded == false",
                textoAnimator = "noChao == false",
                setaCorreta = AnimatorDiagramaUI.SetaAnyPulo,
                descricao = "Verdadeiro quando o personagem não está encostado em nenhum chão: subiu num pulo ou está caindo de uma beirada."
            },
            new Peca
            {
                texto = "transform.position.y > 0",
                setaCorreta = -1,
                descricao = "Compara a altura do personagem com o zero do mundo."
            },
            new Peca
            {
                texto = "linearVelocity.x == 0",
                textoAnimator = "velocidadeX < 0.1",
                setaCorreta = AnimatorDiagramaUI.SetaCorridaIdle,
                descricao = "A velocidade horizontal do Rigidbody2D é zero: o personagem não está indo nem pra esquerda nem pra direita."
            },
        };
    }
}
