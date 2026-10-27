using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Peças em comum dos painéis do Animator dos objetos da fase Animacao
/// (ObjetoAnimatorUI e LampadaAnimatorUI) na moldura do puzzle do Animator:
/// cabeçalho com a lista dos
/// objetos, tecla Esc Fechar, coluna Prévia/Objetivo, encaixes tracejados,
/// cartões de peça e o esconder da placa VIDA e da faixa de objetivo.
/// Coordenadas em pixels da tela de referência (ver MolduraPuzzleUI).
/// </summary>
public static class MolduraObjetoUI
{
    // Janela do objeto: diagrama à esquerda, coluna Prévia/Objetivo à direita.
    public const float DiagX = 70f, DiagY = 140f, DiagW = 900f, DiagH = 653f;
    public const float ColX = 990f, ColY = 140f, ColW = 300f, ColH = 653f;
    public const float ColCentro = ColX + ColW * 0.5f;
    public const float PalcoW = 268f;

    /// <summary>Tamanho das coordenadas do diagrama antigo (1100 x 620), encolhido pra caber na janela.</summary>
    public static readonly Vector2 TamanhoDiagrama = new Vector2(1100f, 620f);
    public const float EscalaDiagrama = 0.8f;

    private static readonly string[] Ordem = { "moeda", "jumppad", "bateria", "alavanca", "lampada" };
    private static readonly string[] Nomes = { "Moeda", "Jumppad", "Bateria", "Alavanca", "Lâmpada" };

    /// <summary>
    /// Fundo da moldura, cabeçalho "Fase Animação  Animator da X", a lista dos
    /// objetos (feitos em verde, o atual em branco) e a tecla Esc Fechar.
    /// </summary>
    public static RectTransform Tela(RectTransform raiz, string fase, string titulo, string tipoAtual, Action aoFechar)
    {
        RectTransform tela = MolduraPuzzleUI.Tela(raiz);
        MolduraPuzzleUI.Txt(tela, "Titulo", $"<b>{fase}</b>  <color=#8A97AD>{titulo}</color>", 155, 41, 28, MolduraPuzzleUI.Corpo, Color.white);
        MolduraPuzzleUI.Txt(tela, "Objetos", LinhaObjetos(tipoAtual), 1640, 40, 20, MolduraPuzzleUI.Corpo, Color.white, true);
        TeclaFechar(tela, aoFechar);
        return tela;
    }

    /// <summary>"Objetos: Moeda Jumppad ..." com os configurados em verde e o atual em branco.</summary>
    public static string LinhaObjetos(string tipoAtual)
    {
        var sb = new System.Text.StringBuilder("<color=#8A97AD>Objetos:</color> ");
        LampadaSensor lampada = UnityEngine.Object.FindAnyObjectByType<LampadaSensor>();
        for (int i = 0; i < Ordem.Length; i++)
        {
            bool existe;
            bool feito;
            if (Ordem[i] == "lampada")
            {
                existe = lampada != null;
                feito = existe && lampada.Configurada;
            }
            else
            {
                ObjetoAnimatorUI p = ObjetoAnimatorUI.Do(Ordem[i]);
                existe = p != null;
                feito = existe && p.Configurado;
            }

            if (!existe) continue;
            sb.Append("   ");
            if (Ordem[i] == tipoAtual) sb.Append($"<color=#FFFFFF><b>{Nomes[i]}</b></color>");
            else if (feito) sb.Append($"<color=#7EE2A8>{Nomes[i]}</color>");
            else sb.Append($"<color=#5D6B84>{Nomes[i]}</color>");
        }
        return sb.ToString();
    }

    /// <summary>Tecla "Esc Fechar" no canto de cima, clicável.</summary>
    public static RectTransform TeclaFechar(RectTransform tela, Action aoFechar)
    {
        RectTransform teclas = TemaLabUI.LegendaTeclas(tela, "Esc", "Fechar");
        teclas.anchorMin = teclas.anchorMax = new Vector2(1f, 1f);
        teclas.pivot = new Vector2(1f, 0.5f);
        teclas.sizeDelta = new Vector2(220f, 52f);
        teclas.anchoredPosition = new Vector2(-28f, -40f);
        teclas.localScale = Vector3.one * 0.8f;

        // Área de clique transparente por cima da tecla e do texto.
        Image alvo = teclas.gameObject.AddComponent<Image>();
        alvo.color = Color.clear;
        Button botao = teclas.gameObject.AddComponent<Button>();
        botao.targetGraphic = alvo;
        botao.transition = Selectable.Transition.None;
        if (aoFechar != null) botao.onClick.AddListener(() => aoFechar());
        return teclas;
    }

    /// <summary>Área do diagrama com a legenda, e a raiz (1100 x 620, encolhida) onde vão os estados, as setas e os encaixes.</summary>
    public static RectTransform AreaDiagrama(RectTransform tela, string aba, string legenda)
    {
        MolduraPuzzleUI.Janela(tela, "JanelaObjeto", 38, 80, 1282, 743, aba);
        RectTransform area = MolduraPuzzleUI.Area(tela, "AreaDiagrama", DiagX, DiagY, DiagW, DiagH);
        MolduraPuzzleUI.Legenda(tela, legenda, DiagX + 20f, DiagY + 27f);

        var go = new GameObject("Diagrama", typeof(RectTransform));
        var raiz = (RectTransform)go.transform;
        raiz.SetParent(area, false);
        VidaUI.Centro(raiz, new Vector2(0f, -10f), TamanhoDiagrama);
        raiz.localScale = Vector3.one * EscalaDiagrama;
        return raiz;
    }

    /// <summary>Legenda do diagrama a partir do título antigo ("Animator da Moeda (Base Layer)" → "ANIMATOR DA MOEDA · BASE LAYER").</summary>
    public static string Legenda(string tituloDiagrama)
    {
        return (tituloDiagrama ?? "").Replace(" (Base Layer)", " · Base Layer").ToUpperInvariant();
    }

    /// <summary>Título curto do cabeçalho ("Configure o Animator da Moeda" → "Animator da Moeda").</summary>
    public static string TituloCurto(string titulo)
    {
        string t = titulo ?? "";
        return t.StartsWith("Configure o ") ? t.Substring("Configure o ".Length) : t;
    }

    public class Coluna
    {
        public TextMeshProUGUI estado;
        public TextMeshProUGUI leitura;
        public TextMeshProUGUI progresso;
    }

    /// <summary>
    /// Coluna da direita: "Prévia" com o nome do estado, o palco (devolvido em
    /// <paramref name="palco"/>, 268 x 200, onde o painel põe o objeto), a
    /// leitura do parâmetro, o Objetivo e o progresso dos encaixes.
    /// </summary>
    public static Coluna ColunaPrevia(RectTransform tela, string tituloPrevia, string objetivo, out RectTransform palco)
    {
        MolduraPuzzleUI.Area(tela, "AreaPrevia", ColX, ColY, ColW, ColH);
        var c = new Coluna();
        MolduraPuzzleUI.Txt(tela, "PreviaTitulo", $"<b>{tituloPrevia}</b>", ColX + 18f, 170, 22, MolduraPuzzleUI.CorpoForte, MolduraPuzzleUI.TituloAzul);
        c.estado = MolduraPuzzleUI.Txt(tela, "PreviaEstado", "", ColX + ColW - 16f, 170, 18, MolduraPuzzleUI.CorpoForte, Color.white, true);

        Image fundo = VidaUI.NovaImagem(tela, "Palco", MolduraPuzzleUI.FundoEncaixe);
        palco = MolduraPuzzleUI.TL(fundo.rectTransform, ColX + 16f, 200f, PalcoW, 200f);

        c.leitura = MolduraPuzzleUI.Txt(tela, "PreviaLeitura", "", ColX + 16f, 412f, 16, MolduraPuzzleUI.Corpo, MolduraPuzzleUI.Rotulo);
        c.leitura.textWrappingMode = TextWrappingModes.Normal;
        c.leitura.alignment = TextAlignmentOptions.Top;
        RectTransform rl = c.leitura.rectTransform;
        rl.pivot = new Vector2(0f, 1f);
        rl.sizeDelta = new Vector2(PalcoW, 60f);
        rl.anchoredPosition = new Vector2(ColX + 16f, -412f);
        c.leitura.enableAutoSizing = true;
        c.leitura.fontSizeMin = 12f;
        c.leitura.fontSizeMax = 16f;

        MolduraPuzzleUI.Divisoria(tela, ColX + 16f, 485f, PalcoW);
        MolduraPuzzleUI.Txt(tela, "ObjetivoTitulo", "<b>Objetivo</b>", ColX + 18f, 515f, 22, MolduraPuzzleUI.CorpoForte, MolduraPuzzleUI.TituloAzul);
        TextMeshProUGUI frase = MolduraPuzzleUI.Txt(tela, "ObjetivoFrase", objetivo, ColX + 18f, 545f, 16, MolduraPuzzleUI.Corpo, new Color32(0xC9, 0xD6, 0xEA, 0xFF));
        frase.textWrappingMode = TextWrappingModes.Normal;
        frase.alignment = TextAlignmentOptions.TopLeft;
        RectTransform rf = frase.rectTransform;
        rf.pivot = new Vector2(0f, 1f);
        rf.sizeDelta = new Vector2(PalcoW, 90f);
        rf.anchoredPosition = new Vector2(ColX + 18f, -538f);

        c.progresso = MolduraPuzzleUI.Txt(tela, "Progresso", "", ColX + 18f, 740f, 19, MolduraPuzzleUI.Corpo, MolduraPuzzleUI.Rotulo);
        MolduraPuzzleUI.Txt(tela, "ProgressoLegenda", "A prévia repete em loop.", ColX + 18f, 767f, 15, MolduraPuzzleUI.Corpo, MolduraPuzzleUI.Escuro);
        return c;
    }

    public static string TextoProgresso(int feitos, int total)
    {
        return $"<b>{feitos} de {total} encaixe{(total == 1 ? "" : "s")}.</b>";
    }

    /// <summary>Chão da prévia (o mesmo piso da prévia do puzzle do Animator), na parte de baixo do palco.</summary>
    public static void ChaoPrevia(RectTransform tela, float topo)
    {
        TemaLaboratorio tema = TemaLaboratorio.Atual;
        Sprite tile = tema != null ? tema.puzzleChaoPrevia : null;
        for (float x = ColX + 16f; x < ColX + 16f + PalcoW; x += 41f)
        {
            Image t = VidaUI.NovaImagem(tela, "Chao", tile != null ? Color.white : VidaUI.Moldura);
            t.sprite = tile;
            MolduraPuzzleUI.TL(t.rectTransform, x, topo, Mathf.Min(41f, ColX + 16f + PalcoW - x), 41f);
        }
    }

    public class Encaixe
    {
        public RectTransform rect;
        public Image miolo;
        public Image tracejado;
        public TextMeshProUGUI dica;
    }

    /// <summary>Encaixe da seta: fundo escuro, tracejado da Física e "arraste uma condição".</summary>
    public static Encaixe NovoEncaixe(RectTransform raizDiagrama, string nome, Vector2 centro, Vector2 tam, string textoVazio)
    {
        TemaLaboratorio tema = TemaLaboratorio.Atual;
        Image fundo = VidaUI.NovaImagem(raizDiagrama, nome, Color.clear);
        RectTransform rect = VidaUI.Centro(fundo.rectTransform, centro, tam);
        Image miolo = VidaUI.NovaImagem(rect, "Miolo", MolduraPuzzleUI.FundoEncaixe);
        VidaUI.Esticar(miolo.rectTransform);
        miolo.rectTransform.offsetMin = new Vector2(11f, 4f);
        miolo.rectTransform.offsetMax = new Vector2(-3f, -4f);

        Image vazio = VidaUI.NovaImagem(rect, "Tracejado", MolduraPuzzleUI.TintaEncaixe);
        vazio.sprite = tema != null ? tema.puzzleEncaixeVazio : null;
        VidaUI.Esticar(vazio.rectTransform);
        if (vazio.sprite == null) VidaUI.BordaTracejada(rect, tam, VidaUI.Tracejado);

        TextMeshProUGUI dica = VidaUI.NovoTexto(rect, "Dica", textoVazio, 20, MolduraPuzzleUI.Apagado, TextAlignmentOptions.Center, FontStyles.Italic);
        VidaUI.Esticar(dica.rectTransform, 12f);
        return new Encaixe { rect = rect, miolo = miolo, tracejado = vazio, dica = dica };
    }

    /// <summary>
    /// Janela Peças com os cartões um embaixo do outro; devolve as peças
    /// arrastáveis. Quando uma peça é encaixada ou volta, as que sobraram
    /// fecham o buraco e a coluna volta a ficar centralizada.
    /// </summary>
    public static List<AnimatorPuzzlePeca> JanelaPecas(RectTransform tela, IDonoPecaAnimator dono, AnimatorPuzzleUI.Peca[] pecas)
    {
        MolduraPuzzleUI.Janela(tela, "JanelaPecas", 1350, 80, 543, 743, "Peças");
        var go = new GameObject("Pecas", typeof(RectTransform));
        RectTransform bandeja = (RectTransform)go.transform;
        bandeja.SetParent(tela, false);
        MolduraPuzzleUI.TL(bandeja, 1350, 80, 543, 743);

        var lista = new List<AnimatorPuzzlePeca>();
        int qtd = pecas != null ? Mathf.Min(pecas.Length, 7) : 0;
        for (int i = 0; i < qtd; i++)
        {
            AnimatorPuzzleUI.Peca dados = pecas[i];
            MolduraPuzzleUI.Cartao c = MolduraPuzzleUI.NovoCartao(bandeja, "Peca_" + dados.texto, dados.moldura, dados.icone,
                AnimatorPuzzleUI.TextoSeguro(dados.texto), dados.subtitulo, dados.categoria, dados.corCategoria);
            VidaUI.Centro(c.moldura.rectTransform, PosicaoNaColuna(i, qtd), MolduraPuzzleUI.TamanhoCartao);
            MolduraPuzzleUI.EstilizarCartao(c, false, null);

            var peca = c.moldura.gameObject.AddComponent<AnimatorPuzzlePeca>();
            peca.Iniciar(dono, dados, c.nome);
            peca.aoMudarDesenho = (encaixada, texto) =>
            {
                MolduraPuzzleUI.EstilizarCartao(c, encaixada, texto);
                ReorganizarColuna(lista);
            };
            lista.Add(peca);
        }
        return lista;
    }

    // Cartões de 62 px a cada 92 px, centrados na janela.
    private static Vector2 PosicaoNaColuna(int indice, int qtd)
    {
        return new Vector2(18.5f, 46f * (qtd - 1) - 92f * indice - 17.5f);
    }

    // As peças que não estão encaixadas sobem, na ordem de sempre, e a coluna recentraliza.
    private static void ReorganizarColuna(List<AnimatorPuzzlePeca> lista)
    {
        int livres = 0;
        foreach (AnimatorPuzzlePeca p in lista)
        {
            if (p != null && !p.Travada) livres++;
        }

        int j = 0;
        foreach (AnimatorPuzzlePeca p in lista)
        {
            if (p != null && !p.Travada) p.DefinirCasa(PosicaoNaColuna(j++, livres));
        }
    }

    /// <summary>Camada de cima de tudo, para onde vai a peça sendo arrastada.</summary>
    public static RectTransform CamadaArraste(RectTransform raiz)
    {
        var go = new GameObject("Arraste", typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(raiz, false);
        VidaUI.Esticar(r);
        return r;
    }

    /// <summary>
    /// Esconde a placa VIDA e a faixa de objetivo enquanto um painel está
    /// aberto, e devolve tudo no Mostrar (mesma regra do
    /// AnimatorPuzzleUI).
    /// </summary>
    public class HudEscondido
    {
        private readonly List<GameObject> placas = new List<GameObject>();
        private Canvas faixa;

        public void Esconder()
        {
            foreach (RectTransform rt in UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude))
            {
                if (rt.name == "PlacaVida")
                {
                    rt.gameObject.SetActive(false);
                    placas.Add(rt.gameObject);
                }
            }

            Canvas c = ObjetivoFase.Instance != null ? ObjetivoFase.Instance.GetComponentInParent<Canvas>() : null;
            if (c != null && c.rootCanvas.enabled)
            {
                faixa = c.rootCanvas;
                faixa.enabled = false;
            }
        }

        public void Mostrar()
        {
            foreach (GameObject go in placas)
            {
                if (go != null) go.SetActive(true);
            }
            placas.Clear();
            if (faixa != null) faixa.enabled = true;
            faixa = null;
        }
    }
}
