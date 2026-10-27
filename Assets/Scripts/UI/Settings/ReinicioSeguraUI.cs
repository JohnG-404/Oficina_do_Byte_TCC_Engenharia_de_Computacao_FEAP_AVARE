using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Segurar a tecla "Reiniciar fase" (padrão R) por 1 s durante o jogo reinicia
/// direto, sem confirmação. Enquanto a tecla está apertada, o anel no canto
/// inferior esquerdo enche com "Segure para reiniciar…"; soltar antes some com
/// ele sem fazer nada. Fica no GameUICanvas (montado por Tools > UI Laboratório >
/// 5. Reiniciar fase) e vale em todas as fases.
///
/// Tecla apertada durante diálogo, puzzle, pausa ou transição é ignorada até
/// ser solta (ver ReinicioFase.TeclaLiberada).
/// </summary>
public class ReinicioSeguraUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private Image preenchimento;
    [SerializeField, Min(0.1f)] private float segundosParaReiniciar = 1f;

    private float segurando = -1f;
    private bool travada;

    private void Awake()
    {
        Esconder();
    }

    private void Update()
    {
        InputAction acao = InputManager.Instance != null ? InputManager.Instance.RestartAction : null;
        if (acao == null || !acao.IsPressed())
        {
            travada = false;
            Esconder();
            return;
        }

        if (travada)
        {
            return;
        }

        if (!ReinicioFase.TeclaLiberada)
        {
            // Apertou (ou continuou segurando) com o jogo parado: só vale de novo depois de soltar.
            travada = true;
            Esconder();
            return;
        }

        if (segurando < 0f)
        {
            if (!acao.WasPressedThisFrame())
            {
                // Já vinha apertada de antes (ex.: da cena anterior): precisa soltar.
                travada = true;
                return;
            }

            segurando = 0f;
        }

        // Limite por quadro: um travamento longo não pode completar o segundo sozinho.
        segurando += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        Mostrar(Mathf.Clamp01(segurando / segundosParaReiniciar));

        if (segurando >= segundosParaReiniciar)
        {
            travada = true;
            Esconder();
            ReinicioFase.Executar("tecla " + ReinicioFase.NomeDaTecla());
        }
    }

    private void Mostrar(float progresso)
    {
        if (grupo != null) grupo.alpha = 1f;
        if (preenchimento != null) preenchimento.fillAmount = progresso;
    }

    private void Esconder()
    {
        segurando = -1f;
        if (grupo != null) grupo.alpha = 0f;
        if (preenchimento != null) preenchimento.fillAmount = 0f;
    }
}
