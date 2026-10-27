using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fase com regra própria de reinício (registro com detalhes da fase, ou
/// recarregar sem repetir a intro). Quem não implementa usa o padrão de
/// <see cref="ReinicioFase"/>.
/// </summary>
public interface IReinicioFase
{
    /// <summary>False quando a fase já terminou (painel de vitória, recarregando...).</summary>
    bool PodeReiniciar { get; }

    /// <summary>Registra o reinício (origem: "tecla R", "botão pausa"...) e recarrega a fase.</summary>
    void Reiniciar(string origem);
}

/// <summary>
/// Fase que guarda os puzzles no reinício: diz o que continua montado, pra
/// linha verde da confirmação "Reiniciar a fase?" (vazio = sem linha).
/// </summary>
public interface ILembreteReinicio
{
    string LembreteReinicio { get; }
}

/// <summary>
/// Ponto único do "Reiniciar fase": botão da pausa (com confirmação) e a tecla
/// da ação Restart segurada por 1 s durante o jogo (ver ReinicioSeguraUI).
/// Registra "reinicio_manual" como falha, igual ao R antigo da Aplicacao_Fisica
/// e da IA_Aplicacao, e recarrega a cena com a transição curta (TrocaDeCena).
/// </summary>
public static class ReinicioFase
{
    private static IReinicioFase faseAtual;

    // Cenas sem gameplay: não têm pausa nem reinício.
    private static readonly string[] CenasSemReinicio = { "Menu", "Questionario", "Sprites" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => faseAtual = null;

    public static void Registrar(IReinicioFase fase) => faseAtual = fase;

    public static void Remover(IReinicioFase fase)
    {
        if (faseAtual == fase)
        {
            faseAtual = null;
        }
    }

    /// <summary>Cena atual é uma fase (pausa e reinício valem aqui).</summary>
    public static bool CenaDeFase
    {
        get
        {
            string cena = SceneManager.GetActiveScene().name;
            return System.Array.IndexOf(CenasSemReinicio, cena) < 0;
        }
    }

    /// <summary>Fase jogável agora, sem contar pausa (usado pelo botão da pausa).</summary>
    public static bool PodeReiniciar
    {
        get
        {
            if (!CenaDeFase || TrocaDeCena.EmAndamento || LevelCompleteUI.Aberto)
            {
                return false;
            }

            Object dono = faseAtual as Object;
            if (faseAtual != null && dono != null)
            {
                return faseAtual.PodeReiniciar;
            }

            return true;
        }
    }

    /// <summary>
    /// A tecla segurada só vale com o jogo correndo: sem diálogo, puzzle ou
    /// painel aberto, pausa, Configurações, troca de tecla ou transição.
    /// </summary>
    public static bool TeclaLiberada
    {
        get
        {
            if (!PodeReiniciar || DialogueManager.IsAnyDialogueActive || Time.timeScale <= 0f)
            {
                return false;
            }

            if (PauseMenuUI.Instance != null && (PauseMenuUI.Instance.PausaAberta || PauseMenuUI.Instance.ConfirmacaoAberta))
            {
                return false;
            }

            if (SettingsPanelUI.Instance != null && SettingsPanelUI.Instance.IsVisible)
            {
                return false;
            }

            if (InputManager.Instance != null && InputManager.Instance.IsRebinding)
            {
                return false;
            }

            return !PuzzleAberto();
        }
    }

    /// <summary>Algum puzzle ou painel de montagem das fases está na tela.</summary>
    public static bool PuzzleAberto()
    {
        if (IADiagramaUI.EstaAberto)
        {
            return true;
        }

        // Física e IA: grupos de peças (PuzzleControl ativa o grupo ao abrir).
        foreach (PuzzleControl p in Object.FindObjectsByType<PuzzleControl>())
        {
            if (p.Puzzle != null && p.Puzzle.activeInHierarchy)
            {
                return true;
            }
        }

        // Interação: painel de configurar/reconfigurar e puzzle da HUD (Vida).
        foreach (InteractionConfigUI ui in Object.FindObjectsByType<InteractionConfigUI>())
        {
            if (ui.PainelAberto)
            {
                return true;
            }
        }

        if (Object.FindAnyObjectByType<HudPuzzleUI>() != null)
        {
            return true;
        }

        // Animação: puzzle do Animator e painéis dos objetos.
        foreach (AnimatorPuzzleUI ui in Object.FindObjectsByType<AnimatorPuzzleUI>())
        {
            if (ui.Aberto) return true;
        }

        foreach (ObjetoAnimatorUI ui in Object.FindObjectsByType<ObjetoAnimatorUI>())
        {
            if (ui.Aberto) return true;
        }

        foreach (LampadaAnimatorUI ui in Object.FindObjectsByType<LampadaAnimatorUI>())
        {
            if (ui.Aberto) return true;
        }

        // Tutorial_Anim: painel de ajuste da animação.
        return Object.FindAnyObjectByType<AjusteAnimacaoUI>() != null;
    }

    /// <summary>O que continua montado depois do reinício (vazio quando nada fica ou a fase não guarda).</summary>
    public static string Lembrete
    {
        get
        {
            Object dono = faseAtual as Object;
            if (dono == null || !(faseAtual is ILembreteReinicio fase))
            {
                return "";
            }

            return fase.LembreteReinicio ?? "";
        }
    }

    /// <summary>Registra e recarrega a fase atual (a fase pode ter regra própria).</summary>
    public static void Executar(string origem)
    {
        if (!PodeReiniciar)
        {
            return;
        }

        Object dono = faseAtual as Object;
        if (faseAtual != null && dono != null)
        {
            faseAtual.Reiniciar(origem);
            return;
        }

        RegistroAprendizagem.RegistrarFalha("reinicio_manual", origem);
        TrocaDeCena.Recarregar();
    }

    /// <summary>Troca "{tecla}" pela tecla atual de Reiniciar fase (falas do Prof que citam a tecla).</summary>
    public static string ComTecla(string texto) =>
        string.IsNullOrEmpty(texto) ? texto : texto.Replace("{tecla}", NomeDaTecla());

    public static string[] ComTecla(string[] linhas)
    {
        if (linhas == null) return null;
        var copia = new string[linhas.Length];
        for (int i = 0; i < linhas.Length; i++) copia[i] = ComTecla(linhas[i]);
        return copia;
    }

    /// <summary>Texto da tecla atual da ação (ex.: "R"), pra chips e dicas.</summary>
    public static string NomeDaTecla()
    {
        string nome = InputManager.Instance != null ? InputManager.Instance.GetBindingName(InputManager.RestartActionName) : "";
        return string.IsNullOrEmpty(nome) ? "R" : nome;
    }
}
