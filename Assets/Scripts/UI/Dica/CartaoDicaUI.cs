using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Versão UGUI do <see cref="CartaoDica"/>, para os puzzles montados em
/// runtime (Animator do Player, objetos animados, mini-lição de HUD): o
/// mesmo cartão de dica, com os mesmos modos e cores, desenhado com Images.
/// Criado pelo MolduraPuzzleUI.Dica.
///
/// Coordenadas: pixels da tela de referência 1920x1080, a partir do canto
/// de cima à esquerda do cartão.
/// </summary>
public class CartaoDicaUI : MonoBehaviour
{
    private Image brilho, borda, circulo, anel, icone, chipFundo, chipBorda;
    private TextMeshProUGUI rotulo, chip, texto, rodape;
    private Sprite iconeDica;

    private CartaoDica.Modo modo = CartaoDica.Modo.Dica;
    private string ultimaDica;
    private float pulso = -1f;

    private const float SegundosPulso = 1.1f;
    private const int Pulsos = 2;
    private const float XTexto = 186f;

    public TextMeshProUGUI Rotulo => rotulo;
    public TextMeshProUGUI Texto => texto;
    public TextMeshProUGUI Rodape => rodape;

    public static CartaoDicaUI Criar(RectTransform tela, float x, float y, float w, float h, string textoRodape)
    {
        TemaLaboratorio t = TemaLaboratorio.Atual;
        var go = new GameObject("CartaoDica", typeof(RectTransform));
        var raiz = (RectTransform)go.transform;
        raiz.SetParent(tela, false);
        MolduraPuzzleUI.TL(raiz, x, y, w, h);
        CartaoDicaUI c = go.AddComponent<CartaoDicaUI>();

        // O brilho tem 32 px de sobra transparente em volta do contorno.
        c.brilho = Fatia(raiz, "Brilho", t != null ? t.dicaBrilho : null, -32f, -32f, w + 64f, h + 64f, 1f);
        Fatia(raiz, "Fundo", t != null ? t.dicaFundo : null, 0f, 0f, w, h, 1.4f).color = CartaoDica.Fundo;
        c.borda = Fatia(raiz, "Borda", t != null ? t.dicaBorda : null, 0f, 0f, w, h, 1.4f);

        float lado = 116f;
        float topoCirculo = (h - lado) / 2f;
        c.anel = Fatia(raiz, "Anel", t != null ? t.dicaAnel : null, 34f - 8f, topoCirculo - 8f, lado + 16f, lado + 16f, 1f);
        c.anel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        c.anel.rectTransform.anchoredPosition = new Vector2(34f + lado / 2f, -(topoCirculo + lado / 2f));
        c.circulo = Fatia(raiz, "Circulo", t != null ? t.dicaCirculo : null, 34f, topoCirculo, lado, lado, 1f);
        c.iconeDica = t != null ? t.puzzleLampada : null;
        c.icone = Fatia(raiz, "Icone", c.iconeDica, 34f + 24f, topoCirculo + 24f, lado - 48f, lado - 48f, 1f);
        c.icone.preserveAspect = true;

        c.rotulo = MolduraPuzzleUI.Txt(raiz, "Rotulo", "DICA", XTexto, 40f, 30f, t != null && t.display != null ? t.display : MolduraPuzzleUI.CorpoForte, CartaoDica.Ambar);
        c.rotulo.characterSpacing = 8f;

        c.chipFundo = Fatia(raiz, "ChipFundo", t != null ? t.dicaFundo : null, 0f, 22f, 100f, 38f, 3f);
        c.chipBorda = Fatia(raiz, "ChipBorda", t != null ? t.dicaBorda : null, 0f, 22f, 100f, 38f, 3f);
        c.chip = MolduraPuzzleUI.Txt(raiz, "Chip", "", 0f, 41f, 22f, MolduraPuzzleUI.MonoForte, CartaoDica.Ambar);

        c.texto = MolduraPuzzleUI.Txt(raiz, "Texto", "", XTexto, 0f, 34f, MolduraPuzzleUI.CorpoForte, Color.white);
        c.texto.textWrappingMode = TextWrappingModes.Normal;
        c.texto.alignment = TextAlignmentOptions.TopLeft;
        c.texto.enableAutoSizing = true;
        c.texto.fontSizeMin = 22f;
        c.texto.fontSizeMax = 34f;
        RectTransform rt = c.texto.rectTransform;
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w - XTexto - 40f, h - 104f);
        rt.anchoredPosition = new Vector2(XTexto, -66f);

        c.rodape = MolduraPuzzleUI.Txt(raiz, "Rodape", textoRodape, XTexto, h - 24f, 18f, MolduraPuzzleUI.Corpo, CartaoDica.Apagado);
        c.Mostrar(CartaoDica.Modo.Dica, "", "");
        return c;
    }

    private static Image Fatia(RectTransform pai, string nome, Sprite s, float x, float y, float w, float h, float multiplicador)
    {
        Image img = MolduraPuzzleUI.Img(pai, nome, s, Color.white, true);
        img.pixelsPerUnitMultiplier = multiplicador;
        img.raycastTarget = false;
        MolduraPuzzleUI.TL(img.rectTransform, x, y, w, h);
        return img;
    }

    /// <summary>Dica com o lugar no formato "Lugar|Texto" (ver CartaoDica.Separar).</summary>
    public void MostrarDica(string bruto, string localSeVazio = "")
    {
        CartaoDica.Separar(bruto, out string local, out string corpo);
        Mostrar(CartaoDica.Modo.Dica, string.IsNullOrEmpty(local) ? localSeVazio : local, corpo);
    }

    public void Mostrar(CartaoDica.Modo m, string local, string corpo)
    {
        bool dicaNova = m == CartaoDica.Modo.Dica && !string.IsNullOrEmpty(corpo) && corpo != ultimaDica;
        if (m == CartaoDica.Modo.Dica) ultimaDica = corpo;
        modo = m;

        Color c = CartaoDica.CorDe(m);
        rotulo.text = CartaoDica.RotuloDe(m);
        rotulo.color = c;

        bool temChip = !string.IsNullOrEmpty(local);
        chip.text = "<noparse>" + (local ?? "") + "</noparse>";
        chip.color = c;
        chip.gameObject.SetActive(temChip);
        chipFundo.gameObject.SetActive(temChip);
        chipBorda.gameObject.SetActive(temChip);
        chipFundo.color = new Color(c.r, c.g, c.b, 0.16f);
        chipBorda.color = c;

        texto.text = CartaoDica.SemPalavraSozinha(corpo);
        rodape.gameObject.SetActive(m == CartaoDica.Modo.Dica && !string.IsNullOrEmpty(rodape.text));
        borda.color = c;

        bool peca = m == CartaoDica.Modo.Peca;
        circulo.color = peca ? CartaoDica.FundoCirculoPeca : c;
        icone.sprite = m == CartaoDica.Modo.Erro && PiscaErro.Xis != null ? PiscaErro.Xis : iconeDica;
        icone.color = peca ? CartaoDica.Azul : CartaoDica.IconeEscuro;
        icone.enabled = icone.sprite != null;

        pulso = dicaNova ? 0f : -1f;
        PintarAnel(peca ? 1f : 0f, 1f, c);
        PintarBrilho();
        ArrumarChip();
    }

    private void Update()
    {
        if (pulso >= 0f)
        {
            pulso += Time.unscaledDeltaTime;
            if (pulso >= SegundosPulso * Pulsos)
            {
                pulso = -1f;
                PintarAnel(0f, 1f, CartaoDica.Ambar);
            }
            else
            {
                float t = (pulso % SegundosPulso) / SegundosPulso;
                PintarAnel(1f - t, Mathf.Lerp(1f, 1.4f, t), CartaoDica.Ambar);
            }
        }

        PintarBrilho();
    }

    private void PintarAnel(float alfa, float escala, Color c)
    {
        anel.color = new Color(c.r, c.g, c.b, alfa);
        anel.rectTransform.localScale = Vector3.one * escala;
    }

    private void PintarBrilho()
    {
        Color c = CartaoDica.CorDe(modo);
        float a;
        if (modo == CartaoDica.Modo.Peca) a = 0f;
        else if (modo == CartaoDica.Modo.Dica) a = 0.32f + 0.12f * Mathf.Sin(Time.unscaledTime * 2.4f) + (pulso >= 0f ? 0.35f : 0f);
        else a = 0.35f;
        brilho.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
    }

    private void ArrumarChip()
    {
        if (!chip.gameObject.activeSelf) return;

        float larguraRotulo = rotulo.GetPreferredValues(rotulo.text).x;
        float larguraChip = chip.GetPreferredValues(chip.text).x;
        float inicio = XTexto + larguraRotulo + 18f;
        float largura = larguraChip + 28f;

        chip.rectTransform.anchoredPosition = new Vector2(inicio + 14f, chip.rectTransform.anchoredPosition.y);
        foreach (Image img in new[] { chipFundo, chipBorda })
        {
            RectTransform r = img.rectTransform;
            r.anchoredPosition = new Vector2(inicio, r.anchoredPosition.y);
            r.sizeDelta = new Vector2(largura, r.sizeDelta.y);
        }
    }
}
