using TMPro;
using UnityEngine;

/// <summary>
/// Contador de FPS no canto da tela (opção "Mostrar FPS" da aba Jogo).
/// Fica no GameUICanvas; o texto some quando a opção está desligada.
/// </summary>
public class ContadorFPSUI : MonoBehaviour
{
    [SerializeField] private TMP_Text texto;
    [SerializeField] private float intervalo = 0.5f;

    private float tempo;
    private int quadros;

    private void Update()
    {
        bool mostrar = ConfiguracoesJogo.MostrarFPS;
        if (texto == null) return;
        if (texto.enabled != mostrar) texto.enabled = mostrar;
        if (!mostrar) return;

        tempo += Time.unscaledDeltaTime;
        quadros++;
        if (tempo < intervalo) return;

        texto.text = Mathf.RoundToInt(quadros / tempo) + " FPS";
        tempo = 0f;
        quadros = 0;
    }
}
