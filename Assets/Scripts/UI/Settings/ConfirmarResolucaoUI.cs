using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// "Manter esta resolução?" (ou "Manter este modo de tela?") depois de escolher
/// uma opção na lista (ListaResolucaoUI). A opção nova já está na tela, mas só
/// é gravada quando o jogador clica em Manter. Sem resposta em
/// <see cref="Segundos"/> segundos, ou com Voltar/Esc, volta à resolução e ao
/// modo de antes. Assim, se o monitor não aceitar e a tela ficar preta ou
/// cortada, basta esperar.
///
/// O foco começa em Voltar: um Enter "às cegas" desfaz a troca, não confirma.
///
/// A janela é uma cópia da confirmação "Reiniciar a fase?" (ConfirmarReinicio
/// no GameUICanvas), feita na primeira vez que é preciso, com os textos e os
/// botões trocados. Mesmo visual, sem mexer no prefab.
/// </summary>
public class ConfirmarResolucaoUI : MonoBehaviour
{
    public const float Segundos = 15f;

    private static ConfirmarResolucaoUI instancia;
    private static int frameFechou = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instancia = null;
        frameFechou = -1;
    }

    public static bool Aberta => instancia != null && instancia.gameObject.activeSelf;

    /// <summary>True também no frame em que fechou com Esc — pra o Esc não fechar as Configurações junto.</summary>
    public static bool SegurandoEsc => Aberta || Time.frameCount == frameFechou;

    private Button manter;
    private Button voltar;
    private TMP_Text titulo;
    private TMP_Text texto;
    private int indiceAnterior;
    private int modoAnterior;
    private bool trocouModo;
    private float limite;
    private bool pendente;
    private int ultimoSegundo = -1;
    private GameObject devolverFoco;

    /// <summary>
    /// Abre a confirmação (a resolução nova já deve estar aplicada sem gravar).
    /// Devolve false se não achou o modelo da janela; aí quem chamou decide.
    /// </summary>
    public static bool Abrir(Transform qualquerNoCanvas, int resolucaoAnterior, int modoTelaAnterior, bool modoTela, GameObject focoAoFechar)
    {
        if (instancia == null)
        {
            instancia = Criar(qualquerNoCanvas);
        }

        if (instancia == null)
        {
            return false;
        }

        instancia.Iniciar(resolucaoAnterior, modoTelaAnterior, modoTela, focoAoFechar);
        return true;
    }

    private static ConfirmarResolucaoUI Criar(Transform qualquerNoCanvas)
    {
        Canvas canvas = qualquerNoCanvas != null ? qualquerNoCanvas.GetComponentInParent<Canvas>() : null;
        Transform raiz = canvas != null ? canvas.rootCanvas.transform : null;
        Transform modelo = raiz != null ? AcharFilho(raiz, "ConfirmarReinicio") : null;
        if (modelo == null)
        {
            Debug.LogWarning("[Resolução] ConfirmarReinicio não encontrado no Canvas: a resolução fica sem confirmação.");
            return null;
        }

        GameObject go = Instantiate(modelo.gameObject, modelo.parent);
        go.SetActive(false);
        go.name = "ConfirmarResolucao";

        var antigo = go.GetComponent<ConfirmarReinicioUI>();
        if (antigo != null) DestroyImmediate(antigo);

        var ui = go.AddComponent<ConfirmarResolucaoUI>();
        if (!ui.Preparar())
        {
            Destroy(go);
            return null;
        }
        return ui;
    }

    private bool Preparar()
    {
        Transform caixa = transform.Find("Caixa");
        if (caixa == null) return false;

        // Linha extra que o ConfirmarReinicioUI cria (e a altura que ela soma).
        Transform lembrete = caixa.Find("Lembrete");
        if (lembrete != null)
        {
            if (lembrete.gameObject.activeSelf)
            {
                var c = (RectTransform)caixa;
                c.sizeDelta -= new Vector2(0f, 70f);
            }
            DestroyImmediate(lembrete.gameObject);
        }

        Transform tituloT = caixa.Find("Titulo");
        titulo = tituloT != null ? tituloT.GetComponent<TMP_Text>() : null;

        Transform t = caixa.Find("Texto");
        texto = t != null ? t.GetComponent<TMP_Text>() : null;

        Transform cancelarT = caixa.Find("CancelarButton");
        Transform reiniciarT = caixa.Find("ReiniciarButton");
        if (cancelarT == null || reiniciarT == null) return false;

        // Esquerda (secundário, Esc): Voltar. Direita (primário): Manter.
        cancelarT.name = "VoltarButton";
        reiniciarT.name = "ManterButton";
        voltar = cancelarT.GetComponent<Button>();
        manter = reiniciarT.GetComponent<Button>();
        voltar.onClick = new Button.ButtonClickedEvent();
        manter.onClick = new Button.ButtonClickedEvent();
        voltar.onClick.AddListener(Voltar);
        manter.onClick.AddListener(Manter);

        DefinirRotulo(cancelarT, "Voltar");
        DefinirRotulo(reiniciarT, "Manter");

        // A tecla "Enter" do primário sai: o Enter vale pro botão em foco (Voltar).
        Transform tecla = reiniciarT.Find("Tecla");
        Transform rotulo = reiniciarT.Find("Label");
        if (tecla != null && rotulo != null)
        {
            tecla.gameObject.SetActive(false);
            var r = (RectTransform)rotulo;
            r.anchoredPosition = new Vector2(0f, r.anchoredPosition.y);
            r.sizeDelta = new Vector2(-40f, r.sizeDelta.y);
        }

        return true;
    }

    private static void DefinirRotulo(Transform botao, string texto)
    {
        Transform l = botao.Find("Label");
        if (l != null && l.TryGetComponent(out TMP_Text t)) t.text = texto;
    }

    private void Iniciar(int resolucaoAnterior, int modoTelaAnterior, bool modoTela, GameObject focoAoFechar)
    {
        indiceAnterior = resolucaoAnterior;
        modoAnterior = modoTelaAnterior;
        trocouModo = modoTela;
        if (titulo != null) titulo.text = modoTela ? "Manter este modo de tela?" : "Manter esta resolução?";
        devolverFoco = focoAoFechar;
        limite = Time.unscaledTime + Segundos;
        pendente = true;
        ultimoSegundo = -1;
        AtualizarTexto();

        transform.SetAsLastSibling();
        gameObject.SetActive(true);

        // O FocoInicialUI da cópia respeita um item já escolhido ao abrir.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(voltar.gameObject);
    }

    private void Update()
    {
        if (!pendente)
        {
            return;
        }

        InputManager im = InputManager.Instance;
        if (im != null && im.UICancelAction != null && im.UICancelAction.WasPressedThisFrame())
        {
            Voltar();
            return;
        }

        if (Time.unscaledTime >= limite)
        {
            Voltar();
            return;
        }

        AtualizarTexto();
    }

    private void AtualizarTexto()
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(limite - Time.unscaledTime));
        if (s == ultimoSegundo || texto == null) return;
        ultimoSegundo = s;

        GameManager gm = GameManager.Instance;
        string anterior;
        if (trocouModo && modoAnterior >= 0 && modoAnterior < GameManager.NomesModoTela.Length)
        {
            anterior = $"ao modo {GameManager.NomesModoTela[modoAnterior]}";
        }
        else if (!trocouModo && gm != null && indiceAnterior >= 0 && indiceAnterior < gm.Resolucoes.Count)
        {
            Vector2Int r = gm.Resolucoes[indiceAnterior];
            anterior = $"para {r.x} × {r.y}";
        }
        else
        {
            anterior = "ao que estava antes";
        }

        string cor = ColorUtility.ToHtmlStringRGB(TemaLabUI.Dourado);
        texto.text = $"Se você não confirmar, a tela volta {anterior} em <color=#{cor}>{s} s</color>.";
    }

    private void Manter()
    {
        if (!pendente) return;
        GameManager.Instance?.SalvarTela();
        Fechar();
    }

    private void Voltar()
    {
        if (!pendente) return;
        GameManager.Instance?.RestaurarTela(indiceAnterior, modoAnterior);
        Fechar();
    }

    private void Fechar()
    {
        pendente = false;
        frameFechou = Time.frameCount;
        gameObject.SetActive(false);
        if (devolverFoco != null && devolverFoco.activeInHierarchy && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(devolverFoco);
        }
    }

    private void OnDisable()
    {
        // Fechou por fora (troca de cena, Canvas desligado) sem resposta: desfaz.
        if (pendente)
        {
            pendente = false;
            GameManager.Instance?.RestaurarTela(indiceAnterior, modoAnterior);
        }
    }

    private static Transform AcharFilho(Transform pai, string nome)
    {
        foreach (Transform filho in pai)
        {
            if (filho.name == nome) return filho;
            Transform achado = AcharFilho(filho, nome);
            if (achado != null) return achado;
        }
        return null;
    }
}
