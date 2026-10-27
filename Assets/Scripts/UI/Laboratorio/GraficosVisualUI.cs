using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Só visual, na aba Gráficos (tema Laboratório): mostra o interruptor
/// ligado/desligado da Tela Cheia e marca qual resolução está em uso. Quem
/// muda a resolução e a tela cheia continua sendo o GraphicsTabUI/GameManager.
/// </summary>
public class GraficosVisualUI : MonoBehaviour
{
    [Header("Tela cheia")]
    [SerializeField] private Image interruptor;
    [SerializeField] private Sprite spriteLigado;
    [SerializeField] private Sprite spriteDesligado;

    [Header("Resolução em uso (mesma ordem do GameManager: 0 = 1280x720, 1 = 1920x1080)")]
    [SerializeField] private GameObject[] marcasResolucao;

    private void Update()
    {
        if (interruptor != null)
        {
            Sprite s = Screen.fullScreen ? spriteLigado : spriteDesligado;
            if (s != null && interruptor.sprite != s) interruptor.sprite = s;
        }

        if (marcasResolucao != null && GameManager.Instance != null)
        {
            for (int i = 0; i < marcasResolucao.Length; i++)
            {
                if (marcasResolucao[i] == null) continue;
                Vector2Int r = GameManager.Instance.ResolucaoAtual;
                bool ativa = i == 0 ? r == new Vector2Int(1280, 720) : r == new Vector2Int(1920, 1080);
                if (marcasResolucao[i].activeSelf != ativa) marcasResolucao[i].SetActive(ativa);
            }
        }
    }
}
