using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fundo "calmo" das Configurações: aparece só quando o painel é aberto no
/// Menu principal. Na pausa (dentro de uma fase) fica desligado e sobra só o
/// véu escuro por cima da fase.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class FundoSomenteNoMenuUI : MonoBehaviour
{
    [SerializeField] private string cenaMenu = "Menu";

    private void OnEnable()
    {
        GetComponent<Graphic>().enabled = SceneManager.GetActiveScene().name == cenaMenu;
    }
}
