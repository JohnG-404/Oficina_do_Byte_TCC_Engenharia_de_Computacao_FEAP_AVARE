using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Sons de um botão/toggle/dropdown: hover (mouse por cima ou navegação por
/// teclado/controle), clique e "voltar" (botões Voltar/Fechar/Cancelar...).
/// Adicionado sozinho em runtime pelo AudioManager — não precisa colocar à mão.
/// Pra tirar o som de um botão específico, basta adicionar este componente
/// nele no Editor com "Silencioso" marcado.
/// </summary>
[DisallowMultipleComponent]
public class SomUI : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, ISubmitHandler, IMoveHandler
{
    [SerializeField] private bool silencioso;

    private static readonly string[] PalavrasDeVoltar = { "voltar", "fechar", "cancelar", "retomar", "sair", "back", "close", "cancel" };

    private Selectable selectable;

    public static bool Suporta(Selectable s) => s is Button || s is Toggle || s is TMP_Dropdown || s is Dropdown;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    private bool Ativo => !silencioso && selectable != null && selectable.IsInteractable();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Ativo)
        {
            AudioManager.TocarEfeito(AudioManager.Efeito.UiHover);
        }
    }

    // Roda depois do OnMove do próprio Selectable (componente adicionado por
    // último): se a seleção saiu daqui, a navegação levou pra outro item.
    public void OnMove(AxisEventData eventData)
    {
        if (!silencioso && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
        {
            AudioManager.TocarEfeito(AudioManager.Efeito.UiHover);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            TocarClique();
        }
    }

    public void OnSubmit(BaseEventData eventData) => TocarClique();

    private void TocarClique()
    {
        if (!Ativo)
        {
            return;
        }

        AudioManager.TocarEfeito(EhVoltar() ? AudioManager.Efeito.UiVoltar : AudioManager.Efeito.UiClique);
    }

    private bool EhVoltar()
    {
        if (!(selectable is Button))
        {
            return false;
        }

        string nome = gameObject.name.ToLowerInvariant();
        TMP_Text texto = GetComponentInChildren<TMP_Text>();
        string rotulo = texto != null ? texto.text.ToLowerInvariant() : "";

        foreach (string palavra in PalavrasDeVoltar)
        {
            if (nome.Contains(palavra) || rotulo.StartsWith(palavra) || rotulo.Contains(" " + palavra))
            {
                return true;
            }
        }

        // Botão "X" de fechar.
        return rotulo.Trim() == "x" || rotulo.Trim() == "×" || rotulo.Trim() == "✕";
    }
}
