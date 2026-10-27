using UnityEngine;
using UnityEngine.UI;

/// <summary>Barra de vida simples no canto da tela, ligada ao PlayerHealth.</summary>
public class InteracaoHealthUI : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private PlayerHealth playerHealth;

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
        }
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += Refresh;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= Refresh;
        }
    }

    private void Start()
    {
        if (playerHealth != null)
        {
            Refresh(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
    }

    private void Refresh(int current, int max)
    {
        if (healthSlider == null)
        {
            return;
        }

        healthSlider.maxValue = max;
        healthSlider.value = current;
    }
}
