using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Puzzle curto de encaixe da mini-lição de HUD (fase "Interacao_Vida", ver
/// HudLicao). Mesma lógica dos puzzles de encaixe da Fisica (PuzzleControl/
/// SnapZone/DragPiece): arrastar a peça até o encaixe, passar o mouse mostra
/// a descrição, e peça errada recomeça o puzzle inteiro (de propósito) e
/// conta como erro no registro.
///
/// A diferença é o "palco": esta cena é de UI/plataforma e não tem o layout
/// world-space da Fisica, então o puzzle é todo UGUI montado em runtime
/// (Canvas próprio, overlay), com arrasto via IBeginDragHandler/IDragHandler/
/// IEndDragHandler. Funciona com Time.timeScale = 0 (tudo aqui usa tempo sem
/// escala), que é como a HudLicao congela a fase enquanto o puzzle está aberto.
///
/// Tela inteira na moldura do
/// puzzle do Animator (MolduraPuzzleUI). Janela com o código separado por
/// script e os encaixes dentro da linha, coluna Prévia com a placa VIDA
/// (vida x barra) e o progresso, janela Peças com cartões e a Dica embaixo,
/// que faz o papel do antigo painel Descrição.
/// </summary>
public class HudPuzzleUI : MonoBehaviour
{
    [Serializable]
    public class Peca
    {
        [Tooltip("Texto da peça — também é o ID usado pra conferir o encaixe.")]
        public string nome;
        [Tooltip("Mostrado no painel Dica ao passar o mouse.")]
        [TextArea(2, 4)] public string descricao;

        [Header("Cartão (moldura da Física)")]
        public Sprite icone;
        [Tooltip("Moldura colorida do cartão (EditorKit/pecas/peca_*).")]
        public Sprite moldura;
        [Tooltip("Etiqueta à direita do cartão (ex: MÉTODO).")]
        public string categoria;
        public Color corCategoria = Color.white;
        [Tooltip("Linha curta embaixo do nome. Não deve dizer em qual script a peça fica.")]
        public string subtitulo;
    }

    [Serializable]
    public class Encaixe
    {
        [Tooltip("Nome da peça que entra aqui (igual ao 'nome' de uma das peças).")]
        public string pecaCorreta;
        [Tooltip("Linha de código do encaixe. '___' marca onde a peça entra.")]
        public string dicaCodigo;
        [Tooltip("Script onde a linha fica (ex: PlayerHealth). Linhas seguidas do mesmo script ficam no mesmo bloco.")]
        public string legenda;
        [Tooltip("O que o bloco do script representa (ex: o dado e o evento). Vai na legenda do bloco.")]
        public string bloco;
    }

    [Serializable]
    public class Textos
    {
        [Header("Cabeçalho")]
        public string fase = "Fase Interação e Vida";
        public string etapa = "Barra de vida";
        public string aba = "HUD";
        [Tooltip("Comentário embaixo do código.")]
        public string comentario = "// quando a vida muda, a barra tem que ser avisada";

        [Header("Prévia")]
        public string barraParada = "barra parada";
        public string barraConectada = "barra conectada";
        [Tooltip("{vida} = vida atual.")]
        public string textoVida = "vidaAtual = {vida}";
        [Tooltip("{barra} = o que os quadrados mostram.")]
        public string textoBarraParada = "a barra continua mostrando {barra}";
        [Tooltip("{vida} = vida atual.")]
        public string textoBarraConectada = "a barra mostra {vida}, igual à vida";
        public string objetivo = "a barra muda junto com a vida";
        public string progressoSub = "Peça errada recomeça o puzzle.";

        [Header("Dica")]
        [Tooltip("Linha apagada embaixo da Dica.")]
        public string instrucao = "Arraste cada peça até o encaixe certo. Passe o mouse numa peça pra ler aqui o que ela faz.";
        public string descricaoTitulo = "Dado, evento, tela";
        [TextArea(2, 4)] public string descricaoPadrao = "O dado muda, o evento avisa, a tela redesenha. A barra só mostra a vida nova se alguém avisar ela.";
        public string erroTitulo = "Essa peça não entra aí";
        [TextArea(2, 4)] public string erroTexto = "O puzzle recomeçou. Leia a descrição das peças e tente de novo.";
    }

    private class Slot
    {
        public int numero;
        public Encaixe dados;
        public RectTransform rect;
        public Image fundo;
        public GameObject vazio;
        public HudPuzzlePeca ocupante;
    }

    // Coordenadas da tela de referência 1920x1080 (ver MolduraPuzzleUI.TL).
    private const float XCodigo = 104f;
    private const float XPrevia = 975f;
    private const float LarguraPrevia = 290f;
    private static readonly Vector2 TamanhoSlot = new Vector2(270f, 56f);
    private static readonly Color BarraApagada = new Color32(0x24, 0x30, 0x48, 0xFF);
    private static readonly Color CorVidaTexto = new Color32(0x3F, 0xB8, 0xC8, 0xFF);

    private readonly List<Slot> slots = new List<Slot>();
    private readonly List<HudPuzzlePeca> pecas = new List<HudPuzzlePeca>();
    private readonly List<Image> segmentos = new List<Image>();
    private readonly List<Image> quadrados = new List<Image>();

    private string conceito;
    private Textos textos;
    private Action aoConcluir;
    private int vida;

    private RectTransform camadaArraste;
    private MolduraPuzzleUI.PainelDica dica;
    private TextMeshProUGUI estadoPrevia;
    private TextMeshProUGUI textoBarra;
    private TextMeshProUGUI textoProgresso;

    private int colocadas;
    private int erros;
    private float inicio;
    private bool concluido;

    public bool Concluido => concluido;

    /// <summary>
    /// Monta e abre o puzzle. <paramref name="aoConcluir"/> é chamado quando
    /// todos os encaixes estiverem certos (o puzzle continua na tela até
    /// alguém chamar <see cref="Fechar"/>, igual ao PuzzleControl, que deixa
    /// as peças encaixadas visíveis por trás do painel de código montado).
    /// <paramref name="vida"/>, <paramref name="barra"/> e <paramref name="vidaMax"/>
    /// desenham a placa VIDA da Prévia (a vida real e o que a barra parada mostra).
    /// </summary>
    public static HudPuzzleUI Abrir(string conceito, Encaixe[] encaixes, Peca[] pecas, Textos textos, int sortingOrder,
        int vida, int barra, int vidaMax, Action aoConcluir)
    {
        Canvas canvas = VidaUI.CriarCanvas("HudPuzzleUI", sortingOrder);
        var ui = canvas.gameObject.AddComponent<HudPuzzleUI>();
        ui.conceito = conceito;
        ui.textos = textos ?? new Textos();
        ui.aoConcluir = aoConcluir;
        ui.vida = vida;
        ui.Montar((RectTransform)canvas.transform, encaixes, pecas, barra, vidaMax);

        ui.inicio = Time.unscaledTime;
        RegistroAprendizagem.Registrar("puzzle_inicio", conceito);
        // Some sozinho do JogoBloqueado quando o Fechar destrói este objeto.
        JogoBloqueado.Abrir(ui);
        return ui;
    }

    public void Fechar()
    {
        Destroy(gameObject);
    }

    // ---------------------------------------------------------------- montagem

    private void Montar(RectTransform raiz, Encaixe[] encaixes, Peca[] dadosPecas, int barra, int vidaMax)
    {
        // Fundo opaco da moldura: também bloqueia cliques no cenário por trás
        // (InteractableItem ignora cliques com o ponteiro em cima de UI).
        RectTransform tela = MolduraPuzzleUI.Tela(raiz);
        MolduraPuzzleUI.Cabecalho(tela, textos.fase, 1, new[] { textos.etapa });

        MolduraPuzzleUI.Janela(tela, "JanelaCodigo", 40, 80, 1280, 740, textos.aba);
        MontarCodigo(tela, encaixes);
        MontarPrevia(tela, barra, vidaMax);

        MolduraPuzzleUI.Janela(tela, "JanelaPecas", 1352, 80, 540, 740, "Peças");
        int qtd = dadosPecas != null ? Mathf.Min(dadosPecas.Length, 7) : 0;
        for (int i = 0; i < qtd; i++)
        {
            pecas.Add(CriarPeca(tela, dadosPecas[i], new Vector2(1409f, 200f + i * 82f)));
        }

        dica = MolduraPuzzleUI.Dica(tela, textos.instrucao);
        MostrarPadrao();
        AtualizarProgresso();

        // Por último = desenhada por cima de tudo: a peça sendo arrastada vem pra cá.
        var arrasteGO = new GameObject("Arraste", typeof(RectTransform));
        camadaArraste = (RectTransform)arrasteGO.transform;
        camadaArraste.SetParent(raiz, false);
        VidaUI.Esticar(camadaArraste);
    }

    // Um bloco por script (legenda), separados por divisória; a linha do
    // encaixe tem o código antes e depois do "___".
    private void MontarCodigo(RectTransform tela, Encaixe[] encaixes)
    {
        MolduraPuzzleUI.Area(tela, "AreaCodigo", 72, 140, 846, 650);

        int total = encaixes != null ? Mathf.Min(encaixes.Length, 5) : 0;
        float y = 0f;
        string script = null;
        for (int i = 0; i < total; i++)
        {
            Encaixe e = encaixes[i];
            if (i == 0 || e.legenda != script)
            {
                float yLegenda = 178f;
                if (i > 0)
                {
                    MolduraPuzzleUI.Divisoria(tela, XCodigo, y + 75f, 782f);
                    yLegenda = y + 123f;
                }

                script = e.legenda;
                MolduraPuzzleUI.Legenda(tela, LegendaDoBloco(e), XCodigo, yLegenda);
                y = yLegenda + 72f;
            }
            else
            {
                y += 95f;
            }

            slots.Add(CriarSlot(tela, i + 1, e, y));
        }

        if (!string.IsNullOrEmpty(textos.comentario))
        {
            TextMeshProUGUI c = MolduraPuzzleUI.Txt(tela, "Comentario", textos.comentario, XCodigo, y + 100f, 18, MolduraPuzzleUI.Mono, MolduraPuzzleUI.Apagado);
            c.richText = false;
        }
    }

    private static string LegendaDoBloco(Encaixe e)
    {
        if (string.IsNullOrEmpty(e.legenda))
        {
            return "";
        }

        string s = e.legenda + ".cs";
        if (!string.IsNullOrEmpty(e.bloco))
        {
            s += " · " + e.bloco;
        }
        return s.ToUpperInvariant();
    }

    private Slot CriarSlot(RectTransform tela, int numero, Encaixe dados, float y)
    {
        TemaLaboratorio tema = TemaLabUI.Tema;
        Image selo = TemaLabUI.Imagem(tela, $"Numero{numero}", tema != null ? tema.seloNumero : null, VidaUI.Amarelo);
        selo.preserveAspect = true;
        MolduraPuzzleUI.TL(selo.rectTransform, XCodigo, y - 20f, 40f, 40f);
        TextMeshProUGUI n = TemaLabUI.Texto(selo.transform, "N", numero.ToString(), 22, TemaLabUI.TextoSeloNumero, TemaLabUI.Fonte.Display, TextAlignmentOptions.Center);
        VidaUI.Esticar(n.rectTransform);
        n.rectTransform.offsetMin = new Vector2(0f, 2f);

        // Sem rich text: a linha pode ter "<int, int>", que o TMP tentaria ler como tag.
        string codigo = dados.dicaCodigo ?? "";
        int corte = codigo.IndexOf("___", StringComparison.Ordinal);
        string antes = corte >= 0 ? codigo.Substring(0, corte) : codigo + " ";
        string depois = corte >= 0 ? codigo.Substring(corte + 3) : "";

        float x = XCodigo + 58f;
        TextMeshProUGUI t1 = MolduraPuzzleUI.Txt(tela, $"Codigo{numero}a", antes, x, y, 24, MolduraPuzzleUI.Mono, MolduraPuzzleUI.TextoClaro);
        t1.richText = false;
        x += t1.GetPreferredValues(antes).x + 4f;

        Image fundo = VidaUI.NovaImagem(tela, $"Encaixe{numero}", MolduraPuzzleUI.FundoEncaixe);
        VidaUI.Arredondar(fundo);
        RectTransform rect = MolduraPuzzleUI.TL(fundo.rectTransform, x, y - TamanhoSlot.y * 0.5f, TamanhoSlot.x, TamanhoSlot.y);

        var vazioGO = new GameObject("Vazio", typeof(RectTransform));
        var vazio = (RectTransform)vazioGO.transform;
        vazio.SetParent(rect, false);
        VidaUI.Esticar(vazio);
        VidaUI.BordaTracejada(vazio, TamanhoSlot, MolduraPuzzleUI.TintaEncaixe, 10f, 7f, 2f);
        TextMeshProUGUI arraste = VidaUI.NovoTexto(vazio, "Arraste", "arraste uma peça", 15, MolduraPuzzleUI.Apagado, TextAlignmentOptions.Center, FontStyles.Italic);
        if (MolduraPuzzleUI.Corpo != null) arraste.font = MolduraPuzzleUI.Corpo;
        VidaUI.Esticar(arraste.rectTransform);

        x += TamanhoSlot.x + 4f;
        TextMeshProUGUI t2 = MolduraPuzzleUI.Txt(tela, $"Codigo{numero}b", depois, x, y, 24, MolduraPuzzleUI.Mono, MolduraPuzzleUI.TextoClaro);
        t2.richText = false;

        return new Slot { numero = numero, dados = dados, rect = rect, fundo = fundo, vazio = vazioGO };
    }

    private void MontarPrevia(RectTransform tela, int barra, int vidaMax)
    {
        MolduraPuzzleUI.Area(tela, "AreaPrevia", 950, 140, 340, 650);
        MolduraPuzzleUI.Txt(tela, "PreviaTitulo", "Prévia", XPrevia, 172, 22, MolduraPuzzleUI.CorpoForte, MolduraPuzzleUI.TituloAzul);
        estadoPrevia = MolduraPuzzleUI.Txt(tela, "PreviaEstado", textos.barraParada, XPrevia + LarguraPrevia, 172, 16, MolduraPuzzleUI.CorpoForte, VidaUI.Vermelho, true);

        // Placa VIDA em miniatura, desenhando o que a barra parada mostra.
        Image placa = VidaUI.NovaImagem(tela, "PlacaVida", new Color32(0x0D, 0x13, 0x1F, 0xFF));
        VidaUI.Arredondar(placa);
        MolduraPuzzleUI.TL(placa.rectTransform, XPrevia, 210, LarguraPrevia, 76);
        MolduraPuzzleUI.Txt(tela, "PlacaVidaTexto", "VIDA", XPrevia + 20f, 248, 20, MolduraPuzzleUI.MonoForte, CorVidaTexto);
        int max = Mathf.Clamp(vidaMax, 1, 6);
        float passo = Mathf.Min(52f, 165f / max);
        float lado = Mathf.Min(42f, passo - 10f);
        for (int i = 0; i < max; i++)
        {
            Image q = VidaUI.NovaImagem(tela, "Quadrado" + (i + 1), Color.white);
            MolduraPuzzleUI.TL(q.rectTransform, XPrevia + 115f + i * passo, 248f - lado * 0.5f, lado, lado);
            quadrados.Add(q);
        }
        DesenharQuadrados(barra);

        TextMeshProUGUI tv = MolduraPuzzleUI.Txt(tela, "VidaAtual", textos.textoVida.Replace("{vida}", vida.ToString()), XPrevia, 322, 20, MolduraPuzzleUI.MonoForte, VidaUI.Vermelho);
        tv.richText = false;
        textoBarra = MolduraPuzzleUI.Txt(tela, "BarraMostra", textos.textoBarraParada.Replace("{barra}", barra.ToString()), XPrevia, 352, 16, MolduraPuzzleUI.Corpo, MolduraPuzzleUI.Rotulo);

        MolduraPuzzleUI.Divisoria(tela, XPrevia, 392, LarguraPrevia);
        MolduraPuzzleUI.Txt(tela, "ObjetivoTitulo", "Objetivo", XPrevia, 430, 22, MolduraPuzzleUI.CorpoForte, MolduraPuzzleUI.TituloAzul);
        MolduraPuzzleUI.Txt(tela, "ObjetivoTexto", textos.objetivo, XPrevia, 462, 16, MolduraPuzzleUI.Corpo, MolduraPuzzleUI.TextoClaro);

        // Progresso: um traço por encaixe, dourado quando preenchido.
        int n = Mathf.Max(1, slots.Count);
        float w = (LarguraPrevia - (n - 1) * 8f) / n;
        for (int i = 0; i < n; i++)
        {
            Image s = VidaUI.NovaImagem(tela, "Progresso" + (i + 1), BarraApagada);
            MolduraPuzzleUI.TL(s.rectTransform, XPrevia + i * (w + 8f), 704, w, 6);
            segmentos.Add(s);
        }
        textoProgresso = MolduraPuzzleUI.Txt(tela, "ProgressoTexto", "", XPrevia, 740, 20, MolduraPuzzleUI.CorpoForte, MolduraPuzzleUI.TextoClaro);
        MolduraPuzzleUI.Txt(tela, "ProgressoSub", textos.progressoSub, XPrevia, 766, 15, MolduraPuzzleUI.Corpo, MolduraPuzzleUI.Apagado);
    }

    private void DesenharQuadrados(int cheios)
    {
        TemaLaboratorio tema = TemaLabUI.Tema;
        for (int i = 0; i < quadrados.Count; i++)
        {
            Image q = quadrados[i];
            bool cheio = i < cheios;
            Sprite s = tema == null ? null : (cheio || tema.vidaVazia == null ? tema.vidaCheia : tema.vidaVazia);
            q.sprite = s;
            q.color = s != null ? (cheio || tema.vidaVazia != null ? Color.white : new Color(1f, 1f, 1f, 0.15f))
                                : (cheio ? VidaUI.Vermelho : new Color(1f, 1f, 1f, 0.15f));
        }
    }

    private void AtualizarProgresso()
    {
        for (int i = 0; i < segmentos.Count; i++)
        {
            segmentos[i].color = i < colocadas ? MolduraPuzzleUI.Dourado : BarraApagada;
        }

        if (textoProgresso != null)
        {
            textoProgresso.text = $"<b>{colocadas} de {slots.Count} encaixes.</b>";
        }
    }

    private HudPuzzlePeca CriarPeca(RectTransform tela, Peca dados, Vector2 cantoTL)
    {
        MolduraPuzzleUI.Cartao c = MolduraPuzzleUI.NovoCartao(tela, "Peca_" + dados.nome, dados.moldura, dados.icone,
            dados.nome, dados.subtitulo, dados.categoria, dados.corCategoria);
        c.nome.richText = false;
        MolduraPuzzleUI.TL(c.moldura.rectTransform, cantoTL.x, cantoTL.y, MolduraPuzzleUI.TamanhoCartao.x, MolduraPuzzleUI.TamanhoCartao.y);
        MolduraPuzzleUI.EstilizarCartao(c, false, null);

        var peca = c.moldura.gameObject.AddComponent<HudPuzzlePeca>();
        peca.Iniciar(this, dados, c);
        return peca;
    }

    // ---------------------------------------------------------------- lógica

    internal RectTransform CamadaArraste => camadaArraste;

    internal void MostrarDescricao(Peca dados)
    {
        dica.titulo.color = MolduraPuzzleUI.TituloAzul;
        dica.titulo.text = dados.nome;
        dica.texto.text = dados.descricao;
    }

    internal void MostrarPadrao()
    {
        dica.titulo.color = MolduraPuzzleUI.TituloAzul;
        dica.titulo.text = textos.descricaoTitulo;
        dica.texto.text = textos.descricaoPadrao;
    }

    /// <summary>Chamado pela peça ao ser solta.</summary>
    internal void Soltar(HudPuzzlePeca peca, Vector2 pontoTela)
    {
        if (concluido)
        {
            peca.VoltarParaCasa();
            return;
        }

        Slot alvo = SlotEm(pontoTela) ?? SlotEm(peca.CentroNaTela());
        if (alvo == null || alvo.ocupante != null)
        {
            peca.VoltarParaCasa();
            return;
        }

        if (peca.Dados.nome == alvo.dados.pecaCorreta)
        {
            Encaixar(peca, alvo);
            return;
        }

        // Peça errada: registra e recomeça o puzzle inteiro (intencional,
        // igual ao PuzzleControl.WrongPiece).
        erros++;
        RegistroAprendizagem.RegistrarErro("puzzle_peca_errada", $"{conceito} | encaixe {alvo.numero} | {peca.Dados.nome}");
        Resetar();
        dica.titulo.text = textos.erroTitulo;
        dica.titulo.color = VidaUI.Vermelho;
        dica.texto.text = textos.erroTexto;
        StartCoroutine(PiscarErro(alvo));
    }

    private Slot SlotEm(Vector2 pontoTela)
    {
        foreach (Slot s in slots)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(s.rect, pontoTela, null))
            {
                return s;
            }
        }

        return null;
    }

    private void Encaixar(HudPuzzlePeca peca, Slot slot)
    {
        slot.ocupante = peca;
        slot.vazio.SetActive(false);
        peca.Travar(slot.rect);
        colocadas++;
        AtualizarProgresso();
        AudioManager.TocarEfeito(AudioManager.Efeito.Encaixe);

        if (colocadas >= slots.Count)
        {
            concluido = true;
            RegistroAprendizagem.Registrar("puzzle_concluido", $"{conceito} | {Time.unscaledTime - inicio:0}s | {erros} erros");
            MostrarPadrao();

            // A Prévia já mostra a barra conectada; a de verdade muda quando o puzzle fecha.
            DesenharQuadrados(vida);
            estadoPrevia.text = textos.barraConectada;
            estadoPrevia.color = VidaUI.Verde;
            textoBarra.text = textos.textoBarraConectada.Replace("{vida}", vida.ToString());
            textoBarra.color = VidaUI.Verde;

            aoConcluir?.Invoke();
        }
    }

    private void Resetar()
    {
        colocadas = 0;
        foreach (Slot s in slots)
        {
            s.ocupante = null;
            s.vazio.SetActive(true);
        }

        foreach (HudPuzzlePeca p in pecas)
        {
            p.VoltarParaCasa();
        }

        AtualizarProgresso();
    }

    private IEnumerator PiscarErro(Slot slot)
    {
        for (int i = 0; i < 2; i++)
        {
            slot.fundo.color = new Color(VidaUI.Vermelho.r, VidaUI.Vermelho.g, VidaUI.Vermelho.b, 0.45f);
            yield return new WaitForSecondsRealtime(0.15f);
            slot.fundo.color = MolduraPuzzleUI.FundoEncaixe;
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
}
