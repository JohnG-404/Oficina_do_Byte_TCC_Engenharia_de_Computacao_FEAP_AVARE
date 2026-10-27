using System;
using UnityEngine;

/// <summary>
/// Opções de qualidade de vida das Configurações (abas Gráficos, Áudio e Jogo),
/// gravadas em PlayerPrefs. Resolução e modo de tela ficam no GameManager.
///
/// Um objeto escondido (criado sozinho ao abrir qualquer cena) aplica a taxa
/// de quadros e silencia o som quando a janela perde o foco.
/// </summary>
public class ConfiguracoesJogo : MonoBehaviour
{
    /// <summary>Disparado sempre que alguma opção muda.</summary>
    public static event Action OnMudou;

    // ------------------------------------------------------------------
    // Gráficos
    // ------------------------------------------------------------------

    public static readonly string[] NomesTaxaQuadros = { "VSync", "30 FPS", "60 FPS", "120 FPS", "144 FPS", "Sem limite" };
    private static readonly int[] valoresTaxaQuadros = { 0, 30, 60, 120, 144, -1 };

    /// <summary>Índice em <see cref="NomesTaxaQuadros"/>. Padrão: VSync.</summary>
    public static int TaxaQuadros
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("Config_TaxaQuadros", 0), 0, NomesTaxaQuadros.Length - 1);
        set { Gravar("Config_TaxaQuadros", Mathf.Clamp(value, 0, NomesTaxaQuadros.Length - 1)); AplicarTaxaQuadros(); }
    }

    /// <summary>Mantém a imagem do jogo em 16:9 com faixas pretas nas sobras. Padrão: ligado.</summary>
    public static bool ManterProporcao
    {
        get => PlayerPrefs.GetInt("Config_ManterProporcao", 1) == 1;
        set => Gravar("Config_ManterProporcao", value ? 1 : 0);
    }

    // ------------------------------------------------------------------
    // Áudio
    // ------------------------------------------------------------------

    /// <summary>Pausa todo o som enquanto a janela do jogo não está em foco. Padrão: desligado.</summary>
    public static bool SilenciarSemFoco
    {
        get => PlayerPrefs.GetInt("Config_SilenciarSemFoco", 0) == 1;
        set
        {
            Gravar("Config_SilenciarSemFoco", value ? 1 : 0);
            if (!value) AudioListener.pause = false;
        }
    }

    // ------------------------------------------------------------------
    // Jogo
    // ------------------------------------------------------------------

    public static readonly string[] NomesVelocidadeTexto = { "Lenta", "Normal", "Rápida", "Instantânea" };
    private static readonly float[] fatoresVelocidadeTexto = { 2f, 1f, 0.45f, 0f };

    /// <summary>Índice em <see cref="NomesVelocidadeTexto"/>. Padrão: Normal.</summary>
    public static int VelocidadeTexto
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("Config_VelocidadeTexto", 1), 0, NomesVelocidadeTexto.Length - 1);
        set => Gravar("Config_VelocidadeTexto", Mathf.Clamp(value, 0, NomesVelocidadeTexto.Length - 1));
    }

    /// <summary>Multiplica o tempo entre letras dos diálogos (0 = linha inteira de uma vez).</summary>
    public static float FatorVelocidadeTexto => fatoresVelocidadeTexto[VelocidadeTexto];

    /// <summary>Balões "aperte a tecla" sobre o jogador. Padrão: ligado.</summary>
    public static bool DicasTecla
    {
        get => PlayerPrefs.GetInt("Config_DicasTecla", 1) == 1;
        set => Gravar("Config_DicasTecla", value ? 1 : 0);
    }

    /// <summary>Contador de quadros por segundo no canto da tela. Padrão: desligado.</summary>
    public static bool MostrarFPS
    {
        get => PlayerPrefs.GetInt("Config_MostrarFPS", 0) == 1;
        set => Gravar("Config_MostrarFPS", value ? 1 : 0);
    }

    /// <summary>
    /// Prende o mouse dentro da janela do jogo (tela cheia sem bordas ou janela),
    /// pra ele não escapar pro outro monitor. Padrão: desligado.
    /// </summary>
    public static bool PrenderMouse
    {
        get => PlayerPrefs.GetInt("Config_PrenderMouse", 0) == 1;
        set { Gravar("Config_PrenderMouse", value ? 1 : 0); AplicarMouse(); }
    }

    // ------------------------------------------------------------------

    private static ConfiguracoesJogo instancia;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instancia = null;
        OnMudou = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (instancia != null) return;

        var go = new GameObject("ConfiguracoesJogo (Auto)");
        go.hideFlags = HideFlags.HideInHierarchy;
        instancia = go.AddComponent<ConfiguracoesJogo>();
        DontDestroyOnLoad(go);
        AplicarTaxaQuadros();
        AplicarMouse();
    }

    /// <summary>
    /// "Restaurar padrão" da aba Gráficos: taxa de quadros e proporção.
    /// Resolução e modo de tela ficam como estão (dependem do monitor).
    /// </summary>
    public static void RestaurarGraficos()
    {
        PlayerPrefs.DeleteKey("Config_TaxaQuadros");
        PlayerPrefs.DeleteKey("Config_ManterProporcao");
        PlayerPrefs.Save();
        AplicarTaxaQuadros();
        OnMudou?.Invoke();
    }

    /// <summary>"Restaurar padrão" da aba Jogo: texto, dicas, contador de FPS e mouse.</summary>
    public static void RestaurarJogo()
    {
        PlayerPrefs.DeleteKey("Config_VelocidadeTexto");
        PlayerPrefs.DeleteKey("Config_DicasTecla");
        PlayerPrefs.DeleteKey("Config_MostrarFPS");
        PlayerPrefs.DeleteKey("Config_PrenderMouse");
        PlayerPrefs.Save();
        AplicarMouse();
        OnMudou?.Invoke();
    }

    private static void AplicarMouse()
    {
        Cursor.lockState = PrenderMouse ? CursorLockMode.Confined : CursorLockMode.None;
    }

    private static void Gravar(string chave, int valor)
    {
        PlayerPrefs.SetInt(chave, valor);
        PlayerPrefs.Save();
        OnMudou?.Invoke();
    }

    private static void AplicarTaxaQuadros()
    {
        int v = valoresTaxaQuadros[TaxaQuadros];
        QualitySettings.vSyncCount = v == 0 ? 1 : 0;
        Application.targetFrameRate = v == 0 ? -1 : v;
    }

    private void OnApplicationFocus(bool emFoco)
    {
        if (SilenciarSemFoco) AudioListener.pause = !emFoco;
        // O Windows solta o mouse quando a janela perde o foco; prende de novo ao voltar.
        if (emFoco) AplicarMouse();
    }
}
