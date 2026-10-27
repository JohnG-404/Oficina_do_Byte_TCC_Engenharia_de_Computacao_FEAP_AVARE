using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolução e modo de tela do jogo, gravados em PlayerPrefs.
///
/// A lista de resoluções vem do monitor do jogador (Screen.resolutions), então
/// qualquer resolução que a placa de vídeo aceitar aparece nas Configurações.
/// Quem garante que o jogo fica certo em qualquer proporção é o
/// AjusteProporcaoCamera (faixas pretas) e os Canvas Scalers em modo Expand.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    /// <summary>Disparado depois de mudar resolução ou modo de tela.</summary>
    public static event Action OnTelaMudou;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnTelaMudou = null;
    }

    public static readonly string[] NomesModoTela = { "Tela cheia", "Tela cheia exclusiva", "Janela" };

    private static readonly FullScreenMode[] modos =
    {
        FullScreenMode.FullScreenWindow,
        FullScreenMode.ExclusiveFullScreen,
        FullScreenMode.Windowed
    };

    // Resoluções menores que isso deixam o texto ilegível.
    private const int LarguraMinima = 1024;
    private const int AlturaMinima = 576;

    private readonly List<Vector2Int> resolucoes = new List<Vector2Int>();

    public IReadOnlyList<Vector2Int> Resolucoes => resolucoes;
    public int indiceResolucao;
    public int indiceModoTela;

    public Vector2Int ResolucaoAtual => resolucoes.Count > 0 ? resolucoes[indiceResolucao] : new Vector2Int(Screen.width, Screen.height);

    // Garante que exista um GameManager mesmo em cenas testadas isoladamente
    // (sem passar pelo Menu), mesmo padrão de bootstrap do InputManager/AudioManager.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("GameManager (Auto)");
        go.AddComponent<GameManager>();
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CarregarConfiguracoes();
    }

    private void MontarListaResolucoes()
    {
        resolucoes.Clear();

        foreach (Resolution r in Screen.resolutions)
        {
            var v = new Vector2Int(r.width, r.height);
            if (v.x >= LarguraMinima && v.y >= AlturaMinima && !resolucoes.Contains(v))
            {
                resolucoes.Add(v);
            }
        }

        Vector2Int nativa = ResolucaoNativa();
        if (!resolucoes.Contains(nativa)) resolucoes.Add(nativa);

        if (resolucoes.Count == 1)
        {
            foreach (var v in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
            {
                if (!resolucoes.Contains(v)) resolucoes.Add(v);
            }
        }

        resolucoes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
    }

    private static Vector2Int ResolucaoNativa()
    {
        int w = Display.main.systemWidth, h = Display.main.systemHeight;
        if (w <= 0 || h <= 0) { w = Screen.currentResolution.width; h = Screen.currentResolution.height; }
        return new Vector2Int(w, h);
    }

    void CarregarConfiguracoes()
    {
        MontarListaResolucoes();

        Vector2Int alvo;
        if (PlayerPrefs.HasKey("ResolucaoLargura"))
        {
            alvo = new Vector2Int(PlayerPrefs.GetInt("ResolucaoLargura"), PlayerPrefs.GetInt("ResolucaoAltura"));
        }
        else if (PlayerPrefs.HasKey("Resolucao"))
        {
            // Configuração antiga: só existiam 0 = 1280x720 e 1 = 1920x1080.
            alvo = PlayerPrefs.GetInt("Resolucao") == 1 ? new Vector2Int(1920, 1080) : new Vector2Int(1280, 720);
        }
        else
        {
            alvo = ResolucaoNativa();
        }

        indiceResolucao = IndiceMaisProximo(alvo);

        if (PlayerPrefs.HasKey("ModoTela"))
        {
            indiceModoTela = Mathf.Clamp(PlayerPrefs.GetInt("ModoTela"), 0, modos.Length - 1);
        }
        else
        {
            indiceModoTela = PlayerPrefs.GetInt("Fullscreen", 1) == 1 ? 0 : 2;
        }

        Aplicar(false);
    }

    private int IndiceMaisProximo(Vector2Int alvo)
    {
        int melhor = 0;
        long menor = long.MaxValue;
        for (int i = 0; i < resolucoes.Count; i++)
        {
            long dx = resolucoes[i].x - alvo.x, dy = resolucoes[i].y - alvo.y;
            long d = dx * dx + dy * dy;
            if (d < menor) { menor = d; melhor = i; }
        }
        return melhor;
    }

    private void Aplicar(bool salvar)
    {
        Vector2Int r = ResolucaoAtual;
        Screen.SetResolution(r.x, r.y, modos[indiceModoTela]);

        if (salvar)
        {
            PlayerPrefs.SetInt("ResolucaoLargura", r.x);
            PlayerPrefs.SetInt("ResolucaoAltura", r.y);
            PlayerPrefs.SetInt("ModoTela", indiceModoTela);
            PlayerPrefs.SetInt("Fullscreen", indiceModoTela == 2 ? 0 : 1);
            PlayerPrefs.Save();
        }

        OnTelaMudou?.Invoke();
    }

    /// <summary>Escolhe uma resolução da lista <see cref="Resolucoes"/>.</summary>
    public void DefinirIndiceResolucao(int indice)
    {
        indiceResolucao = Mathf.Clamp(indice, 0, resolucoes.Count - 1);
        Aplicar(true);
    }

    /// <summary>Usa a resolução da lista mais próxima de largura x altura.</summary>
    public void DefinirResolucao(int largura, int altura)
    {
        DefinirIndiceResolucao(IndiceMaisProximo(new Vector2Int(largura, altura)));
    }

    /// <summary>0 = tela cheia, 1 = tela cheia exclusiva, 2 = janela.</summary>
    public void DefinirModoTela(int indice)
    {
        indiceModoTela = Mathf.Clamp(indice, 0, modos.Length - 1);
        Aplicar(true);
    }

    /// <summary>Compatibilidade com os botões antigos: 0 = 1280x720, 1 = 1920x1080.</summary>
    public void AlterarResolucao(int indice)
    {
        if (indice == 1) DefinirResolucao(1920, 1080);
        else DefinirResolucao(1280, 720);
    }

    public void AlterarFullscreen(bool valor)
    {
        DefinirModoTela(valor ? 0 : 2);
    }
}
