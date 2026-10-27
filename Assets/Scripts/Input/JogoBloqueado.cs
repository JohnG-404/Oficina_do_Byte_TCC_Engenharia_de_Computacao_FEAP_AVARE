using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Diz se alguma janela está por cima da fase: pausa, Configurações, painel
/// "Configurar item", Game Over ou um puzzle aberto. Enquanto
/// <see cref="Ativo"/> for verdadeiro, o robô não anda, não pula, não agacha
/// e os itens ignoram a tecla de Interagir — o teclado não "passa" para trás
/// da janela. Os controladores também apagam o pulo guardado nesse tempo, para
/// o robô não pular sozinho quando a janela fecha.
///
/// Janelas que já têm um jeito próprio de dizer que estão abertas (pausa,
/// Configurações, Configurar item, diagrama da IA, puzzles da Física) são
/// lidas direto. As outras se registram com <see cref="Abrir"/> ao aparecer e
/// <see cref="Fechar"/> ao sumir; um registro cujo objeto foi destruído (troca
/// de cena, Destroy) deixa de contar sozinho.
/// </summary>
public static class JogoBloqueado
{
    private static readonly HashSet<Object> janelas = new HashSet<Object>();
    private static readonly List<Object> mortas = new List<Object>();

    /// <summary>Quanto tempo (real) depois de fechar a pausa ela ainda conta em <see cref="PausaPorCima"/>.</summary>
    private const float FolgaAoFecharPausa = 0.25f;
    private static float pausaVistaAbertaEm = -10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        janelas.Clear();
        pausaVistaAbertaEm = -10f;
    }

    /// <summary>Registra uma janela aberta (chame de novo sem problema).</summary>
    public static void Abrir(Object dono)
    {
        if (dono != null) janelas.Add(dono);
    }

    /// <summary>Tira a janela do registro (fechou ou foi destruída).</summary>
    public static void Fechar(Object dono)
    {
        janelas.Remove(dono);
    }

    public static bool Ativo
    {
        get
        {
            if (PauseMenuUI.Instance != null && (PauseMenuUI.Instance.PausaAberta || PauseMenuUI.Instance.ConfirmacaoAberta))
                return true;
            if (SettingsPanelUI.Instance != null && SettingsPanelUI.Instance.IsVisible)
                return true;
            if (InteractionConfigUI.Instance != null && InteractionConfigUI.Instance.PainelAberto)
                return true;
            if (IADiagramaUI.EstaAberto || PuzzleControl.AlgumAberto)
                return true;
            return AlgumaJanelaRegistrada();
        }
    }

    /// <summary>
    /// Pausa, confirmação de reinício ou Configurações na tela, ou fechadas há
    /// um instante. As telas que esperam Enter/clique por baixo da pausa
    /// (cartão "Fase concluída", Game Over, painel do código montado, falas)
    /// ignoram a entrada enquanto isto for verdadeiro: o clique ou o Enter que
    /// escolhe um botão da pausa não vale também para elas. A folga cobre o
    /// frame em que o botão fecha a pausa antes de a tela de trás ler o clique.
    /// Diferente de <see cref="Ativo"/>, não conta o Game Over nem os puzzles,
    /// que são justamente as telas que perguntam.
    /// </summary>
    public static bool PausaPorCima
    {
        get
        {
            bool aberta = (PauseMenuUI.Instance != null && (PauseMenuUI.Instance.PausaAberta || PauseMenuUI.Instance.ConfirmacaoAberta))
                || (SettingsPanelUI.Instance != null && SettingsPanelUI.Instance.IsVisible);
            if (aberta)
            {
                pausaVistaAbertaEm = Time.unscaledTime;
                return true;
            }
            return Time.unscaledTime - pausaVistaAbertaEm < FolgaAoFecharPausa;
        }
    }

    private static bool AlgumaJanelaRegistrada()
    {
        if (janelas.Count == 0) return false;

        bool alguma = false;
        foreach (Object j in janelas)
        {
            // Comparação do Unity: objeto destruído vale null.
            if (j == null) mortas.Add(j);
            else alguma = true;
        }
        if (mortas.Count > 0)
        {
            foreach (Object m in mortas) janelas.Remove(m);
            mortas.Clear();
        }
        return alguma;
    }
}
