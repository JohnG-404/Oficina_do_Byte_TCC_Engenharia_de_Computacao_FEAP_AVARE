using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Painel "Animator do objeto" da fase Animacao (moeda, jumppad, alavanca,
/// bateria). Mesmo visual e mesmas regras do painel da lâmpada
/// (LampadaAnimatorUI): diagrama com os estados do Animator do objeto, as
/// setas vazias, e o jogador arrasta a condição certa pra cada seta. Peça
/// errada recomeça o painel e conta como erro no RegistroAprendizagem.
///
/// Um componente por tipo de objeto, salvo na cena (objeto
/// "PaineisAnimatorObjetos"), com estados, setas, peças e textos editáveis
/// no Inspector. Os objetos (ObjetoAnimadoInteracao) só funcionam depois que
/// o painel do tipo deles foi configurado; configurar uma moeda configura
/// todas, porque usam o mesmo Animator.
///
/// A prévia mostra o objeto com o parâmetro mudando sozinho (o roteiro de
/// eventos) e o estado que o Animator escolheria só com as setas já
/// preenchidas.
///
/// A tela usa a moldura do puzzle
/// do Animator (MolduraPuzzleUI + MolduraObjetoUI): cabeçalho com a lista
/// dos objetos, janela do objeto com o diagrama e a coluna Prévia/Objetivo,
/// peças em cartões, a descrição no painel Dica e a tecla Esc Fechar. A placa
/// VIDA e a faixa de objetivo somem enquanto o painel está aberto.
/// </summary>
public class ObjetoAnimatorUI : MonoBehaviour, IDonoPecaAnimator
{
    public enum Condicao
    {
        [Tooltip("Parâmetro Trigger disparado (consome o Trigger).")]
        Trigger,
        [Tooltip("Sem condição: espera o clipe do estado acabar.")]
        ExitTime,
        [Tooltip("Parâmetro Int igual a 'valor'.")]
        IntIgual,
    }

    [Serializable]
    public class Estado
    {
        public string nome;
        [Tooltip("Quadros do clipe (prévia).")]
        public Sprite[] quadros;
        public float fps = 10f;
        public bool loop = true;
        [Tooltip("Some da prévia quando o clipe acaba (ex.: moeda coletada).")]
        public bool someNoFim;
    }

    [Serializable]
    public class Seta
    {
        [Tooltip("Índice do estado de origem; -1 = Any State.")]
        public int de;
        public int para;
        public Condicao condicao;
        public int valor;
    }

    [Serializable]
    public class Evento
    {
        [Tooltip("Segundos dentro do ciclo da prévia.")]
        public float tempo;
        [Tooltip("Valor do parâmetro Int (ignorado no Trigger).")]
        public int valor;
        public string leitura;
    }

    private class Encaixe
    {
        public int seta;
        public RectTransform rect;
        public Image fundo;
        public Image tracejado;
        public TextMeshProUGUI dica;
        public AnimatorPuzzlePeca ocupante;
    }

    private static readonly Dictionary<string, ObjetoAnimatorUI> Paineis = new Dictionary<string, ObjetoAnimatorUI>();

    /// <summary>Painel do tipo (moeda, jumppad...), ou null.</summary>
    public static ObjetoAnimatorUI Do(string tipo)
    {
        return tipo != null && Paineis.TryGetValue(tipo, out ObjetoAnimatorUI p) && p != null ? p : null;
    }

    private static readonly HashSet<ObjetoAnimatorUI> Abertos = new HashSet<ObjetoAnimatorUI>();
    private static int frameFechou = -1;

    /// <summary>True com algum painel aberto e no frame em que fechou — pra o Esc não abrir a pausa junto.</summary>
    public static bool SegurandoEsc => Abertos.Count > 0 || Time.frameCount == frameFechou;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Paineis.Clear();
        Abertos.Clear();
        frameFechou = -1;
    }

    private static readonly Color CorEntry = new Color32(0x1E, 0x5A, 0x3A, 0xFF);
    private static readonly Color CorAny = new Color32(0x1F, 0x4E, 0x5A, 0xFF);
    private static readonly Color CorParametros = new Color32(0x16, 0x1E, 0x2E, 0xFF);

    [Header("Objeto")]
    [Tooltip("Chave do tipo (moeda, jumppad, alavanca, bateria): os objetos acham o painel por ela.")]
    [SerializeField] private string tipo = "moeda";
    [Tooltip("Ligado = o Animator já começa configurado (os objetos funcionam direto).")]
    [SerializeField] private bool configurado;

    [Header("Animator")]
    [SerializeField] private string parametro = "coletada";
    [SerializeField] private string tipoParametro = "Trigger";
    [SerializeField] private string quemMuda = "(o script muda)";
    [SerializeField] private Estado[] estados = new Estado[0];
    [SerializeField] private int estadoInicial;
    [SerializeField] private Seta[] setas = new Seta[0];
    [SerializeField] private AnimatorPuzzleUI.Peca[] pecas = new AnimatorPuzzleUI.Peca[0];

    [Header("Textos")]
    [Tooltip("Sem o \"Configure o \" do começo, vira o cabeçalho (\"Animator da Moeda\").")]
    [SerializeField] private string titulo = "Configure o Animator";
    [SerializeField] private string fase = "Fase Animação";
    [Tooltip("Frase da caixa Objetivo. Vazio = usa o título da descrição.")]
    [SerializeField] private string objetivo = "";
    [Tooltip("Vira a legenda do diagrama (\"ANIMATOR DA MOEDA · BASE LAYER\").")]
    [SerializeField] private string tituloDiagrama = "Animator (Base Layer)";
    [SerializeField] private string textoEncaixeVazio = "arraste uma condição";
    [SerializeField] private string descricaoTitulo = "";
    [TextArea(2, 5)]
    [SerializeField] private string descricaoPadrao = "";
    [SerializeField] private string erroTitulo = "Essa condição não serve nessa seta";
    [TextArea(2, 5)]
    [SerializeField] private string erroTexto = "O Animator foi zerado. Pense no que precisa acontecer pra cada seta ser seguida.";
    [SerializeField] private string concluidoTitulo = "Animator configurado!";
    [TextArea(2, 5)]
    [SerializeField] private string concluidoTexto = "";
    [SerializeField] private string tituloPrevia = "Prévia";
    [SerializeField] private string rodapeDica = "Passe o mouse numa peça pra ler aqui o que ela faz.";

    [Header("Prévia")]
    [SerializeField] private float cicloSegundos = 3f;
    [Tooltip("No fim de cada ciclo a prévia volta para o estado inicial (ex.: uma moeda nova).")]
    [SerializeField] private bool reiniciarNoCiclo;
    [SerializeField] private Evento[] eventos = new Evento[0];
    [SerializeField] private string leituraParado = "";
    [SerializeField] private bool inverterX;
    [SerializeField] private bool inverterY;
    [SerializeField] private Vector2 tamanhoPrevia = new Vector2(150f, 150f);

    [Header("Prof Byte (perto do objeto sem Animator)")]
    [TextArea(2, 4)]
    [SerializeField] private string[] falasSemAnimator = new string[0];

    [Header("Código mostrado depois do painel")]
    [SerializeField] private string tituloCodigo = "Animator";
    [TextArea(4, 12)]
    [SerializeField] private string codigo = "";
    [Tooltip("Não fecha mais o painel: ele fica aberto até Enter ou clique (ver CodigoMontadoUI).")]
    [SerializeField] private float segundosCodigo = 12f;

    [Tooltip("Ordem do Canvas: acima do HUD/diálogo da cena, abaixo do CodigoMontadoUI (500).")]
    [SerializeField] private int sortingOrder = 400;

    public string Tipo => tipo;
    public bool Configurado => configurado;
    public string[] FalasSemAnimator => falasSemAnimator;
    public bool Aberto => canvasGO != null;
    public bool Concluido { get; private set; }

    /// <summary>Chamado quando o painel de código fecha depois de configurar: os objetos voltam a funcionar.</summary>
    public event Action AoConfigurar;

    private readonly List<Encaixe> encaixes = new List<Encaixe>();
    private readonly List<AnimatorPuzzlePeca> pecasUI = new List<AnimatorPuzzlePeca>();
    private readonly Dictionary<int, Image> bordas = new Dictionary<int, Image>();
    private readonly Dictionary<int, Image> fundos = new Dictionary<int, Image>();
    private readonly Dictionary<int, Color> fundoNormal = new Dictionary<int, Color>();
    private List<Image>[] partesSeta = new List<Image>[0];

    private GameObject canvasGO;
    private RectTransform camadaArraste;
    private TextMeshProUGUI descTitulo;
    private TextMeshProUGUI descTexto;
    private TextMeshProUGUI textoProgresso;
    private readonly MolduraObjetoUI.HudEscondido hud = new MolduraObjetoUI.HudEscondido();
    private Action aoConcluir;
    private Action aoCancelar;
    private float inicio;
    private int erros;

    // Diagrama (coordenadas dentro da área do diagrama, 1100 x 620)
    private Vector2[] posEstado;
    private Vector2 tamCaixa;
    private Vector2 posEntry;
    private Vector2 posAny;
    private bool temAny;
    private Vector2[,] pontosSeta;
    private Vector2[] posEncaixe;
    private Vector2[] tamEncaixe;

    // Prévia
    private Image previaImagem;
    private TextMeshProUGUI previaEstado;
    private TextMeshProUGUI previaLeitura;
    private TextMeshProUGUI parametroValor;
    private Image parametroCaixa;
    private float cicloT;
    private int atual;
    private float tempoEstado;
    private bool triggerPendente;
    private int valorInt;
    private string leituraAtual;
    private int setaAcesa = -1;
    private float setaAcesaAte;

    private string Conceito => "Animator " + tipo;

    private void OnEnable()
    {
        Paineis[tipo] = this;
    }

    // Depois dos Awake da cena: a AnimacaoLevelFlow já sabe se é uma volta
    // (morte ou reinício) ou uma chegada nova. Na volta, o Animator configurado
    // continua configurado.
    private void Start()
    {
        if (AnimacaoLevelFlow.ObjetoMontado(tipo))
        {
            configurado = true;
        }
    }

    private void OnDisable()
    {
        if (Paineis.TryGetValue(tipo, out ObjetoAnimatorUI p) && p == this)
        {
            Paineis.Remove(tipo);
        }
    }

    private void OnDestroy()
    {
        Fechar();
    }

    // ---------------------------------------------------------------- fluxo

    /// <summary>
    /// Abre o painel a partir de um objeto do tipo (clique ou tecla de
    /// interagir). Trava o personagem enquanto estiver aberto.
    /// </summary>
    public void Configurar()
    {
        AnimacaoLevelFlow flow = AnimacaoLevelFlow.Instance;
        if (configurado || Aberto || flow == null || !flow.AbrirPainelDeObjeto())
        {
            return;
        }

        Abrir(AoConcluirPainel, AoCancelarPainel);
    }

    private void AoConcluirPainel()
    {
        configurado = true;
        AnimacaoLevelFlow.MarcarObjetoMontado(tipo);
        // Um instante pra ver o diagrama completo antes do painel de código.
        Invoke(nameof(MostrarCodigo), 0.8f);
    }

    private void MostrarCodigo()
    {
        CodigoMontadoUI.Show(tituloCodigo, codigo, segundosCodigo, AoFecharCodigo);
    }

    private void AoFecharCodigo()
    {
        Fechar();
        if (AnimacaoLevelFlow.Instance != null)
        {
            AnimacaoLevelFlow.Instance.FecharPainelDeObjeto();
        }

        AoConfigurar?.Invoke();
    }

    private void AoCancelarPainel()
    {
        if (AnimacaoLevelFlow.Instance != null)
        {
            AnimacaoLevelFlow.Instance.FecharPainelDeObjeto();
        }
    }

    /// <summary>Monta e abre o painel.</summary>
    public void Abrir(Action aoConcluir, Action aoCancelar)
    {
        if (Aberto || estados == null || estados.Length == 0)
        {
            return;
        }

        this.aoConcluir = aoConcluir;
        this.aoCancelar = aoCancelar;
        Concluido = false;
        erros = 0;
        encaixes.Clear();
        pecasUI.Clear();
        bordas.Clear();
        fundos.Clear();
        fundoNormal.Clear();
        ReiniciarPrevia();

        Canvas canvas = VidaUI.CriarCanvas("ObjetoAnimatorUI_" + tipo, sortingOrder);
        canvasGO = canvas.gameObject;
        Abertos.Add(this);
        JogoBloqueado.Abrir(this);
        Montar((RectTransform)canvas.transform);
        hud.Esconder();

        inicio = Time.unscaledTime;
        RegistroAprendizagem.Registrar("puzzle_inicio", Conceito);
    }

    public void Fechar()
    {
        if (canvasGO != null)
        {
            Destroy(canvasGO);
            frameFechou = Time.frameCount;
        }

        canvasGO = null;
        Abertos.Remove(this);
        JogoBloqueado.Fechar(this);
        encaixes.Clear();
        pecasUI.Clear();
        hud.Mostrar();
    }

    /// <summary>Fechar pelo botão ou pelo Esc, antes de terminar.</summary>
    public void Cancelar()
    {
        if (!Aberto || Concluido)
        {
            return;
        }

        RegistroAprendizagem.Registrar("puzzle_fechado", $"{Conceito} | {Time.unscaledTime - inicio:0}s | {erros} erros");
        Fechar();
        aoCancelar?.Invoke();
    }

    // ---------------------------------------------------------------- layout

    private void CalcularLayout()
    {
        int n = estados.Length;
        temAny = false;
        int qtdAny = 0;
        foreach (Seta s in setas)
        {
            if (s.de < 0)
            {
                temAny = true;
                qtdAny++;
            }
        }

        float y = temAny ? -40f : 20f;
        posEstado = new Vector2[n];
        if (n <= 2)
        {
            tamCaixa = new Vector2(260f, 150f);
            posEstado[0] = new Vector2(n == 1 ? 0f : -300f, y);
            if (n == 2) posEstado[1] = new Vector2(300f, y);
        }
        else
        {
            tamCaixa = new Vector2(230f, 130f);
            float passo = 740f / (n - 1);
            for (int i = 0; i < n; i++)
            {
                posEstado[i] = new Vector2(-370f + passo * i, y);
            }
        }

        Vector2 ini = posEstado[Mathf.Clamp(estadoInicial, 0, n - 1)];
        posEntry = temAny ? new Vector2(-460f, 215f) : new Vector2(ini.x, 215f);
        posAny = new Vector2(60f, 215f);

        pontosSeta = new Vector2[setas.Length, 2];
        posEncaixe = new Vector2[setas.Length];
        tamEncaixe = new Vector2[setas.Length];
        int iAny = 0;
        for (int i = 0; i < setas.Length; i++)
        {
            Seta s = setas[i];
            if (s.de < 0)
            {
                float desvio = qtdAny > 1 ? (iAny - (qtdAny - 1) * 0.5f) * 110f : 0f;
                iAny++;
                Vector2 a = posAny + new Vector2(desvio, -32f);
                Vector2 b = posEstado[s.para] + new Vector2(0f, tamCaixa.y * 0.5f + 6f);
                pontosSeta[i, 0] = a;
                pontosSeta[i, 1] = b;
                posEncaixe[i] = Vector2.Lerp(a, b, 0.55f);
                tamEncaixe[i] = new Vector2(230f, 60f);
                continue;
            }

            Vector2 pa = posEstado[s.de];
            Vector2 pb = posEstado[s.para];
            bool temVolta = false;
            foreach (Seta o in setas)
            {
                if (o.de == s.para && o.para == s.de) temVolta = true;
            }

            float dir = Mathf.Sign(pb.x - pa.x);
            float dy = temVolta ? (dir > 0f ? 45f : -45f) : 0f;
            Vector2 p0 = new Vector2(pa.x + dir * (tamCaixa.x * 0.5f + 8f), pa.y + dy);
            Vector2 p1 = new Vector2(pb.x - dir * (tamCaixa.x * 0.5f + 8f), pb.y + dy);
            pontosSeta[i, 0] = p0;
            pontosSeta[i, 1] = p1;
            if (n <= 2)
            {
                posEncaixe[i] = (p0 + p1) * 0.5f;
                tamEncaixe[i] = new Vector2(310f, 68f);
            }
            else
            {
                // Entre caixas próximas não cabe o encaixe em cima da seta: vai logo acima (ou abaixo).
                float acima = dy >= 0f ? tamCaixa.y * 0.5f + 48f : -tamCaixa.y * 0.5f - 48f;
                posEncaixe[i] = new Vector2((p0.x + p1.x) * 0.5f, pa.y + acima);
                tamEncaixe[i] = new Vector2(280f, 60f);
            }
        }
    }

    // ---------------------------------------------------------------- montagem

    private void Montar(RectTransform raiz)
    {
        CalcularLayout();

        // Fundo opaco da moldura: também bloqueia cliques no cenário por trás.
        RectTransform tela = MolduraObjetoUI.Tela(raiz, fase, MolduraObjetoUI.TituloCurto(titulo), tipo, Cancelar);

        // Janela do objeto: diagrama (esquerda) + Prévia/Objetivo (direita).
        RectTransform areaDiagrama = MolduraObjetoUI.AreaDiagrama(tela, NomeAba(), MolduraObjetoUI.Legenda(tituloDiagrama));
        MontarDiagrama(areaDiagrama);
        MontarParametros(areaDiagrama);

        for (int i = 0; i < setas.Length; i++)
        {
            encaixes.Add(CriarEncaixe(areaDiagrama, i));
        }

        MontarPrevia(tela);

        // Peças (direita) e Dica (embaixo, com a descrição da peça).
        pecasUI.AddRange(MolduraObjetoUI.JanelaPecas(tela, this, pecas));
        MolduraPuzzleUI.PainelDica dica = MolduraPuzzleUI.Dica(tela, rodapeDica);
        descTitulo = dica.titulo;
        descTexto = dica.texto;
        MostrarPadrao();

        // Por último = desenhada por cima de tudo: a peça sendo arrastada vem pra cá.
        camadaArraste = MolduraObjetoUI.CamadaArraste(raiz);

        DestacarEstado(atual);
        AtualizarProgresso();
    }

    /// <summary>Aba da janela: o nome do objeto, tirado do título ("Configure o Animator da Moeda" → "Moeda").</summary>
    private string NomeAba()
    {
        string t = MolduraObjetoUI.TituloCurto(titulo);
        foreach (string prefixo in new[] { "Animator da ", "Animator do ", "Animator " })
        {
            if (t.StartsWith(prefixo)) return t.Substring(prefixo.Length);
        }
        return t;
    }

    private void MontarDiagrama(RectTransform area)
    {
        // Setas primeiro: ficam por baixo das caixas e dos encaixes.
        Vector2 ini = posEstado[Mathf.Clamp(estadoInicial, 0, estados.Length - 1)];
        AnimatorDiagramaUI.NovaSeta(area, posEntry + new Vector2(0f, -32f), ini + new Vector2(0f, tamCaixa.y * 0.5f));
        partesSeta = new List<Image>[setas.Length];
        for (int i = 0; i < setas.Length; i++)
        {
            partesSeta[i] = AnimatorDiagramaUI.NovaSeta(area, pontosSeta[i, 0], pontosSeta[i, 1]);
        }

        AnimatorDiagramaUI.NovaCaixaEstado(area, "Entry", posEntry, new Vector2(170f, 64f), CorEntry);
        if (temAny)
        {
            AnimatorDiagramaUI.NovaCaixaEstado(area, "Any State", posAny, new Vector2(200f, 64f), CorAny);
        }

        for (int i = 0; i < estados.Length; i++)
        {
            Color cor = i == estadoInicial ? AnimatorDiagramaUI.CorIdle : AnimatorDiagramaUI.CorEstado;
            Image borda = AnimatorDiagramaUI.NovaCaixaEstado(area, estados[i].nome, posEstado[i], tamCaixa, cor);
            bordas[i] = borda;
            fundos[i] = borda.transform.GetChild(0).GetComponent<Image>();
            fundoNormal[i] = cor;
        }

        TextMeshProUGUI leg = VidaUI.NovoTexto(area, "Legenda_Inicial", "estado inicial", 20, VidaUI.TextoApagado, TextAlignmentOptions.Center, FontStyles.Italic);
        VidaUI.Centro(leg.rectTransform, ini + new Vector2(0f, -tamCaixa.y * 0.5f - 20f), new Vector2(300f, 32f));
    }

    private void MontarParametros(RectTransform area)
    {
        RectTransform caixa = VidaUI.CaixaComBorda(area, "Parametros", new Vector2(0f, -220f), new Vector2(1040f, 120f), MolduraPuzzleUI.Escuro, CorParametros);
        VidaUI.Centro(VidaUI.NovoTexto(caixa, "Cabecalho", "Parameters", 22, MolduraPuzzleUI.TituloAzul, TextAlignmentOptions.Left, FontStyles.Bold).rectTransform,
            new Vector2(0f, 32f), new Vector2(990f, 30f));
        VidaUI.Centro(VidaUI.NovoTexto(caixa, "Nome", parametro, 28, VidaUI.Texto, TextAlignmentOptions.Left, FontStyles.Bold).rectTransform,
            new Vector2(-300f, -15f), new Vector2(390f, 40f));
        VidaUI.Centro(VidaUI.NovoTexto(caixa, "Tipo", tipoParametro, 24, VidaUI.TextoApagado, TextAlignmentOptions.Left).rectTransform,
            new Vector2(-30f, -15f), new Vector2(150f, 40f));

        parametroCaixa = VidaUI.NovaImagem(caixa, "Valor", VidaUI.Moldura);
        VidaUI.Centro(parametroCaixa.rectTransform, new Vector2(110f, -15f), new Vector2(34f, 34f));
        parametroValor = VidaUI.NovoTexto(caixa, "ValorTexto", "", 24, VidaUI.TextoApagado, TextAlignmentOptions.Left, FontStyles.Bold);
        VidaUI.Centro(parametroValor.rectTransform, new Vector2(210f, -15f), new Vector2(140f, 40f));
        // "Quem muda" fica à direita do valor, sem encostar nele: diminui a letra se o texto for longo.
        TextMeshProUGUI quem = VidaUI.NovoTexto(caixa, "Quem", quemMuda, 20, VidaUI.TextoApagado, TextAlignmentOptions.Right, FontStyles.Italic);
        quem.enableAutoSizing = true;
        quem.fontSizeMin = 13f;
        quem.fontSizeMax = 20f;
        VidaUI.Centro(quem.rectTransform, new Vector2(390f, -15f), new Vector2(220f, 44f));
    }

    private Encaixe CriarEncaixe(RectTransform area, int seta)
    {
        Seta s = setas[seta];
        string nome = (s.de < 0 ? "AnyState" : estados[s.de].nome) + "->" + estados[s.para].nome;
        MolduraObjetoUI.Encaixe e = MolduraObjetoUI.NovoEncaixe(area, "Encaixe_" + nome, posEncaixe[seta], tamEncaixe[seta], textoEncaixeVazio);
        return new Encaixe { seta = seta, rect = e.rect, fundo = e.miolo, tracejado = e.tracejado, dica = e.dica };
    }

    private void MontarPrevia(RectTransform tela)
    {
        string frase = string.IsNullOrEmpty(objetivo) ? descricaoTitulo : objetivo;
        MolduraObjetoUI.Coluna col = MolduraObjetoUI.ColunaPrevia(tela, tituloPrevia, frase, out RectTransform palco);
        previaEstado = col.estado;
        previaLeitura = col.leitura;
        textoProgresso = col.progresso;

        // Chão embaixo do palco e o objeto logo acima dele (no mesmo tamanho relativo de antes).
        const float topoChao = 359f;
        MolduraObjetoUI.ChaoPrevia(tela, topoChao);
        Vector2 tam = tamanhoPrevia * 0.8f;
        previaImagem = VidaUI.NovaImagem(tela, "Objeto", Color.white);
        previaImagem.preserveAspect = true;
        RectTransform r = previaImagem.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = tam;
        r.anchoredPosition = new Vector2(MolduraObjetoUI.ColCentro, -(topoChao - 4f - tam.y * 0.5f));
        r.localScale = new Vector3(inverterX ? -1f : 1f, inverterY ? -1f : 1f, 1f);
    }

    private void AtualizarProgresso()
    {
        if (textoProgresso == null) return;
        int feitos = 0;
        foreach (Encaixe e in encaixes)
        {
            if (e.ocupante != null) feitos++;
        }
        textoProgresso.text = MolduraObjetoUI.TextoProgresso(feitos, encaixes.Count);
    }

    // ---------------------------------------------------------------- lógica (IDonoPecaAnimator)

    public RectTransform CamadaArraste => camadaArraste;

    public void MostrarDescricao(AnimatorPuzzleUI.Peca dados)
    {
        if (descTitulo == null)
        {
            return;
        }

        descTitulo.color = Color.white;
        descTitulo.text = AnimatorPuzzleUI.TextoSeguro(dados.texto);
        descTexto.text = dados.descricao;
    }

    public void MostrarPadrao()
    {
        if (descTitulo == null)
        {
            return;
        }

        descTitulo.color = Concluido ? VidaUI.Verde : MolduraPuzzleUI.TituloAzul;
        descTitulo.text = Concluido ? concluidoTitulo : descricaoTitulo;
        descTexto.text = Concluido ? concluidoTexto : descricaoPadrao;
    }

    /// <summary>Chamado pela peça ao ser solta.</summary>
    public void Soltar(AnimatorPuzzlePeca peca, Vector2 pontoTela)
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

        erros++;
        RegistroAprendizagem.RegistrarErro("puzzle_peca_errada", $"{Conceito} | seta {alvo.rect.name} | {peca.Dados.texto}");
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
        encaixe.dica.gameObject.SetActive(false);
        encaixe.tracejado.enabled = false;
        peca.Travar(encaixe.rect, AnimatorPuzzleUI.TextoSeguro(peca.Dados.texto));
        AudioManager.TocarEfeito(AudioManager.Efeito.Encaixe);
        AtualizarProgresso();

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
            e.dica.gameObject.SetActive(true);
            e.tracejado.enabled = true;
        }

        foreach (AnimatorPuzzlePeca p in pecasUI)
        {
            p.VoltarParaCasa();
        }
        AtualizarProgresso();
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

    private void ReiniciarPrevia()
    {
        cicloT = 0f;
        atual = Mathf.Clamp(estadoInicial, 0, Mathf.Max(0, estados.Length - 1));
        tempoEstado = 0f;
        triggerPendente = false;
        valorInt = 0;
        leituraAtual = leituraParado;
        setaAcesa = -1;
    }

    private float DuracaoClipe(Estado e)
    {
        int n = e.quadros != null ? e.quadros.Length : 0;
        return n > 0 ? n / Mathf.Max(1f, e.fps) : 0.4f;
    }

    private void Update()
    {
        if (!Aberto || previaImagem == null)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !Concluido)
        {
            Cancelar();
            return;
        }

        float dt = Time.unscaledDeltaTime;
        float antes = cicloT;
        cicloT += dt;
        tempoEstado += dt;
        float ciclo = Mathf.Max(0.5f, cicloSegundos);
        if (cicloT >= ciclo)
        {
            cicloT -= ciclo;
            antes = -1f;
            if (reiniciarNoCiclo)
            {
                int seta = setaAcesa;
                ReiniciarPrevia();
                cicloT = 0f;
                DestacarEstado(atual);
                if (seta >= 0) DestacarSeta(seta, false);
            }
        }

        if (eventos != null)
        {
            foreach (Evento ev in eventos)
            {
                if (ev.tempo > antes && ev.tempo <= cicloT)
                {
                    if (tipoParametro == "Trigger") triggerPendente = true;
                    else valorInt = ev.valor;
                    leituraAtual = ev.leitura;
                }
            }
        }

        for (int i = 0; i < setas.Length; i++)
        {
            Seta s = setas[i];
            bool daqui = s.de == atual || (s.de < 0 && s.para != atual);
            if (!daqui || !SetaPreenchida(i))
            {
                continue;
            }

            bool vale = s.condicao == Condicao.Trigger ? triggerPendente
                : s.condicao == Condicao.ExitTime ? tempoEstado >= DuracaoClipe(estados[atual])
                : valorInt == s.valor;
            if (!vale)
            {
                continue;
            }

            if (s.condicao == Condicao.Trigger) triggerPendente = false;
            atual = s.para;
            tempoEstado = 0f;
            AcenderSeta(i);
            DestacarEstado(atual);
            break;
        }

        if (setaAcesa >= 0 && Time.unscaledTime >= setaAcesaAte)
        {
            DestacarSeta(setaAcesa, false);
            setaAcesa = -1;
        }

        Estado e = estados[atual];
        int n = e.quadros != null ? e.quadros.Length : 0;
        if (n == 0)
        {
            previaImagem.enabled = false;
        }
        else
        {
            int f = Mathf.FloorToInt(tempoEstado * Mathf.Max(1f, e.fps));
            f = e.loop ? f % n : Mathf.Min(n - 1, f);
            previaImagem.enabled = !(e.someNoFim && tempoEstado >= DuracaoClipe(e));
            previaImagem.sprite = e.quadros[f];
        }

        previaEstado.text = e.nome;
        previaLeitura.text = leituraAtual;

        if (tipoParametro == "Trigger")
        {
            parametroCaixa.color = triggerPendente ? VidaUI.Amarelo : VidaUI.Moldura;
            parametroValor.text = triggerPendente ? "disparado" : "";
            parametroValor.color = VidaUI.Amarelo;
        }
        else
        {
            parametroCaixa.color = VidaUI.Moldura;
            parametroValor.text = valorInt.ToString();
            parametroValor.color = VidaUI.Amarelo;
        }
    }

    private void DestacarEstado(int estado)
    {
        foreach (KeyValuePair<int, Image> par in bordas)
        {
            bool ativo = par.Key == estado;
            par.Value.color = ativo ? VidaUI.Amarelo : VidaUI.Moldura;
            fundos[par.Key].color = ativo ? Color.Lerp(fundoNormal[par.Key], VidaUI.Amarelo, 0.28f) : fundoNormal[par.Key];
        }
    }

    private void AcenderSeta(int seta)
    {
        if (setaAcesa >= 0)
        {
            DestacarSeta(setaAcesa, false);
        }

        setaAcesa = seta;
        setaAcesaAte = Time.unscaledTime + 0.6f;
        DestacarSeta(seta, true);
    }

    private void DestacarSeta(int seta, bool acesa)
    {
        if (seta < 0 || seta >= partesSeta.Length || partesSeta[seta] == null)
        {
            return;
        }

        foreach (Image parte in partesSeta[seta])
        {
            if (parte != null)
            {
                parte.color = acesa ? VidaUI.Amarelo : AnimatorDiagramaUI.CorSeta;
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>Editor: preenche estados, setas, peças e textos de um tipo (moeda, jumppad, alavanca, bateria).</summary>
    public void AplicarModelo(string novoTipo)
    {
        ObjetoAnimatorModelos.Aplicar(this, novoTipo);
    }

    public void DefinirTudo(string tipo, string parametro, string tipoParametro, string quemMuda, Estado[] estados, int estadoInicial,
        Seta[] setas, AnimatorPuzzleUI.Peca[] pecas, string titulo, string tituloDiagrama, string descricaoTitulo, string descricaoPadrao,
        string erroTexto, string concluidoTitulo, string concluidoTexto, float cicloSegundos, bool reiniciarNoCiclo, Evento[] eventos,
        string leituraParado, string[] falasSemAnimator, string tituloCodigo, string codigo)
    {
        this.tipo = tipo;
        this.parametro = parametro;
        this.tipoParametro = tipoParametro;
        this.quemMuda = quemMuda;
        this.estados = estados;
        this.estadoInicial = estadoInicial;
        this.setas = setas;
        this.pecas = pecas;
        this.titulo = titulo;
        this.tituloDiagrama = tituloDiagrama;
        this.descricaoTitulo = descricaoTitulo;
        this.descricaoPadrao = descricaoPadrao;
        this.erroTexto = erroTexto;
        this.concluidoTitulo = concluidoTitulo;
        this.concluidoTexto = concluidoTexto;
        this.cicloSegundos = cicloSegundos;
        this.reiniciarNoCiclo = reiniciarNoCiclo;
        this.eventos = eventos;
        this.leituraParado = leituraParado;
        this.falasSemAnimator = falasSemAnimator;
        this.tituloCodigo = tituloCodigo;
        this.codigo = codigo;
    }

    public void DefinirPrevia(bool inverterX, bool inverterY, Vector2 tamanho)
    {
        this.inverterX = inverterX;
        this.inverterY = inverterY;
        tamanhoPrevia = tamanho;
    }

    public Estado[] Estados => estados;
#endif
}
