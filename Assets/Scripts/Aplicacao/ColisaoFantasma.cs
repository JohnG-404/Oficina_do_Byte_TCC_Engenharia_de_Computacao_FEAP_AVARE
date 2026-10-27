using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mostra no próprio bloco se ele já tem colisão (modelo "fantasma"):
/// - Sem Collider2D: sprites com pouca opacidade e um contorno tracejado que
///   segue o formato real do collider (o mesmo que a física usa; numa escada,
///   os degraus).
/// - Com Collider2D (aplicado pelo jogador ou já vindo do cenário): o bloco
///   "se materializa" (opacidade sobe em ~0,2 s) e o tracejado some.
/// - Com Rigidbody2D: uma seta azul embaixo do bloco.
///
/// Quais componentes o jogador aplicou aparece nos selos acima do objeto
/// (ver SelosComponentes), não em contornos.
///
/// Só lê o estado do PhysicsTarget; não muda a física.
/// </summary>
[RequireComponent(typeof(PhysicsTarget))]
public class ColisaoFantasma : MonoBehaviour
{
    [Header("Fantasma (sem Collider2D)")]
    [Range(0f, 1f)]
    [SerializeField] private float opacidadeSemColisao = 0.6f;
    [Tooltip("Tempo para o bloco ficar opaco quando recebe o Collider2D.")]
    [SerializeField] private float tempoMaterializar = 0.2f;

    [Header("Tracejado")]
    [Tooltip("Material com a textura tracejada (repetida ao longo da linha).")]
    [SerializeField] private Material materialTracejado;
    [SerializeField] private Color corTracejado = new Color(0.2f, 0.255f, 0.333f, 0.95f);
    [SerializeField] private float larguraTracejado = 0.06f;
    [Tooltip("Comprimento (em unidades do mundo) de um traço + um espaço.")]
    [SerializeField] private float tamanhoTraco = 0.36f;

    [Header("Seta do Rigidbody2D")]
    [SerializeField] private Sprite spriteSeta;
    [SerializeField] private float distanciaSeta = 0.12f;

    private PhysicsTarget alvo;
    private readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>();
    private readonly List<Color> coresOriginais = new List<Color>();
    private readonly List<LineRenderer> linhas = new List<LineRenderer>();
    private SpriteRenderer seta;
    private float opacidadeAtual;
    private bool tinhaFisica;

    private void Start()
    {
        alvo = GetComponent<PhysicsTarget>();

        // Só os sprites do desenho (sem selos nem contornos antigos).
        int ordemMaxima = int.MinValue;
        string camada = null;
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            string nome = sr.gameObject.name;
            if (nome.Contains("Outline") || nome.StartsWith("Selo"))
            {
                continue;
            }

            sprites.Add(sr);
            coresOriginais.Add(sr.color);
            if (sr.sortingOrder > ordemMaxima)
            {
                ordemMaxima = sr.sortingOrder;
                camada = sr.sortingLayerName;
            }
        }

        if (ordemMaxima == int.MinValue)
        {
            ordemMaxima = 0;
        }

        CriarTracejado(CaminhosDoCollider(), camada, ordemMaxima + 3);
        CriarSeta(camada, ordemMaxima + 1);

        opacidadeAtual = alvo.HasCollision ? 1f : opacidadeSemColisao;
        AplicarOpacidade(opacidadeAtual);
        AtualizarTracejado(!alvo.HasCollision);
        AtualizarSeta();
    }

    private void Update()
    {
        if (alvo == null)
        {
            return;
        }

        float desejada = alvo.HasCollision ? 1f : opacidadeSemColisao;
        if (!Mathf.Approximately(opacidadeAtual, desejada))
        {
            float passo = tempoMaterializar > 0f ? (1f - opacidadeSemColisao) * Time.deltaTime / tempoMaterializar : 1f;
            opacidadeAtual = Mathf.MoveTowards(opacidadeAtual, desejada, passo);
            AplicarOpacidade(opacidadeAtual);
            AtualizarTracejado(!alvo.HasCollision);
        }

        if (tinhaFisica != alvo.HasPhysics)
        {
            AtualizarSeta();
        }

        if (seta != null && seta.enabled)
        {
            PosicionarSeta();
        }
    }

    private void AplicarOpacidade(float a)
    {
        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] == null)
            {
                continue;
            }

            Color c = coresOriginais[i];
            c.a *= a;
            sprites[i].color = c;
        }
    }

    private LineRenderer CriarLinha(string nome, List<Vector2> caminho, Material mat, float largura, Color cor, string camada, int ordem, bool tracejada)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = caminho.Count;
        for (int i = 0; i < caminho.Count; i++)
        {
            lr.SetPosition(i, caminho[i]);
        }

        lr.sharedMaterial = mat;
        lr.textureMode = tracejada ? LineTextureMode.Tile : LineTextureMode.Stretch;
        if (tracejada)
        {
            lr.textureScale = new Vector2(1f / Mathf.Max(0.01f, tamanhoTraco), 1f);
        }

        lr.widthMultiplier = largura;
        lr.startColor = lr.endColor = cor;
        lr.numCornerVertices = 0;
        lr.alignment = LineAlignment.TransformZ;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        if (camada != null)
        {
            lr.sortingLayerName = camada;
        }

        lr.sortingOrder = ordem;
        lr.enabled = false;
        return lr;
    }

    // ------------------------------------------------------------ tracejado

    private void CriarTracejado(List<List<Vector2>> caminhos, string camada, int ordem)
    {
        if (materialTracejado == null)
        {
            return;
        }

        // O traço precisa repetir ao longo da linha.
        if (materialTracejado.mainTexture != null)
        {
            materialTracejado.mainTexture.wrapMode = TextureWrapMode.Repeat;
        }

        foreach (List<Vector2> caminho in caminhos)
        {
            LineRenderer lr = CriarLinha("Tracejado", caminho, materialTracejado, larguraTracejado, corTracejado, camada, ordem, true);
            lr.enabled = true;
            linhas.Add(lr);
        }
    }

    /// <summary>Contorno do collider em coordenadas locais deste objeto.</summary>
    private List<List<Vector2>> CaminhosDoCollider()
    {
        var caminhos = new List<List<Vector2>>();

        var composite = GetComponent<CompositeCollider2D>();
        if (composite != null)
        {
            if (composite.pathCount == 0)
            {
                composite.GenerateGeometry();
            }

            var pontos = new List<Vector2>();
            for (int p = 0; p < composite.pathCount; p++)
            {
                pontos.Clear();
                composite.GetPath(p, pontos);
                List<Vector2> limpo = Simplificar(pontos);
                if (limpo.Count >= 3)
                {
                    caminhos.Add(limpo);
                }
            }

            return caminhos;
        }

        var poligono = GetComponent<PolygonCollider2D>();
        if (poligono != null)
        {
            for (int p = 0; p < poligono.pathCount; p++)
            {
                var caminho = new List<Vector2>();
                foreach (Vector2 v in poligono.GetPath(p))
                {
                    caminho.Add(v + poligono.offset);
                }

                caminhos.Add(caminho);
            }

            return caminhos;
        }

        var caixa = GetComponent<BoxCollider2D>();
        if (caixa != null)
        {
            Vector2 h = caixa.size * 0.5f;
            Vector2 o = caixa.offset;
            caminhos.Add(new List<Vector2> { o + new Vector2(-h.x, -h.y), o + new Vector2(-h.x, h.y), o + new Vector2(h.x, h.y), o + new Vector2(h.x, -h.y) });
        }

        return caminhos;
    }

    /// <summary>
    /// Tira os cantinhos chanfrados (pontos quase repetidos) e os pontos no
    /// meio de retas que o CompositeCollider2D gera. Sem isso, a linha faz
    /// "bicos" nas quinas.
    /// </summary>
    private static List<Vector2> Simplificar(List<Vector2> pontos)
    {
        const float distanciaMinima = 0.05f;
        var semRepetidos = new List<Vector2>();
        foreach (Vector2 p in pontos)
        {
            if (semRepetidos.Count == 0 || Vector2.Distance(semRepetidos[semRepetidos.Count - 1], p) >= distanciaMinima)
            {
                semRepetidos.Add(p);
            }
        }

        if (semRepetidos.Count > 1 && Vector2.Distance(semRepetidos[0], semRepetidos[semRepetidos.Count - 1]) < distanciaMinima)
        {
            semRepetidos.RemoveAt(semRepetidos.Count - 1);
        }

        // Quinas de 90° com chanfro viram um ponto só; pontos alinhados somem.
        var resultado = new List<Vector2>();
        int n = semRepetidos.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 anterior = semRepetidos[(i - 1 + n) % n];
            Vector2 atual = semRepetidos[i];
            Vector2 proximo = semRepetidos[(i + 1) % n];
            Vector2 a = (atual - anterior).normalized;
            Vector2 b = (proximo - atual).normalized;
            if (Mathf.Abs(a.x * b.y - a.y * b.x) > 0.02f)
            {
                resultado.Add(atual);
            }
        }

        return resultado.Count >= 3 ? resultado : semRepetidos;
    }

    private void AtualizarTracejado(bool visivel)
    {
        foreach (LineRenderer lr in linhas)
        {
            if (lr != null)
            {
                lr.enabled = visivel;
            }
        }
    }

    // ----------------------------------------------------------------- seta

    private void CriarSeta(string camada, int ordem)
    {
        if (spriteSeta == null)
        {
            return;
        }

        var go = new GameObject("SetaRigidbody2D");
        go.transform.SetParent(transform, false);
        seta = go.AddComponent<SpriteRenderer>();
        seta.sprite = spriteSeta;
        seta.color = ToolColors.Fisica;
        if (camada != null)
        {
            seta.sortingLayerName = camada;
        }

        seta.sortingOrder = ordem;
        seta.enabled = false;
    }

    private void AtualizarSeta()
    {
        tinhaFisica = alvo.HasPhysics;
        if (seta == null)
        {
            return;
        }

        seta.enabled = tinhaFisica;
        if (tinhaFisica)
        {
            PosicionarSeta();
        }
    }

    private void PosicionarSeta()
    {
        // Centro de baixo do desenho (não do collider, que pode estar maior).
        Bounds b = new Bounds();
        bool primeiro = true;
        foreach (SpriteRenderer sr in sprites)
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
            return;
        }

        float meiaAltura = seta.sprite.bounds.extents.y;
        seta.transform.position = new Vector3(b.center.x, b.min.y - distanciaSeta - meiaAltura, transform.position.z);
        seta.transform.rotation = Quaternion.identity;
    }
}
