using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Desenha em UGUI o diagrama do Animator do Player (mesmo desenho do mockup
/// aprovado): caixas Idle, Corrida, Pulo e Any State, e as 4 setas de
/// transição. Usado em dois tamanhos: grande no puzzle (AnimatorPuzzleUI,
/// com os encaixes em cima das setas) e pequeno no painel de canto
/// (AnimatorEstadoHUD), que acende o estado ativo em tempo real.
///
/// Classe comum (não é componente): só monta e guarda as referências das
/// Images pra acender/apagar depois. As coordenadas abaixo são da
/// resolução de referência (1920x1080) com escala 1; o painel de canto
/// passa uma escala menor.
///
/// Com <c>comAgachar</c>, ganha uma linha em cima com Agachado e
/// AndarAgachado e mais 2 setas (Idle -> Agachado e Agachado ->
/// AndarAgachado): é o diagrama do mini-puzzle do túnel e do painel de canto
/// depois dele.
/// </summary>
public class AnimatorDiagramaUI
{
    /// <summary>Índices das setas (e dos encaixes do puzzle, que ficam em cima delas).</summary>
    public const int SetaIdleCorrida = 0;
    public const int SetaCorridaIdle = 1;
    public const int SetaAnyPulo = 2;
    public const int SetaPuloIdle = 3;
    public const int TotalSetas = 4;
    public const int SetaIdleAgachado = 4;
    public const int SetaAgachadoAndar = 5;
    public const int TotalSetasComAgachar = 6;

    /// <summary>Altura extra da linha de cima (Agachado / AndarAgachado), com escala 1.</summary>
    public const float AlturaLinhaAgachar = 270f;

    public const string EstadoAny = "Any State";

    /// <summary>Tamanho da área que o diagrama ocupa com escala 1.</summary>
    public static readonly Vector2 TamanhoBase = new Vector2(1040f, 520f);

    /// <summary>Tamanho dos encaixes do puzzle (escala 1).</summary>
    public static readonly Vector2 TamanhoEncaixe = new Vector2(310f, 68f);

    private static readonly Vector2 TamanhoCaixa = new Vector2(230f, 100f);

    // AndarAgachado é o nome mais comprido: a caixa cresce pra direita (a seta
    // continua chegando na borda esquerda), senão o texto encolhe demais no
    // painel de canto.
    private const float LarguraExtraAndarAgachado = 60f;

    private static readonly Dictionary<string, Vector2> PosicaoCaixa = new Dictionary<string, Vector2>
    {
        { AnimacaoPlayerFisica.EstadoIdle, new Vector2(-370f, 150f) },
        { AnimacaoPlayerFisica.EstadoCorrida, new Vector2(370f, 150f) },
        { AnimacaoPlayerFisica.EstadoPulo, new Vector2(-370f, -170f) },
        { EstadoAny, new Vector2(370f, -170f) },
        { AnimacaoPlayerFisica.EstadoAgachado, new Vector2(-370f, 420f) },
        { AnimacaoPlayerFisica.EstadoAndarAgachado, new Vector2(370f, 420f) },
    };

    // Início e fim de cada seta (fim = ponta da flecha).
    private static readonly Vector2[,] Setas =
    {
        { new Vector2(-255f, 190f), new Vector2(255f, 190f) },   // Idle -> Corrida
        { new Vector2(255f, 110f), new Vector2(-255f, 110f) },   // Corrida -> Idle
        { new Vector2(255f, -170f), new Vector2(-255f, -170f) }, // Any State -> Pulo
        { new Vector2(-370f, -120f), new Vector2(-370f, 100f) }, // Pulo -> Idle
        { new Vector2(-370f, 200f), new Vector2(-370f, 370f) },  // Idle -> Agachado
        { new Vector2(-255f, 420f), new Vector2(255f, 420f) },   // Agachado -> AndarAgachado
    };

    private static readonly Vector2[] CentrosEncaixe =
    {
        new Vector2(0f, 190f),
        new Vector2(0f, 110f),
        new Vector2(0f, -170f),
        new Vector2(-370f, -10f),
        new Vector2(-370f, 285f),
        new Vector2(0f, 420f),
    };

    public static readonly Color CorAny = new Color32(0x1E, 0x5A, 0x66, 0xFF);
    public static readonly Color CorIdle = new Color32(0x6B, 0x3F, 0x14, 0xFF); // laranja escuro: estado padrão, como no Unity
    public static readonly Color CorEstado = new Color32(0x22, 0x2C, 0x42, 0xFF);
    public static readonly Color CorSeta = new Color32(0x56, 0x65, 0x7F, 0xFF);

    private static Sprite spriteTriangulo;

    public RectTransform Raiz { get; private set; }

    private readonly float escala;
    private readonly bool comAgachar;
    // Com a linha do agachar, tudo desce metade da altura extra pra continuar centrado.
    private Vector2 Desloc => comAgachar ? new Vector2(0f, -AlturaLinhaAgachar * 0.5f) : Vector2.zero;
    public int QtdSetas => comAgachar ? TotalSetasComAgachar : TotalSetas;
    private readonly Dictionary<string, Image> bordas = new Dictionary<string, Image>();
    private readonly Dictionary<string, Image> fundos = new Dictionary<string, Image>();
    private readonly Dictionary<string, Color> fundoNormal = new Dictionary<string, Color>();
    private readonly List<Image>[] partesSeta = new List<Image>[TotalSetasComAgachar];

    private AnimatorDiagramaUI(float escala, bool comAgachar)
    {
        this.escala = escala;
        this.comAgachar = comAgachar;
    }

    /// <summary>Tamanho do diagrama com escala 1 (mais alto com a linha do agachar).</summary>
    public static Vector2 Tamanho(bool comAgachar)
    {
        return comAgachar ? TamanhoBase + new Vector2(0f, AlturaLinhaAgachar) : TamanhoBase;
    }

    /// <summary>Monta o diagrama dentro de <paramref name="pai"/>, centrado em <paramref name="posicao"/>.</summary>
    public static AnimatorDiagramaUI Criar(RectTransform pai, Vector2 posicao, float escala, bool mostrarLegendas, bool comAgachar = false)
    {
        var d = new AnimatorDiagramaUI(escala, comAgachar);

        var go = new GameObject("DiagramaAnimator", typeof(RectTransform));
        d.Raiz = (RectTransform)go.transform;
        d.Raiz.SetParent(pai, false);
        VidaUI.Centro(d.Raiz, posicao, Tamanho(comAgachar) * escala);

        // Setas primeiro: ficam por baixo das caixas e dos encaixes.
        for (int i = 0; i < d.QtdSetas; i++)
        {
            d.partesSeta[i] = d.CriarSeta(Setas[i, 0], Setas[i, 1]);
        }

        // Com a linha do agachar, a seta Idle -> Agachado sai de cima do Idle,
        // onde ficava a legenda "estado inicial".
        d.CriarCaixa(AnimacaoPlayerFisica.EstadoIdle, CorIdle, mostrarLegendas && !comAgachar ? "estado inicial" : null);
        d.CriarCaixa(AnimacaoPlayerFisica.EstadoCorrida, CorEstado, null);
        d.CriarCaixa(AnimacaoPlayerFisica.EstadoPulo, CorEstado, null);
        d.CriarCaixa(EstadoAny, CorAny, mostrarLegendas ? "vale pra qualquer estado" : null);
        if (comAgachar)
        {
            d.CriarCaixa(AnimacaoPlayerFisica.EstadoAgachado, CorEstado, null);
            d.CriarCaixa(AnimacaoPlayerFisica.EstadoAndarAgachado, CorEstado, null);
        }

        return d;
    }

    /// <summary>Centro do encaixe que fica em cima da seta <paramref name="seta"/>, em coordenadas locais da Raiz.</summary>
    public Vector2 CentroEncaixe(int seta)
    {
        return (CentrosEncaixe[seta] + Desloc) * escala;
    }

    /// <summary>Acende a caixa do estado ativo e apaga as outras (Any State nunca acende: não é um estado de verdade).</summary>
    public void DestacarEstado(string estado)
    {
        foreach (KeyValuePair<string, Image> par in bordas)
        {
            bool ativo = par.Key == estado;
            par.Value.color = ativo ? VidaUI.Amarelo : VidaUI.Moldura;
            fundos[par.Key].color = ativo ? Color.Lerp(fundoNormal[par.Key], VidaUI.Amarelo, 0.28f) : fundoNormal[par.Key];
        }
    }

    public void DestacarSeta(int seta, bool acesa)
    {
        if (seta < 0 || seta >= QtdSetas)
        {
            return;
        }

        foreach (Image parte in partesSeta[seta])
        {
            parte.color = acesa ? VidaUI.Amarelo : CorSeta;
        }
    }

    /// <summary>Qual seta leva de <paramref name="de"/> pra <paramref name="para"/> (-1 se nenhuma).</summary>
    public static int SetaEntre(string de, string para)
    {
        if (para == AnimacaoPlayerFisica.EstadoPulo && de != AnimacaoPlayerFisica.EstadoPulo) return SetaAnyPulo;
        if (de == AnimacaoPlayerFisica.EstadoIdle && para == AnimacaoPlayerFisica.EstadoCorrida) return SetaIdleCorrida;
        if (de == AnimacaoPlayerFisica.EstadoCorrida && para == AnimacaoPlayerFisica.EstadoIdle) return SetaCorridaIdle;
        if (de == AnimacaoPlayerFisica.EstadoPulo && para == AnimacaoPlayerFisica.EstadoIdle) return SetaPuloIdle;
        if (de == AnimacaoPlayerFisica.EstadoIdle && para == AnimacaoPlayerFisica.EstadoAgachado) return SetaIdleAgachado;
        if (de == AnimacaoPlayerFisica.EstadoAgachado && para == AnimacaoPlayerFisica.EstadoAndarAgachado) return SetaAgachadoAndar;
        return -1;
    }

    // ---------------------------------------------------------------- montagem

    private void CriarCaixa(string estado, Color corFundo, string legenda)
    {
        Vector2 pos = (PosicaoCaixa[estado] + Desloc) * escala;
        Vector2 tam = TamanhoCaixa * escala;
        if (estado == AnimacaoPlayerFisica.EstadoAndarAgachado)
        {
            tam.x += LarguraExtraAndarAgachado * escala;
            pos.x += LarguraExtraAndarAgachado * 0.5f * escala;
        }
        float espessura = Mathf.Max(2f, 4f * escala);

        Image borda = VidaUI.NovaImagem(Raiz, "Estado_" + estado, VidaUI.Moldura);
        VidaUI.Centro(borda.rectTransform, pos, tam);
        Image fundo = VidaUI.NovaImagem(borda.transform, "Fundo", corFundo);
        VidaUI.Esticar(fundo.rectTransform, espessura);

        TextMeshProUGUI nome = VidaUI.NovoTexto(fundo.transform, "Nome", estado, 38f * escala, VidaUI.Texto, TextAlignmentOptions.Center, FontStyles.Bold);
        nome.enableAutoSizing = true;
        nome.fontSizeMin = 8f;
        nome.fontSizeMax = 38f * escala;
        VidaUI.Esticar(nome.rectTransform, 6f * escala);

        if (!string.IsNullOrEmpty(legenda))
        {
            TextMeshProUGUI leg = VidaUI.NovoTexto(Raiz, "Legenda_" + estado, legenda, 20f * escala, VidaUI.TextoApagado, TextAlignmentOptions.Center, FontStyles.Italic);
            // Idle: legenda em cima (embaixo passa a seta Pulo -> Idle).
            float lado = estado == AnimacaoPlayerFisica.EstadoIdle ? 1f : -1f;
            VidaUI.Centro(leg.rectTransform, pos + new Vector2(0f, lado * (TamanhoCaixa.y * 0.5f + 20f) * escala), new Vector2(TamanhoCaixa.x + 80f, 32f) * escala);
        }

        bordas[estado] = borda;
        fundos[estado] = fundo;
        fundoNormal[estado] = corFundo;
    }

    /// <summary>
    /// Caixa de estado (borda + fundo + nome), no mesmo desenho das caixas do
    /// diagrama do Player. Devolve a borda; o fundo é o primeiro filho dela.
    /// </summary>
    public static Image NovaCaixaEstado(RectTransform pai, string estado, Vector2 pos, Vector2 tam, Color corFundo, float escala = 1f)
    {
        float espessura = Mathf.Max(2f, 4f * escala);
        Image borda = VidaUI.NovaImagem(pai, "Estado_" + estado, VidaUI.Moldura);
        VidaUI.Centro(borda.rectTransform, pos, tam);
        Image fundo = VidaUI.NovaImagem(borda.transform, "Fundo", corFundo);
        VidaUI.Esticar(fundo.rectTransform, espessura);

        TextMeshProUGUI nome = VidaUI.NovoTexto(fundo.transform, "Nome", estado, 38f * escala, VidaUI.Texto, TextAlignmentOptions.Center, FontStyles.Bold);
        nome.enableAutoSizing = true;
        nome.fontSizeMin = 8f;
        nome.fontSizeMax = 38f * escala;
        VidaUI.Esticar(nome.rectTransform, 6f * escala);
        return borda;
    }

    private List<Image> CriarSeta(Vector2 inicio, Vector2 fim)
    {
        return NovaSeta(Raiz, (inicio + Desloc) * escala, (fim + Desloc) * escala, escala);
    }

    /// <summary>
    /// Seta de transição (linha + ponta) de <paramref name="a"/> até
    /// <paramref name="b"/>, em coordenadas locais de <paramref name="pai"/>.
    /// Pública pra outros diagramas (ex.: o da lâmpada) usarem o mesmo desenho.
    /// </summary>
    public static List<Image> NovaSeta(RectTransform pai, Vector2 a, Vector2 b, float escala = 1f)
    {
        var partes = new List<Image>();
        Vector2 dir = (b - a).normalized;
        float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float espessura = Mathf.Max(2f, 5f * escala);
        float tamPonta = Mathf.Max(10f, 26f * escala);

        // Linha: vai até a base da ponta.
        Vector2 baseDaPonta = b - dir * tamPonta * 0.8f;
        Image linha = VidaUI.NovaImagem(pai, "Seta", CorSeta);
        RectTransform lrt = linha.rectTransform;
        VidaUI.Centro(lrt, (a + baseDaPonta) * 0.5f, new Vector2(Vector2.Distance(a, baseDaPonta), espessura));
        lrt.localRotation = Quaternion.Euler(0f, 0f, angulo);
        partes.Add(linha);

        // Ponta: triângulo gerado em runtime (UGUI não tem forma pronta).
        Image ponta = VidaUI.NovaImagem(pai, "Ponta", CorSeta);
        ponta.sprite = SpriteTriangulo();
        RectTransform prt = ponta.rectTransform;
        VidaUI.Centro(prt, b - dir * tamPonta * 0.5f, new Vector2(tamPonta, tamPonta));
        prt.localRotation = Quaternion.Euler(0f, 0f, angulo);
        partes.Add(ponta);

        return partes;
    }

    // Triângulo branco apontando pra direita (+X); a cor vem do Image.
    private static Sprite SpriteTriangulo()
    {
        if (spriteTriangulo != null)
        {
            return spriteTriangulo;
        }

        const int tam = 64;
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[tam * tam];
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                // Largura da ponta diminui linearmente de x=0 (base) a x=tam (ponta).
                float meiaAltura = (1f - (x + 0.5f) / tam) * (tam * 0.5f);
                float dy = Mathf.Abs(y + 0.5f - tam * 0.5f);
                pixels[y * tam + x] = dy <= meiaAltura ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        spriteTriangulo = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 100f);
        return spriteTriangulo;
    }
}
