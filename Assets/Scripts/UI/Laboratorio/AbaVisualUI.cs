using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual de uma aba (tema Laboratório): aba_ativa / aba_inativa e a cor do
/// texto e do ícone. Chamado pelo SettingsPanelUI ao trocar de aba; o foco
/// (aba_foco) vem do Sprite Swap do próprio Button.
/// </summary>
public class AbaVisualUI : MonoBehaviour
{
    [SerializeField] private Image fundo;
    [SerializeField] private Sprite spriteAtiva;
    [SerializeField] private Sprite spriteInativa;
    [SerializeField] private TMP_Text rotulo;
    [SerializeField] private Image icone;
    [SerializeField] private Color corAtiva = new Color32(0xE8, 0xEE, 0xF9, 0xFF);
    [SerializeField] private Color corInativa = new Color32(0x9A, 0xA8, 0xC4, 0xFF);

    public void Definir(bool ativa)
    {
        if (fundo != null)
        {
            fundo.sprite = ativa ? spriteAtiva : spriteInativa;
            fundo.color = Color.white;
        }

        Color c = ativa ? corAtiva : corInativa;
        if (rotulo != null) rotulo.color = c;
        if (icone != null) icone.color = c;
    }
}
