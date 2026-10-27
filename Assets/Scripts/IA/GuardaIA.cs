using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Cérebro" do Guarda: uma máquina de estados finita com três estados
/// (Patrulhar, Perseguir, Atacar). É o que as fases IA e IA_Aplicacao
/// ensinam: IA de jogo é um conjunto pequeno de regras que decide, a cada
/// frame (Update), o que o personagem faz.
///
/// - Estado = o que ele faz: Patrulhar anda entre A e B, Perseguir anda na
///   direção do alvo, Atacar tira vida (PlayerHealth.TakeDamage) com intervalo.
///   O movimento usa Rigidbody2D.linearVelocity, igual ao jogador.
/// - Transição = quando ele troca: distância até o alvo (Vector2.Distance)
///   comparada com raioVisao, raioPerder e alcance.
/// - Percepção = como ele "vê": só distância (atravessa paredes) ou
///   distância + Physics2D.Raycast (a parede bloqueia a linha de visão).
///
/// raioPerder é maior que raioVisao de propósito (histerese): se fossem
/// iguais, o guarda ficaria trocando de estado sem parar na borda do raio.
///
/// Os puzzles da fase IA ligam as partes do cérebro aos poucos
/// (<see cref="MontarEstados"/>, <see cref="MontarTransicoes"/>,
/// <see cref="SetPercepcao"/>). Sem estados montados ele fica parado, "sem
/// cérebro". Alvos são escolhidos por tag (lista editável), o que permite ao
/// jogador trocar "tag Player" por "tag Player ou Aliado" na IA_Aplicacao.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GuardaIA : MonoBehaviour
{
    public enum Estado
    {
        SemCerebro,
        Patrulhar,
        Perseguir,
        Atacar
    }

    public enum Percepcao
    {
        Nenhuma,
        Distancia,
        Raycast
    }

    [Header("Cérebro (cada puzzle liga uma parte)")]
    [Tooltip("Puzzle 1 (Estados): sem isso o guarda fica parado, sem fazer nada.")]
    [SerializeField] private bool estadosMontados;
    [Tooltip("Puzzle 2 (Transições): sem isso ele patrulha pra sempre, sem nunca mudar de ideia.")]
    [SerializeField] private bool transicoesMontadas;
    [Tooltip("Como ele percebe o alvo. Distância atravessa paredes; Raycast exige linha de visão livre.")]
    [SerializeField] private Percepcao percepcao = Percepcao.Distancia;

    [Header("Patrulha")]
    [SerializeField] private Transform pontoA;
    [SerializeField] private Transform pontoB;
    [SerializeField] private float velocidadePatrulha = 1.6f;
    [SerializeField] private float velocidadePerseguir = 3f;
    [Tooltip("Se ficar parado contra algo (ex: uma parede no caminho) por esse tempo, vira e volta.")]
    [SerializeField] private float tempoPresoParaVirar = 1f;

    [Header("Percepção")]
    [Tooltip("Distância em que ele passa a perseguir (Patrulhar → Perseguir).")]
    [SerializeField] private float raioVisao = 7f;
    [Tooltip("Distância em que ele desiste (Perseguir → Patrulhar). Maior que raioVisao pra não ficar trocando de estado na borda.")]
    [SerializeField] private float raioPerder = 9f;
    [Tooltip("Distância em que ele ataca (Perseguir → Atacar) e volta a perseguir (Atacar → Perseguir).")]
    [SerializeField] private float alcance = 1.1f;
    [Tooltip("O que bloqueia a visão no modo Raycast (paredes, chão, plataformas).")]
    [SerializeField] private LayerMask obstaculos = 1;
    [Tooltip("De onde sai a linha de visão, relativo ao centro do guarda.")]
    [SerializeField] private Vector2 olhoOffset = new Vector2(0f, 0.2f);

    [Header("Alvos (por tag)")]
    [Tooltip("Persegue qualquer um que ele veja com uma destas tags. Ex: só 'Player', ou 'Player' e 'Aliado'.")]
    [SerializeField] private List<string> tagsAlvo = new List<string> { "Player" };
    [Tooltip("Guarda e alvos se atravessam: quem 'pega' é o dano, não um empurrão.")]
    [SerializeField] private bool atravessarAlvos = true;

    [Header("Ataque")]
    [SerializeField] private int dano = 1;
    [Tooltip("Segundos entre um CausarDano e outro, pra não tirar toda a vida de uma vez.")]
    [SerializeField] private float intervaloAtaque = 1f;
    [Tooltip("Tempo entre entrar em Atacar e o primeiro golpe (dá pra ver o '!' antes).")]
    [SerializeField] private float atrasoPrimeiroGolpe = 0.2f;

    [Header("Visual (opcional)")]
    [Tooltip("Filho com o sprite: vira pro lado em que anda e dá um 'soco' ao atacar.")]
    [SerializeField] private Transform visual;

    /// <summary>(estado anterior, estado novo).</summary>
    public event Action<Estado, Estado> OnStateChanged;

    /// <summary>Disparado a cada golpe, com quem foi atingido.</summary>
    public event Action<Transform> OnAtaque;

    public Estado EstadoAtual => estado;
    public Transform AlvoAtual => alvo;
    public Percepcao PercepcaoAtual => percepcao;
    public bool EstadosMontados => estadosMontados;
    public bool TransicoesMontadas => transicoesMontadas;
    public float RaioVisao => raioVisao;
    public float RaioPerder => raioPerder;
    public float Alcance => alcance;
    public LayerMask Obstaculos => obstaculos;
    public IReadOnlyList<string> TagsAlvo => tagsAlvo;
    public Vector2 Olho => IAVisao.Centro(transform) + olhoOffset;

    /// <summary>Pausa o cérebro (diálogo, puzzle ou painel aberto): o guarda para onde está.</summary>
    public bool Pausado { get; set; }

    private Rigidbody2D rb;
    private SpriteRenderer visualRenderer;
    private Estado estado = Estado.SemCerebro;
    private Transform alvo;
    private bool indoParaB = true;
    private float timerAtaque;
    private float tempoPreso;
    private float ultimoXFisica;
    private float velocidadeDesejadaX;
    private Coroutine soco;
    private Vector3 escalaVisual = Vector3.one;

    private readonly List<Transform> candidatos = new List<Transform>();
    private readonly HashSet<Collider2D> colisoesIgnoradas = new HashSet<Collider2D>();
    private float proximaBuscaDeAlvos;
    private const float IntervaloBuscaDeAlvos = 0.5f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Physics2DTuning.PreventCornerSnag(col);
        }

        if (visual != null)
        {
            visualRenderer = visual.GetComponent<SpriteRenderer>();
            escalaVisual = visual.localScale;
        }

        estado = estadosMontados ? Estado.Patrulhar : Estado.SemCerebro;
        ultimoXFisica = rb.position.x;
    }

    // ------------------------------------------------------------------
    // API usada pelos fluxos das fases (e pelos puzzles)
    // ------------------------------------------------------------------

    /// <summary>Puzzle 1: cada estado ganha um comportamento. Ele passa a patrulhar.</summary>
    public void MontarEstados(bool montado = true)
    {
        estadosMontados = montado;
        if (!montado)
        {
            alvo = null;
            MudarEstado(Estado.SemCerebro);
        }
        else if (estado == Estado.SemCerebro)
        {
            MudarEstado(Estado.Patrulhar);
        }
    }

    /// <summary>Puzzle 2: as setas ganham condições. Ele passa a trocar de estado sozinho.</summary>
    public void MontarTransicoes(bool montado = true)
    {
        transicoesMontadas = montado;
        if (!montado)
        {
            EsquecerAlvo();
        }
    }

    /// <summary>Puzzle 3: troca "Distance &lt; raioVisao" por Physics2D.Raycast.</summary>
    public void SetPercepcao(Percepcao nova)
    {
        percepcao = nova;
    }

    /// <summary>Quem ele persegue (ex: "Player", ou "Player" e "Aliado").</summary>
    public void SetTagsAlvo(params string[] tags)
    {
        tagsAlvo = new List<string>(tags ?? new string[0]);
        proximaBuscaDeAlvos = 0f;

        // O alvo atual deixou de valer (ex: tiraram a tag dele da lista).
        if (alvo != null && !TemTagDeAlvo(alvo))
        {
            EsquecerAlvo();
        }
    }

    public void SetPontosDePatrulha(Transform a, Transform b)
    {
        pontoA = a;
        pontoB = b;
    }

    /// <summary>Solta o alvo e volta a patrulhar (ex: depois de o jogador ser pego e voltar pro início).</summary>
    public void EsquecerAlvo()
    {
        alvo = null;
        if (estadosMontados)
        {
            MudarEstado(Estado.Patrulhar);
        }
    }

    /// <summary>Distância (Vector2.Distance) do centro do guarda até o centro de um personagem.</summary>
    public float DistanciaAte(Transform t)
    {
        return Vector2.Distance(IAVisao.Centro(transform), IAVisao.Centro(t));
    }

    /// <summary>A linha do olho do guarda até <paramref name="t"/> está livre (nenhuma parede no meio)?</summary>
    public bool TemLinhaDeVisao(Transform t)
    {
        return IAVisao.LinhaLivre(Olho, t, transform, obstaculos);
    }

    // ------------------------------------------------------------------
    // Loop: decide (transições) e age (estado atual) a cada frame
    // ------------------------------------------------------------------

    private void Update()
    {
        if (Pausado)
        {
            velocidadeDesejadaX = 0f;
            AplicarVelocidade();
            return;
        }

        if (!estadosMontados)
        {
            // Sem cérebro: nem pisca.
            if (estado != Estado.SemCerebro)
            {
                MudarEstado(Estado.SemCerebro);
            }
            velocidadeDesejadaX = 0f;
            AplicarVelocidade();
            return;
        }

        if (estado == Estado.SemCerebro)
        {
            MudarEstado(Estado.Patrulhar);
        }

        AtualizarCandidatos();

        if (transicoesMontadas)
        {
            Decidir();
        }
        else if (estado != Estado.Patrulhar)
        {
            // Estados sem regras de troca: só sabe patrulhar.
            alvo = null;
            MudarEstado(Estado.Patrulhar);
        }

        Agir();
        AplicarVelocidade();
    }

    private void FixedUpdate()
    {
        // Detecta "preso contra algo" pela posição da física (no Update a
        // posição só muda nos frames em que a física rodou).
        float x = rb.position.x;
        bool tentandoAndar = estado == Estado.Patrulhar && Mathf.Abs(velocidadeDesejadaX) > 0.01f && !Pausado;
        if (tentandoAndar && Mathf.Abs(x - ultimoXFisica) < 0.002f)
        {
            tempoPreso += Time.fixedDeltaTime;
            if (tempoPreso >= tempoPresoParaVirar)
            {
                tempoPreso = 0f;
                indoParaB = !indoParaB;
            }
        }
        else
        {
            tempoPreso = 0f;
        }

        ultimoXFisica = x;
    }

    // Transições (o que o puzzle 2 monta; a percepção vem do puzzle 3).
    private void Decidir()
    {
        switch (estado)
        {
            case Estado.Patrulhar:
            {
                Transform visto = MelhorAlvoVisivel();
                if (visto != null)
                {
                    alvo = visto;
                    MudarEstado(Estado.Perseguir);
                }
                break;
            }

            case Estado.Perseguir:
                if (!AlvoValido(alvo) || Perde(alvo))
                {
                    alvo = null;
                    MudarEstado(Estado.Patrulhar);
                }
                else if (DistanciaAte(alvo) < alcance)
                {
                    MudarEstado(Estado.Atacar);
                }
                break;

            case Estado.Atacar:
                if (!AlvoValido(alvo))
                {
                    alvo = null;
                    MudarEstado(Estado.Patrulhar);
                }
                else if (DistanciaAte(alvo) > alcance)
                {
                    MudarEstado(Estado.Perseguir);
                }
                break;
        }
    }

    // Comportamentos (o que o puzzle 1 monta).
    private void Agir()
    {
        switch (estado)
        {
            case Estado.Patrulhar:
                velocidadeDesejadaX = IrEntre();
                break;

            case Estado.Perseguir:
                velocidadeDesejadaX = MoverAte(alvo);
                break;

            case Estado.Atacar:
                velocidadeDesejadaX = 0f;
                timerAtaque -= Time.deltaTime;
                if (timerAtaque <= 0f)
                {
                    timerAtaque = intervaloAtaque;
                    CausarDano();
                }
                break;

            default:
                velocidadeDesejadaX = 0f;
                break;
        }
    }

    /// <summary>IrEntre(A, B): anda até um ponto e, ao chegar, vira pro outro.</summary>
    private float IrEntre()
    {
        if (pontoA == null || pontoB == null)
        {
            return 0f;
        }

        float destino = indoParaB ? pontoB.position.x : pontoA.position.x;
        float dx = destino - transform.position.x;
        if (Mathf.Abs(dx) < 0.1f)
        {
            indoParaB = !indoParaB;
            return 0f;
        }

        return Mathf.Sign(dx) * velocidadePatrulha;
    }

    /// <summary>MoverAté(alvo): velocidade positiva se o alvo está à direita, negativa se à esquerda.</summary>
    private float MoverAte(Transform t)
    {
        if (t == null)
        {
            return 0f;
        }

        float dx = IAVisao.Centro(t).x - transform.position.x;
        return Mathf.Abs(dx) > 0.05f ? Mathf.Sign(dx) * velocidadePerseguir : 0f;
    }

    /// <summary>CausarDano(1): o mesmo TakeDamage que as armadilhas usam.</summary>
    private void CausarDano()
    {
        if (alvo == null)
        {
            return;
        }

        PlayerHealth vida = alvo.GetComponentInParent<PlayerHealth>();
        if (vida != null)
        {
            vida.TakeDamage(dano);
        }

        if (visual != null && isActiveAndEnabled)
        {
            if (soco != null) StopCoroutine(soco);
            soco = StartCoroutine(AnimarSoco());
        }

        OnAtaque?.Invoke(alvo);
    }

    private void AplicarVelocidade()
    {
        rb.linearVelocity = new Vector2(velocidadeDesejadaX, rb.linearVelocity.y);

        if (visualRenderer != null && Mathf.Abs(velocidadeDesejadaX) > 0.01f)
        {
            visualRenderer.flipX = velocidadeDesejadaX < 0f;
        }
    }

    private IEnumerator AnimarSoco()
    {
        const float duracao = 0.18f;
        float t = 0f;
        while (t < duracao)
        {
            t += Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(t / duracao) * Mathf.PI);
            visual.localScale = new Vector3(escalaVisual.x * (1f + 0.25f * k), escalaVisual.y * (1f - 0.12f * k), escalaVisual.z);
            yield return null;
        }

        visual.localScale = escalaVisual;
        soco = null;
    }

    private void MudarEstado(Estado novo)
    {
        if (novo == estado)
        {
            return;
        }

        Estado anterior = estado;
        estado = novo;

        if (novo == Estado.Atacar)
        {
            timerAtaque = atrasoPrimeiroGolpe;
        }

        // Viu o alvo (Patrulhar → Perseguir) / desistiu (Perseguir/Atacar → Patrulhar).
        if (anterior == Estado.Patrulhar && novo == Estado.Perseguir)
        {
            AudioManager.TocarEfeito(AudioManager.Efeito.GuardaAlerta);
        }
        else if ((anterior == Estado.Perseguir || anterior == Estado.Atacar) && novo == Estado.Patrulhar)
        {
            AudioManager.TocarEfeito(AudioManager.Efeito.GuardaCalmo);
        }

        OnStateChanged?.Invoke(anterior, novo);
    }

    // ------------------------------------------------------------------
    // Percepção
    // ------------------------------------------------------------------

    private bool Ve(Transform t)
    {
        switch (percepcao)
        {
            case Percepcao.Distancia:
                return DistanciaAte(t) < raioVisao;
            case Percepcao.Raycast:
                return DistanciaAte(t) < raioVisao && TemLinhaDeVisao(t);
            default:
                return false;
        }
    }

    private bool Perde(Transform t)
    {
        switch (percepcao)
        {
            case Percepcao.Distancia:
                return DistanciaAte(t) > raioPerder;
            case Percepcao.Raycast:
                return DistanciaAte(t) > raioPerder || !TemLinhaDeVisao(t);
            default:
                return true;
        }
    }

    private Transform MelhorAlvoVisivel()
    {
        Transform melhor = null;
        float melhorDistancia = float.MaxValue;

        foreach (Transform c in candidatos)
        {
            if (!AlvoValido(c) || !Ve(c))
            {
                continue;
            }

            float d = DistanciaAte(c);
            if (d < melhorDistancia)
            {
                melhorDistancia = d;
                melhor = c;
            }
        }

        return melhor;
    }

    private bool AlvoValido(Transform t)
    {
        return t != null && t.gameObject.activeInHierarchy && TemTagDeAlvo(t);
    }

    private bool TemTagDeAlvo(Transform t)
    {
        foreach (string tag in tagsAlvo)
        {
            if (!string.IsNullOrEmpty(tag) && t.gameObject.tag == tag)
            {
                return true;
            }
        }

        return false;
    }

    // Busca quem tem as tags de alvo (de tempos em tempos, não todo frame).
    private void AtualizarCandidatos()
    {
        if (Time.time < proximaBuscaDeAlvos)
        {
            return;
        }

        proximaBuscaDeAlvos = Time.time + IntervaloBuscaDeAlvos;
        candidatos.Clear();

        foreach (string tag in tagsAlvo)
        {
            if (string.IsNullOrEmpty(tag))
            {
                continue;
            }

            GameObject[] encontrados;
            try
            {
                encontrados = GameObject.FindGameObjectsWithTag(tag);
            }
            catch (UnityException)
            {
                // Tag não cadastrada no projeto (Tags and Layers).
                Debug.LogWarning($"[GuardaIA] A tag '{tag}' não existe no projeto. Rode Tools/IA/Criar Cena IA_Aplicacao (ela cadastra a tag) ou crie em Project Settings > Tags and Layers.");
                continue;
            }

            foreach (GameObject go in encontrados)
            {
                if (go == gameObject || candidatos.Contains(go.transform))
                {
                    continue;
                }

                candidatos.Add(go.transform);
                IgnorarColisaoCom(go);
            }
        }
    }

    private void IgnorarColisaoCom(GameObject outro)
    {
        if (!atravessarAlvos)
        {
            return;
        }

        Collider2D[] meus = GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D deles in outro.GetComponentsInChildren<Collider2D>(true))
        {
            if (!colisoesIgnoradas.Add(deles))
            {
                continue;
            }

            foreach (Collider2D meu in meus)
            {
                Physics2D.IgnoreCollision(meu, deles, true);
            }
        }
    }

    // ------------------------------------------------------------------
    // Gizmos de depuração: raios, pontos de patrulha e linha até o alvo
    // ------------------------------------------------------------------

    private void OnDrawGizmos()
    {
        Vector3 centro = Application.isPlaying ? (Vector3)IAVisao.Centro(transform) : transform.position;

        Gizmos.color = new Color(0.96f, 0.77f, 0.26f, 0.8f);
        Gizmos.DrawWireSphere(centro, raioVisao);
        Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(centro, raioPerder);
        Gizmos.color = new Color(0.94f, 0.29f, 0.29f, 0.9f);
        Gizmos.DrawWireSphere(centro, alcance);

        Gizmos.color = new Color(0.55f, 0.6f, 0.71f, 1f);
        if (pontoA != null) Gizmos.DrawLine(pontoA.position + Vector3.down * 0.5f, pontoA.position + Vector3.up * 0.5f);
        if (pontoB != null) Gizmos.DrawLine(pontoB.position + Vector3.down * 0.5f, pontoB.position + Vector3.up * 0.5f);
        if (pontoA != null && pontoB != null) Gizmos.DrawLine(pontoA.position, pontoB.position);

        if (Application.isPlaying && alvo != null)
        {
            bool livre = IAVisao.LinhaLivre(Olho, alvo, transform, obstaculos, out Vector2 fim);
            Gizmos.color = livre ? new Color(0.31f, 0.78f, 0.45f) : new Color(0.94f, 0.29f, 0.29f);
            Gizmos.DrawLine(Olho, fim);
        }
    }
}
