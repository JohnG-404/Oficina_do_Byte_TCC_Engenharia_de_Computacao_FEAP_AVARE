using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Moldura dos puzzles no padrão da Física e da IA, em UGUI:
/// fundo de grade com os eixos nos cantos, cabeçalho
/// "Fase X  Puzzle N de M · Nome" com as etapas à direita, janelas com aba,
/// cartões de peça (ícone + nome + categoria) e o painel Dica com a lâmpada.
/// Os puzzles da Física e da IA são SpriteRenderers salvos na cena
/// (FisicaCodigoBuilder / IAPuzzleLayoutBuilder); esta classe desenha a mesma
/// coisa para os puzzles montados em runtime (AnimatorPuzzleUI), com os
/// sprites do kit guardados no TemaLaboratorio.
///
/// Coordenadas: pixels da tela de referência 1920x1080, a partir do canto
/// de cima à esquerda (<see cref="Tela"/> cria esse quadro no centro do
/// Canvas, então funciona em qualquer proporção).
/// </summary>
public static class MolduraPuzzleUI
{
    public static readonly Color FundoTela = new Color32(19, 26, 38, 255);
    public static readonly Color TituloAzul = new Color32(0x9F, 0xC2, 0xFF, 0xFF);

    private const float AlfaGrade = 0.15f;
    public static readonly Color TextoClaro = new Color32(0xDC, 0xE6, 0xF5, 0xFF);
    public static readonly Color Apagado = new Color32(0x6F, 0x7D, 0x95, 0xFF);
    public static readonly Color Rotulo = new Color32(0x9A, 0xA6, 0xBA, 0xFF);
    public static readonly Color Escuro = new Color32(0x4B, 0x5A, 0x75, 0xFF);
    public static readonly Color Dourado = new Color32(0xF5, 0xC5, 0x42, 0xFF);
    public static readonly Color VerdeOk = new Color32(0x7E, 0xE2, 0xA8, 0xFF);
    public static readonly Color CaixaApagada = new Color(0.17f, 0.22f, 0.32f, 1f);
    public static readonly Color FundoEncaixe = new Color32(0x0B, 0x13, 0x22, 0xFF);
    public static readonly Color TintaEncaixe = new Color(0.75f, 0.8f, 0.9f, 0.9f);

    private static TemaLaboratorio T => TemaLaboratorio.Atual;

    public static TMP_FontAsset Corpo => T != null ? T.corpo : null;
    public static TMP_FontAsset CorpoForte => T != null && T.corpoForte != null ? T.corpoForte : Corpo;
    public static TMP_FontAsset Mono => T != null ? T.mono : null;
    public static TMP_FontAsset MonoForte => T != null && T.monoForte != null ? T.monoForte : Mono;

    // ------------------------------------------------------------ básicos

    /// <summary>Fundo opaco da tela inteira (bloqueia cliques no cenário) e o quadro 1920x1080 onde vai o resto.</summary>
    public static RectTransform Tela(RectTransform raizCanvas)
    {
        Image cor = VidaUI.NovaImagem(raizCanvas, "Fundo", FundoTela, raycast: true);
        VidaUI.Esticar(cor.rectTransform);
        if (T != null && T.puzzleGradeFundo != null)
        {
            // Mesma força das linhas do fundo da Física e da IA (sprite de mundo, mais apagado).
            Image grade = Img(raizCanvas, "Grade", T.puzzleGradeFundo, new Color(1f, 1f, 1f, AlfaGrade), false);
            VidaUI.Esticar(grade.rectTransform);
        }

        var go = new GameObject("Tela", typeof(RectTransform));
        var tela = (RectTransform)go.transform;
        tela.SetParent(raizCanvas, false);
        VidaUI.Centro(tela, Vector2.zero, new Vector2(1920f, 1080f));

        // Eixos nos cantos da esquerda, como no fundo da Física. O de cima é
        // espelhado na vertical (não girado), para o X vermelho continuar
        // apontando para a direita, como no Unity.
        if (T != null && T.puzzleGizmoEixos != null)
        {
            TL(Img(tela, "EixosBaixo", T.puzzleGizmoEixos, Color.white, false).rectTransform, 14, 960, 120, 120);
            RectTransform cima = TL(Img(tela, "EixosCima", T.puzzleGizmoEixos, Color.white, false).rectTransform, 14, 26, 120, 120);
            cima.pivot = new Vector2(0.5f, 0.5f);
            cima.anchoredPosition = new Vector2(74f, -86f);
            cima.localScale = new Vector3(1f, -1f, 1f);
        }
        return tela;
    }

    /// <summary>"Fase X  Puzzle N de M · Nome" à esquerda e as etapas à direita (feitas em verde, a atual em branco).</summary>
    public static void Cabecalho(RectTransform tela, string fase, int numero, string[] etapas)
    {
        int total = etapas != null ? etapas.Length : 1;
        string nome = etapas != null && numero >= 1 && numero <= total ? etapas[numero - 1] : "";
        Txt(tela, "Titulo", $"<b>{fase}</b>  <color=#8A97AD>Puzzle {numero} de {total} · {nome}</color>", 155, 41, 28, Corpo, Color.white);

        var partes = new System.Text.StringBuilder();
        for (int i = 0; i < total; i++)
        {
            string n = $"{i + 1}. {etapas[i]}";
            if (i > 0) partes.Append("   ");
            if (i + 1 < numero) partes.Append($"<color=#7EE2A8>{n}</color>");
            else if (i + 1 == numero) partes.Append($"<color=#FFFFFF><b>{n}</b></color>");
            else partes.Append($"<color=#5D6B84>{n}</color>");
        }
        Txt(tela, "Etapas", partes.ToString(), 1888, 40, 20, Corpo, Color.white, true);
    }

    /// <summary>Janela com faixa de cabeçalho e aba com título.</summary>
    public static RectTransform Janela(RectTransform tela, string nome, float x, float y, float w, float h, string aba)
    {
        RectTransform r = TL(Img(tela, nome, T != null ? T.puzzleJanela : null, new Color32(0x1E, 0x25, 0x33, 0xFF), true).rectTransform, x, y, w, h);
        if (T != null && T.puzzleJanela != null) r.GetComponent<Image>().color = Color.white;
        TL(Img(tela, "Aba_" + aba, T != null ? T.puzzleAba : null, Color.white, true).rectTransform, x + 20, y, 150, 28);
        Txt(tela, "AbaTexto_" + aba, aba, x + 35, y + 15, 15, CorpoForte, Color.white);
        return r;
    }

    /// <summary>Área escura de dentro da janela (onde ficam o código, o diagrama ou a prévia).</summary>
    public static RectTransform Area(RectTransform tela, string nome, float x, float y, float w, float h)
    {
        Image img = Img(tela, nome, T != null ? T.puzzleArea : null, new Color(0.035f, 0.055f, 0.09f, 1f), true);
        if (T != null && T.puzzleArea != null) img.color = Color.white;
        return TL(img.rectTransform, x, y, w, h);
    }

    /// <summary>Legenda em caixa alta e espaçada (ex. "CÉREBRO DO GUARDA · ESTADOS").</summary>
    public static TextMeshProUGUI Legenda(RectTransform tela, string texto, float x, float y)
    {
        TextMeshProUGUI t = Txt(tela, "Legenda", texto, x, y, 16, CorpoForte, Apagado);
        t.characterSpacing = 6;
        return t;
    }

    public static Image Divisoria(RectTransform tela, float x, float y, float w)
    {
        Image d = Img(tela, "Divisoria", T != null ? T.puzzleDivisoria : null, Escuro, false);
        if (T != null && T.puzzleDivisoria != null) d.color = Color.white;
        TL(d.rectTransform, x, y, w, 3);
        return d;
    }

    // ------------------------------------------------------------ Dica

    public class PainelDica
    {
        public TextMeshProUGUI titulo;
        public TextMeshProUGUI texto;
        public TextMeshProUGUI rodape;
    }

    /// <summary>Painel "Dica" de baixo, com a lâmpada: título azul, texto e uma linha apagada de rodapé.</summary>
    public static PainelDica Dica(RectTransform tela, string rodape)
    {
        Janela(tela, "JanelaDica", 42, 868, 1801, 182, "Dica");
        if (T != null && T.puzzleLampada != null)
        {
            Image lamp = Img(tela, "Lampada", T.puzzleLampada, Color.white, false);
            lamp.preserveAspect = true;
            TL(lamp.rectTransform, 97, 929, 96, 96);
        }
        var p = new PainelDica
        {
            titulo = Txt(tela, "DicaTitulo", "Dica", 230, 922, 20, CorpoForte, TituloAzul),
            texto = Txt(tela, "DicaTexto", "", 230, 948, 18, Corpo, TextoClaro),
            rodape = Txt(tela, "DicaRodape", rodape, 230, 1018, 15, Corpo, Apagado),
        };
        // Texto da dica/descrição: até duas linhas.
        p.texto.textWrappingMode = TextWrappingModes.Normal;
        p.texto.alignment = TextAlignmentOptions.TopLeft;
        RectTransform rt = p.texto.rectTransform;
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(1560f, 56f);
        rt.anchoredPosition = new Vector2(230f, -952f);
        p.texto.enableAutoSizing = true;
        p.texto.fontSizeMin = 14;
        p.texto.fontSizeMax = 18;
        return p;
    }

    // ------------------------------------------------------------ peças

    /// <summary>Partes de um cartão de peça, pra trocar o desenho quando ela encaixa.</summary>
    public class Cartao
    {
        public Image moldura;
        public Sprite molduraBandeja;
        public RectTransform icone;
        public TextMeshProUGUI nome;
        public TextMeshProUGUI sub;
        public TextMeshProUGUI categoria;
    }

    public static readonly Vector2 TamanhoCartao = new Vector2(426f, 62f);

    /// <summary>
    /// Cartão da bandeja Peças (426x62): moldura da cor do tipo, ícone, nome
    /// em mono, uma linha menor embaixo e a etiqueta da categoria à direita.
    /// O pai deve posicionar o RectTransform da moldura (pivô no centro).
    /// </summary>
    public static Cartao NovoCartao(Transform pai, string nome, Sprite moldura, Sprite icone, string texto, string sub, string categoria, Color corCategoria)
    {
        var c = new Cartao();
        c.moldura = VidaUI.NovaImagem(pai, nome, moldura != null ? Color.white : corCategoria, raycast: true);
        c.moldura.sprite = moldura;
        c.moldura.type = Image.Type.Sliced;
        c.moldura.pixelsPerUnitMultiplier = 1.6f;
        c.molduraBandeja = moldura;

        Image ic = VidaUI.NovaImagem(c.moldura.transform, "Icone", Color.white);
        ic.sprite = icone;
        ic.preserveAspect = true;
        ic.enabled = icone != null;
        c.icone = ic.rectTransform;

        c.categoria = VidaUI.NovoTexto(c.moldura.transform, "Categoria", (categoria ?? "").ToUpperInvariant(), 11, corCategoria, TextAlignmentOptions.MidlineRight);
        c.categoria.font = CorpoForte;
        c.categoria.fontStyle = FontStyles.Normal;
        c.categoria.characterSpacing = 4;
        c.categoria.textWrappingMode = TextWrappingModes.NoWrap;

        c.nome = VidaUI.NovoTexto(c.moldura.transform, "Texto", texto, 20, Color.white, TextAlignmentOptions.MidlineLeft);
        c.nome.font = MonoForte;
        c.nome.fontStyle = FontStyles.Bold;
        c.nome.textWrappingMode = TextWrappingModes.NoWrap;

        c.sub = VidaUI.NovoTexto(c.moldura.transform, "Sub", "<noparse>" + (sub ?? "") + "</noparse>", 14, Rotulo, TextAlignmentOptions.MidlineLeft);
        c.sub.font = Mono;
        c.sub.fontStyle = FontStyles.Normal;
        c.sub.textWrappingMode = TextWrappingModes.NoWrap;

        EstilizarCartao(c, false, null);
        return c;
    }

    /// <summary>
    /// Desenho do cartão na bandeja ou encaixado (moldura neutra, ícone menor,
    /// nome dourado e sem a linha de baixo nem a etiqueta, como o Preenchido da Física).
    /// </summary>
    public static void EstilizarCartao(Cartao c, bool encaixado, string textoEncaixado)
    {
        RectTransform rIc = c.icone;
        rIc.anchorMin = rIc.anchorMax = new Vector2(0f, 0.5f);
        rIc.pivot = new Vector2(0.5f, 0.5f);

        RectTransform rNome = c.nome.rectTransform;
        if (!encaixado)
        {
            c.moldura.sprite = c.molduraBandeja;
            c.moldura.pixelsPerUnitMultiplier = 1.6f;
            rIc.sizeDelta = new Vector2(41f, 41f);
            rIc.anchoredPosition = new Vector2(42.5f, 0f);
            c.sub.gameObject.SetActive(c.sub.text != "<noparse></noparse>");
            c.categoria.gameObject.SetActive(true);

            RectTransform rCat = c.categoria.rectTransform;
            rCat.anchorMin = rCat.anchorMax = new Vector2(1f, 0.5f);
            rCat.pivot = new Vector2(1f, 0.5f);
            rCat.sizeDelta = new Vector2(140f, 20f);
            rCat.anchoredPosition = new Vector2(-17f, 0f);
            c.categoria.ForceMeshUpdate();
            float livre = TamanhoCartao.x - 83f - 17f - c.categoria.preferredWidth - 14f;

            bool duas = c.sub.gameObject.activeSelf;
            rNome.anchorMin = rNome.anchorMax = new Vector2(0f, 0.5f);
            rNome.pivot = new Vector2(0f, 0.5f);
            rNome.sizeDelta = new Vector2(livre, 28f);
            rNome.anchoredPosition = new Vector2(83f, duas ? 10f : 0f);
            c.nome.color = Color.white;
            c.nome.enableAutoSizing = true;
            c.nome.fontSizeMin = 11f;
            c.nome.fontSizeMax = 20f;

            RectTransform rSub = c.sub.rectTransform;
            rSub.anchorMin = rSub.anchorMax = new Vector2(0f, 0.5f);
            rSub.pivot = new Vector2(0f, 0.5f);
            rSub.sizeDelta = new Vector2(livre, 20f);
            rSub.anchoredPosition = new Vector2(83f, -14f);
            return;
        }

        if (T != null && T.puzzlePecaEncaixada != null) c.moldura.sprite = T.puzzlePecaEncaixada;
        c.moldura.pixelsPerUnitMultiplier = 2.2f;
        c.sub.gameObject.SetActive(false);
        c.categoria.gameObject.SetActive(false);

        float h = Mathf.Max(30f, ((RectTransform)c.moldura.transform).rect.height);
        float lado = h * 0.55f;
        rIc.sizeDelta = new Vector2(lado, lado);
        rIc.anchoredPosition = new Vector2(14f + lado * 0.5f, 0f);

        rNome.anchorMin = Vector2.zero;
        rNome.anchorMax = Vector2.one;
        rNome.pivot = new Vector2(0.5f, 0.5f);
        rNome.offsetMin = new Vector2(24f + lado, 2f);
        rNome.offsetMax = new Vector2(-8f, -2f);
        c.nome.color = Dourado;
        c.nome.enableAutoSizing = true;
        c.nome.fontSizeMin = 9f;
        c.nome.fontSizeMax = 16f;
        if (textoEncaixado != null) c.nome.text = textoEncaixado;
    }

    // ------------------------------------------------------------ utilitários

    /// <summary>Posiciona pelo canto de cima à esquerda, em pixels da tela de referência.</summary>
    public static RectTransform TL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    public static Image Img(Transform pai, string nome, Sprite s, Color cor, bool sliced)
    {
        Image img = VidaUI.NovaImagem(pai, nome, cor);
        img.sprite = s;
        if (s != null && sliced && s.border != Vector4.zero) img.type = Image.Type.Sliced;
        return img;
    }

    /// <summary>Texto de uma linha; (x, yCentro) é o começo da linha (ou o fim, com <paramref name="direita"/>).</summary>
    public static TextMeshProUGUI Txt(Transform pai, string nome, string texto, float x, float yCentro, float tam, TMP_FontAsset fonte, Color cor, bool direita = false)
    {
        TextMeshProUGUI t = VidaUI.NovoTexto(pai, nome, texto, tam, cor, direita ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
        if (fonte != null) t.font = fonte;
        t.fontStyle = FontStyles.Normal;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(direita ? 1f : 0f, 0.5f);
        rt.sizeDelta = new Vector2(1400f, tam * 1.8f);
        rt.anchoredPosition = new Vector2(x, -yCentro);
        return t;
    }
}
