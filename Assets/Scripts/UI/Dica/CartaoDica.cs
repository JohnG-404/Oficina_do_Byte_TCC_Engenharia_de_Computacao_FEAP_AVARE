using TMPro;
using UnityEngine;

/// <summary>
/// Cartão de dica dos puzzles (opção A aprovada pelo John em 29/10): fundo
/// escuro com borda e brilho na cor do modo, círculo com a lâmpada, rótulo
/// ("DICA"), etiqueta com o lugar da lacuna ("Linha 2 · espaço 1") e o texto
/// grande embaixo.
///
/// Modos:
/// - Dica (âmbar): a dica da lacuna da vez. Pulsa quando a dica muda.
/// - Peça (azul): o mouse está em cima de uma peça; mostra o que ela faz.
/// - Pronto (verde): tudo encaixado.
/// - Erro (vermelho): peça errada.
///
/// Os textos de dica podem trazer o lugar antes de uma barra:
/// "Linha 2 · espaço 1|Qual peça lê as teclas que você aperta?" (ver
/// <see cref="Separar"/>). Sem barra, o cartão mostra só o texto.
///
/// Esta é a versão em SpriteRenderer/TextMeshPro dos puzzles salvos na cena
/// (Física e IA), montada e salva pelo CartaoDicaBuilder
/// (Tools > Dicas > Aplicar cartão de dica); quem manda o conteúdo é o
/// InfoPanelUI. Os puzzles montados em runtime usam o CartaoDicaUI.
/// </summary>
public class CartaoDica : MonoBehaviour
{
    public enum Modo { Dica, Peca, Pronto, Erro }

    public static readonly Color Ambar = new Color32(0xF5, 0xC5, 0x42, 0xFF);
    public static readonly Color Azul = new Color32(0x3D, 0xA5, 0xFF, 0xFF);
    public static readonly Color Verde = new Color32(0x2B, 0xD1, 0x8A, 0xFF);
    public static readonly Color Vermelho = new Color32(0xEF, 0x5B, 0x5B, 0xFF);
    public static readonly Color Fundo = new Color32(0x1E, 0x26, 0x38, 0xFF);
    public static readonly Color FundoCirculoPeca = new Color32(0x12, 0x18, 0x26, 0xFF);
    public static readonly Color IconeEscuro = new Color32(0x1A, 0x20, 0x30, 0xFF);
    public static readonly Color Apagado = new Color32(0x8D, 0x99, 0xAE, 0xFF);

    public const string RodapePadrao = "Passe o mouse numa peça para ler o que ela faz.";

    /// <summary>
    /// Prende as duas últimas palavras com espaço que não quebra, para a
    /// segunda linha nunca ficar com uma palavra sozinha ("ele?", "ela.").
    /// </summary>
    public static string SemPalavraSozinha(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        int i = s.TrimEnd().LastIndexOf(' ');
        if (i <= 0) return s;
        // Espaço dentro de uma tag (<font="a b">) fica como está.
        int abre = s.LastIndexOf('<', i);
        if (abre >= 0 && s.LastIndexOf('>', i) < abre) return s;
        return s.Substring(0, i) + '\u00A0' + s.Substring(i + 1);
    }

    public static Color CorDe(Modo m)
    {
        switch (m)
        {
            case Modo.Peca: return Azul;
            case Modo.Pronto: return Verde;
            case Modo.Erro: return Vermelho;
            default: return Ambar;
        }
    }

    public static string RotuloDe(Modo m)
    {
        switch (m)
        {
            case Modo.Peca: return "PEÇA";
            case Modo.Pronto: return "PRONTO";
            case Modo.Erro: return "OPA!";
            default: return "DICA";
        }
    }

    /// <summary>"Lugar|Texto" vira lugar e texto; sem barra, o lugar fica vazio.</summary>
    public static void Separar(string bruto, out string local, out string texto)
    {
        bruto = bruto ?? "";
        int i = bruto.IndexOf('|');
        if (i < 0)
        {
            local = "";
            texto = bruto.Trim();
            return;
        }

        local = bruto.Substring(0, i).Trim();
        texto = bruto.Substring(i + 1).Trim();
    }

    /// <summary>Só o texto, sem o lugar (para quem mostra a dica fora do cartão).</summary>
    public static string SemLocal(string bruto)
    {
        Separar(bruto, out _, out string texto);
        return texto;
    }

    [Header("Partes (montadas pelo CartaoDicaBuilder)")]
    [SerializeField] private SpriteRenderer brilho;
    [SerializeField] private SpriteRenderer borda;
    [SerializeField] private SpriteRenderer circulo;
    [SerializeField] private SpriteRenderer anel;
    [SerializeField] private SpriteRenderer icone;
    [SerializeField] private SpriteRenderer chipFundo;
    [SerializeField] private SpriteRenderer chipBorda;
    [SerializeField] private TMP_Text rotulo;
    [SerializeField] private TMP_Text chip;
    [SerializeField] private TMP_Text texto;
    [SerializeField] private TMP_Text rodape;
    [SerializeField] private Sprite iconeDica;
    [SerializeField] private Sprite iconeErro;

    [Header("Medidas (unidades de mundo)")]
    [SerializeField] private float folgaChip = 0.22f;
    [SerializeField] private float margemChip = 0.14f;
    [SerializeField] private float alturaChip = 0.36f;
    [SerializeField] private float escalaAnel = 1.12f;
    [Tooltip("Distância entre a base da área do texto e o centro do rodapé.")]
    [SerializeField] private float folgaRodape = 0.16f;

    [Header("Pulso quando a dica muda")]
    [SerializeField] private float segundosPulso = 1.1f;
    [SerializeField] private int pulsos = 2;

    private Modo modo = Modo.Dica;
    private string ultimaDica;
    private float pulso = -1f;

    private void Awake()
    {
        AlinharRodape();
    }

    // O rodapé ("Passe o mouse numa peça...") fica logo abaixo da área do
    // texto, alinhado com ele à esquerda. Dois cartões (PuzzleCam na Física e
    // IA_Depuracao na IA) foram salvos com o rodapé no meio do cartão, em
    // cima da 2ª linha da dica; calculando aqui ele cai no lugar certo em
    // todos, mesmo se o CartaoDicaBuilder errar de novo.
    private void AlinharRodape()
    {
        if (texto == null || rodape == null || rodape.transform.parent != texto.transform.parent) return;
        RectTransform rt = texto.rectTransform;
        RectTransform rr = rodape.rectTransform;
        Vector3 p = texto.transform.localPosition;
        float esquerda = p.x - rt.rect.width * rt.pivot.x;
        float baseTexto = p.y - rt.rect.height * rt.pivot.y;
        float y = baseTexto - folgaRodape - rr.rect.height * (0.5f - rr.pivot.y);
        rodape.transform.localPosition = new Vector3(esquerda + rr.rect.width * rr.pivot.x, y, rodape.transform.localPosition.z);
    }

    public void Mostrar(Modo m, string local, string corpo, Sprite iconePeca = null)
    {
        bool dicaNova = m == Modo.Dica && corpo != ultimaDica;
        if (m == Modo.Dica) ultimaDica = corpo;
        modo = m;

        Color c = CorDe(m);
        if (rotulo != null)
        {
            rotulo.text = RotuloDe(m);
            rotulo.color = c;
        }

        bool temChip = !string.IsNullOrEmpty(local);
        if (chip != null)
        {
            chip.text = "<noparse>" + (local ?? "") + "</noparse>";
            chip.color = c;
            chip.gameObject.SetActive(temChip);
        }
        if (chipFundo != null)
        {
            chipFundo.gameObject.SetActive(temChip);
            chipFundo.color = new Color(c.r, c.g, c.b, 0.16f);
        }
        if (chipBorda != null)
        {
            chipBorda.gameObject.SetActive(temChip);
            chipBorda.color = c;
        }

        if (texto != null) texto.text = SemPalavraSozinha(corpo);
        if (rodape != null) rodape.gameObject.SetActive(m == Modo.Dica);
        if (borda != null) borda.color = c;

        bool peca = m == Modo.Peca;
        if (circulo != null) circulo.color = peca ? FundoCirculoPeca : c;
        if (icone != null)
        {
            Sprite s = peca ? (iconePeca != null ? iconePeca : iconeDica) : (m == Modo.Erro ? (iconeErro != null ? iconeErro : PiscaErro.Xis) : iconeDica);
            icone.sprite = s;
            icone.color = peca && iconePeca != null ? Color.white : (peca ? Azul : IconeEscuro);
            icone.enabled = s != null;
        }

        // No modo peça o anel fica parado em volta do círculo escuro; nos
        // outros ele só aparece no pulso.
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
            float total = segundosPulso * Mathf.Max(1, pulsos);
            if (pulso >= total)
            {
                pulso = -1f;
                PintarAnel(0f, 1f, Ambar);
            }
            else
            {
                float t = (pulso % segundosPulso) / segundosPulso;
                PintarAnel(1f - t, Mathf.Lerp(1f, 1.4f, t), Ambar);
            }
        }

        PintarBrilho();
    }

    private void PintarAnel(float alfa, float escala, Color c)
    {
        if (anel == null) return;
        anel.color = new Color(c.r, c.g, c.b, alfa);
        anel.transform.localScale = Vector3.one * escalaAnel * escala;
    }

    // Brilho em volta do cartão: respira devagar na dica, fica mais forte no
    // pulso e some no modo peça (que é só leitura).
    private void PintarBrilho()
    {
        if (brilho == null) return;
        Color c = CorDe(modo);
        float a;
        if (modo == Modo.Peca) a = 0f;
        else if (modo == Modo.Dica) a = 0.32f + 0.12f * Mathf.Sin(Time.unscaledTime * 2.4f) + (pulso >= 0f ? 0.35f : 0f);
        else a = 0.35f;
        brilho.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
    }

    // A etiqueta vai logo depois do rótulo ("DICA", "PEÇA"...), com a largura
    // do texto dela. Os textos têm pivô na esquerda (ver CartaoDicaBuilder).
    private void ArrumarChip()
    {
        if (rotulo == null || chip == null || !chip.gameObject.activeSelf) return;

        rotulo.ForceMeshUpdate();
        chip.ForceMeshUpdate();
        float larguraRotulo = rotulo.GetPreferredValues(rotulo.text).x * rotulo.transform.localScale.x;
        float larguraChip = chip.GetPreferredValues(chip.text).x * chip.transform.localScale.x;

        Vector3 r = rotulo.transform.localPosition;
        float inicio = r.x + larguraRotulo + folgaChip;
        chip.transform.localPosition = new Vector3(inicio + margemChip, r.y, chip.transform.localPosition.z);

        float largura = larguraChip + margemChip * 2f;
        foreach (SpriteRenderer sr in new[] { chipFundo, chipBorda })
        {
            if (sr == null) continue;
            // Desenhado no dobro do tamanho com escala 0,5: os cantos ficam
            // na metade do raio do cartão.
            sr.size = new Vector2(largura * 2f, alturaChip * 2f);
            sr.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            sr.transform.localPosition = new Vector3(inicio + largura / 2f, r.y, sr.transform.localPosition.z);
        }
    }
}
