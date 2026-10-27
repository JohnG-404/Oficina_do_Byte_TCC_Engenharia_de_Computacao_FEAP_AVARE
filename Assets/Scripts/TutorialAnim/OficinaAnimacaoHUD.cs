using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Parte visual da Oficina de Animação (cena Tutorial_Anim), no tema
/// Laboratório Tecno: barra de rodadas no topo (uma por animação, com check
/// quando concluída, sem estrelas), rótulos da linha do tempo, da bandeja e
/// do monitor de prévia, e a cor de cada encaixe (vazio, cheio, travado,
/// destacado durante o arrasto, certo ou errado).
///
/// Tudo em espaço de mundo (SpriteRenderer + TextMeshPro), igual às peças,
/// pra não brigar com o arrasto por OnMouse. Quem decide o que acontece é
/// o AnimationSequencePuzzle; este script só mostra.
///
/// A barra de rodadas é criada a partir da lista de rodadas do puzzle, então
/// acrescentar uma animação no Inspector já cria o botão dela.
/// </summary>
public class OficinaAnimacaoHUD : MonoBehaviour
{
    public enum EstadoEncaixe
    {
        Vazio,
        Cheio,
        Travado,
        Destaque,
        Certo,
        Errado
    }

    [Header("Rótulos")]
    [SerializeField] private TMP_Text textoContagem;
    [SerializeField] private TMP_Text textoTituloRodada;
    [SerializeField] private TMP_Text textoFrames;
    [SerializeField] private TMP_Text textoIscas;
    [SerializeField] private TMP_Text textoEstadoPrevia;
    [SerializeField] private TMP_Text textoFps;
    [SerializeField] private TMP_Text textoComoJogar;
    [Tooltip("Cor do título (a parte em <b>) do quadro \"como jogar\". Alfa 0 = deixa o texto como veio (azul da faixa com a lâmpada).")]
    [SerializeField] private Color corTituloComoJogar = new Color(0f, 0f, 0f, 0f);

    [Header("Encaixes (um por slot do puzzle, mesma ordem)")]
    [SerializeField] private SpriteRenderer[] fundosEncaixe;
    [SerializeField] private SpriteRenderer[] bordasEncaixe;
    [SerializeField] private GameObject[] marcasTravado;

    [Header("Barra de rodadas")]
    [Tooltip("Canto esquerdo (meio da altura) da barra; os botões das rodadas nascem daqui pra direita.")]
    [SerializeField] private Transform barraRodadas;
    [SerializeField] private Sprite spriteFundo;
    [SerializeField] private Sprite spriteBorda;
    [SerializeField] private Sprite spriteCheck;
    [SerializeField] private TMP_FontAsset fonteRodadas;
    [SerializeField] private float alturaChip = 0.5f;
    [SerializeField] private float folgaChip = 0.28f;
    [SerializeField] private float espacoChip = 0.12f;
    [SerializeField] private float tamanhoFonteChip = 2.2f;
    [SerializeField] private int ordemChip = 6;

    [Tooltip("Barra de progresso em segmentos (um por rodada, objetos da cena). Se preenchida, substitui os botões com nome de cada rodada, que não cabem com muitas rodadas.")]
    [SerializeField] private SpriteRenderer[] segmentosProgresso;

    [Header("Painel de ajuste (FPS/Loop)")]
    [SerializeField] private TMP_FontAsset fonteTituloAjuste;
    [SerializeField] private TMP_FontAsset fonteTextoAjuste;
    [SerializeField] private Sprite spriteArredondadoAjuste;

    [Header("Cores (tokens do tema)")]
    [SerializeField] private Color corTexto = Hex("#E8EEF9");
    [SerializeField] private Color corSuave = Hex("#9AA8C4");
    [SerializeField] private Color corSuperficie = Hex("#151C2C");
    [SerializeField] private Color corElevado = Hex("#243049");
    [SerializeField] private Color corLinha = Hex("#34425F");
    [SerializeField] private Color corAzul = Hex("#4A8CF7");
    [SerializeField] private Color corCiano = Hex("#5FD4E8");
    [SerializeField] private Color corRoxo = Hex("#9B7BF0");
    [SerializeField] private Color corVerde = Hex("#52C98A");
    [SerializeField] private Color corVermelho = Hex("#E5646A");
    [SerializeField] private Color corDestaque = Hex("#1D2A42");

    private sealed class Chip
    {
        public SpriteRenderer fundo;
        public SpriteRenderer borda;
        public SpriteRenderer check;
        public TMP_Text texto;
    }

    private readonly List<Chip> chips = new List<Chip>();
    private readonly HashSet<int> concluidas = new HashSet<int>();
    private int atual = -1;
    private int total;

    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }

    /// <summary>Monta a barra de rodadas a partir das rodadas do puzzle.</summary>
    public void Preparar(AnimationSequencePuzzle puzzle)
    {
        total = puzzle.Rounds.Count;

        AjusteAnimacaoUI.FonteTitulo = fonteTituloAjuste;
        AjusteAnimacaoUI.FonteTexto = fonteTextoAjuste;
        AjusteAnimacaoUI.SpriteArredondado = spriteArredondadoAjuste;

        if (segmentosProgresso != null && segmentosProgresso.Length > 0)
        {
            if (segmentosProgresso.Length < total)
            {
                Debug.LogWarning($"[OficinaAnimacaoHUD] {total} rodadas, mas só {segmentosProgresso.Length} segmentos na barra de progresso.");
            }
            PintarChips();
            return;
        }

        if (barraRodadas == null)
        {
            return;
        }

        foreach (Chip chip in chips)
        {
            Destroy(chip.fundo.gameObject);
        }
        chips.Clear();

        float x = 0f;
        for (int i = 0; i < puzzle.Rounds.Count; i++)
        {
            Chip chip = CriarChip(puzzle.Rounds[i].roundName, ref x);
            chips.Add(chip);
        }
        PintarChips();
    }

    private Chip CriarChip(string nome, ref float x)
    {
        var raiz = new GameObject("Rodada_" + nome);
        raiz.transform.SetParent(barraRodadas, false);

        var texto = new GameObject("Texto").AddComponent<TextMeshPro>();
        texto.transform.SetParent(raiz.transform, false);
        if (fonteRodadas != null)
        {
            texto.font = fonteRodadas;
        }
        texto.fontSize = tamanhoFonteChip;
        texto.text = nome;
        texto.alignment = TextAlignmentOptions.Center;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.sortingOrder = ordemChip + 2;

        float larguraTexto = texto.GetPreferredValues(nome).x;
        bool temCheck = spriteCheck != null;
        float largura = larguraTexto + folgaChip * 2f + (temCheck ? alturaChip * 0.6f : 0f);
        texto.rectTransform.sizeDelta = new Vector2(largura, alturaChip);

        raiz.transform.localPosition = new Vector3(x + largura * 0.5f, 0f, 0f);
        x += largura + espacoChip;

        var fundo = raiz.AddComponent<SpriteRenderer>();
        fundo.sprite = spriteFundo;
        fundo.drawMode = SpriteDrawMode.Sliced;
        fundo.size = new Vector2(largura, alturaChip);
        fundo.sortingOrder = ordemChip;

        var borda = new GameObject("Borda").AddComponent<SpriteRenderer>();
        borda.transform.SetParent(raiz.transform, false);
        borda.sprite = spriteBorda;
        borda.drawMode = SpriteDrawMode.Sliced;
        borda.size = new Vector2(largura, alturaChip);
        borda.sortingOrder = ordemChip + 1;

        SpriteRenderer check = null;
        if (temCheck)
        {
            check = new GameObject("Check").AddComponent<SpriteRenderer>();
            check.transform.SetParent(raiz.transform, false);
            check.sprite = spriteCheck;
            float lado = alturaChip * 0.5f;
            float escala = lado / Mathf.Max(0.0001f, spriteCheck.bounds.size.y);
            check.transform.localScale = new Vector3(escala, escala, 1f);
            check.transform.localPosition = new Vector3(-largura * 0.5f + folgaChip + lado * 0.5f, 0f, 0f);
            check.sortingOrder = ordemChip + 2;
            texto.transform.localPosition = new Vector3(alturaChip * 0.3f, 0f, 0f);
        }

        return new Chip { fundo = fundo, borda = borda, check = check, texto = texto };
    }

    private void PintarChips()
    {
        if (segmentosProgresso != null)
        {
            for (int i = 0; i < segmentosProgresso.Length; i++)
            {
                if (segmentosProgresso[i] == null)
                {
                    continue;
                }
                segmentosProgresso[i].color = i == atual ? corCiano : concluidas.Contains(i) ? corVerde : corLinha;
            }
        }

        for (int i = 0; i < chips.Count; i++)
        {
            Chip chip = chips[i];
            bool eAtual = i == atual;
            bool feita = concluidas.Contains(i) && !eAtual;

            chip.fundo.color = eAtual ? corAzul : corSuperficie;
            chip.borda.color = eAtual ? Color.black : feita ? corVerde : corLinha;
            chip.texto.color = eAtual ? Color.white : feita ? corVerde : corSuave;
            if (chip.check != null)
            {
                bool comCheck = concluidas.Contains(i);
                chip.check.enabled = comCheck;
                chip.check.color = eAtual ? Color.white : corVerde;
                chip.texto.transform.localPosition = new Vector3(comCheck ? alturaChip * 0.3f : 0f, 0f, 0f);
            }
        }
    }

    /// <summary>Atualiza os rótulos pra rodada que começou.</summary>
    public void IniciarRodada(int indice, AnimationSequencePuzzle.RoundData rodada, int frames, int iscas, float fps)
    {
        atual = indice;
        PintarChips();

        Definir(textoContagem, $"{indice + 1} / {Mathf.Max(total, indice + 1)}");
        Definir(textoTituloRodada, rodada.Titulo);
        Definir(textoFrames, $"{frames} frames");
        Definir(textoIscas, iscas == 1 ? "1 não faz parte" : $"{iscas} não fazem parte");
        Definir(textoFps, $"{Mathf.RoundToInt(fps)} FPS");
        DefinirComoJogar(rodada.TemFixos
            ? "<b>Falta um frame!</b>\nOs outros já estão no lugar. Ache o que falta."
            : rodada.primeiroFrameTravado
            ? "<b>É um loop!</b>\nO frame 1 já está no lugar. Monte o resto do ciclo."
            : "<b>Arraste ou clique</b>\nArraste cada frame da bandeja até a linha do tempo, ou só clique nele. Clique num frame encaixado para devolver.");
        EstadoPrevia(false);
    }

    /// <summary>Troca o quadro "como jogar" (ex: no passo de evento de animação).</summary>
    public void DefinirComoJogar(string texto)
    {
        Definir(textoComoJogar, ComTituloColorido(texto, corTituloComoJogar));
    }

    /// <summary>Pinta o título (a parte em negrito) do "como jogar" e deixa ele um pouco maior.</summary>
    public static string ComTituloColorido(string texto, Color cor)
    {
        if (string.IsNullOrEmpty(texto) || cor.a <= 0f || texto.Contains("<color"))
        {
            return texto;
        }
        string hex = ColorUtility.ToHtmlStringRGB(cor);
        return texto.Replace("<b>", $"<size=110%><color=#{hex}><b>").Replace("</b>", "</b></color></size>");
    }

    public void MarcarConcluida(int indice)
    {
        concluidas.Add(indice);
        atual = -1;
        PintarChips();
    }

    public void EstadoPrevia(bool tocando)
    {
        if (textoEstadoPrevia == null)
        {
            return;
        }

        textoEstadoPrevia.text = tocando ? "• tocando" : "• parado";
        textoEstadoPrevia.color = tocando ? corVerde : corSuave;
    }

    public void PintarEncaixe(int indice, EstadoEncaixe estado)
    {
        Color borda, fundo;
        switch (estado)
        {
            case EstadoEncaixe.Cheio: borda = corAzul; fundo = corElevado; break;
            case EstadoEncaixe.Travado: borda = corRoxo; fundo = corElevado; break;
            case EstadoEncaixe.Destaque: borda = corCiano; fundo = corDestaque; break;
            case EstadoEncaixe.Certo: borda = corVerde; fundo = corElevado; break;
            case EstadoEncaixe.Errado: borda = corVermelho; fundo = corElevado; break;
            default: borda = corLinha; fundo = corSuperficie; break;
        }

        if (bordasEncaixe != null && indice < bordasEncaixe.Length && bordasEncaixe[indice] != null)
        {
            bordasEncaixe[indice].color = borda;
        }
        if (fundosEncaixe != null && indice < fundosEncaixe.Length && fundosEncaixe[indice] != null)
        {
            fundosEncaixe[indice].color = fundo;
        }
        if (marcasTravado != null && indice < marcasTravado.Length && marcasTravado[indice] != null)
        {
            marcasTravado[indice].SetActive(estado == EstadoEncaixe.Travado);
        }
    }

    private static void Definir(TMP_Text texto, string valor)
    {
        if (texto != null)
        {
            texto.text = valor;
        }
    }
}
