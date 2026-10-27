using System.Collections;
using UnityEngine;

/// <summary>
/// Visual da plataforma falsa: o sprite mostra a escolha do jogador logo
/// depois de configurar (lisa com "?" sem interação, rachada pra "Quebrar e
/// voltar", com seta e pistões pra "Subir e voltar").
///
/// Também dá corpo à quebra, ouvindo os eventos do FakePlatform: treme
/// enquanto racha, as duas metades caem e somem, fica um contorno tracejado
/// enquanto ela está fora e ela volta piscando. Não mexe na física — isso
/// continua todo no FakePlatform.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(FakePlatform))]
public class PlataformaFalsaVisual : MonoBehaviour
{
    [Header("Sprites por estado")]
    [SerializeField] private Sprite neutra;
    [SerializeField] private Sprite quebrar;
    [SerializeField] private Sprite mover;
    [SerializeField] private Sprite pistoes;

    [Header("Quebra")]
    [SerializeField] private Sprite metadeEsquerda;
    [SerializeField] private Sprite metadeDireita;
    [SerializeField] private Sprite contorno;
    [SerializeField] private float tremor = 0.04f;
    [SerializeField] private float duracaoQueda = 0.8f;

    private SpriteRenderer sr;
    private FakePlatform plataforma;
    private InteractableItem item;
    private SpriteRenderer srPistoes, srTremor, srContorno;
    private Coroutine rotina;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        plataforma = GetComponent<FakePlatform>();
        item = GetComponent<InteractableItem>();

        srPistoes = Filho("Pistoes", pistoes, sr.sortingOrder - 1);
        // Pistões embaixo da placa, encostados na borda de baixo dela.
        float meiaPlaca = sr.sprite != null ? sr.sprite.bounds.extents.y : 0.35f;
        float meioPistao = pistoes != null ? pistoes.bounds.extents.y : 0.25f;
        srPistoes.transform.localPosition = new Vector3(0f, -(meiaPlaca + meioPistao) + 0.02f, 0f);

        srTremor = Filho("Tremor", null, sr.sortingOrder);
        srContorno = Filho("Contorno", contorno, sr.sortingOrder);
    }

    private void OnEnable()
    {
        plataforma.Rachando += AoRachar;
        plataforma.Sumiu += AoSumir;
        plataforma.Voltou += AoVoltar;
    }

    private void OnDisable()
    {
        plataforma.Rachando -= AoRachar;
        plataforma.Sumiu -= AoSumir;
        plataforma.Voltou -= AoVoltar;
    }

    private void Update()
    {
        bool configurado = item != null && item.IsConfigured;
        Sprite alvo = neutra;
        if (configurado && item.ChosenConsequence == ConsequenceType.BreakAndRespawn) alvo = quebrar;
        else if (configurado && item.ChosenConsequence == ConsequenceType.BounceAndReturn) alvo = mover;

        if (alvo != null && sr.sprite != alvo) sr.sprite = alvo;
        srPistoes.enabled = configurado && item.ChosenConsequence == ConsequenceType.BounceAndReturn;
    }

    private void AoRachar(float duracao)
    {
        Trocar(Tremer(duracao));
    }

    private void AoSumir()
    {
        Trocar(null);
        srTremor.enabled = false;
        SoltarMetade(metadeEsquerda, -1f);
        SoltarMetade(metadeDireita, 1f);
        srContorno.enabled = true;
    }

    private void AoVoltar()
    {
        srContorno.enabled = false;
        Trocar(Piscar());
    }

    private void Trocar(IEnumerator nova)
    {
        if (rotina != null) StopCoroutine(rotina);
        rotina = nova != null ? StartCoroutine(nova) : null;
        sr.color = Color.white;
    }

    // A placa de verdade fica parada (o collider não pode tremer); quem treme
    // é uma cópia, enquanto a original fica transparente.
    private IEnumerator Tremer(float duracao)
    {
        srTremor.sprite = sr.sprite;
        srTremor.enabled = true;
        sr.color = new Color(1f, 1f, 1f, 0f);
        float t = 0f;
        while (t < duracao)
        {
            t += Time.deltaTime;
            float forca = tremor * (0.5f + t / duracao);
            srTremor.transform.localPosition = new Vector3(Random.Range(-forca, forca), Random.Range(-forca, forca) * 0.5f, 0f);
            yield return null;
        }
        srTremor.transform.localPosition = Vector3.zero;
    }

    private IEnumerator Piscar()
    {
        for (int i = 0; i < 3; i++)
        {
            sr.color = new Color(1f, 1f, 1f, 0.3f);
            yield return new WaitForSeconds(0.08f);
            sr.color = Color.white;
            yield return new WaitForSeconds(0.08f);
        }
        rotina = null;
    }

    private void SoltarMetade(Sprite sprite, float lado)
    {
        if (sprite == null) return;
        var go = new GameObject("Pedaco");
        go.transform.position = transform.TransformPoint(new Vector3(lado * sprite.bounds.extents.x, 0f, 0f));
        go.transform.localScale = transform.lossyScale;
        var pedaco = go.AddComponent<SpriteRenderer>();
        pedaco.sprite = sprite;
        pedaco.sortingLayerID = sr.sortingLayerID;
        pedaco.sortingOrder = sr.sortingOrder;
        StartCoroutine(Cair(pedaco, lado));
    }

    private IEnumerator Cair(SpriteRenderer pedaco, float lado)
    {
        Vector3 velocidade = new Vector3(lado * 0.8f, 0.6f, 0f);
        float t = 0f;
        while (t < duracaoQueda && pedaco != null)
        {
            t += Time.deltaTime;
            velocidade.y -= 14f * Time.deltaTime;
            pedaco.transform.position += velocidade * Time.deltaTime;
            pedaco.transform.Rotate(0f, 0f, -lado * 140f * Time.deltaTime);
            pedaco.color = new Color(1f, 1f, 1f, 1f - t / duracaoQueda);
            yield return null;
        }
        if (pedaco != null) Destroy(pedaco.gameObject);
    }

    private SpriteRenderer Filho(string nome, Sprite sprite, int ordem)
    {
        Transform t = transform.Find(nome);
        if (t == null)
        {
            t = new GameObject(nome).transform;
            t.SetParent(transform, false);
        }
        var filho = t.GetComponent<SpriteRenderer>();
        if (filho == null) filho = t.gameObject.AddComponent<SpriteRenderer>();
        filho.sprite = sprite;
        filho.sortingLayerID = sr.sortingLayerID;
        filho.sortingOrder = ordem;
        filho.enabled = false;
        return filho;
    }
}

