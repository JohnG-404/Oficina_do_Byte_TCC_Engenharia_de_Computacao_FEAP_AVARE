using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// Painel pequeno no canto da tela, ligado depois do puzzle do Animator:
/// mostra o diagrama de estados do Player com o estado ATUAL aceso em tempo
/// real (lido do Animator de verdade, ver AnimacaoPlayerFisica.EstadoAtual),
/// a seta usada piscando a cada troca, e os valores dos parâmetros
/// velocidadeX/noChao. É a ponte pra fase de IA, que também é uma máquina de
/// estados.
///
/// Montado em runtime (Canvas próprio), igual aos outros painéis do jogo.
/// </summary>
public class AnimatorEstadoHUD : MonoBehaviour
{
    private const float Escala = 0.4f;
    private const int SortingOrder = 20;

    private AnimacaoPlayerFisica player;

    private PlayerAgachar agachar;
    private AnimatorDiagramaUI diagrama;
    private TextMeshProUGUI parametros;
    private GameObject canvasGO;

    private string estadoAnterior;
    private RectTransform raiz;
    private string titulo;
    private bool comAgachar;
    private int setaAcesa = -1;
    private float setaAcesaAte;

    /// <summary>Cria o painel (se ainda não existir) e começa a acompanhar o player.</summary>
    public static AnimatorEstadoHUD Mostrar(AnimacaoPlayerFisica player, string titulo, bool comAgachar = false)
    {
        AnimatorEstadoHUD existente = FindAnyObjectByType<AnimatorEstadoHUD>();
        if (existente != null)
        {
            existente.player = player;
            if (comAgachar && !existente.comAgachar)
            {
                existente.MostrarAgachar();
            }
            return existente;
        }

        Canvas canvas = VidaUI.CriarCanvas("AnimatorEstadoHUD", SortingOrder);
        var hud = canvas.gameObject.AddComponent<AnimatorEstadoHUD>();
        hud.canvasGO = canvas.gameObject;
        hud.player = player;
        hud.raiz = (RectTransform)canvas.transform;
        hud.titulo = titulo;
        hud.comAgachar = comAgachar;
        hud.Montar(hud.raiz, titulo);
        return hud;
    }

    /// <summary>Remonta o painel com a linha Agachado / AndarAgachado (depois do mini-puzzle do túnel).</summary>
    public void MostrarAgachar()
    {
        comAgachar = true;
        if (raiz == null)
        {
            return;
        }

        for (int i = raiz.childCount - 1; i >= 0; i--)
        {
            Destroy(raiz.GetChild(i).gameObject);
        }

        estadoAnterior = null;
        setaAcesa = -1;
        Montar(raiz, titulo);
    }

    public void Esconder()
    {
        if (canvasGO != null)
        {
            canvasGO.SetActive(false);
        }
    }

    private void Montar(RectTransform raiz, string titulo)
    {
        Vector2 tamDiagrama = AnimatorDiagramaUI.Tamanho(comAgachar) * Escala;
        // Com o agachar a caixa AndarAgachado passa da borda direita do diagrama: painel um pouco mais largo.
        float folgaLateral = comAgachar ? 60f : 30f;
        var tamPainel = new Vector2(tamDiagrama.x + folgaLateral, tamDiagrama.y + 100f);

        // Canto superior direito (onde ficava o antigo inventário de animações).
        RectTransform painel = VidaUI.CaixaComBorda(raiz, "PainelAnimator", Vector2.zero, tamPainel, VidaUI.Moldura, VidaUI.Fundo);
        RectTransform borda = (RectTransform)painel.parent;
        borda.anchorMin = borda.anchorMax = new Vector2(1f, 1f);
        borda.pivot = new Vector2(1f, 1f);
        borda.anchoredPosition = new Vector2(-30f, -30f);

        VidaUI.Centro(VidaUI.NovoTexto(painel, "Titulo", titulo, 20, VidaUI.Amarelo, TextAlignmentOptions.Left, FontStyles.Bold).rectTransform,
            new Vector2(0f, tamPainel.y * 0.5f - 22f), new Vector2(tamPainel.x - 24f, 28f));

        diagrama = AnimatorDiagramaUI.Criar(painel, new Vector2(comAgachar ? -10f : 0f, 2f), Escala, false, comAgachar);

        parametros = VidaUI.NovoTexto(painel, "Parametros", "", 17, VidaUI.TextoApagado, TextAlignmentOptions.Center);
        VidaUI.Centro(parametros.rectTransform, new Vector2(0f, -tamPainel.y * 0.5f + 22f), new Vector2(tamPainel.x - 20f, 26f));
        // Com o agachado são três parâmetros: cabe numa linha só, diminuindo a letra se precisar.
        parametros.textWrappingMode = TextWrappingModes.NoWrap;
        parametros.enableAutoSizing = true;
        parametros.fontSizeMin = 11f;
        parametros.fontSizeMax = 17f;
    }

    private void Update()
    {
        if (player == null || diagrama == null)
        {
            return;
        }

        if (agachar == null) agachar = player.GetComponent<PlayerAgachar>();
        string estado = player.EstadoAtual;
        if (estado != estadoAnterior)
        {
            if (estadoAnterior != null)
            {
                AcenderSeta(AnimatorDiagramaUI.SetaEntre(estadoAnterior, estado));
            }

            estadoAnterior = estado;
            diagrama.DestacarEstado(estado);
        }

        if (setaAcesa >= 0 && Time.unscaledTime >= setaAcesaAte)
        {
            diagrama.DestacarSeta(setaAcesa, false);
            setaAcesa = -1;
        }

        float vx = Mathf.Abs(player.Velocidade.x);
        string agachadoTxt = agachar != null && player.AgacharMontado ? $"   agachado = {(agachar.Agachado ? "true" : "false")}" : "";
        parametros.text = $"velocidadeX = {vx.ToString("0.0", CultureInfo.InvariantCulture)}     noChao = {(player.IsGrounded ? "true" : "false")}{agachadoTxt}";
    }

    private void AcenderSeta(int seta)
    {
        if (seta < 0)
        {
            return;
        }

        if (setaAcesa >= 0)
        {
            diagrama.DestacarSeta(setaAcesa, false);
        }

        setaAcesa = seta;
        setaAcesaAte = Time.unscaledTime + 0.5f;
        diagrama.DestacarSeta(seta, true);
    }
}
