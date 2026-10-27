using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Cartãozinho que aparece ao lado do mouse quando ele passa em cima de um
/// objeto do mundo que recebe ferramentas (blocos com PhysicsTarget e o
/// próprio Player). Lista os componentes com os nomes do Unity e diz se o
/// objeto já "tem", se "falta" ou se "veio com o cenário" — o mesmo
/// vocabulário do Inspector.
///
/// Fica num objeto próprio do Canvas (sempre ativo, sem gráfico); a caixa
/// visível é um filho que só aparece quando há algo embaixo do mouse.
/// Esconde enquanto o botão do mouse está apertado (arrastando do inventário),
/// com o mouse em cima da UI ou durante um diálogo.
/// </summary>
public class MiniInspector : MonoBehaviour
{
    [System.Serializable]
    public struct Linha
    {
        public ToolType ferramenta;
        public GameObject raiz;
        public TMP_Text nome;
        public TMP_Text estado;
    }

    [Tooltip("Caixa visível (filho deste objeto).")]
    [SerializeField] private RectTransform caixa;
    [SerializeField] private TMP_Text titulo;
    [SerializeField] private Linha[] linhas;
    [SerializeField] private TMP_Text rodape;
    [Tooltip("Distância entre a ponta do mouse e o cartão (pixels da tela de referência).")]
    [SerializeField] private Vector2 deslocamento = new Vector2(22f, -22f);
    [SerializeField] private float alturaLinha = 28f;
    [SerializeField] private float alturaBase = 74f;

    [Header("Cores do estado")]
    [SerializeField] private Color corTem = new Color(0.525f, 0.937f, 0.675f);
    [SerializeField] private Color corFalta = new Color(0.973f, 0.443f, 0.443f);
    [SerializeField] private Color corCenario = new Color(0.541f, 0.576f, 0.651f);

    private readonly Collider2D[] acertos = new Collider2D[8];
    private Camera cam;
    private Canvas canvas;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (caixa != null)
        {
            caixa.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        IToolReceiver alvo = ObjetoEmbaixoDoMouse(out GameObject go, out Vector2 telaMouse);
        if (alvo == null)
        {
            Mostrar(false);
            return;
        }

        Preencher(alvo, go);
        Posicionar(telaMouse);
        Mostrar(true);
    }

    private IToolReceiver ObjetoEmbaixoDoMouse(out GameObject go, out Vector2 telaMouse)
    {
        go = null;
        telaMouse = Vector2.zero;
        Mouse mouse = Mouse.current;
        if (mouse == null || mouse.leftButton.isPressed || DialogueManager.IsAnyDialogueActive)
        {
            return null;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return null;
        }

        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null)
            {
                return null;
            }
        }

        telaMouse = mouse.position.ReadValue();
        Vector2 mundo = cam.ScreenToWorldPoint(telaMouse);
        int n = Physics2D.OverlapPoint(mundo, ContactFilter2D.noFilter, acertos);
        for (int i = 0; i < n; i++)
        {
            IToolReceiver r = acertos[i].GetComponentInParent<IToolReceiver>();
            if (r != null)
            {
                go = ((Component)r).gameObject;
                return r;
            }
        }

        return null;
    }

    private void Preencher(IToolReceiver alvo, GameObject go)
    {
        if (titulo != null)
        {
            titulo.text = go.name;
        }

        var pt = go.GetComponent<PhysicsTarget>();
        int visiveis = 0;
        for (int i = 0; i < linhas.Length; i++)
        {
            Linha l = linhas[i];
            bool aceita = alvo.CanReceive(l.ferramenta);
            if (l.raiz != null)
            {
                l.raiz.SetActive(aceita);
            }

            if (!aceita)
            {
                continue;
            }

            if (l.raiz != null)
            {
                var rt = (RectTransform)l.raiz.transform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -44f - visiveis * alturaLinha);
            }

            visiveis++;
            if (l.estado == null)
            {
                continue;
            }

            bool doCenario = pt != null && l.ferramenta == ToolType.Colisao && pt.ComecaComColisao;
            if (doCenario)
            {
                l.estado.text = "veio com o cenário";
                l.estado.color = corCenario;
            }
            else if (alvo.HasTool(l.ferramenta))
            {
                l.estado.text = "tem";
                l.estado.color = corTem;
            }
            else
            {
                l.estado.text = "falta";
                l.estado.color = corFalta;
            }
        }

        bool faltaAlgo = false;
        foreach (Linha l in linhas)
        {
            if (alvo.CanReceive(l.ferramenta) && !alvo.HasTool(l.ferramenta))
            {
                faltaAlgo = true;
            }
        }

        if (rodape != null)
        {
            rodape.text = faltaAlgo ? "arraste do inventário" : "nada faltando";
            var rr = rodape.rectTransform;
            rr.anchoredPosition = new Vector2(rr.anchoredPosition.x, -44f - visiveis * alturaLinha - 2f);
        }

        caixa.sizeDelta = new Vector2(caixa.sizeDelta.x, alturaBase + visiveis * alturaLinha);
    }

    private void Posicionar(Vector2 telaMouse)
    {
        var pai = (RectTransform)caixa.parent;
        Camera camUi = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(pai, telaMouse, camUi, out Vector2 local);

        // Vira para o outro lado do mouse quando encostaria na borda da tela.
        Rect r = pai.rect;
        Vector2 tam = caixa.sizeDelta;
        float x = local.x + deslocamento.x;
        float y = local.y + deslocamento.y;
        if (x + tam.x > r.xMax)
        {
            x = local.x - deslocamento.x - tam.x;
        }

        if (y - tam.y < r.yMin)
        {
            y = local.y - deslocamento.y + tam.y;
        }

        caixa.pivot = new Vector2(0f, 1f);
        caixa.localPosition = new Vector3(x, y, 0f);
    }

    private void Mostrar(bool sim)
    {
        if (caixa != null && caixa.gameObject.activeSelf != sim)
        {
            caixa.gameObject.SetActive(sim);
            if (sim)
            {
                transform.SetAsLastSibling();
            }
        }
    }
}
