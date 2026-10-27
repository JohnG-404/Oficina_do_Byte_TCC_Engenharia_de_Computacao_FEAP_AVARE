using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Uma peça arrastável do HudPuzzleUI (cartão da moldura, ver
/// MolduraPuzzleUI.NovoCartao). Durante o arrasto ela sobe pra camada
/// "Arraste" (desenhada por cima de tudo) e, ao soltar, avisa o puzzle, que
/// decide se encaixa, volta pra bandeja ou recomeça tudo.
/// </summary>
public class HudPuzzlePeca : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    private HudPuzzleUI dono;
    private MolduraPuzzleUI.Cartao cartao;
    private RectTransform rt;
    private CanvasGroup grupo;

    // Lugar na bandeja Peças (o cartão é posicionado pelo canto de cima à esquerda).
    private Transform casaPai;
    private Vector2 casaAncoraMin;
    private Vector2 casaAncoraMax;
    private Vector2 casaPivo;
    private Vector2 casaPosicao;
    private Vector2 casaTamanho;

    private bool travada;
    private bool arrastando;

    public HudPuzzleUI.Peca Dados { get; private set; }

    internal void Iniciar(HudPuzzleUI dono, HudPuzzleUI.Peca dados, MolduraPuzzleUI.Cartao cartao)
    {
        this.dono = dono;
        this.cartao = cartao;
        Dados = dados;

        rt = (RectTransform)transform;
        grupo = gameObject.AddComponent<CanvasGroup>();
        casaPai = rt.parent;
        casaAncoraMin = rt.anchorMin;
        casaAncoraMax = rt.anchorMax;
        casaPivo = rt.pivot;
        casaPosicao = rt.anchoredPosition;
        casaTamanho = rt.sizeDelta;
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
        if (!travada && !arrastando && dono != null)
        {
            dono.MostrarDescricao(Dados);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!travada && !arrastando && dono != null)
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
        rt.anchorMin = casaAncoraMin;
        rt.anchorMax = casaAncoraMax;
        rt.pivot = casaPivo;
        rt.anchoredPosition = casaPosicao;
        rt.sizeDelta = casaTamanho;

        cartao.nome.text = Dados.nome;
        MolduraPuzzleUI.EstilizarCartao(cartao, false, null);
    }

    // Encaixada: ocupa o encaixe inteiro, no desenho "preenchido" da Física
    // (moldura neutra, ícone menor, nome dourado).
    internal void Travar(RectTransform slot)
    {
        travada = true;
        rt.SetParent(slot, false);
        VidaUI.Esticar(rt);
        Canvas.ForceUpdateCanvases();
        MolduraPuzzleUI.EstilizarCartao(cartao, true, Dados.nome);
    }
}
