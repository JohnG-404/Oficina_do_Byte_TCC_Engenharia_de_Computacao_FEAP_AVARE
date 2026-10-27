using TMPro;
using UnityEngine;

/// <summary>
/// Desenha na tela o contorno do BoxCollider2D do personagem (que no jogo é
/// invisível), com uma etiqueta "collider" em cima. Usado na fase "Animacao"
/// durante o teste do agachar: o jogador vê o corpo (collider) abaixar
/// enquanto o desenho continua em pé. O AnimacaoLevelFlow liga e desliga
/// este objeto (filho do Player na cena).
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ContornoCollider : MonoBehaviour
{
    [Tooltip("Collider desenhado. Vazio = o BoxCollider2D do objeto pai.")]
    [SerializeField] private BoxCollider2D alvo;
    [Tooltip("Desenho do personagem. A etiqueta fica acima do mais alto entre o desenho e o collider, pra não cobrir o rosto. Vazio = o SpriteRenderer do objeto pai.")]
    [SerializeField] private SpriteRenderer desenho;
    [Tooltip("Espaço vazio (transparente) no topo da imagem do desenho, descontado na altura da etiqueta.")]
    [SerializeField] private float folgaTopoDesenho = 0.45f;
    [Tooltip("Etiqueta mostrada em cima do contorno (opcional).")]
    [SerializeField] private TextMeshPro etiqueta;
    [Tooltip("Folga acima do contorno para a etiqueta.")]
    [SerializeField] private float alturaEtiqueta = 0.28f;
    [Tooltip("Pisca suave pra chamar atenção (0 = sem piscar).")]
    [SerializeField] private float velocidadePiscar = 3f;

    private LineRenderer linha;
    private Color corBase;
    private readonly Vector3[] pontos = new Vector3[4];

    private void Awake()
    {
        linha = GetComponent<LineRenderer>();
        linha.useWorldSpace = true;
        linha.loop = true;
        linha.positionCount = 4;
        corBase = linha.startColor;
        if (alvo == null && transform.parent != null)
        {
            alvo = transform.parent.GetComponent<BoxCollider2D>();
        }

        if (desenho == null && transform.parent != null)
        {
            desenho = transform.parent.GetComponentInChildren<SpriteRenderer>();
        }

        if (etiqueta != null)
        {
            // Contorno escuro nas letras pra ler em cima de qualquer fundo.
            etiqueta.outlineWidth = 0.25f;
            etiqueta.outlineColor = new Color32(10, 14, 23, 255);
        }
    }

    private void LateUpdate()
    {
        if (alvo == null)
        {
            return;
        }

        Bounds b = alvo.bounds;
        pontos[0] = new Vector3(b.min.x, b.min.y, 0f);
        pontos[1] = new Vector3(b.min.x, b.max.y, 0f);
        pontos[2] = new Vector3(b.max.x, b.max.y, 0f);
        pontos[3] = new Vector3(b.max.x, b.min.y, 0f);
        linha.SetPositions(pontos);

        if (velocidadePiscar > 0f)
        {
            Color c = corBase;
            c.a = corBase.a * (0.85f + 0.15f * Mathf.Sin(Time.time * velocidadePiscar));
            linha.startColor = c;
            linha.endColor = c;
        }

        if (etiqueta != null)
        {
            float topo = b.max.y;
            if (desenho != null)
            {
                topo = Mathf.Max(topo, desenho.bounds.max.y - folgaTopoDesenho);
            }

            etiqueta.transform.position = new Vector3(b.center.x, topo + alturaEtiqueta, 0f);
        }
    }
}
