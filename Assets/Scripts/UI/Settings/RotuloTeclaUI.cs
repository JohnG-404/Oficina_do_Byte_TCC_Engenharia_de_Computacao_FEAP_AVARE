using TMPro;
using UnityEngine;

/// <summary>
/// Mostra num texto a tecla atual de uma ação do InputManager (ex.: o chip "R"
/// do botão Reiniciar fase da pausa) e acompanha a troca na aba Controles.
/// </summary>
public class RotuloTeclaUI : MonoBehaviour
{
    [SerializeField] private TMP_Text texto;
    [SerializeField] private string acao = InputManager.RestartActionName;
    [SerializeField] private string padrao = "R";

    private InputManager inscrito;

    private void OnEnable()
    {
        Atualizar();
    }

    private void OnDisable()
    {
        if (inscrito != null)
        {
            inscrito.OnBindingsChanged -= Atualizar;
            inscrito = null;
        }
    }

    private void Atualizar()
    {
        if (inscrito == null && InputManager.Instance != null)
        {
            inscrito = InputManager.Instance;
            inscrito.OnBindingsChanged += Atualizar;
        }

        if (texto == null)
        {
            return;
        }

        string nome = InputManager.Instance != null ? InputManager.Instance.GetBindingName(acao) : "";
        texto.text = string.IsNullOrEmpty(nome) ? padrao : nome;
    }
}
