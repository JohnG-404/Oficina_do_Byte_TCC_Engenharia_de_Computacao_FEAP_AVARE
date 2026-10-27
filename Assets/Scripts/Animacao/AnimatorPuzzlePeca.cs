using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Quem recebe as peças: o puzzle do Animator do Player (AnimatorPuzzleUI)
/// ou o da lâmpada (LampadaAnimatorUI).
/// </summary>
public interface IDonoPecaAnimator
{
    RectTransform CamadaArraste { get; }
    bool Concluido { get; }
    void Soltar(AnimatorPuzzlePeca peca, Vector2 pontoTela);
    void MostrarDescricao(AnimatorPuzzleUI.Peca dados);
    void MostrarPadrao();
}

/// <summary>
/// Uma condição arrastável do puzzle do Animator (AnimatorPuzzleUI). Mesmo
/// comportamento das peças dos outros puzzles UGUI do jogo (HudPuzzlePeca):
/// durante o arrasto sobe pra camada "Arraste", passar o mouse mostra a
/// descrição, e ao soltar quem decide o que acontece é o puzzle.
/// </summary>
public class AnimatorPuzzlePeca : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    private IDonoPecaAnimator dono;
    private TextMeshProUGUI rotulo;
    private RectTransform rt;
    private CanvasGroup grupo;

    private Transform casaPai;
    private Vector2 casaPosicao;
    private Vector2 casaTamanho;

    private bool travada;
    private bool arrastando;

    public AnimatorPuzzleUI.Peca Dados { get; private set; }

    internal void Iniciar(IDonoPecaAnimator dono, AnimatorPuzzleUI.Peca dados, TextMeshProUGUI rotulo)
    {
        this.dono = dono;
        this.rotulo = rotulo;
        Dados = dados;

        rt = (RectTransform)transform;
        grupo = gameObject.AddComponent<CanvasGroup>();
        casaPai = rt.parent;
        casaPosicao = rt.anchoredPosition;
        casaTamanho = rt.sizeDelta;
    }

    /// <summary>Encaixada num lugar (fora da bandeja).</summary>
    internal bool Travada => travada;

    /// <summary>Muda o lugar da peça na bandeja; se ela está lá agora, já move.</summary>
    internal void DefinirCasa(Vector2 posicao)
    {
        casaPosicao = posicao;
        if (!travada && !arrastando && rt.parent == casaPai)
        {
            rt.anchoredPosition = posicao;
        }
    }

    internal Vector2 CentroNaTela()
    {
        return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (travada || dono == null || dono.Concluido)
        {
            return;
        }

        arrastando = true;
        grupo.blocksRaycasts = false;
        grupo.alpha = 0.85f;
        rt.SetParent(dono.CamadaArraste, true);
        rt.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!arrastando)
        {
            return;
        }

        // Canvas overlay: posição de mundo = pixels de tela.
        rt.position += (Vector3)eventData.delta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!arrastando)
        {
            return;
        }

        arrastando = false;
        grupo.blocksRaycasts = true;
        grupo.alpha = 1f;
        dono.Soltar(this, eventData.position);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (dono != null && !arrastando)
        {
            dono.MostrarDescricao(Dados);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!arrastando && dono != null)
        {
            dono.MostrarPadrao();
        }
    }

    internal void VoltarParaCasa()
    {
        travada = false;
        arrastando = false;
        grupo.blocksRaycasts = true;
        grupo.alpha = 1f;

        rt.SetParent(casaPai, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition = casaPosicao;
        rt.sizeDelta = casaTamanho;

        rotulo.color = VidaUI.Texto;
        rotulo.text = AnimatorPuzzleUI.TextoSeguro(Dados.texto);
        aoMudarDesenho?.Invoke(false, null);
    }

    internal void Travar(RectTransform encaixe, string textoEncaixado)
    {
        travada = true;
        rt.SetParent(encaixe, false);
        rt.localRotation = Quaternion.identity;
        VidaUI.Esticar(rt, 4f);
        rotulo.text = textoEncaixado;
        aoMudarDesenho?.Invoke(true, textoEncaixado);
    }

    /// <summary>
    /// Opcional: troca o desenho do cartão ao encaixar (true) ou ao voltar pra
    /// bandeja (false). Usado pelos cartões da moldura da Física (MolduraPuzzleUI).
    /// </summary>
    internal System.Action<bool, string> aoMudarDesenho;
}
