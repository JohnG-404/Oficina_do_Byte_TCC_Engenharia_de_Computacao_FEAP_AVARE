using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Linguagem visual da fase Aplicacao_Fisica: o próprio objeto mostra quais
/// componentes tem, sem contornos, selos ou setas.
///
/// - Sem Collider2D: o objeto é um holograma (linhas azuis com vãos). Dá
///   para ver que ele está ali, mas também que não é sólido.
/// - Recebeu Collider2D: vira matéria. A matéria desce do topo até a base,
///   com uma faixa de luz, e o objeto volta ao desenho normal.
/// - Recebeu Rigidbody2D: ganha peso. Amassa e volta como uma mola, e cada
///   pouso forte levanta poeira. Sem Collider2D, ele cai atravessando tudo,
///   que é o próprio comportamento do Rigidbody2D sem collider.
/// - Arrastando um cartão por cima: prévia do que vai acontecer (o
///   holograma acende com Collider2D; o objeto afunda um pouco com
///   Rigidbody2D).
///
/// Funciona em qualquer IToolReceiver (blocos e Player). Só lê o estado; não
/// muda a física. Fora dos efeitos, os sprites voltam ao material original.
/// </summary>
public class Materializacao : MonoBehaviour
{
    [Tooltip("Material com o shader Aplicacao/Materializacao.")]
    [SerializeField] private Material material;

    [Header("Collider2D: holograma vira matéria")]
    [SerializeField] private float tempoMaterializar = 0.55f;

    [Header("Rigidbody2D: peso")]
    [Tooltip("Quanto o objeto amassa quando recebe o Rigidbody2D.")]
    [SerializeField] private float amassarAoGanharPeso = 0.18f;
    [SerializeField] private float tempoMola = 0.5f;
    [Tooltip("Velocidade de impacto (unidades/s) a partir da qual o pouso levanta poeira.")]
    [SerializeField] private float impactoPoeira = 3f;
    [SerializeField] private Sprite spritePoeira;
    [SerializeField] private Color corPoeira = new Color(0.62f, 0.65f, 0.7f, 0.9f);

    [Header("Prévia ao arrastar um cartão")]
    [SerializeField] private float brilhoPrevia = 0.45f;
    [SerializeField] private float afundarPrevia = 0.05f;

    private static readonly int IdMateria = Shader.PropertyToID("_Materia");
    private static readonly int IdTopo = Shader.PropertyToID("_TopoY");
    private static readonly int IdBase = Shader.PropertyToID("_BaseY");
    private static readonly int IdCentro = Shader.PropertyToID("_CentroX");
    private static readonly int IdAmassar = Shader.PropertyToID("_Amassar");
    private static readonly int IdBrilho = Shader.PropertyToID("_Brilho");

    private IToolReceiver receptor;
    private readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>();
    private readonly List<Material> materiaisOriginais = new List<Material>();
    private MaterialPropertyBlock bloco;
    private bool usandoEfeito;
    private string camada;
    private int ordem;

    private float materia;
    private bool tinhaFisica;
    private float tempoDesdeMola = -1f;
    private float forcaMola;
    private float brilho;
    private float afundar;

    private void Start()
    {
        receptor = GetComponent<IToolReceiver>();
        bloco = new MaterialPropertyBlock();

        int ordemMaxima = int.MinValue;
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sprites.Add(sr);
            materiaisOriginais.Add(sr.sharedMaterial);
            if (sr.sortingOrder > ordemMaxima)
            {
                ordemMaxima = sr.sortingOrder;
                camada = sr.sortingLayerName;
            }
        }

        ordem = (ordemMaxima == int.MinValue ? 0 : ordemMaxima) + 1;

        if (receptor != null)
        {
            materia = receptor.HasTool(ToolType.Colisao) ? 1f : 0f;
            tinhaFisica = receptor.HasTool(ToolType.Fisica);
        }

        AtualizarVisual(0f);
    }

    private void Update()
    {
        if (receptor == null)
        {
            return;
        }

        // Collider2D: a matéria chega aos poucos; sem ele, é holograma.
        if (receptor.HasTool(ToolType.Colisao))
        {
            materia = Mathf.MoveTowards(materia, 1f, tempoMaterializar > 0f ? Time.deltaTime / tempoMaterializar : 1f);
        }
        else
        {
            materia = 0f;
        }

        // Rigidbody2D: amassa uma vez, na hora em que chega.
        bool fisica = receptor.HasTool(ToolType.Fisica);
        if (fisica && !tinhaFisica)
        {
            Amassar(amassarAoGanharPeso);
        }

        tinhaFisica = fisica;

        float mola = 0f;
        if (tempoDesdeMola >= 0f)
        {
            tempoDesdeMola += Time.deltaTime;
            float t = tempoDesdeMola / Mathf.Max(0.01f, tempoMola);
            if (t >= 1f)
            {
                tempoDesdeMola = -1f;
            }
            else
            {
                // Amassa, estica um pouco e assenta.
                mola = forcaMola * Mathf.Exp(-4f * t) * Mathf.Cos(t * Mathf.PI * 3f);
            }
        }

        AtualizarPrevia();
        AtualizarVisual(mola + afundar);
    }

    private void AtualizarPrevia()
    {
        float brilhoAlvo = 0f;
        float afundarAlvo = 0f;
        DraggableToolIcon arrastando = DraggableToolIcon.Arrastando;
        if (arrastando != null && ReferenceEquals(DraggableToolIcon.AlvoSobCursor, receptor))
        {
            ToolType tool = arrastando.Tool;
            if (receptor.CanReceive(tool) && !receptor.HasTool(tool))
            {
                float pulso = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
                if (tool == ToolType.Colisao)
                {
                    brilhoAlvo = brilhoPrevia * (0.6f + 0.4f * pulso);
                }
                else if (tool == ToolType.Fisica)
                {
                    afundarAlvo = afundarPrevia * (0.5f + 0.5f * pulso);
                }
            }
        }

        float passo = Time.unscaledDeltaTime * 6f;
        brilho = Mathf.MoveTowards(brilho, brilhoAlvo, passo);
        afundar = Mathf.MoveTowards(afundar, afundarAlvo, passo * 0.3f);
    }

    private void Amassar(float forca)
    {
        forcaMola = forca;
        tempoDesdeMola = 0f;
    }

    private void AtualizarVisual(float amassar)
    {
        bool precisaEfeito = materia < 1f || Mathf.Abs(amassar) > 0.0005f || brilho > 0.001f;
        if (!precisaEfeito || material == null)
        {
            if (usandoEfeito)
            {
                TrocarMaterial(false);
            }

            return;
        }

        if (!usandoEfeito)
        {
            TrocarMaterial(true);
        }

        Bounds b = LimitesDoDesenho();
        // A matéria chega rápido no começo e assenta no fim.
        float m = 1f - (1f - materia) * (1f - materia);
        foreach (SpriteRenderer sr in sprites)
        {
            if (sr == null)
            {
                continue;
            }

            sr.GetPropertyBlock(bloco);
            bloco.SetFloat(IdMateria, m);
            bloco.SetFloat(IdTopo, b.max.y);
            bloco.SetFloat(IdBase, b.min.y);
            bloco.SetFloat(IdCentro, b.center.x);
            bloco.SetFloat(IdAmassar, amassar);
            bloco.SetFloat(IdBrilho, brilho);
            sr.SetPropertyBlock(bloco);
        }
    }

    private void TrocarMaterial(bool efeito)
    {
        usandoEfeito = efeito;
        for (int i = 0; i < sprites.Count; i++)
        {
            SpriteRenderer sr = sprites[i];
            if (sr == null)
            {
                continue;
            }

            sr.sharedMaterial = efeito ? material : materiaisOriginais[i];
            if (!efeito)
            {
                sr.SetPropertyBlock(null);
            }
        }
    }

    private Bounds LimitesDoDesenho()
    {
        Bounds b = new Bounds(transform.position, Vector3.zero);
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

        return b;
    }

    // ---------------------------------------------------------------- poeira

    private void OnCollisionEnter2D(Collision2D colisao)
    {
        // Só matéria com peso levanta poeira (holograma não pousa em nada).
        if (receptor == null || !tinhaFisica || !receptor.HasTool(ToolType.Colisao) || colisao.contactCount == 0)
        {
            return;
        }

        ContactPoint2D contato = colisao.GetContact(0);
        float impacto = Mathf.Abs(colisao.relativeVelocity.y);
        Bounds b = LimitesDoDesenho();

        // Só quem pousou (contato embaixo dele), não quem foi atingido por cima.
        if (impacto < impactoPoeira || contato.point.y > b.center.y)
        {
            return;
        }

        Amassar(Mathf.Clamp(impacto * 0.012f, 0.03f, 0.12f));
        LevantarPoeira(new Vector2(b.center.x, b.min.y), b.size.x, impacto);
    }

    private void LevantarPoeira(Vector2 ponto, float largura, float impacto)
    {
        if (spritePoeira == null)
        {
            return;
        }

        int quantidade = Mathf.Clamp(Mathf.RoundToInt(4 + largura * 2f), 6, 14);
        var raiz = new GameObject("Poeira");
        raiz.transform.position = ponto;
        var nuvem = raiz.AddComponent<NuvemDePoeira>();
        nuvem.Iniciar(spritePoeira, corPoeira, camada, ordem, quantidade, largura, Mathf.Clamp01(impacto / 12f));
    }

    /// <summary>Partículas simples de poeira (sprites), que se apagam sozinhas.</summary>
    private class NuvemDePoeira : MonoBehaviour
    {
        private const float Duracao = 0.45f;
        private readonly List<SpriteRenderer> graos = new List<SpriteRenderer>();
        private readonly List<Vector2> velocidades = new List<Vector2>();
        private readonly List<float> tamanhos = new List<float>();
        private Color cor;
        private float tempo;

        public void Iniciar(Sprite sprite, Color corBase, string camada, int ordem, int quantidade, float largura, float forca)
        {
            cor = corBase;
            for (int i = 0; i < quantidade; i++)
            {
                var go = new GameObject("Grao");
                go.transform.SetParent(transform, false);
                float lado = i % 2 == 0 ? -1f : 1f;
                float x = lado * Random.Range(0.1f, 0.5f) * largura;
                go.transform.localPosition = new Vector3(x, Random.Range(0f, 0.08f), 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = cor;
                if (camada != null)
                {
                    sr.sortingLayerName = camada;
                }

                sr.sortingOrder = ordem;
                graos.Add(sr);
                velocidades.Add(new Vector2(lado * Random.Range(0.8f, 2.2f) * (0.6f + forca), Random.Range(0.3f, 1.1f) * (0.6f + forca)));
                tamanhos.Add(Random.Range(0.12f, 0.22f) * (0.8f + forca));
            }

            AtualizarGraos(0f);
        }

        private void Update()
        {
            tempo += Time.deltaTime;
            if (tempo >= Duracao)
            {
                Destroy(gameObject);
                return;
            }

            AtualizarGraos(Time.deltaTime);
        }

        private void AtualizarGraos(float dt)
        {
            float t = tempo / Duracao;
            for (int i = 0; i < graos.Count; i++)
            {
                Transform g = graos[i].transform;
                velocidades[i] *= 1f - 3f * dt;
                g.localPosition += (Vector3)(velocidades[i] * dt);
                float lado = tamanhos[i] * (1f + t * 1.2f);
                float escala = graos[i].sprite.bounds.size.x > 0f ? lado / graos[i].sprite.bounds.size.x : 1f;
                g.localScale = new Vector3(escala, escala, 1f);
                Color c = cor;
                c.a *= 1f - t * t;
                graos[i].color = c;
            }
        }
    }
}
