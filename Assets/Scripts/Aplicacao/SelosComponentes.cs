using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mostra quais componentes o jogador já aplicou num objeto com "selos":
/// pequenos quadrados com o mesmo ícone do cartão do inventário, em fila
/// (como a lista de componentes do Inspector). Nos blocos os selos ficam
/// colados por dentro, no canto de cima à esquerda da parte visível; no
/// Player, acima da cabeça. Cada selo aparece com um "pop" quando a
/// ferramenta é aplicada.
///
/// Substitui os contornos coloridos: o objeto fica com o desenho limpo e a
/// informação fica num lugar só, sempre na mesma ordem
/// (Collider2D, Rigidbody2D, Movimento, Pulo).
///
/// Num PhysicsTarget que já começa com colisão, o Collider2D veio com o
/// cenário e não ganha selo.
///
/// Só lê o estado do IToolReceiver; não muda a física.
/// </summary>
public class SelosComponentes : MonoBehaviour
{
    private static readonly ToolType[] Ordem = { ToolType.Colisao, ToolType.Fisica, ToolType.Movimento, ToolType.Pulo };

    [Header("Aparência")]
    [Tooltip("Quadrado arredondado cheio (pintado com a cor do fundo).")]
    [SerializeField] private Sprite spriteFundo;
    [Tooltip("Borda do quadrado (pintada com a cor da ferramenta).")]
    [SerializeField] private Sprite spriteBorda;
    [SerializeField] private Color corFundo = new Color(0.117f, 0.141f, 0.2f, 0.95f);
    [Tooltip("Lado do selo, em unidades do mundo.")]
    [SerializeField] private float tamanho = 0.5f;
    [Tooltip("Quanto do selo o ícone ocupa.")]
    [Range(0.3f, 1f)]
    [SerializeField] private float tamanhoIcone = 0.9f;
    [SerializeField] private float espaco = 0.06f;
    [Tooltip("Distância entre o topo do desenho e a fila de selos.")]
    [SerializeField] private float alturaAcima = 0.12f;
    [SerializeField] private float tempoPop = 0.25f;
    [Tooltip("Nos blocos: distância entre a borda do bloco e os selos.")]
    [SerializeField] private float margemDentro = 0.14f;

    [Header("Ícones (os mesmos dos cartões do inventário)")]
    [SerializeField] private Sprite iconeColisao;
    [SerializeField] private Sprite iconeFisica;
    [SerializeField] private Sprite iconeMovimento;
    [SerializeField] private Sprite iconePulo;

    private class Selo
    {
        public Transform raiz;
        public float tempo;
    }

    private IToolReceiver receptor;
    private PhysicsTarget alvo;
    private readonly List<SpriteRenderer> desenho = new List<SpriteRenderer>();
    private readonly Dictionary<ToolType, Selo> selos = new Dictionary<ToolType, Selo>();
    private readonly List<ToolType> visiveis = new List<ToolType>();
    private Collider2D colisor;
    private string camada;
    private int ordem;

    private void Start()
    {
        receptor = GetComponent<IToolReceiver>();
        alvo = GetComponent<PhysicsTarget>();

        int ordemMaxima = int.MinValue;
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            // Só o desenho do objeto; contornos antigos, seta e selos ficam de fora.
            string nome = sr.gameObject.name;
            if (nome.Contains("Outline") || nome.StartsWith("Seta") || nome.StartsWith("Selo"))
            {
                continue;
            }

            desenho.Add(sr);
            if (sr.sortingOrder > ordemMaxima)
            {
                ordemMaxima = sr.sortingOrder;
                camada = sr.sortingLayerName;
            }
        }

        ordem = (ordemMaxima == int.MinValue ? 0 : ordemMaxima) + 5;
    }

    private void Update()
    {
        if (receptor == null)
        {
            return;
        }

        bool mudou = false;
        foreach (ToolType tool in Ordem)
        {
            bool mostrar = DeveMostrar(tool);
            bool estava = visiveis.Contains(tool);
            if (mostrar == estava)
            {
                continue;
            }

            mudou = true;
            if (mostrar)
            {
                Selo selo = ObterSelo(tool);
                if (selo == null)
                {
                    continue;
                }

                selo.raiz.gameObject.SetActive(true);
                selo.tempo = 0f;
                visiveis.Add(tool);
            }
            else
            {
                if (selos.TryGetValue(tool, out Selo selo))
                {
                    selo.raiz.gameObject.SetActive(false);
                }

                visiveis.Remove(tool);
            }
        }

        if (mudou)
        {
            visiveis.Sort((a, b) => System.Array.IndexOf(Ordem, a).CompareTo(System.Array.IndexOf(Ordem, b)));
        }

        if (visiveis.Count > 0)
        {
            Posicionar();
        }
    }

    private bool DeveMostrar(ToolType tool)
    {
        if (!receptor.HasTool(tool))
        {
            return false;
        }

        return !(tool == ToolType.Colisao && alvo != null && alvo.ComecaComColisao);
    }

    private Sprite IconeDe(ToolType tool)
    {
        switch (tool)
        {
            case ToolType.Colisao: return iconeColisao;
            case ToolType.Fisica: return iconeFisica;
            case ToolType.Movimento: return iconeMovimento;
            case ToolType.Pulo: return iconePulo;
            default: return null;
        }
    }

    private Selo ObterSelo(ToolType tool)
    {
        if (selos.TryGetValue(tool, out Selo existente))
        {
            return existente;
        }

        if (spriteFundo == null)
        {
            return null;
        }

        var raiz = new GameObject("Selo" + tool).transform;
        raiz.SetParent(transform, false);

        CriarCamada(raiz, "Fundo", spriteFundo, corFundo, 0, tamanho);
        if (spriteBorda != null)
        {
            CriarCamada(raiz, "Borda", spriteBorda, ToolColors.For(tool), 1, tamanho);
        }

        Sprite icone = IconeDe(tool);
        if (icone != null)
        {
            CriarCamada(raiz, "Icone", icone, Color.white, 2, tamanho * tamanhoIcone);
        }

        var selo = new Selo { raiz = raiz };
        selos[tool] = selo;
        return selo;
    }

    private void CriarCamada(Transform raiz, string nome, Sprite sprite, Color cor, int deslocamento, float lado)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(raiz, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = cor;
        if (camada != null)
        {
            sr.sortingLayerName = camada;
        }

        sr.sortingOrder = ordem + deslocamento;

        // Cabe num quadrado de "lado" unidades, sem distorcer.
        Vector2 tamanhoSprite = sprite.bounds.size;
        float maior = Mathf.Max(tamanhoSprite.x, tamanhoSprite.y);
        float escala = maior > 0f ? lado / maior : 1f;
        go.transform.localScale = new Vector3(escala, escala, 1f);
        go.transform.localPosition = -(Vector3)(Vector2)sprite.bounds.center * escala;
    }

    private void Posicionar()
    {
        Bounds b = new Bounds();
        bool primeiro = true;
        foreach (SpriteRenderer sr in desenho)
        {
            if (sr == null)
            {
                continue;
            }

            if (primeiro)
            {
                b = sr.bounds;
                primeiro = false;
            }
            else
            {
                b.Encapsulate(sr.bounds);
            }
        }

        if (primeiro)
        {
            b = new Bounds(transform.position, Vector3.zero);
        }

        float x;
        float y;
        if (alvo != null && PontoNoTopo(b, out Vector2 canto))
        {
            // Blocos: selos "colados" por dentro do bloco, logo abaixo da
            // superfície de cima, começando pela esquerda da parte visível.
            x = canto.x + tamanho * 0.5f;
            y = canto.y - margemDentro - tamanho * 0.5f;
        }
        else
        {
            // Player (ou bloco sem collider): fila centralizada acima do desenho.
            float largura = visiveis.Count * tamanho + (visiveis.Count - 1) * espaco;
            x = b.center.x - largura * 0.5f + tamanho * 0.5f;
            y = b.max.y + alturaAcima + tamanho * 0.5f;
        }

        Vector3 escalaPai = transform.lossyScale;

        foreach (ToolType tool in visiveis)
        {
            Selo selo = selos[tool];
            selo.raiz.position = new Vector3(x, y, transform.position.z);
            selo.raiz.rotation = Quaternion.identity;

            selo.tempo += Time.deltaTime;
            float s = EscalaPop(tempoPop > 0f ? selo.tempo / tempoPop : 1f);
            selo.raiz.localScale = new Vector3(
                s / Mathf.Max(0.0001f, Mathf.Abs(escalaPai.x)),
                s / Mathf.Max(0.0001f, Mathf.Abs(escalaPai.y)),
                1f);

            x += tamanho + espaco;
        }
    }

    /// <summary>
    /// Canto de cima à esquerda da parte do bloco que aparece na câmera:
    /// x = borda esquerda visível (com margem), y = topo do collider ali.
    /// </summary>
    private bool PontoNoTopo(Bounds b, out Vector2 canto)
    {
        canto = Vector2.zero;
        if (colisor == null)
        {
            colisor = GetComponent<Collider2D>();
            if (colisor == null)
            {
                return false;
            }
        }

        Bounds c = colisor.bounds;
        float esquerda = c.min.x;
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float meiaLargura = cam.orthographicSize * cam.aspect;
            esquerda = Mathf.Max(esquerda, cam.transform.position.x - meiaLargura);
        }

        float xTeste = esquerda + margemDentro + tamanho * 0.5f;
        if (xTeste > c.max.x)
        {
            return false;
        }

        // Desce do topo até encontrar o collider nessa coluna.
        for (float y = c.max.y - 0.01f; y > c.min.y; y -= 0.05f)
        {
            if (colisor.OverlapPoint(new Vector2(xTeste, y)))
            {
                canto = new Vector2(esquerda + margemDentro, y);
                return true;
            }
        }

        return false;
    }

    /// <summary>Cresce até 120% e volta para 100%.</summary>
    private static float EscalaPop(float t)
    {
        if (t >= 1f)
        {
            return 1f;
        }

        return t < 0.6f ? Mathf.Lerp(0f, 1.2f, t / 0.6f) : Mathf.Lerp(1.2f, 1f, (t - 0.6f) / 0.4f);
    }
}
