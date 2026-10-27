using TMPro;
using UnityEngine;

/// <summary>
/// Dica de uma linha que aparece embaixo do cartão do inventário quando o
/// mouse passa em cima (ver DraggableToolIcon). Fica num objeto próprio do
/// Canvas (sempre ativo, sem gráfico); a caixa visível é um filho que só
/// aparece em Mostrar.
/// </summary>
public class InventarioDica : MonoBehaviour
{
    public static InventarioDica Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    [Tooltip("Caixa visível da dica (filho deste objeto).")]
    [SerializeField] private RectTransform caixa;
    [SerializeField] private TMP_Text titulo;
    [SerializeField] private TMP_Text texto;
    [Tooltip("Borda tingida com a cor da ferramenta.")]
    [SerializeField] private UnityEngine.UI.Image borda;
    [Tooltip("Distância (em pixels da tela de referência) entre o cartão e a dica.")]
    [SerializeField] private float espaco = 10f;

    private void Awake()
    {
        Instance = this;
        if (caixa != null)
        {
            caixa.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void Mostrar(string nome, string descricao, Color cor, RectTransform cartao)
    {
        if (Instance == null || Instance.caixa == null || string.IsNullOrEmpty(descricao))
        {
            return;
        }

        Instance.MostrarInterno(nome, descricao, cor, cartao);
    }

    public static void Esconder()
    {
        if (Instance != null && Instance.caixa != null)
        {
            Instance.caixa.gameObject.SetActive(false);
        }
    }

    private void MostrarInterno(string nome, string descricao, Color cor, RectTransform cartao)
    {
        if (titulo != null)
        {
            titulo.text = nome;
            titulo.color = cor;
        }

        if (texto != null)
        {
            texto.text = descricao;
        }

        if (borda != null)
        {
            borda.color = cor;
        }

        // Canto superior esquerdo da dica logo abaixo do canto inferior
        // esquerdo do cartão, em coordenadas do pai da dica.
        Vector3[] cantos = new Vector3[4];
        cartao.GetWorldCorners(cantos);
        var pai = (RectTransform)caixa.parent;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(pai, RectTransformUtility.WorldToScreenPoint(null, cantos[0]), null, out local);
        caixa.pivot = new Vector2(0f, 1f);
        caixa.localPosition = new Vector3(local.x, local.y - espaco, 0f);

        caixa.gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }
}
