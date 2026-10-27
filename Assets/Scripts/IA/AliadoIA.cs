using System;
using UnityEngine;

/// <summary>
/// "Cérebro" do Aliado (o robozinho verde da IA_Aplicacao). É a MESMA
/// estrutura do Guarda: estados + transições + percepção. Só mudam o alvo e
/// as regras. É isso que a fase ensina: inimigo e aliado são a mesma
/// ferramenta configurada de outro jeito.
///
/// Estados:
/// - Seguir: anda na direção do jogador (linearVelocity).
/// - Esperar: fica parado perto do jogador.
/// - Distrair: fica visível na frente do guarda até ele mirar no aliado e
///   então corre pro lado oposto, levando o guarda junto.
///
/// As condições das setas NÃO são fixas: o jogador arrasta cada uma pro
/// diagrama (IADiagramaUI). Condição errada = comportamento errado, visível
/// no cenário (ex: um aliado que só anda quando você aperta tecla).
///
/// Encaixes: 0 = Esperar → Seguir, 1 = Seguir → Esperar, 2 = Seguir/Esperar → Distrair.
/// Saída do Distrair (implícita): se o guarda desistir dele por um tempo,
/// volta a Seguir, pra poder distrair de novo.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class AliadoIA : MonoBehaviour, ICerebroConfiguravel
{
    public enum Estado
    {
        Seguir,
        Esperar,
        Distrair
    }

    // Ids das condições (também usados no registro e na configuração salva).
    public const string CondDistanciaMaior2 = "dist_jogador_maior_2";
    public const string CondDistanciaMenor15 = "dist_jogador_menor_1_5";
    public const string CondRaycastVeGuarda = "raycast_ve_guarda";
    public const string CondEncostouNoGuarda = "oncollisionenter2d_guarda";
    public const string CondInputGetAxis = "input_getaxis";
    public const string CondStart = "start";

    public const int SlotEsperarParaSeguir = 0;
    public const int SlotSeguirParaEsperar = 1;
    public const int SlotParaDistrair = 2;

    [Header("Referências")]
    [SerializeField] private Transform jogador;
    [SerializeField] private GuardaIA guarda;
    [Tooltip("Filho com o sprite (vira pro lado em que anda).")]
    [SerializeField] private Transform visual;

    [Header("Movimento")]
    [SerializeField] private float velocidadeSeguir = 3.5f;
    [SerializeField] private float velocidadeFuga = 3.6f;
    [Tooltip("Seguir: para de andar quando está a essa distância horizontal do jogador.")]
    [SerializeField] private float distanciaParar = 0.4f;
    [Tooltip("Limites do cenário pra fuga (x mínimo e máximo).")]
    [SerializeField] private Vector2 limitesX = new Vector2(-100f, 100f);

    [Header("Percepção (Raycast vê o Guarda)")]
    [SerializeField] private float raioVisao = 6f;
    [SerializeField] private LayerMask obstaculos = 1;
    [SerializeField] private Vector2 olhoOffset = new Vector2(0f, 0.15f);

    [Header("Condições nas setas (ids — o jogador troca pelo painel)")]
    [SerializeField] private string condEsperarParaSeguir = "";
    [SerializeField] private string condSeguirParaEsperar = "";
    [SerializeField] private string condParaDistrair = "";

    [Header("Estados")]
    [SerializeField] private Estado estadoInicial = Estado.Esperar;
    [Tooltip("Distrair acaba se o guarda ficar esse tempo sem mirar no aliado depois de já ter mirado.")]
    [SerializeField] private float tempoParaDesistir = 2.5f;

    public event Action<Estado, Estado> OnStateChanged;

    public Estado EstadoAtual => estado;
    public bool Pausado { get; set; }
    public Vector2 Olho => IAVisao.Centro(transform) + olhoOffset;

    /// <summary>As três setas com as condições certas (usado nas dicas).</summary>
    public bool ConfiguracaoCorreta =>
        condEsperarParaSeguir == CondDistanciaMaior2 &&
        condSeguirParaEsperar == CondDistanciaMenor15 &&
        condParaDistrair == CondRaycastVeGuarda;

    public bool DistracaoCorreta => condParaDistrair == CondRaycastVeGuarda;
    public bool SegueCorretamente => condEsperarParaSeguir == CondDistanciaMaior2;

    private Rigidbody2D rb;
    private SpriteRenderer visualRenderer;
    private Estado estado;
    private float velocidadeDesejadaX;
    private bool foiNotado;
    private float tempoSemMira;
    private int frameDeInicio;
    private IADiagrama diagrama;

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
        }

        if (jogador == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) jogador = p.transform;
        }

        estado = estadoInicial;
        frameDeInicio = Time.frameCount;
    }

    private void Update()
    {
        if (Pausado || jogador == null)
        {
            velocidadeDesejadaX = 0f;
            AplicarVelocidade();
            return;
        }

        Decidir();
        Agir();
        AplicarVelocidade();
    }

    // ------------------------------------------------------------------
    // Transições (condições escolhidas pelo jogador)
    // ------------------------------------------------------------------

    private void Decidir()
    {
        if (estado != Estado.Distrair && Avaliar(condParaDistrair))
        {
            MudarEstado(Estado.Distrair);
            return;
        }

        if (estado == Estado.Seguir && Avaliar(condSeguirParaEsperar))
        {
            MudarEstado(Estado.Esperar);
        }
        else if (estado == Estado.Esperar && Avaliar(condEsperarParaSeguir))
        {
            MudarEstado(Estado.Seguir);
        }
    }

    /// <summary>Avalia uma condição do inventário. Condições "isca" também funcionam, do jeito errado.</summary>
    private bool Avaliar(string condicao)
    {
        switch (condicao)
        {
            case CondDistanciaMaior2:
                return DistanciaDoJogador() > 2f;

            case CondDistanciaMenor15:
                return DistanciaDoJogador() < 1.5f;

            case CondRaycastVeGuarda:
                return VeGuarda();

            case CondEncostouNoGuarda:
                // Só quando já encostou: tarde demais pra distrair de longe.
                return guarda != null && Vector2.Distance(IAVisao.Centro(transform), IAVisao.Centro(guarda.transform)) < 0.9f;

            case CondInputGetAxis:
                // Lê o teclado do jogador: o aliado só "decide" quando você aperta uma tecla.
                return InputManager.Instance != null && Mathf.Abs(InputManager.Instance.MoveAction.ReadValue<float>()) > 0.1f;

            case CondStart:
                // Roda uma vez, quando o aliado nasce.
                return Time.frameCount == frameDeInicio;

            default:
                return false;
        }
    }

    private float DistanciaDoJogador()
    {
        return Vector2.Distance(IAVisao.Centro(transform), IAVisao.Centro(jogador));
    }

    private bool VeGuarda()
    {
        if (guarda == null)
        {
            return false;
        }

        float d = Vector2.Distance(IAVisao.Centro(transform), IAVisao.Centro(guarda.transform));
        return d < raioVisao && IAVisao.LinhaLivre(Olho, guarda.transform, transform, obstaculos);
    }

    // ------------------------------------------------------------------
    // Comportamentos
    // ------------------------------------------------------------------

    private void Agir()
    {
        switch (estado)
        {
            case Estado.Seguir:
            {
                float dx = IAVisao.Centro(jogador).x - transform.position.x;
                velocidadeDesejadaX = Mathf.Abs(dx) > distanciaParar ? Mathf.Sign(dx) * velocidadeSeguir : 0f;
                break;
            }

            case Estado.Esperar:
                velocidadeDesejadaX = 0f;
                break;

            case Estado.Distrair:
                Distrair();
                break;
        }
    }

    // Fica visível até o guarda mirar nele; aí corre pro lado oposto ao guarda.
    private void Distrair()
    {
        bool guardaMirandoEmMim = guarda != null &&
            guarda.AlvoAtual != null &&
            guarda.AlvoAtual.IsChildOf(transform) &&
            guarda.EstadoAtual != GuardaIA.Estado.Patrulhar;

        if (guardaMirandoEmMim)
        {
            foiNotado = true;
            tempoSemMira = 0f;

            float direcao = Mathf.Sign(transform.position.x - guarda.transform.position.x);
            if (direcao == 0f) direcao = -1f;

            bool noLimite = (direcao < 0f && transform.position.x <= limitesX.x) ||
                            (direcao > 0f && transform.position.x >= limitesX.y);
            velocidadeDesejadaX = noLimite ? 0f : direcao * velocidadeFuga;
            return;
        }

        velocidadeDesejadaX = 0f;

        if (foiNotado)
        {
            tempoSemMira += Time.deltaTime;
            if (tempoSemMira >= tempoParaDesistir)
            {
                foiNotado = false;
                tempoSemMira = 0f;
                MudarEstado(Estado.Seguir);
            }
        }
    }

    private void AplicarVelocidade()
    {
        rb.linearVelocity = new Vector2(velocidadeDesejadaX, rb.linearVelocity.y);

        if (visualRenderer != null && Mathf.Abs(velocidadeDesejadaX) > 0.01f)
        {
            visualRenderer.flipX = velocidadeDesejadaX < 0f;
        }
    }

    private void MudarEstado(Estado novo)
    {
        if (novo == estado)
        {
            return;
        }

        Estado anterior = estado;
        estado = novo;
        if (novo != Estado.Distrair)
        {
            foiNotado = false;
            tempoSemMira = 0f;
        }

        OnStateChanged?.Invoke(anterior, novo);
    }

    /// <summary>Volta ao estado inicial (ex: jogador foi pego e voltou pro começo).</summary>
    public void Reiniciar()
    {
        MudarEstado(estadoInicial);
        foiNotado = false;
        tempoSemMira = 0f;
    }

    // ------------------------------------------------------------------
    // ICerebroConfiguravel (painel do diagrama)
    // ------------------------------------------------------------------

    public string IdCerebro => "Aliado";

    public IADiagrama Diagrama => diagrama ?? (diagrama = CriarDiagrama());

    public int EstadoAtivoIndice => (int)estado;

    public int QuantidadeDeSlots => 3;

    public string GetOpcaoDoSlot(int slot)
    {
        switch (slot)
        {
            case SlotEsperarParaSeguir: return condEsperarParaSeguir;
            case SlotSeguirParaEsperar: return condSeguirParaEsperar;
            case SlotParaDistrair: return condParaDistrair;
            default: return null;
        }
    }

    public void SetOpcaoDoSlot(int slot, string opcaoId)
    {
        switch (slot)
        {
            case SlotEsperarParaSeguir: condEsperarParaSeguir = opcaoId ?? ""; break;
            case SlotSeguirParaEsperar: condSeguirParaEsperar = opcaoId ?? ""; break;
            case SlotParaDistrair: condParaDistrair = opcaoId ?? ""; break;
        }

        // Regras novas valem na hora; "Start" roda de novo como se tivesse nascido.
        frameDeInicio = Time.frameCount + 1;
    }

    private static IADiagrama CriarDiagrama()
    {
        var d = new IADiagrama
        {
            titulo = "Cérebro do Aliado",
            subtitulo = "Arraste uma condição do inventário até o encaixe de uma seta. A regra vale na hora."
        };

        // A ordem dos estados segue o enum Estado (EstadoAtivoIndice).
        // Seguir e Esperar mais altas: as duas setas saem da lateral, a de ida em cima e a de volta embaixo.
        var caixaAlta = new Vector2(210f, 170f);
        d.estados.Add(new IADiagramaEstado("Seguir", new Vector2(0.17f, 0.757f), caixaAlta));
        d.estados.Add(new IADiagramaEstado("Esperar", new Vector2(0.83f, 0.757f), caixaAlta));
        d.estados.Add(new IADiagramaEstado("Distrair", new Vector2(0.5f, 0.12f)));

        d.setas.Add(new IADiagramaSeta(0, 1, 0.083f));   // Seguir → Esperar (em cima)
        d.setas.Add(new IADiagramaSeta(1, 0, 0.083f));   // Esperar → Seguir (embaixo)
        d.setas.Add(new IADiagramaSeta(0, 2));          // Seguir → Distrair
        d.setas.Add(new IADiagramaSeta(1, 2));          // Esperar → Distrair

        // Cada encaixe fica em cima da sua seta.
        d.slots.Add(new IADiagramaSlot("Esperar -> Seguir", new Vector2(0.5f, 0.674f), legendaEmbaixo: true));
        d.slots.Add(new IADiagramaSlot("Seguir -> Esperar", new Vector2(0.5f, 0.841f)));
        d.slots.Add(new IADiagramaSlot("Seguir ou Esperar -> Distrair", new Vector2(0.5f, 0.36f)));

        d.inventario.Add(new IAOpcao(CondDistanciaMaior2, "Distance(jogador) > 2", "Vector2.Distance",
            "Verdadeiro quando a distância até o jogador é maior que 2.").ComArte("vector2"));
        d.inventario.Add(new IAOpcao(CondDistanciaMenor15, "Distance(jogador) < 1.5f", "Vector2.Distance",
            "Verdadeiro quando a distância até o jogador é menor que 1.5.").ComArte("vector2"));
        d.inventario.Add(new IAOpcao(CondRaycastVeGuarda, "Raycast vê o Guarda", "Physics2D.Raycast",
            "Lança uma linha do aliado até o guarda. Verdadeiro quando nada bloqueia o caminho.").ComArte("isgrounded"));
        d.inventario.Add(new IAOpcao(CondEncostouNoGuarda, "OnCollisionEnter2D", "",
            "Método chamado quando o aliado encosta no guarda.").ComArte("oncollisionenter2d"));
        d.inventario.Add(new IAOpcao(CondInputGetAxis, "Input.GetAxis", "",
            "Lê o teclado: devolve -1, 0 ou 1 conforme as setas apertadas.").ComArte("input"));
        d.inventario.Add(new IAOpcao(CondStart, "Start", "",
            "Roda uma vez, quando o aliado nasce.").ComArte("start"));

        return d;
    }
}
