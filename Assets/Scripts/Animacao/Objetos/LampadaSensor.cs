using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Lâmpada sensora da fase "Animacao", no teto da área escura. A área é
/// dividida em trechos, um por lâmpada; cada uma tem a própria escuridão
/// (filha do Visual) e um trigger que cobre só o trecho dela: com alguém
/// dentro, ligada = true; saiu, false. O Animator dela tem um parâmetro Bool
/// "ligada" e dois estados, Apagada e Acesa.
///
/// No começo o Animator das lâmpadas não está configurado: a área fica escura
/// (não dá pra ver a escada) e as lâmpadas continuam visíveis. Clicar em uma
/// delas (ou apertar a tecla de interagir dentro da área) abre o painel
/// LampadaAnimatorUI, onde o jogador monta as duas transições. Todas usam o
/// mesmo Animator, então configurar uma configura todas.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LampadaSensor : MonoBehaviour
{
    public const string ParamLigada = "ligada";

    [SerializeField] private Animator animator;
    [Tooltip("Painel do Animator da lâmpada (aberto ao clicar nela).")]
    [SerializeField] private LampadaAnimatorUI painel;
    [Tooltip("Sprite da lâmpada: é nele que o clique é conferido.")]
    [SerializeField] private SpriteRenderer visual;
    [Tooltip("Texto \"clique na lâmpada\" ao lado dela. Com uma DicaTecla no mesmo objeto, vira o balão da tecla de interagir sobre o jogador.")]
    [SerializeField] private TMP_Text dicaClique;
    [Tooltip("Folga (em unidades) em volta do sprite que ainda conta como clique na lâmpada.")]
    [SerializeField] private float folgaClique = 0.25f;
    [Tooltip("Ligado = o Animator já começa configurado (pula o painel).")]
    [SerializeField] private bool configurada;

    [Header("Prof Byte")]
    [TextArea(2, 4)]
    [SerializeField] private string[] falasEscuro =
    {
        "Está escuro aqui! Essa área tem lâmpadas com sensor, mas o Animator delas ainda não foi configurado.",
        "Sem as transições, elas ficam paradas no estado Apagada. Clique em uma lâmpada ou use a tecla de interagir pra abrir o Animator.",
    };
    [TextArea(2, 4)]
    [SerializeField] private string[] falas =
    {
        "Viu a lâmpada acender? O sensor dela muda o parâmetro Bool ligada, e as setas que você montou fazem o resto.",
        "Todas usam o mesmo Animator Controller: no trecho de uma lâmpada, ligada = true e ela fica Acesa. Saiu, ligada = false e ela volta para Apagada.",
    };

    [Header("Código mostrado depois do painel")]
    [SerializeField] private string tituloCodigo = "Animator da lâmpada";
    [TextArea(4, 12)]
    [SerializeField] private string codigo = CodigoPadrao;
    [Tooltip("Não fecha mais o painel: ele fica aberto até Enter ou clique (ver CodigoMontadoUI).")]
    [SerializeField] private float segundosCodigo = 12f;

    public const string CodigoPadrao =
        "<color=#8B95A7>// LampadaSensor: um collider Is Trigger cobre a sala</color>\n" +
        "void OnTriggerEnter2D(Collider2D outro) {\n" +
        "    animator.SetBool(<color=#F5C542>\"ligada\"</color>, true);\n}\n" +
        "void OnTriggerExit2D(Collider2D outro) {\n" +
        "    animator.SetBool(<color=#F5C542>\"ligada\"</color>, false);\n}\n" +
        "<color=#8B95A7>// Transições (as setas que você montou)</color>\n" +
        "Apagada -> Acesa:  <color=#F5C542>ligada == true</color>\n" +
        "Acesa -> Apagada:  <color=#F5C542>ligada == false</color>";

    public bool Configurada => configurada;

    private static readonly Color CorDestaque = new Color(1f, 0.93f, 0.55f, 1f);

    // Todas as lâmpadas da cena: o Animator é o mesmo, a configuração vale pra todas.
    private static readonly List<LampadaSensor> Todas = new List<LampadaSensor>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Todas.Clear();

    private const string TipoLampada = "lampada";

    private int dentro;
    private DicaTecla dicaTecla;

    private void OnEnable()
    {
        Todas.Add(this);
    }

    private void OnDisable()
    {
        Todas.Remove(this);
    }

    /// <summary>O jogador está no trecho de alguma lâmpada (a área escura inteira).</summary>
    private static bool JogadorNaArea()
    {
        foreach (LampadaSensor l in Todas)
        {
            if (l.dentro > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (painel == null)
        {
            painel = GetComponent<LampadaAnimatorUI>();
        }

        if (dicaClique != null)
        {
            dicaTecla = dicaClique.GetComponent<DicaTecla>();
        }
    }

    // Volta de uma morte ou de um reinício com o Animator já configurado.
    private void Start()
    {
        if (AnimacaoLevelFlow.ObjetoMontado(TipoLampada))
        {
            configurada = true;
        }
    }

    private void Update()
    {
        bool podeClicar = !configurada && painel != null && !painel.Aberto
            && AnimacaoLevelFlow.Instance != null && AnimacaoLevelFlow.Instance.PodeAbrirPainelDeObjeto;
        bool mouseEmCima = podeClicar && PonteiroNaLampada();

        if (visual != null)
        {
            // Passar o mouse destaca a lâmpada (tom amarelado): dá pra ver que
            // ela é clicável. Só a cor: a escala do Visual também mexeria na
            // escuridão, que é filha dele.
            Color alvo = mouseEmCima ? CorDestaque : Color.white;
            visual.color = Color.Lerp(visual.color, alvo, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        }

        if (dicaTecla != null)
        {
            // Balão com a tecla de interagir sobre o jogador, só com ele na sala.
            if (!dicaClique.gameObject.activeSelf) dicaClique.gameObject.SetActive(true);
            dicaTecla.Mostrar(podeClicar && JogadorNaArea());
        }
        else if (dicaClique != null)
        {
            // Só com o personagem na sala: é ali que a dica faz sentido (e
            // longe dela o texto ficaria embaixo do painel do Animator do Player).
            bool mostrar = podeClicar && JogadorNaArea();
            if (dicaClique.gameObject.activeSelf != mostrar)
            {
                dicaClique.gameObject.SetActive(mostrar);
            }

            if (mostrar)
            {
                Color c = dicaClique.color;
                c.a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
                dicaClique.color = c;
            }
        }

        if (mouseEmCima && Mouse.current.leftButton.wasPressedThisFrame)
        {
            AbrirPainel();
        }
        else if (dicaClique != null && podeClicar && JogadorNaArea() && !DialogueManager.IsAnyDialogueActive && Time.timeScale > 0f && !JogoBloqueado.Ativo
            && InputManager.Instance != null && InputManager.Instance.InteractAction.WasPressedThisFrame())
        {
            // Dentro da área, a tecla de interagir também abre o painel (só a
            // lâmpada com a dica cuida disso, pra não abrir duas vezes).
            AbrirPainel();
        }
    }

    private bool PonteiroNaLampada()
    {
        if (visual == null || Mouse.current == null)
        {
            return false;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return false;
        }

        Vector2 tela = Mouse.current.position.ReadValue();
        Vector3 mundo = cam.ScreenToWorldPoint(new Vector3(tela.x, tela.y, -cam.transform.position.z));
        Bounds b = visual.bounds;
        b.Expand(folgaClique * 2f);
        mundo.z = b.center.z;
        return b.Contains(mundo);
    }

    /// <summary>Abre o painel do Animator da lâmpada (o clique chama isto).</summary>
    public void AbrirPainel()
    {
        if (configurada || painel == null || painel.Aberto || AnimacaoLevelFlow.Instance == null)
        {
            return;
        }

        if (!AnimacaoLevelFlow.Instance.AbrirPainelDeObjeto())
        {
            return;
        }

        painel.Abrir(AoConfigurar, AoCancelar);
    }

    private void AoConfigurar()
    {
        foreach (LampadaSensor l in Todas)
        {
            l.configurada = true;
        }

        AnimacaoLevelFlow.MarcarObjetoMontado(TipoLampada);

        // Um instante pra ver o diagrama completo antes do painel de código.
        Invoke(nameof(MostrarCodigo), 0.8f);
    }

    private void MostrarCodigo()
    {
        CodigoMontadoUI.Show(tituloCodigo, codigo, segundosCodigo, AoFecharCodigo);
    }

    private void AoFecharCodigo()
    {
        painel.Fechar();
        if (AnimacaoLevelFlow.Instance != null)
        {
            AnimacaoLevelFlow.Instance.FecharPainelDeObjeto();
        }

        // Quem já está na área vê a lâmpada do trecho acender na hora.
        foreach (LampadaSensor l in Todas)
        {
            if (l.dentro > 0 && l.animator != null)
            {
                l.animator.SetBool(ParamLigada, true);
            }
        }
    }

    private void AoCancelar()
    {
        if (AnimacaoLevelFlow.Instance != null)
        {
            AnimacaoLevelFlow.Instance.FecharPainelDeObjeto();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<AnimacaoPlayerFisica>() == null)
        {
            return;
        }

        dentro++;
        if (configurada && animator != null) animator.SetBool(ParamLigada, true);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (dentro > 0 && AnimacaoLevelFlow.Instance != null && other.GetComponentInParent<AnimacaoPlayerFisica>() != null)
        {
            if (configurada)
            {
                AnimacaoLevelFlow.Instance.FalarSobreObjeto("lampada", falas);
            }
            else
            {
                AnimacaoLevelFlow.Instance.FalarSobreObjeto("lampada_escura", falasEscuro);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<AnimacaoPlayerFisica>() == null)
        {
            return;
        }

        dentro = Mathf.Max(0, dentro - 1);
        if (dentro == 0 && animator != null) animator.SetBool(ParamLigada, false);
    }
}
