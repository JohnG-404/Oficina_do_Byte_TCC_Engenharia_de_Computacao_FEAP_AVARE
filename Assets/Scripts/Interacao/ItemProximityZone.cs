using UnityEngine;

/// <summary>
/// Gatilho de proximidade de um InteractableItem — GameObject filho separado
/// (não o collider do próprio item) pra não misturar "detectar o jogador
/// perto" com um eventual collider sólido do objeto (ex: a porta bloqueando
/// passagem). Só repassa os eventos de trigger pro InteractableItem dono.
///
/// Filtra pela tag "Player" logo aqui (ver InteractableItem.IsPlayer):
/// inimigos e caixas entram na área sem ativar nada.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ItemProximityZone : MonoBehaviour, IItemZone
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

        owner.HandleProximityEnter(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (owner == null || !InteractableItem.IsPlayer(other))
        {
            return;
        }

        owner.HandleProximityExit(other);
    }
}
