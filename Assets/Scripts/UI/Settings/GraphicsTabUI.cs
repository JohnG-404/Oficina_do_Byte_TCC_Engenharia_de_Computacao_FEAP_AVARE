using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conteúdo da aba "Gráfico": resolução e tela cheia, usando o GameManager.
///
/// Reconhece a própria estrutura pelo nome dos filhos: se já existirem
/// (montados/ajustados no Editor via GameUIBuilder), apenas reconecta os
/// cliques; senão, constrói a estrutura padrão em runtime.
/// </summary>
public class GraphicsTabUI : MonoBehaviour
{
    private Button res720Button;
    private Button res1080Button;
    private Button fullscreenButton;
    private TMP_Text fullscreenLabel;

    private void Awake()
    {
        EnsureBuilt();
    }

    /// <summary>
    /// Constrói (ou reconecta, se já existir) a estrutura desta aba.
    /// Público para poder ser chamado explicitamente por uma ferramenta de
    /// Editor, já que Awake() nem sempre roda de forma síncrona fora do Play Mode.
    /// </summary>
    public void EnsureBuilt()
    {
        BuildOrFindStructure();
        WireEvents();
        RefreshFullscreenLabel();
    }

    private void OnEnable()
    {
        RefreshFullscreenLabel();
    }

    private void BuildOrFindStructure()
    {
        // Versão nova (linhas OpcaoConfigUI montadas pelo construtor do tema): cada
        // linha cuida de si, não há botões antigos pra reconectar nem criar.
        if (GetComponentInChildren<OpcaoConfigUI>(true) != null)
        {
            return;
        }

        Transform res720T = transform.Find("Resolucao720Button");
        Transform res1080T = transform.Find("Resolucao1080Button");
        Transform fullscreenT = transform.Find("FullscreenButton");

        if (res720T != null && res1080T != null && fullscreenT != null)
        {
            res720Button = res720T.GetComponent<Button>();
            res1080Button = res1080T.GetComponent<Button>();
            fullscreenButton = fullscreenT.GetComponent<Button>();
            fullscreenLabel = fullscreenT.GetComponentInChildren<TMP_Text>();
            return;
        }

        var res720GO = UIBuildUtils.CreateButtonWithLabel(transform, "Resolucao720Button", "1280 x 720", out _);
        res720Button = res720GO.GetComponent<Button>();

        var res1080GO = UIBuildUtils.CreateButtonWithLabel(transform, "Resolucao1080Button", "1920 x 1080", out _);
        res1080Button = res1080GO.GetComponent<Button>();

        var fullscreenGO = UIBuildUtils.CreateButtonWithLabel(transform, "FullscreenButton", "Tela Cheia", out fullscreenLabel);
        fullscreenButton = fullscreenGO.GetComponent<Button>();
    }

    private void WireEvents()
    {
        if (res720Button != null)
        {
            res720Button.onClick.RemoveListener(SetResolution720);
            res720Button.onClick.AddListener(SetResolution720);
        }

        if (res1080Button != null)
        {
            res1080Button.onClick.RemoveListener(SetResolution1080);
            res1080Button.onClick.AddListener(SetResolution1080);
        }

        if (fullscreenButton != null)
        {
            fullscreenButton.onClick.RemoveListener(ToggleFullscreen);
            fullscreenButton.onClick.AddListener(ToggleFullscreen);
        }
    }

    private void SetResolution720()
    {
        GameManager.Instance.AlterarResolucao(0);
    }

    private void SetResolution1080()
    {
        GameManager.Instance.AlterarResolucao(1);
    }

    private void ToggleFullscreen()
    {
        GameManager.Instance.AlterarFullscreen(GameManager.Instance.indiceModoTela == 2);
        RefreshFullscreenLabel();
    }

    private void RefreshFullscreenLabel()
    {
        if (fullscreenLabel != null)
        {
            bool cheia = GameManager.Instance != null ? GameManager.Instance.indiceModoTela != 2 : Screen.fullScreen;
            fullscreenLabel.text = cheia ? "Tela cheia: ligada" : "Tela cheia: desligada";
        }
    }
}
