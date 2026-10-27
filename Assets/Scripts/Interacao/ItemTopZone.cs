using UnityEngine;

/// <summary>
/// Gatilho "em cima" de um InteractableItem — um GameObject filho fino
/// posicionado logo acima do sprite, usado pela Ação "Pular em cima"
/// (ActionType.JumpOnTop). Fica separado do resto do collider pra detectar
/// especificamente um pouso por cima, não qualquer toque lateral.
///
/// Filtra pela tag "Player" (ver InteractableItem.IsPlayer): uma caixa
/// caindo em cima do item não ativa nada.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ItemTopZone : MonoBehaviour, IItemZone
{
    private InteractableItem owner;

    public void Initialize(InteractableItem itemOwner)
    {
        owner = itemOwner;
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner == null || !InteractableItem.IsPlayer(other))
        {
            return;
        }

        owner.HandleLandedOnTop(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (owner == null || !InteractableItem.IsPlayer(other))
        {
            return;
        }

        owner.HandleLeftTop(other);
    }
}
