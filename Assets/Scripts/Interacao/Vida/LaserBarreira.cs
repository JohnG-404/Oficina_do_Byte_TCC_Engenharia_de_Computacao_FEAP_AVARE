using UnityEngine;

/// <summary>
/// Laser em forma de barreira, com distância ajustável: uma base em cada
/// ponta (tamanho fixo) e o feixe no meio, desenhado em modo Tiled — só o
/// feixe estica quando a distância muda, e o collider acompanha.
///
/// Fica no mesmo objeto do InteractableItem do laser e só cuida do que é
/// próprio da barreira (não mexe na lógica de Ação/Consequência):
/// - Sem interação configurada, o laser é sólido (feixe pálido tracejado) e,
///   ao encostar, mostra uma dica pedindo pra clicar nele.
/// - Configurado com dano em "Uma vez", depois de disparar o laser desliga
///   de vez (o jogador "pagou" pra passar). Repelir nunca abre.
/// - Desligar(segundos) apaga o feixe por um tempo — pronto pra uma
///   consequência "Desligar por X s".
///
/// Orientação Vertical: a raiz fica no chão e o laser sobe até "distancia".
/// Horizontal: a raiz fica na ponta esquerda e o laser vai pra direita.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider2D))]
public class LaserBarreira : MonoBehaviour
{
    public enum Orientacao { Vertical, Horizontal }

    [Header("Tamanho")]
    [Tooltip("Distância total, de uma base à outra (as bases entram na conta).")]
    [SerializeField, Min(1f)] private float distancia = 4f;
    [SerializeField] private Orientacao orientacao = Orientacao.Vertical;
    [Tooltip("Espessura do collider do feixe.")]
    [SerializeField] private float espessuraColisor = 0.6f;
    [Tooltip("Quanto a zona de 'Chegar perto' se estende pra cada lado do feixe.")]
    [SerializeField] private float alcanceProximidade = 1f;

    [Header("Sprites")]
    [SerializeField] private Sprite spriteBase;
    [SerializeField] private Sprite spriteLed;
    [SerializeField] private Sprite feixeContinuo;
    [SerializeField] private Sprite feixeTracejado;

    [Header("Cores por estado")]
    [SerializeField] private Color corSemInteracao = new Color(0.78f, 0.9f, 0.96f, 1f);
    [SerializeField] private Color corDano = new Color(1f, 0.27f, 0.27f, 1f);
    [SerializeField] private Color corRepelir = new Color(0.25f, 0.62f, 1f, 1f);
    [SerializeField] private Color corDesligado = new Color(0.3f, 0.86f, 0.47f, 1f);
    [Tooltip("Cor do feixe nas consequências com tempo (\"Desligar por 3 segundos\" e \"Ligar depois que passar\").")]
    [SerializeField] private Color corTempo = new Color(1f, 0.78f, 0.25f, 1f);

    [Header("Dica")]
    [SerializeField] private string dicaSemInteracao = "Laser sem interação. Clique nele para escolher o que ele faz.";

    private const float AlturaBase = 0.4f;   // laser_base.png: 80 x 40 px a 100 PPU
    private const float CentroLente = 0.35f; // da borda da base até o centro da lente
    private const float LarguraFeixe = 0.24f;

    private SpriteRenderer feixe, baseInicio, baseFim, ledInicio, ledFim;
    private BoxCollider2D colisor;
    private InteractableItem item;
    private bool desligadoDeVez;
    private float religarEm = -1f;
    private bool desligadoAteLigar;
    private float proximaDica;

    public bool EstaLigado => !desligadoDeVez && religarEm < 0f && !desligadoAteLigar;

    public float Distancia
    {
        get => distancia;
        set { distancia = Mathf.Max(1f, value); Montar(); }
    }

    /// <summary>Apaga o feixe (e o collider) por alguns segundos.</summary>
    public void Desligar(float segundos)
    {
        religarEm = Time.time + Mathf.Max(0f, segundos);
        AplicarEstado();
    }

    /// <summary>Apaga o feixe até alguém chamar Ligar() (Consequência "Ligar depois que passar").</summary>
    public void DesligarAteLigar()
    {
        desligadoAteLigar = true;
        AplicarEstado();
    }

    /// <summary>Liga o feixe de novo (e o collider).</summary>
    public void Ligar()
    {
        desligadoAteLigar = false;
        religarEm = -1f;
        AplicarEstado();
    }

    private void OnEnable()
    {
        Montar();
    }

    private void OnValidate()
    {
        // Montar() cria filhos — não pode rodar dentro do OnValidate.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Montar(); };
#endif
    }

    /// <summary>Cria/posiciona as peças conforme a distância e a orientação.</summary>
    public void Montar()
    {
        colisor = GetComponent<BoxCollider2D>();
        item = GetComponent<InteractableItem>();

        Vector2 eixo = orientacao == Orientacao.Vertical ? Vector2.up : Vector2.right;
        float angulo = orientacao == Orientacao.Vertical ? 0f : -90f;

        baseInicio = Peca("BaseInicio", spriteBase, 6);
        baseInicio.transform.localPosition = eixo * (AlturaBase * 0.5f);
        baseInicio.transform.localRotation = Quaternion.Euler(0f, 0f, angulo);

        baseFim = Peca("BaseFim", spriteBase, 6);
        baseFim.transform.localPosition = eixo * (distancia - AlturaBase * 0.5f);
        baseFim.transform.localRotation = Quaternion.Euler(0f, 0f, angulo + 180f);

        ledInicio = Peca("LedInicio", spriteLed, 7);
        ledInicio.transform.localPosition = eixo * CentroLente;
        ledFim = Peca("LedFim", spriteLed, 7);
        ledFim.transform.localPosition = eixo * (distancia - CentroLente);

        feixe = Peca("Feixe", feixeContinuo, 5);
        feixe.drawMode = SpriteDrawMode.Tiled;
        feixe.tileMode = SpriteTileMode.Continuous;
        float comprimentoFeixe = Mathf.Max(0.1f, distancia - 2f * CentroLente);
        feixe.size = new Vector2(LarguraFeixe, comprimentoFeixe);
        feixe.transform.localPosition = eixo * (distancia * 0.5f);
        feixe.transform.localRotation = Quaternion.Euler(0f, 0f, angulo);

        Vector2 tamanho = orientacao == Orientacao.Vertical
            ? new Vector2(espessuraColisor, distancia)
            : new Vector2(distancia, espessuraColisor);
        colisor.size = tamanho;
        colisor.offset = eixo * (distancia * 0.5f);

        // Zonas do InteractableItem: se já existirem com esses nomes, ele
        // só as usa (em vez de criar um círculo pequeno no pé do laser).
        Zona("ProximityZone", tamanho + (orientacao == Orientacao.Vertical
            ? new Vector2(alcanceProximidade * 2f, 0f)
            : new Vector2(0f, alcanceProximidade * 2f)), colisor.offset);
        Zona("TopZone", new Vector2(0.2f, 0.2f), eixo * (distancia + 0.3f));

        AplicarEstado();
    }

    private SpriteRenderer Peca(string nome, Sprite sprite, int ordem)
    {
        Transform t = transform.Find(nome);
        if (t == null)
        {
            t = new GameObject(nome).transform;
            t.SetParent(transform, false);
        }
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr == null) sr = t.gameObject.AddComponent<SpriteRenderer>();
        if (sprite != null) sr.sprite = sprite;
        sr.sortingOrder = ordem;
        t.localScale = Vector3.one;
        return sr;
    }

    private void Zona(string nome, Vector2 tamanho, Vector2 centro)
    {
        Transform t = transform.Find(nome);
        if (t == null)
        {
            t = new GameObject(nome).transform;
            t.SetParent(transform, false);
        }
        t.localPosition = Vector3.zero;
        t.localScale = Vector3.one;
        var box = t.GetComponent<BoxCollider2D>();
        if (box == null) box = t.gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = tamanho;
        box.offset = centro;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        if (religarEm >= 0f && Time.time >= religarEm)
        {
            religarEm = -1f;
        }

        // Dano em "Uma vez": o InteractableItem marca HasTriggered depois do
        // disparo e o jogador "pagou" a passagem. Repelir e as consequências
        // com tempo nunca abrem de vez.
        if (item != null && item.IsConfigured && item.HasTriggered && !item.ChosenRepeats
            && (item.ChosenConsequence == ConsequenceType.DamageOneHeart || item.ChosenConsequence == ConsequenceType.DamageTwoHearts))
        {
            desligadoDeVez = true;
        }

        AplicarEstado();
    }

    private void AplicarEstado()
    {
        if (feixe == null || colisor == null) return;

        bool configurado = item != null && item.IsConfigured;
        bool ligado = EstaLigado;

        Color cor = corSemInteracao;
        if (configurado)
        {
            switch (item.ChosenConsequence)
            {
                case ConsequenceType.Repel: cor = corRepelir; break;
                case ConsequenceType.DisableForSeconds:
                case ConsequenceType.TurnOnBehindPlayer: cor = corTempo; break;
                default: cor = corDano; break;
            }
        }

        feixe.enabled = ligado;
        feixe.sprite = configurado ? feixeContinuo : feixeTracejado;
        feixe.color = cor;
        Color corLed = ligado ? cor : corDesligado;
        if (ledInicio != null) ledInicio.color = corLed;
        if (ledFim != null) ledFim.color = corLed;

        if (Application.isPlaying)
        {
            colisor.enabled = ligado;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!Application.isPlaying || item == null || item.IsConfigured) return;
        if (!InteractableItem.IsPlayer(collision.collider) || Time.time < proximaDica) return;

        InteractionConfigUI.Instance?.ShowHint(dicaSemInteracao);
        proximaDica = Time.time + 3f;
    }
}

