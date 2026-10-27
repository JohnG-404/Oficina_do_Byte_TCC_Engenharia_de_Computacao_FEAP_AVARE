using UnityEngine;

/// <summary>
/// Peça já encaixada no diagrama do puzzle de Depuração (fase IA): não dá
/// pra arrastar, mas mostra a descrição no painel "Descrição" ao passar o
/// mouse, como as peças normais (PieceHoverInfo).
///
/// A peça da regra errada pode ser destacada em vermelho
/// (<see cref="Destacar"/>) — o IAFlow faz isso só depois de 2 erros, pra
/// o jogador tentar achar o problema observando o guarda primeiro.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IAPecaFixa : MonoBehaviour
{
    [SerializeField] private string titulo;
    [TextArea(2, 5)]
    [SerializeField] private string descricao;
    [Tooltip("Descrição usada depois de destacada (ex: 'Esta é a regra errada...'). Vazio = mantém a normal.")]
    [TextArea(2, 5)]
    [SerializeField] private string descricaoDestacada;
    [SerializeField] private Sprite icone;
    [Tooltip("Sprite de fundo da peça (pisca em vermelho quando destacada).")]
    [SerializeField] private SpriteRenderer fundo;
    [SerializeField] private Color corDestaque = new Color(1f, 0.35f, 0.35f, 1f);

    private bool destacada;
    private Color corOriginal = Color.white;

    public bool Destacada => destacada;

    private void Awake()
    {
        if (fundo == null)
        {
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!sr.gameObject.name.StartsWith("icone"))
                {
                    fundo = sr;
                    break;
                }
            }
        }

        if (fundo != null)
        {
            corOriginal = fundo.color;
        }

        if (icone == null)
        {
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.gameObject.name.StartsWith("icone") && sr.sprite != null)
                {
                    icone = sr.sprite;
                    break;
                }
            }
        }
    }

    public void Destacar(bool ligar)
    {
        destacada = ligar;
        if (!ligar && fundo != null)
        {
            fundo.color = corOriginal;
        }
    }

    private void Update()
    {
        if (!destacada || fundo == null)
        {
            return;
        }

        float k = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
        fundo.color = Color.Lerp(corOriginal, corDestaque, k);
    }

    private void OnMouseEnter()
    {
        if (InfoPanelUI.Instance == null)
        {
            return;
        }

        string texto = destacada && !string.IsNullOrEmpty(descricaoDestacada) ? descricaoDestacada : descricao;
        InfoPanelUI.Instance.ShowDescription(titulo, texto, icone);
    }

    private void OnMouseExit()
    {
        InfoPanelUI.Instance?.Clear();
    }
}
