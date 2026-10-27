using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Encaixe de uma seta no painel "Cérebro do X" (IADiagramaUI). Recebe a
/// condição solta em cima dele (IADiagramaChipUI). Vazio, mostra o contorno
/// tracejado e "arraste aqui"; cheio, mostra a mesma peça em cartão do
/// inventário.
/// </summary>
public class IADiagramaSlotUI : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    private IADiagramaUI painel;
    private Image fundo;
    private Image tracejado;
    private TMP_Text rotuloVazio;
    private Color corVazio;
    private Color corAlvo;
    private IAOpcao atual;
    private RectTransform peca;

    public int Indice { get; private set; }

    public void Configurar(IADiagramaUI dono, int indice, Image imagem, Image contorno, TMP_Text vazio, Color corDoVazio, Color corDoAlvo)
    {
        painel = dono;
        Indice = indice;
        fundo = imagem;
        tracejado = contorno;
        rotuloVazio = vazio;
        corVazio = corDoVazio;
        corAlvo = corDoAlvo;
    }

    public void Atualizar(IAOpcao opcao)
    {
        // Só recria a peça quando a condição muda (entrar e sair com o mouse chama isto de novo).
        if (opcao != atual || (opcao != null && peca == null))
        {
            if (peca != null)
            {
                Destroy(peca.gameObject);
                peca = null;
            }

            if (opcao != null && painel != null)
            {
                RectTransform rt = (RectTransform)transform;
                peca = painel.CriarVisualChip(rt, opcao, rt.rect.size);
                foreach (Graphic g in peca.GetComponentsInChildren<Graphic>(true))
                {
                    g.raycastTarget = false;
                }
            }
        }

        atual = opcao;
        bool vazio = opcao == null;
        fundo.color = corVazio;
        rotuloVazio.enabled = vazio;
        tracejado.color = Color.white;
        tracejado.enabled = vazio && tracejado.sprite != null;
        if (peca != null)
        {
            peca.gameObject.SetActive(true);
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        IADiagramaChipUI chip = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<IADiagramaChipUI>() : null;
        if (chip == null || painel == null)
        {
            return;
        }

        painel.AoSoltar(Indice, chip.Opcao);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.dragging && eventData.pointerDrag != null && eventData.pointerDrag.GetComponent<IADiagramaChipUI>() != null)
        {
            // Arrastando por cima: o encaixe acende, mesmo se já tiver uma peça.
            if (peca != null)
            {
                peca.gameObject.SetActive(false);
            }

            fundo.color = corAlvo;
            tracejado.enabled = tracejado.sprite != null;
            tracejado.color = TemaLabUI.Ciano;
            rotuloVazio.enabled = true;
            return;
        }

        if (atual != null && painel != null)
        {
            painel.MostrarDescricao(atual);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Atualizar(atual);
    }
}
