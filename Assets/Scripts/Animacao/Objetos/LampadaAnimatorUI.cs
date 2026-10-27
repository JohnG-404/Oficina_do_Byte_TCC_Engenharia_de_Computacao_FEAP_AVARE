using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Painel "Animator da Lâmpada" da fase Animacao: abre ao clicar na lâmpada
/// da sala escura (LampadaSensor). Mesmo visual e mesmas regras do puzzle do
/// Animator do Player (AnimatorPuzzleUI): diagrama com os estados Apagada e
/// Acesa, as duas setas vazias, e o jogador arrasta a condição certa pra
/// cada seta. Peça errada recomeça o painel e conta como erro no
/// RegistroAprendizagem.
///
/// A prévia mostra a lâmpada com o parâmetro Bool "ligada" mudando sozinho
/// (o personagem entrando e saindo da sala) e o estado que o Animator
/// escolheria só com as setas já preenchidas. Embaixo do diagrama fica a
/// lista de parâmetros, com o valor de ligada ao vivo.
///
/// O componente fica salvo na cena (no objeto LampadaSensor), com textos e
/// peças editáveis no Inspector; a UI é montada em runtime com Canvas próprio,
/// igual aos outros painéis do Animator.
///
/// Mesma moldura dos outros
/// painéis de objeto (MolduraObjetoUI), com a lâmpada na coluna Prévia.
/// </summary>
public class LampadaAnimatorUI : MonoBehaviour, IDonoPecaAnimator
{
    public const int SetaApagadaAcesa = 0;
    public const int SetaAcesaApagada = 1;

    public const string EstadoApagada = "Apagada";
    public const string EstadoAcesa = "Acesa";

    private class Encaixe
    {
        public int seta;
        public RectTransform rect;
        public Image fundo;
        public Image tracejado;
        public TextMeshProUGUI dica;
        public AnimatorPuzzlePeca ocupante;
    }

    private const string Conceito = "Animator Lampada";
    private static readonly string[] NomesSeta = { "Apagada->Acesa", "Acesa->Apagada" };

    private static readonly Vector2 TamanhoEncaixe = new Vector2(310f, 68f);
    private static readonly Vector2 TamanhoCaixa = new Vector2(260f, 170f);
    private static readonly Color CorEntry = new Color32(0x1E, 0x5A, 0x3A, 0xFF);
    private static readonly Color CorParametros = new Color32(0x16, 0x1E, 0x2E, 0xFF);
    private static readonly Color CorSalaEscura = new Color32(0x05, 0x07, 0x10, 0xFF);

    // Diagrama (coordenadas dentro da área do diagrama, 1100 x 620)
    private static readonly Vector2 PosApagada = new Vector2(-300f, 20f);
    private static readonly Vector2 PosAcesa = new Vector2(300f, 20f);
    private static readonly Vector2 PosEntry = new Vector2(-300f, 215f);
    private static readonly Vector2[,] Setas =
    {
        { new Vector2(-170f, 65f), new Vector2(170f, 65f) },   // Apagada -> Acesa
        { new Vector2(170f, -25f), new Vector2(-170f, -25f) }, // Acesa -> Apagada
    };

    [Header("Peças (condições)")]
    [SerializeField] private AnimatorPuzzleUI.Peca[] pecas = PecasPadrao();

    [Header("Textos")]
    [Tooltip("Sem o \"Configure o \" do começo, vira o cabeçalho (\"Animator da Lâmpada\").")]
    [SerializeField] private string titulo = "Configure o Animator da Lâmpada";
    [SerializeField] private string fase = "Fase Animação";
    [Tooltip("Frase da caixa Objetivo. Vazio = usa o título da descrição.")]
    [SerializeField] private string objetivo = "";
    [SerializeField] private string tituloDiagrama = "Animator da Lâmpada (Base Layer)";
    [SerializeField] private string textoEncaixeVazio = "arraste uma condição";
    [SerializeField] private string descricaoTitulo = "Uma lâmpada com sensor";
    [TextArea(2, 5)]
    [SerializeField] private string descricaoPadrao = "O sensor da sala muda o parâmetro Bool ligada: true quando alguém entra, false quando sai. As setas decidem o que o Animator faz com isso. Sem condição nas setas, a lâmpada fica sempre Apagada.";
    [SerializeField] private string erroTitulo = "Essa condição não serve nessa seta";
    [TextArea(2, 5)]
    [SerializeField] private string erroTexto = "O Animator da lâmpada foi zerado. Pense: o que precisa ser verdade pra lâmpada acender? E pra apagar?";
    [SerializeField] private string concluidoTitulo = "Lâmpada configurada!";
    [TextArea(2, 5)]
    [SerializeField] private string concluidoTexto = "As duas setas têm condição. Na prévia, a lâmpada acende quando ligada vira true e apaga quando vira false.";
    [SerializeField] private string tituloPrevia = "Prévia";
    [SerializeField] private string rodapeDica = "Passe o mouse numa peça pra ler aqui o que ela faz.";

    [Header("Prévia")]
    [SerializeField] private Sprite spriteApagada;
    [SerializeField] private Sprite spriteAcesa;
    [Tooltip("Os desenhos da lâmpada estão de cabeça pra baixo (na cena o SpriteRenderer usa Flip Y): a prévia vira o desenho do mesmo jeito.")]
    [SerializeField] private bool inverterPrevia = true;
    [Tooltip("Segundos com ligada = false e depois com ligada = true, alternando.")]
    [SerializeField] private float segundosPorFase = 2.2f;

    [Tooltip("Ordem do Canvas: acima do HUD/diálogo da cena, abaixo do CodigoMontadoUI (500).")]
    [SerializeField] private int sortingOrder = 400;

    public bool Aberto => canvasGO != null;
    public bool Concluido { get; private set; }

    private static LampadaAnimatorUI aberto;
    private static int frameFechou = -1;

    /// <summary>True com o painel aberto e no frame em que fechou — pra o Esc não abrir a pausa junto.</summary>
    public static bool SegurandoEsc => aberto != null || Time.frameCount == frameFechou;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        aberto = null;
        frameFechou = -1;
    }

    private readonly List<Encaixe> encaixes = new List<Encaixe>();
    private readonly List<AnimatorPuzzlePeca> pecasUI = new List<AnimatorPuzzlePeca>();
    private readonly Dictionary<string, Image> bordas = new Dictionary<string, Image>();
    private readonly Dictionary<string, Image> fundos = new Dictionary<string, Image>();
    private readonly Dictionary<string, Color> fundoNormal = new Dictionary<string, Color>();
    private readonly List<Image>[] partesSeta = new List<Image>[2];

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

    // Prévia
    private Image previaFundo;
    private Image previaLampada;
    private Image previaLuz;
    private TextMeshProUGUI previaEstado;
    private TextMeshProUGUI previaLeitura;
    private TextMeshProUGUI parametroValor;
    private Image parametroCaixa;
    private float previaTempo;
    private string previaEstadoAtual = EstadoApagada;
    private int setaAcesa = -1;
    private float setaAcesaAte;

    /// <summary>
    /// Monta e abre o painel. <paramref name="aoConcluir"/> é chamado quando as
    /// duas setas estiverem certas (o painel continua na tela até alguém chamar
    /// <see cref="Fechar"/>); <paramref name="aoCancelar"/>, se o jogador fechar
    /// antes de terminar.
    /// </summary>
    public void Abrir(Action aoConcluir, Action aoCancelar)
    {
        if (Aberto)
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
        previaTempo = 0f;
        previaEstadoAtual = EstadoApagada;
        setaAcesa = -1;

        Canvas canvas = VidaUI.CriarCanvas("LampadaAnimatorUI", sortingOrder);
        canvasGO = canvas.gameObject;
        aberto = this;
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
        JogoBloqueado.Fechar(this);
        if (aberto == this)
        {
            aberto = null;
        }

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

    private void OnDestroy()
    {
        Fechar();
    }

    // ---------------------------------------------------------------- montagem

    private void Montar(RectTransform raiz)
    {
        // Fundo opaco da moldura: também bloqueia cliques no cenário por trás.
        RectTransform tela = MolduraObjetoUI.Tela(raiz, fase, MolduraObjetoUI.TituloCurto(titulo), "lampada", Cancelar);

        // Janela da lâmpada: diagrama (esquerda) + Prévia/Objetivo (direita).
        RectTransform areaDiagrama = MolduraObjetoUI.AreaDiagrama(tela, "Lâmpada", MolduraObjetoUI.Legenda(tituloDiagrama));
        MontarDiagrama(areaDiagrama);
        MontarParametros(areaDiagrama);

        foreach (int seta in new[] { SetaApagadaAcesa, SetaAcesaApagada })
        {
            encaixes.Add(CriarEncaixe(areaDiagrama, seta));
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

        DestacarEstado(previaEstadoAtual);
        AtualizarProgresso();
    }

    private void MontarDiagrama(RectTransform area)
    {
        // Setas primeiro: ficam por baixo das caixas e dos encaixes.
        AnimatorDiagramaUI.NovaSeta(area, PosEntry + new Vector2(0f, -32f), PosApagada + new Vector2(0f, TamanhoCaixa.y * 0.5f));
        for (int i = 0; i < 2; i++)
        {
            partesSeta[i] = AnimatorDiagramaUI.NovaSeta(area, Setas[i, 0], Setas[i, 1]);
        }

        CriarCaixa(area, "Entry", PosEntry, new Vector2(170f, 64f), CorEntry);
        CriarCaixa(area, EstadoApagada, PosApagada, TamanhoCaixa, AnimatorDiagramaUI.CorIdle);
        CriarCaixa(area, EstadoAcesa, PosAcesa, TamanhoCaixa, AnimatorDiagramaUI.CorEstado);

        TextMeshProUGUI leg = VidaUI.NovoTexto(area, "Legenda_Apagada", "estado inicial", 20, VidaUI.TextoApagado, TextAlignmentOptions.Center, FontStyles.Italic);
        VidaUI.Centro(leg.rectTransform, PosApagada + new Vector2(0f, -TamanhoCaixa.y * 0.5f - 20f), new Vector2(300f, 32f));
    }

    private void CriarCaixa(RectTransform area, string estado, Vector2 pos, Vector2 tam, Color corFundo)
    {
        Image borda = AnimatorDiagramaUI.NovaCaixaEstado(area, estado, pos, tam, corFundo);
        if (estado == "Entry")
        {
            return; // Entry não é estado de verdade: nunca acende
        }

        bordas[estado] = borda;
        fundos[estado] = borda.transform.GetChild(0).GetComponent<Image>();
        fundoNormal[estado] = corFundo;
    }

    private void MontarParametros(RectTransform area)
    {
        RectTransform caixa = VidaUI.CaixaComBorda(area, "Parametros", new Vector2(0f, -220f), new Vector2(1040f, 120f), MolduraPuzzleUI.Escuro, CorParametros);
        VidaUI.Centro(VidaUI.NovoTexto(caixa, "Cabecalho", "Parameters", 22, MolduraPuzzleUI.TituloAzul, TextAlignmentOptions.Left, FontStyles.Bold).rectTransform,
            new Vector2(0f, 32f), new Vector2(990f, 30f));
        VidaUI.Centro(VidaUI.NovoTexto(caixa, "Nome", "ligada", 28, VidaUI.Texto, TextAlignmentOptions.Left, FontStyles.Bold).rectTransform,
            new Vector2(-300f, -15f), new Vector2(390f, 40f));
        VidaUI.Centro(VidaUI.NovoTexto(caixa, "Tipo", "Bool", 24, VidaUI.TextoApagado, TextAlignmentOptions.Left).rectTransform,
            new Vector2(-30f, -15f), new Vector2(150f, 40f));

        // "Checkbox" do Bool, como na janela Animator do Unity, com o valor ao vivo da prévia.
        parametroCaixa = VidaUI.NovaImagem(caixa, "Valor", VidaUI.Moldura);
        VidaUI.Centro(parametroCaixa.rectTransform, new Vector2(110f, -15f), new Vector2(34f, 34f));
        parametroValor = VidaUI.NovoTexto(caixa, "ValorTexto", "false", 24, VidaUI.TextoApagado, TextAlignmentOptions.Left, FontStyles.Bold);
        VidaUI.Centro(parametroValor.rectTransform, new Vector2(240f, -15f), new Vector2(200f, 40f));
        VidaUI.Centro(VidaUI.NovoTexto(caixa, "Quem", "(o sensor da sala muda)", 20, VidaUI.TextoApagado, TextAlignmentOptions.Right, FontStyles.Italic).rectTransform,
            new Vector2(330f, -15f), new Vector2(330f, 40f));
    }

    private Encaixe CriarEncaixe(RectTransform area, int seta)
    {
        Vector2 centro = (Setas[seta, 0] + Setas[seta, 1]) * 0.5f;
        MolduraObjetoUI.Encaixe e = MolduraObjetoUI.NovoEncaixe(area, "Encaixe_" + NomesSeta[seta], centro, TamanhoEncaixe, textoEncaixeVazio);
        return new Encaixe { seta = seta, rect = e.rect, fundo = e.miolo, tracejado = e.tracejado, dica = e.dica };
    }

    private void MontarPrevia(RectTransform tela)
    {
        string frase = string.IsNullOrEmpty(objetivo) ? descricaoTitulo : objetivo;
        MolduraObjetoUI.Coluna col = MolduraObjetoUI.ColunaPrevia(tela, tituloPrevia, frase, out RectTransform palco);
        previaEstado = col.estado;
        previaLeitura = col.leitura;
        textoProgresso = col.progresso;

        // O palco vira um pedaço da sala: fica claro quando a lâmpada acende.
        previaFundo = palco.GetComponent<Image>();
        previaFundo.color = CorSalaEscura;
        float x = MolduraObjetoUI.ColX + 16f, w = MolduraObjetoUI.PalcoW;
        Image teto = VidaUI.NovaImagem(tela, "Teto", MolduraPuzzleUI.Escuro);
        MolduraPuzzleUI.TL(teto.rectTransform, x, 200f, w, 12f);

        previaLuz = VidaUI.NovaImagem(tela, "Luz", new Color(1f, 0.84f, 0.51f, 0f));
        MolduraPuzzleUI.TL(previaLuz.rectTransform, x, 200f, w, 200f);

        previaLampada = VidaUI.NovaImagem(tela, "Lampada", Color.white);
        previaLampada.preserveAspect = true;
        RectTransform r = previaLampada.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(110f, 110f);
        r.anchoredPosition = new Vector2(MolduraObjetoUI.ColCentro, -272f);
        previaLampada.sprite = spriteApagada;
        r.localScale = new Vector3(1f, inverterPrevia ? -1f : 1f, 1f);
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

        // Peça errada: registra e recomeça (igual aos outros puzzles de encaixe).
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

    private void Update()
    {
        if (!Aberto || previaLampada == null)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !Concluido)
        {
            Cancelar();
            return;
        }

        float dt = Time.unscaledDeltaTime;
        previaTempo += dt;
        // Roteiro: ninguém na sala (false), alguém entra (true), sai (false)...
        bool ligada = Mathf.FloorToInt(previaTempo / Mathf.Max(0.5f, segundosPorFase)) % 2 == 1;

        string novo = previaEstadoAtual;
        if (previaEstadoAtual == EstadoApagada && ligada && SetaPreenchida(SetaApagadaAcesa))
        {
            novo = EstadoAcesa;
        }
        else if (previaEstadoAtual == EstadoAcesa && !ligada && SetaPreenchida(SetaAcesaApagada))
        {
            novo = EstadoApagada;
        }

        if (novo != previaEstadoAtual)
        {
            AcenderSeta(novo == EstadoAcesa ? SetaApagadaAcesa : SetaAcesaApagada);
            previaEstadoAtual = novo;
            DestacarEstado(novo);
        }

        if (setaAcesa >= 0 && Time.unscaledTime >= setaAcesaAte)
        {
            DestacarSeta(setaAcesa, false);
            setaAcesa = -1;
        }

        bool acesa = previaEstadoAtual == EstadoAcesa;
        previaLampada.sprite = acesa && spriteAcesa != null ? spriteAcesa : spriteApagada;
        previaFundo.color = acesa ? new Color32(0x9A, 0xA1, 0xAE, 0xFF) : CorSalaEscura;
        previaLuz.color = new Color(1f, 0.84f, 0.51f, acesa ? 0.22f : 0f);
        previaEstado.text = previaEstadoAtual;
        previaLeitura.text = ligada ? "alguém na sala:  ligada = true" : "sala vazia:  ligada = false";

        parametroCaixa.color = ligada ? VidaUI.Amarelo : VidaUI.Moldura;
        parametroValor.text = ligada ? "true" : "false";
        parametroValor.color = ligada ? VidaUI.Amarelo : VidaUI.TextoApagado;
    }

    private void DestacarEstado(string estado)
    {
        foreach (KeyValuePair<string, Image> par in bordas)
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

    // ---------------------------------------------------------------- padrão

    private static AnimatorPuzzleUI.Peca[] PecasPadrao()
    {
        return new[]
        {
            new AnimatorPuzzleUI.Peca
            {
                texto = "isGrounded == true",
                setaCorreta = -1,
                descricao = "Parâmetro do Animator do Player: verdadeiro quando ele está com os pés no chão."
            },
            new AnimatorPuzzleUI.Peca
            {
                texto = "ligada == true",
                setaCorreta = SetaApagadaAcesa,
                descricao = "O parâmetro Bool ligada vale true. O sensor da sala deixa ligada = true enquanto tem alguém lá dentro."
            },
            new AnimatorPuzzleUI.Peca
            {
                texto = "Input.GetMouseButton(0)",
                setaCorreta = -1,
                descricao = "Verdadeiro enquanto o botão esquerdo do mouse está apertado."
            },
            new AnimatorPuzzleUI.Peca
            {
                texto = "velocidadeX > 0.1",
                setaCorreta = -1,
                descricao = "Parâmetro do Animator do Player: a velocidade horizontal dele."
            },
            new AnimatorPuzzleUI.Peca
            {
                texto = "ligada == false",
                setaCorreta = SetaAcesaApagada,
                descricao = "O parâmetro Bool ligada vale false: não tem ninguém na sala, e o sensor desligou o parâmetro."
            },
            new AnimatorPuzzleUI.Peca
            {
                texto = "Start()",
                setaCorreta = -1,
                descricao = "Método que o Unity chama uma única vez, quando o objeto começa."
            },
        };
    }
}
