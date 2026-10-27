using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

/// <summary>
/// Ticker vertical infinito de texto (ex: trechos de código) para decorar
/// o menu principal. Diferente do CreditsScroller antigo, este script cria
/// os blocos de texto sozinho em runtime — não depende de dois
/// RectTransforms fixos configurados manualmente na cena — e gera quantos
/// blocos forem necessários pra cobrir a área visível de uma vez. Isso
/// elimina o corte seco: sempre há blocos suficientes na fila, então nunca
/// existe um instante sem texto na tela esperando o próximo aparecer.
///
/// A altura de cada bloco é sempre calculada com TMP_Text.preferredHeight
/// no momento em que o texto é atribuído (nunca lida de um RectTransform
/// que pode não ter atualizado o layout ainda), então blocos com
/// quantidades de texto diferentes se encaixam sem folga nem sobreposição.
///
/// Para trocar o conteúdo: edite a lista "Snippets" no Inspector — cada
/// item é um bloco de texto mostrado em sequência, em loop. Adicione,
/// remova ou reordene livremente, nenhuma outra configuração é necessária.
/// </summary>
public class InfiniteCodeScroller : MonoBehaviour
{
    [Header("Conteúdo (cada item é um bloco de texto, mostrado em loop)")]
    [SerializeField]
    [TextArea(3, 15)]
    private List<string> snippets = new List<string>();

    [Header("Aparência")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float fontSize = 18f;
    [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private float blockWidth = 500f;
    [SerializeField] private float gapBetweenBlocks = 60f;
    [Tooltip("Desligado = cada linha do texto fica numa linha só (o que passar da largura é cortado pela máscara do painel). Use com código que tem número de linha.")]
    [SerializeField] private bool quebrarLinhas = true;
    [Tooltip("Ligado = a linha vazia entre dois blocos com número de linha mostra o número dela, como num editor (no lugar da folga).")]
    [SerializeField] private bool numerarLinhaVazia = true;

    [Header("Movimento")]
    [SerializeField] private float speed = 50f;
    [Tooltip("Ligado = o texto sobe e os blocos novos entram por baixo (como um terminal), mantendo a ordem de leitura. Desligado = desce (comportamento antigo).")]
    [SerializeField] private bool rolarParaCima = false;

    // RectTransform deste próprio GameObject: representa a área visível de referência.
    private RectTransform viewport;

    private readonly List<RectTransform> blocks = new List<RectTransform>();
    private readonly List<TMP_Text> texts = new List<TMP_Text>();
    private int nextSnippetIndex;

    // Número da linha no começo de cada linha do código (ver LaboratorioUIBuilder.BlocosDeCodigo).
    private static readonly Regex NumeroDaLinha = new Regex(@"<pos=4>\s*(\d+)</color>");
    private const string CorNumero = "#4A5570";

    // Com a linha vazia numerada dentro do bloco, a folga já está no texto.
    private float Folga => numerarLinhaVazia ? 0f : gapBetweenBlocks;

    private void Awake()
    {
        viewport = (RectTransform)transform;
    }

    private void Start()
    {
        if (snippets.Count == 0)
        {
            Debug.LogWarning("[InfiniteCodeScroller] Nenhum texto configurado em 'Snippets'.");
            return;
        }

        // Preenche do topo até um pouco além do fim da área visível, com margem de segurança.
        float coveredFromTop = 0f;
        float startY = viewport.rect.height * 0.5f;
        float targetCoverage = viewport.rect.height + 600f;

        while (coveredFromTop < targetCoverage)
        {
            RectTransform block = CreateBlock();
            float y = startY - coveredFromTop - block.rect.height * 0.5f;
            SetBlockY(block, y);
            coveredFromTop += block.rect.height + Folga;
        }
    }

    private void Update()
    {
        if (blocks.Count == 0)
        {
            return;
        }

        float movement = speed * Time.deltaTime * (rolarParaCima ? -1f : 1f);
        for (int i = 0; i < blocks.Count; i++)
        {
            SetBlockY(blocks[i], blocks[i].anchoredPosition.y - movement);
        }

        if (rolarParaCima)
        {
            RecycleTopBlockIfOffscreen();
        }
        else
        {
            RecycleBottomBlockIfOffscreen();
        }
    }

    // Versão "subindo": se o bloco mais de cima já saiu inteiro pelo topo,
    // recebe o próximo texto e vai pro fim da fila, logo abaixo do último.
    private void RecycleTopBlockIfOffscreen()
    {
        RectTransform topBlock = blocks[0];
        float bottomEdge = topBlock.anchoredPosition.y - topBlock.rect.height * 0.5f;

        if (bottomEdge <= viewport.rect.height * 0.5f)
        {
            return;
        }

        int lastIndex = blocks.Count - 1;
        RectTransform lastBlock = blocks[lastIndex];
        float lastBottomEdge = lastBlock.anchoredPosition.y - lastBlock.rect.height * 0.5f;

        TMP_Text text = texts[0];
        ApplyNextSnippet(text);

        float newHeight = text.preferredHeight;
        topBlock.sizeDelta = new Vector2(blockWidth, newHeight);
        SetBlockY(topBlock, lastBottomEdge - Folga - newHeight * 0.5f);

        blocks.RemoveAt(0);
        blocks.Add(topBlock);
        texts.RemoveAt(0);
        texts.Add(text);
    }

    // Se o bloco mais de baixo já saiu inteiramente da área visível por baixo,
    // reaproveita ele: recebe o próximo texto da lista e volta pro topo da fila.
    private void RecycleBottomBlockIfOffscreen()
    {
        int bottomIndex = blocks.Count - 1;
        RectTransform bottomBlock = blocks[bottomIndex];
        float topEdge = bottomBlock.anchoredPosition.y + bottomBlock.rect.height * 0.5f;

        if (topEdge >= -viewport.rect.height * 0.5f)
        {
            return;
        }

        RectTransform topBlock = blocks[0];
        float topBlockTopEdge = topBlock.anchoredPosition.y + topBlock.rect.height * 0.5f;

        TMP_Text text = texts[bottomIndex];
        ApplyNextSnippet(text);

        float newHeight = text.preferredHeight;
        bottomBlock.sizeDelta = new Vector2(blockWidth, newHeight);

        float newY = topBlockTopEdge + Folga + newHeight * 0.5f;
        SetBlockY(bottomBlock, newY);

        // Esse bloco agora é o novo topo da fila.
        blocks.RemoveAt(bottomIndex);
        blocks.Insert(0, bottomBlock);
        texts.RemoveAt(bottomIndex);
        texts.Insert(0, text);
    }

    private RectTransform CreateBlock()
    {
        var go = new GameObject("CodeBlock", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(viewport, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(blockWidth, 10f); // largura antes do texto, pro word-wrap calcular certo

        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.overflowMode = TextOverflowModes.Overflow;
        text.textWrappingMode = quebrarLinhas ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        blocks.Add(rect);
        texts.Add(text);

        ApplyNextSnippet(text);
        rect.sizeDelta = new Vector2(blockWidth, text.preferredHeight);

        return rect;
    }

    private void ApplyNextSnippet(TMP_Text text)
    {
        if (snippets.Count == 0)
        {
            text.text = string.Empty;
            return;
        }

        text.text = ComLinhaVazia(snippets[nextSnippetIndex]);
        nextSnippetIndex = (nextSnippetIndex + 1) % snippets.Count;
        text.ForceMeshUpdate();
    }

    // Acrescenta no fim do bloco a linha vazia que separa do próximo, com o
    // número seguinte ao último do bloco.
    private string ComLinhaVazia(string trecho)
    {
        if (!numerarLinhaVazia)
        {
            return trecho;
        }

        MatchCollection numeros = NumeroDaLinha.Matches(trecho);
        if (numeros.Count == 0 || !int.TryParse(numeros[numeros.Count - 1].Groups[1].Value, out int ultimo))
        {
            return trecho + "\n ";
        }
        return trecho + "\n<color=" + CorNumero + "><pos=4>" + (ultimo + 1).ToString().PadLeft(3) + "</color>";
    }

    private void SetBlockY(RectTransform block, float y)
    {
        block.anchoredPosition = new Vector2(block.anchoredPosition.x, y);
    }
}
