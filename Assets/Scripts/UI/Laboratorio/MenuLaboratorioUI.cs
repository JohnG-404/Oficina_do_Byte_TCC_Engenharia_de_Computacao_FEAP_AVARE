using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Textos informativos do menu principal no tema Laboratório: o subtítulo do
/// botão Continuar (qual fase está salva), o ícone Continuar/Jogar, o
/// "X de 8 fases" com a barra verde e a versão no rodapé. Só lê o
/// ProgressoJogo — a ação dos botões continua no MenuControl.
/// </summary>
public class MenuLaboratorioUI : MonoBehaviour
{
    [Header("Botão Continuar / Jogar")]
    [SerializeField] private TMP_Text subtituloContinuar;
    [Tooltip("Texto principal do botão (Continuar/Jogar): sobe pra metade de cima quando há subtítulo e centraliza quando não há.")]
    [SerializeField] private TMP_Text rotuloContinuar;
    [SerializeField] private Image iconeContinuar;
    [SerializeField] private Sprite spriteContinuar;
    [SerializeField] private Sprite spriteJogar;
    [Tooltip("Onde o texto começa (depois do ícone) e a margem direita, em px.")]
    [SerializeField] private float margemEsquerda = 112f;
    [SerializeField] private float margemDireita = 24f;
    [Tooltip("Sem progresso: centro vertical do \"Jogar\" (0 = meio do botão).")]
    [SerializeField] private float ySozinho = 2f;
    [Tooltip("Com progresso: centro do título e do subtítulo, formando um bloco no meio do botão.")]
    [SerializeField] private float yTitulo = 20f;
    [SerializeField] private float ySubtitulo = -20f;

    [Header("Progresso (topo à direita)")]
    [SerializeField] private TMP_Text textoProgresso;
    [SerializeField] private Image barraProgresso;
    [SerializeField] private string formatoProgresso = "{0} de {1} fases";

    [Header("Rodapé")]
    [SerializeField] private TMP_Text textoVersao;
    [SerializeField] private string formatoVersao = "v{0} · TCC 2026";

    private void OnEnable()
    {
        if (rotuloContinuar == null && subtituloContinuar != null)
        {
            Transform t = subtituloContinuar.transform.parent.Find("Text (TMP)");
            if (t != null) rotuloContinuar = t.GetComponent<TMP_Text>();
        }

        Atualizar();
        ProgressoJogo.OnMudou += Atualizar;
        Jogadores.OnMudou += Atualizar;
    }

    private void OnDisable()
    {
        ProgressoJogo.OnMudou -= Atualizar;
        Jogadores.OnMudou -= Atualizar;
    }

    private void Atualizar()
    {
        // Antes de escolher o jogador, nada de progresso de outro aluno na tela.
        bool escolhido = Jogadores.Escolhido;
        string proxima = escolhido && ProgressoJogo.TemProgresso ? ProgressoJogo.ProximaCena() : null;
        bool continuar = !string.IsNullOrEmpty(proxima);

        if (subtituloContinuar != null)
        {
            subtituloContinuar.gameObject.SetActive(continuar);
            if (continuar)
            {
                int i = ProgressoJogo.IndiceDaFase(proxima);
                subtituloContinuar.text = i >= 0
                    ? $"Fase {i + 1} · {ProgressoJogo.Fases[i].titulo}"
                    : ProgressoJogo.TituloDaCena(proxima);
            }
        }

        PosicionarTextos(continuar);

        if (iconeContinuar != null)
        {
            Sprite s = continuar ? spriteContinuar : spriteJogar;
            if (s != null) iconeContinuar.sprite = s;
        }

        int feitas = escolhido ? ProgressoJogo.QuantidadeConcluidas : 0;
        int total = ProgressoJogo.Fases.Length;

        if (textoProgresso != null)
        {
            textoProgresso.text = string.Format(formatoProgresso, feitas, total);
        }

        if (barraProgresso != null)
        {
            // A barra é um filho esticado na horizontal: a âncora direita vira a fração concluída.
            RectTransform r = barraProgresso.rectTransform;
            float f = total > 0 ? (float)feitas / total : 0f;
            r.anchorMax = new Vector2(f, r.anchorMax.y);
            barraProgresso.enabled = feitas > 0;
        }

        if (textoVersao != null)
        {
            textoVersao.text = string.Format(formatoVersao, Application.version);
        }
    }

    // Sem subtítulo o título fica sozinho no meio do botão; com subtítulo os dois
    // formam um bloco centralizado (o ícone fica no meio do botão, alinhado a ele).
    private void PosicionarTextos(bool comSubtitulo)
    {
        if (rotuloContinuar != null)
        {
            Posicionar(rotuloContinuar, comSubtitulo ? yTitulo : ySozinho, 64f);
        }

        if (subtituloContinuar != null)
        {
            // Caixa mais alta que a linha da fonte: com o corte "..." (Ellipsis), uma
            // caixa baixa demais esconde a linha inteira.
            Posicionar(subtituloContinuar, ySubtitulo, 44f);
        }
    }

    private void Posicionar(TMP_Text texto, float y, float altura)
    {
        RectTransform r = texto.rectTransform;
        r.anchorMin = new Vector2(0f, 0.5f);
        r.anchorMax = new Vector2(1f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(margemEsquerda, y - altura / 2f);
        r.offsetMax = new Vector2(-margemDireita, y + altura / 2f);
        texto.alignment = TextAlignmentOptions.MidlineLeft;
    }
}
