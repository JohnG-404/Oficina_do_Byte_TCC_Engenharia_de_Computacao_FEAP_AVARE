using UnityEngine;

/// <summary>
/// Mostra uma descrição no painel "Descrição" (ver InfoPanelUI) quando o
/// mouse passa sobre esta peça do puzzle. Componente separado do DragPiece de
/// propósito — cada um cuida de uma responsabilidade (arrastar vs. mostrar
/// informação) — mas os dois convivem no mesmo GameObject sem conflito:
/// OnMouseEnter/OnMouseExit são eventos de mouse independentes de
/// OnMouseDown/OnMouseDrag/OnMouseUp, disparados pelo mesmo Collider2D que o
/// DragPiece já usa.
///
/// Reutilizável: adicione este componente em qualquer peça e preencha a
/// "Descrição" no Inspector. O ícone mostrado no painel é o da própria peça
/// (filho cujo nome começa com "icone") — ou o campo "Icon", se preenchido.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PieceHoverInfo : MonoBehaviour
{
    [Tooltip("Título mostrado no painel ao passar o mouse sobre esta peça.")]
    [SerializeField] private string title;

    [Tooltip("Texto descritivo mostrado no painel ao passar o mouse sobre esta peça.")]
    [TextArea(2, 5)]
    [SerializeField] private string description;

    [Tooltip("Opcional — se vazio, usa o sprite do filho cujo nome começa com 'icone'.")]
    [SerializeField] private Sprite icon;

    private DragPiece dragPiece;

    private void Awake()
    {
        dragPiece = GetComponent<DragPiece>();

        if (icon == null)
        {
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.gameObject.name.StartsWith("icone") && sr.sprite != null)
                {
                    icon = sr.sprite;
                    break;
                }
            }
        }
    }

    private void OnMouseEnter()
    {
        // Peça já encaixada corretamente: não precisa mais mostrar a dica.
        if (dragPiece != null && dragPiece.locked)
        {
            return;
        }

        if (InfoPanelUI.Instance == null)
        {
            return;
        }

        InfoPanelUI.Instance.ShowDescription(title, description, icon);
    }

    private void OnMouseExit()
    {
        InfoPanelUI.Instance?.Clear();
    }
}
