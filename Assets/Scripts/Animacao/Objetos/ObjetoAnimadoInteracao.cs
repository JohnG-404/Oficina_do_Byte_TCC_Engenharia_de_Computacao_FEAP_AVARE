using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Objeto animado da fase Animacao (moeda, jumppad, alavanca, bateria) que
/// só funciona depois de o Animator do tipo dele ser configurado no painel
/// ObjetoAnimatorUI. Enquanto não estiver configurado:
/// - o Animator fica desligado (o objeto fica parado no primeiro desenho);
/// - o script do objeto (MoedaFase, JumppadFase...) não faz nada
///   (pergunta <see cref="Liberado"/>);
/// - clicar no objeto, ou apertar a tecla de interagir perto dele, abre o
///   painel; o balão da tecla aparece com o personagem perto e o Prof Byte
///   explica na primeira vez.
/// Quando o painel é concluído, todos os objetos do mesmo tipo voltam a
/// funcionar e recebem a mensagem "AoAnimatorConfigurado".
/// </summary>
public class ObjetoAnimadoInteracao : MonoBehaviour
{
    [Tooltip("Tipo do objeto: acha o painel ObjetoAnimatorUI com a mesma chave.")]
    [SerializeField] private string tipo = "moeda";
    [Tooltip("Sprite do objeto: é nele que o clique é conferido.")]
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Animator animator;
    [Tooltip("Balão da tecla de interagir (DicaTecla, zona PeloObjeto) enquanto o Animator não está configurado.")]
    [SerializeField] private DicaTecla dica;
    [Tooltip("Distância (unidades) do personagem até o desenho do objeto pra valer a tecla de interagir.")]
    [SerializeField] private float raioPerto = 1.4f;
    [Tooltip("Folga (unidades) em volta do sprite que ainda conta como clique.")]
    [SerializeField] private float folgaClique = 0.2f;

    private static readonly Color CorDestaque = new Color(1f, 0.93f, 0.55f, 1f);

    private ObjetoAnimatorUI painel;
    private AnimacaoPlayerFisica player;
    private Color corBase = Color.white;
    private bool liberadoAntes;

    /// <summary>O Animator do tipo já foi configurado (ou não há painel pra ele).</summary>
    public bool Liberado
    {
        get
        {
            if (painel == null) painel = ObjetoAnimatorUI.Do(tipo);
            return painel == null || painel.Configurado;
        }
    }

    public string Tipo => tipo;

    /// <summary>Atalho para os scripts dos objetos: sem este componente, funciona como antes.</summary>
    public static bool EstaLiberado(Component c)
    {
        var i = c != null ? c.GetComponent<ObjetoAnimadoInteracao>() : null;
        return i == null || i.Liberado;
    }

    private void Start()
    {
        player = FindAnyObjectByType<AnimacaoPlayerFisica>();
        if (visual != null) corBase = visual.color;
        liberadoAntes = Liberado;
        if (animator != null) animator.enabled = liberadoAntes;
    }

    private void Update()
    {
        bool liberado = Liberado;
        if (liberado)
        {
            if (!liberadoAntes)
            {
                liberadoAntes = true;
                if (animator != null) animator.enabled = true;
                if (visual != null) visual.color = corBase;
                if (dica != null) dica.Mostrar(false);
                SendMessage("AoAnimatorConfigurado", SendMessageOptions.DontRequireReceiver);
            }

            return;
        }

        if (animator != null && animator.enabled) animator.enabled = false;

        AnimacaoLevelFlow flow = AnimacaoLevelFlow.Instance;
        bool podeAbrir = flow != null && painel != null && !painel.Aberto && flow.PodeAbrirPainelDeObjeto
            && visual != null && visual.gameObject.activeInHierarchy;
        bool perto = podeAbrir && player != null && !player.EstaMorto && Perto();
        bool mouseEmCima = podeAbrir && PonteiroEmCima();

        if (visual != null)
        {
            Color alvo = mouseEmCima ? CorDestaque : corBase;
            visual.color = Color.Lerp(visual.color, alvo, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        }

        if (dica != null) dica.Mostrar(perto);

        if (perto)
        {
            flow.FalarSobreObjeto(tipo + "_sem_animator", painel.FalasSemAnimator);
        }

        if (mouseEmCima && Mouse.current.leftButton.wasPressedThisFrame)
        {
            painel.Configurar();
        }
        else if (perto && !DialogueManager.IsAnyDialogueActive && Time.timeScale > 0f && !JogoBloqueado.Ativo
            && InputManager.Instance != null && InputManager.Instance.InteractAction != null
            && InputManager.Instance.InteractAction.WasPressedThisFrame())
        {
            painel.Configurar();
        }
    }

    private bool Perto()
    {
        Bounds b = visual.bounds;
        Vector3 p = player.transform.position;
        Vector3 maisPerto = b.ClosestPoint(new Vector3(p.x, p.y, b.center.z));
        return Vector2.Distance(maisPerto, p) < raioPerto;
    }

    private bool PonteiroEmCima()
    {
        if (Mouse.current == null)
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
}
