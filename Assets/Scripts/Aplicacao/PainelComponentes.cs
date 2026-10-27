using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Painel que aparece ao lado do mouse quando ele para em cima de um objeto
/// que recebe ferramentas (blocos com PhysicsTarget e o próprio Player).
/// Mostra em duas seções o que o objeto já tem ("Aplicados") e o que ainda
/// dá pra arrastar até ele ("Pode aplicar"), com os nomes do Unity, mais uma
/// frase curta sobre o que isso significa no jogo.
///
/// É o único retorno visual de quais componentes cada objeto tem: os objetos
/// em si não ganham contorno, selo nem transparência.
///
/// Fica num objeto próprio do Canvas (sempre ativo, sem gráfico); a caixa
/// visível é um filho que só aparece quando há algo embaixo do mouse.
/// Esconde enquanto o botão do mouse está apertado (arrastando do
/// inventário), com o mouse em cima da UI ou durante um diálogo.
/// Montado por Tools > Aplicação > Montar Painel de Componentes.
/// </summary>
public class PainelComponentes : MonoBehaviour
{
    [System.Serializable]
    public struct Linha
    {
        public ToolType ferramenta;
        public RectTransform raiz;
        public Image fundo;
        public Image borda;
        public Image icone;
        public TMP_Text nome;
        public TMP_Text frase;
        public RectTransform estado;
        public Image estadoFundo;
        public Image estadoBorda;
        public TMP_Text estadoTexto;
    }

    [Tooltip("Caixa visível (filho deste objeto).")]
    [SerializeField] private RectTransform caixa;
    [SerializeField] private TMP_Text titulo;
    [SerializeField] private RectTransform tipo;
    [SerializeField] private TMP_Text tipoTexto;
    [SerializeField] private RectTransform rotuloAplicados;
    [SerializeField] private RectTransform vazioAplicados;
    [SerializeField] private RectTransform rotuloPodeAplicar;
    [SerializeField] private RectTransform vazioPodeAplicar;
    [SerializeField] private Linha[] linhas;
    [SerializeField] private RectTransform separadorRodape;
    [SerializeField] private TMP_Text rodape;

    [Header("Posição")]
    [Tooltip("Distância entre a ponta do mouse e o painel (pixels da tela de referência).")]
    [SerializeField] private Vector2 deslocamento = new Vector2(22f, 30f);
    [Tooltip("Folga mínima até a borda da tela.")]
    [SerializeField] private float margemTela = 20f;

    [Header("Cores")]
    [SerializeField] private Color cartao = new Color(0.059f, 0.082f, 0.133f);
    [SerializeField] private Color fundoVazio = new Color(0.047f, 0.067f, 0.106f);
    [SerializeField] private Color bordaVazia = new Color(0.180f, 0.196f, 0.235f);
    [SerializeField] private Color pilulaNeutra = new Color(0.118f, 0.125f, 0.149f);
    [SerializeField] private Color texto = new Color(0.902f, 0.914f, 0.937f);
    [SerializeField] private Color textoApagado = new Color(0.541f, 0.576f, 0.651f);
    [SerializeField] private Color textoFraco = new Color(0.361f, 0.392f, 0.459f);
    [SerializeField] private Color fraseApagada = new Color(0.420f, 0.451f, 0.522f);

    private const float Largura = 360f;
    private const float AlturaLinha = 50f;
    private const float AlturaVazio = 30f;
    private const float AlturaRotulo = 22f;

    private readonly Collider2D[] acertos = new Collider2D[8];
    private Camera cam;
    private Canvas canvas;

    // Só refaz textos e layout quando o objeto ou o que ele tem muda.
    private GameObject ultimoAlvo;
    private int ultimoEstado = -1;

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

        int estado = Estado(alvo);
        if (go != ultimoAlvo || estado != ultimoEstado)
        {
            ultimoAlvo = go;
            ultimoEstado = estado;
            Preencher(alvo, go);
        }

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
        // Tudo que ainda não tem Collider2D é Trigger, e o projeto deixa
        // Physics2D.queriesHitTriggers desligado (a flag vale mesmo com
        // useTriggers no filtro): liga só durante a busca, como o
        // DraggableToolIcon faz.
        var filtro = new ContactFilter2D { useTriggers = true };
        bool queriesHitTriggers = Physics2D.queriesHitTriggers;
        int n;
        try
        {
            Physics2D.queriesHitTriggers = true;
            n = Physics2D.OverlapPoint(mundo, filtro, acertos);
        }
        finally
        {
            Physics2D.queriesHitTriggers = queriesHitTriggers;
        }

        // O Player tem prioridade quando está na frente de um bloco.
        IToolReceiver achado = null;
        for (int i = 0; i < n; i++)
        {
            IToolReceiver r = acertos[i].GetComponentInParent<IToolReceiver>();
            if (r == null)
            {
                continue;
            }

            if (r is AplicacaoPlayerController)
            {
                achado = r;
                break;
            }

            achado ??= r;
        }

        if (achado != null)
        {
            go = ((Component)achado).gameObject;
        }

        return achado;
    }

    private int Estado(IToolReceiver alvo)
    {
        int bits = 0;
        for (int i = 0; i < linhas.Length; i++)
        {
            if (alvo.HasTool(linhas[i].ferramenta))
            {
                bits |= 1 << i;
            }
        }

        return bits;
    }

    private void Preencher(IToolReceiver alvo, GameObject go)
    {
        var pt = go.GetComponent<PhysicsTarget>();
        bool ehPlayer = go.GetComponent<AplicacaoPlayerController>() != null;

        titulo.text = NomeLimpo(go.name);
        tipoTexto.text = ehPlayer ? "personagem" : "objeto";
        float larguraTipo = tipoTexto.text.Length * 8.5f + 22f;
        tipo.sizeDelta = new Vector2(larguraTipo, tipo.sizeDelta.y);
        tipo.anchoredPosition = new Vector2(Largura - 16f - larguraTipo, tipo.anchoredPosition.y);

        float y = 63f;
        Colocar(rotuloAplicados, y);
        y += AlturaRotulo;
        int aplicados = 0;
        for (int i = 0; i < linhas.Length; i++)
        {
            if (!alvo.HasTool(linhas[i].ferramenta))
            {
                continue;
            }

            bool doCenario = pt != null && linhas[i].ferramenta == ToolType.Colisao && pt.ComecaComColisao;
            PintarLinha(linhas[i], true, doCenario);
            Colocar(linhas[i].raiz, y);
            y += AlturaLinha;
            aplicados++;
        }

        vazioAplicados.gameObject.SetActive(aplicados == 0);
        if (aplicados == 0)
        {
            Colocar(vazioAplicados, y);
            y += AlturaVazio;
        }

        y += 8f;
        Colocar(rotuloPodeAplicar, y);
        y += AlturaRotulo;
        int podeAplicar = 0;
        for (int i = 0; i < linhas.Length; i++)
        {
            Linha l = linhas[i];
            bool tem = alvo.HasTool(l.ferramenta);
            bool aceita = alvo.CanReceive(l.ferramenta);
            l.raiz.gameObject.SetActive(tem || aceita);
            if (tem || !aceita)
            {
                continue;
            }

            PintarLinha(l, false, false);
            Colocar(l.raiz, y);
            y += AlturaLinha;
            podeAplicar++;
        }

        vazioPodeAplicar.gameObject.SetActive(podeAplicar == 0);
        if (podeAplicar == 0)
        {
            Colocar(vazioPodeAplicar, y);
            y += AlturaVazio;
        }

        y += 6f;
        Colocar(separadorRodape, y);
        Colocar(rodape.rectTransform, y + 1f);
        rodape.text = ehPlayer ? FrasePlayer(alvo) : FraseObjeto(alvo, pt);
        caixa.sizeDelta = new Vector2(Largura, y + 40f);
    }

    private void PintarLinha(Linha l, bool tem, bool doCenario)
    {
        Color cor = ToolColors.For(l.ferramenta);
        l.fundo.color = tem ? Color.Lerp(cartao, cor, 0.12f) : fundoVazio;
        l.borda.color = tem ? ComAlfa(cor, 0.55f) : bordaVazia;
        l.icone.color = new Color(1f, 1f, 1f, tem ? 1f : 0.35f);
        l.nome.color = tem ? texto : textoApagado;
        l.frase.text = tem ? FraseTem(l.ferramenta) : FrasePode(l.ferramenta);
        l.frase.color = tem ? Color.Lerp(cor, Color.white, 0.35f) : fraseApagada;

        string pilula = !tem ? "ARRASTE" : doCenario ? "CENÁRIO" : "TEM";
        float largura = pilula.Length * 8f + 18f;
        l.estado.sizeDelta = new Vector2(largura, l.estado.sizeDelta.y);
        l.estado.anchoredPosition = new Vector2(Largura - 28f - largura - 10f, l.estado.anchoredPosition.y);
        l.estadoTexto.text = pilula;
        if (tem && !doCenario)
        {
            l.estadoFundo.color = ComAlfa(cor, 0.2f);
            l.estadoBorda.color = ComAlfa(cor, 0.8f);
            l.estadoTexto.color = cor;
        }
        else if (tem)
        {
            l.estadoFundo.color = pilulaNeutra;
            l.estadoBorda.color = ComAlfa(cor, 0.45f);
            l.estadoTexto.color = Color.Lerp(cor, textoApagado, 0.5f);
        }
        else
        {
            l.estadoFundo.color = pilulaNeutra;
            l.estadoBorda.color = bordaVazia;
            l.estadoTexto.color = textoFraco;
        }
    }

    private static string FraseTem(ToolType t)
    {
        switch (t)
        {
            case ToolType.Colisao: return "sólido: dá pra pisar";
            case ToolType.Fisica: return "tem peso: cai com a gravidade";
            case ToolType.Movimento: return "anda para os lados";
            default: return "pula";
        }
    }

    private static string FrasePode(ToolType t)
    {
        switch (t)
        {
            case ToolType.Colisao: return "deixaria sólido";
            case ToolType.Fisica: return "faria cair com a gravidade";
            case ToolType.Movimento: return "faria andar";
            default: return "faria pular";
        }
    }

    private static string FraseObjeto(IToolReceiver alvo, PhysicsTarget pt)
    {
        bool colisao = alvo.HasTool(ToolType.Colisao);
        bool fisica = alvo.HasTool(ToolType.Fisica);
        if (!colisao)
        {
            return fisica ? "Sem Collider2D: cai atravessando tudo." : "Sem Collider2D: o Player atravessa.";
        }

        if (fisica)
        {
            return "Sólido e com peso: cai até apoiar em algo.";
        }

        return pt != null && pt.ComecaComColisao ? "Já veio sólido com o cenário." : "Sólido: dá pra pisar em cima.";
    }

    private static string FrasePlayer(IToolReceiver alvo)
    {
        bool colisao = alvo.HasTool(ToolType.Colisao);
        bool fisica = alvo.HasTool(ToolType.Fisica);
        bool anda = alvo.HasTool(ToolType.Movimento);
        bool pula = alvo.HasTool(ToolType.Pulo);
        if (!fisica && !colisao)
        {
            return "Sem Rigidbody2D nem Collider2D: fica no ar.";
        }

        if (fisica && !colisao)
        {
            return "Sem Collider2D: cai atravessando o chão.";
        }

        if (!fisica)
        {
            return "Sólido, mas sem Rigidbody2D: não cai.";
        }

        if (anda && pula)
        {
            return "Pronto: anda e pula.";
        }

        if (anda)
        {
            return "Anda para os lados, mas ainda não pula.";
        }

        return pula ? "Pula, mas ainda não anda." : "Sólido e com peso: cai e pisa no chão.";
    }

    // "Pilar (1)" vira "Pilar": o sufixo é só do Unity duplicando objetos.
    private static string NomeLimpo(string nome)
    {
        int i = nome.LastIndexOf(" (");
        return i > 0 && nome.EndsWith(")") ? nome.Substring(0, i) : nome;
    }

    private static void Colocar(RectTransform rt, float y)
    {
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -y);
    }

    private static Color ComAlfa(Color c, float a)
    {
        c.a = a;
        return c;
    }

    private void Posicionar(Vector2 telaMouse)
    {
        var pai = (RectTransform)caixa.parent;
        Camera camUi = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(pai, telaMouse, camUi, out Vector2 local);

        // Abaixo e à direita do mouse; vira para cima ou para a esquerda
        // quando encostaria na borda da tela.
        Rect r = pai.rect;
        Vector2 tam = caixa.sizeDelta;
        float x = local.x + deslocamento.x;
        float y = local.y - deslocamento.y;
        if (y - tam.y < r.yMin + margemTela)
        {
            y = local.y + 16f + tam.y;
        }

        if (x + tam.x > r.xMax - margemTela)
        {
            x = local.x - deslocamento.x - tam.x;
        }

        x = Mathf.Clamp(x, r.xMin + margemTela, r.xMax - margemTela - tam.x);
        y = Mathf.Clamp(y, r.yMin + margemTela + tam.y, r.yMax - margemTela);
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
