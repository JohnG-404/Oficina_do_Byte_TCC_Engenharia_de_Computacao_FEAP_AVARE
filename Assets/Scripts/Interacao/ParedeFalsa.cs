using System.Collections;
using UnityEngine;

/// <summary>
/// Parede que bloqueia a passagem (ex: "DoorFalse" na cena Interacao) até
/// ser aberta pela Consequência "Abrir uma porta" do InteractableItem no
/// mesmo objeto (ver InteractableItem.ApplyConsequence). É o mesmo padrão da
/// Door, só que sem porta_cima: as partes visuais (os blocos de parede
/// filhos) sobem ou somem, conforme o Modo escolhido no Inspector.
///
/// Só os filhos com SpriteRenderer se mexem; o próprio objeto fica parado,
/// para as zonas de proximidade/topo do InteractableItem não irem junto.
/// </summary>
public class ParedeFalsa : MonoBehaviour
{
    public enum Modo
    {
        [Tooltip("Sobe com animação suave até sair do caminho.")]
        Levantar,

        [Tooltip("Desaparece com um fade rápido.")]
        Sumir
    }

    [Tooltip("Levantar = sobe com animação suave. Sumir = desaparece com um fade rápido.")]
    [SerializeField] private Modo modo = Modo.Levantar;

    [Tooltip("Collider sólido que bloqueia a passagem. Se vazio, usa o Collider2D deste objeto.")]
    [SerializeField] private Collider2D blockingCollider;

    [Tooltip("Partes que sobem/somem. Se vazio, usa todos os SpriteRenderers filhos.")]
    [SerializeField] private SpriteRenderer[] partes;

    [Tooltip("Quanto sobe no modo Levantar (unidades). 0 = a própria altura da parede.")]
    [SerializeField] private float distanciaLevantar = 0f;

    [SerializeField] private float duracao = 0.8f;

    public bool IsOpen { get; private set; }

    /// <summary>Consequência "Deixar passar quando perto": a parede está atravessável agora.</summary>
    public bool Atravessavel { get; private set; }

    [Tooltip("Transparência das partes enquanto a parede está atravessável.")]
    [SerializeField, Range(0f, 1f)] private float alfaAtravessavel = 0.35f;

    private Coroutine fadeAtravessavel;

    private void Awake()
    {
        if (blockingCollider == null)
        {
            blockingCollider = GetComponent<Collider2D>();
        }

        if (partes == null || partes.Length == 0)
        {
            partes = GetComponentsInChildren<SpriteRenderer>();
        }
    }

    public void Abrir()
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;

        if (blockingCollider != null)
        {
            blockingCollider.enabled = false;
        }

        AudioManager.TocarEfeito(AudioManager.Efeito.PortaAbrir);
        StartCoroutine(modo == Modo.Levantar ? Levantar() : Sumir());
    }

    /// <summary>
    /// Liga/desliga o modo atravessável: o collider vira trigger (o jogador
    /// passa por dentro, mas ainda dá pra clicar na parede) e as partes ficam
    /// transparentes. Chamado pelo InteractableItem quando o jogador chega
    /// perto e quando ele se afasta.
    /// </summary>
    /// <summary>Atalho pra DefinirAtravessavel(false) (usado ao reconfigurar o item).</summary>
    public void DefinirAtravessavelFalso() => DefinirAtravessavel(false);

    public void DefinirAtravessavel(bool valor)
    {
        if (IsOpen || Atravessavel == valor)
        {
            return;
        }

        Atravessavel = valor;
        if (blockingCollider != null)
        {
            blockingCollider.isTrigger = valor;
        }

        if (valor)
        {
            AudioManager.TocarEfeito(AudioManager.Efeito.PortaAbrir);
        }

        if (fadeAtravessavel != null)
        {
            StopCoroutine(fadeAtravessavel);
        }
        fadeAtravessavel = StartCoroutine(FadeAlfa(valor ? alfaAtravessavel : 1f));
    }

    private IEnumerator FadeAlfa(float alvo)
    {
        float inicio = partes.Length > 0 ? partes[0].color.a : 1f;
        float t = 0f;
        const float tempo = 0.25f;
        while (t < tempo)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(inicio, alvo, t / tempo);
            foreach (SpriteRenderer sr in partes)
            {
                Color c = sr.color;
                c.a = a;
                sr.color = c;
            }
            yield return null;
        }
        fadeAtravessavel = null;
    }

    private IEnumerator Levantar()
    {
        float distancia = distanciaLevantar > 0f ? distanciaLevantar : AlturaDasPartes();
        var inicio = new Vector3[partes.Length];
        for (int i = 0; i < partes.Length; i++)
        {
            inicio[i] = partes[i].transform.position;
        }

        float t = 0f;
        while (t < duracao)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duracao);
            for (int i = 0; i < partes.Length; i++)
            {
                partes[i].transform.position = inicio[i] + Vector3.up * (distancia * k);
            }
            yield return null;
        }

        for (int i = 0; i < partes.Length; i++)
        {
            partes[i].transform.position = inicio[i] + Vector3.up * distancia;
        }
    }

    private IEnumerator Sumir()
    {
        var cores = new Color[partes.Length];
        for (int i = 0; i < partes.Length; i++)
        {
            cores[i] = partes[i].color;
        }

        float t = 0f;
        float tempo = Mathf.Min(duracao, 0.35f);
        while (t < tempo)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / tempo);
            for (int i = 0; i < partes.Length; i++)
            {
                Color c = cores[i];
                c.a *= a;
                partes[i].color = c;
            }
            yield return null;
        }

        foreach (SpriteRenderer sr in partes)
        {
            sr.enabled = false;
        }
    }

    private float AlturaDasPartes()
    {
        if (partes.Length == 0)
        {
            return 2f;
        }

        Bounds b = partes[0].bounds;
        foreach (SpriteRenderer sr in partes)
        {
            b.Encapsulate(sr.bounds);
        }
        return b.size.y;
    }
}
