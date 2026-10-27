using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Condição do inventário do painel "Cérebro do X" (IADiagramaUI). Arrastar
/// cria um "fantasma" que segue o mouse; soltar em cima de um encaixe
/// (IADiagramaSlotUI) troca a regra daquela seta. A condição continua no
/// inventário (dá pra usar de novo em outro encaixe).
///
/// Passar o mouse mostra o que a condição faz no rodapé do painel. Funciona
/// com o jogo pausado: a UI usa tempo não escalado.
/// </summary>
public class IADiagramaChipUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    private IADiagramaUI painel;
    private IAOpcao opcao;
    private RectTransform fantasma;

    public IAOpcao Opcao => opcao;

    public void Configurar(IADiagramaUI dono, IAOpcao condicao)
    {
        painel = dono;
        opcao = condicao;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (painel == null || opcao == null)
        {
            return;
        }

        RectTransform self = (RectTransform)transform;
        fantasma = painel.CriarVisualChip(painel.Raiz, opcao, self.rect.size);
        fantasma.name = "Arrastando_" + opcao.id;

        // O fantasma não pode "tampar" o encaixe embaixo do mouse, senão o
        // encaixe não recebe o OnDrop.
        CanvasGroup grupo = fantasma.gameObject.AddComponent<CanvasGroup>();
        grupo.blocksRaycasts = false;
        grupo.alpha = 0.85f;

        MoverFantasma(eventData);
        painel.MostrarDescricao(opcao);
    }

    public void OnDrag(PointerEventData eventData)
    {
        MoverFantasma(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (fantasma != null)
        {
            Destroy(fantasma.gameObject);
            fantasma = null;
        }
    }

    private void OnDisable()
    {
        if (fantasma != null)
        {
            Destroy(fantasma.gameObject);
            fantasma = null;
        }
    }

    private void MoverFantasma(PointerEventData eventData)
    {
        if (fantasma == null || painel == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(painel.Raiz, eventData.position, null, out Vector2 local))
        {
            fantasma.anchoredPosition = local;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (painel != null && !eventData.dragging)
        {
            painel.MostrarDescricao(opcao);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (painel != null && !eventData.dragging)
        {
            painel.LimparDescricao();
        }
    }
}
