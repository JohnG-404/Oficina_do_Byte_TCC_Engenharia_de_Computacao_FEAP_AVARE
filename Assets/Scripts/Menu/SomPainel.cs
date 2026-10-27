using UnityEngine;

/// <summary>
/// Toca "abrir painel"/"fechar painel" quando o objeto é ativado/desativado.
/// Adicionado em runtime pelo AudioManager nos painéis de Configurações e
/// Pausa, sem mexer nos prefabs. Também pode ser colocado à mão em qualquer
/// outro painel que abre/fecha com SetActive.
/// </summary>
[DisallowMultipleComponent]
public class SomPainel : MonoBehaviour
{
    // false durante o AddComponent (o OnEnable imediato de um painel já
    // aberto não é "abrir") e até o primeiro frame de um colocado no Editor.
    private bool armado;

    public static void Garantir(GameObject painel)
    {
        if (painel.TryGetComponent(out SomPainel _))
        {
            return;
        }

        SomPainel som = painel.AddComponent<SomPainel>();
        som.armado = true;
    }

    private void Start()
    {
        armado = true;
    }

    private void OnEnable()
    {
        if (armado && AudioManager.Instance != null)
        {
            AudioManager.Instance.NotifyPanel(gameObject, opened: true);
        }
    }

    private void OnDisable()
    {
        if (armado && AudioManager.Instance != null)
        {
            AudioManager.Instance.NotifyPanel(gameObject, opened: false);
        }
    }
}
