using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Janela "Novo jogador" do Menu: "Quem vai jogar agora?". O aluno escolhe
/// entre um apelido (digitado) ou anônimo (o jogo dá um número), ou continua
/// um jogador já salvo neste PC pelos botões de baixo. Ver Jogadores.
///
/// O visual é o prefab Assets/Resources/NovoJogador.prefab, gerado por
/// Tools > Menu > Criar tela Novo jogador e editável à vontade. Este script só
/// cuida da lógica e liga os botões sozinho.
/// </summary>
public class NovoJogadorUI : MonoBehaviour
{
    private const string CaminhoPrefab = "NovoJogador";

    [Header("Com apelido")]
    [SerializeField] private Button cartaoApelido;
    [SerializeField] private GameObject marcaApelido;
    [SerializeField] private GameObject pontoApelido;
    [SerializeField] private TMP_InputField campoApelido;
    [SerializeField] private TMP_Text contador;

    [Header("Anônimo")]
    [SerializeField] private Button cartaoAnonimo;
    [SerializeField] private GameObject marcaAnonimo;
    [SerializeField] private GameObject pontoAnonimo;
    [SerializeField] private TMP_Text numeroAnonimo;

    [Header("Jogadores salvos")]
    [SerializeField] private GameObject linhaSalvos;
    [SerializeField] private RectTransform listaSalvos;
    [Tooltip("Chip-modelo (desligado), clonado uma vez por jogador salvo.")]
    [SerializeField] private Button chipModelo;
    [SerializeField] private int chipsNaLinha = 4;
    [Tooltip("Largura máxima de um chip na linha. Apelido mais comprido diminui a letra e, se ainda não couber, ganha \"…\".")]
    [SerializeField] private float larguraMaxChip = 196f;
    [SerializeField] private GameObject painelTodos;
    [SerializeField] private RectTransform gradeTodos;
    [SerializeField] private Button botaoFecharTodos;

    [Header("Rodapé")]
    [SerializeField] private TMP_Text aviso;
    [SerializeField] private Button botaoCancelar;
    [SerializeField] private Button botaoComecar;

    [Header("Cores")]
    [SerializeField] private Color corAviso = new Color32(0xE5, 0x64, 0x6A, 0xFF);

    private static NovoJogadorUI instancia;
    private static int frameFechou = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instancia = null;
        frameFechou = -1;
    }

    private Action aoEscolher;
    private bool modoAnonimo;
    private int ignorarSelecaoAte = -1;
    private readonly List<GameObject> chipsCriados = new List<GameObject>();

    /// <summary>Abre a janela. <paramref name="depois"/> roda quando alguém for escolhido (não roda no Cancelar).</summary>
    public static void Abrir(Action depois = null)
    {
        if (instancia == null)
        {
            GameObject prefab = Resources.Load<GameObject>(CaminhoPrefab);
            if (prefab == null)
            {
                Debug.LogError("[NovoJogadorUI] Prefab 'Resources/NovoJogador' não encontrado. Rode Tools > Menu > Criar tela Novo jogador.");
                return;
            }

            GameObject go = Instantiate(prefab);
            go.name = prefab.name;
            instancia = go.GetComponent<NovoJogadorUI>();
        }

        instancia.aoEscolher = depois;
        instancia.gameObject.SetActive(true);
        instancia.Preparar();
    }

    public static bool Aberto => instancia != null && instancia.gameObject.activeSelf;

    /// <summary>True também no frame em que a janela fechou com Esc — pra o Esc não abrir as Configurações junto.</summary>
    public static bool SegurandoEsc => Aberto || Time.frameCount == frameFechou;

    private void Awake()
    {
        if (chipModelo != null) chipModelo.gameObject.SetActive(false);
        if (painelTodos != null) painelTodos.SetActive(false);

        cartaoApelido.onClick.AddListener(() => TrocarModo(false));
        cartaoAnonimo.onClick.AddListener(() => TrocarModo(true));
        botaoCancelar.onClick.AddListener(Fechar);
        botaoComecar.onClick.AddListener(Comecar);
        if (botaoFecharTodos != null) botaoFecharTodos.onClick.AddListener(() => MostrarTodos(false));

        campoApelido.characterLimit = Jogadores.TamanhoMaximoApelido;
        campoApelido.onValidateInput = (texto, indice, c) => Jogadores.CaractereValido(c) ? c : '\0';
        campoApelido.onValueChanged.AddListener(_ => AtualizarContador());
        // Clicar no campo volta pro apelido (menos logo depois de escolher
        // anônimo: o campo ainda pode estar terminando de ganhar o foco).
        campoApelido.onSelect.AddListener(_ => { if (modoAnonimo && Time.frameCount > ignorarSelecaoAte) TrocarModo(false); });
        campoApelido.onSubmit.AddListener(_ => Comecar());
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    private void Preparar()
    {
        campoApelido.text = "";
        Avisar("");
        numeroAnonimo.text = Jogadores.NomeDoProximoAnonimo;
        MontarChips();
        MostrarTodos(false);
        TrocarModo(false);
    }

    private void Update()
    {
        Keyboard k = Keyboard.current;
        if (k == null) return;

        if (k.escapeKey.wasPressedThisFrame)
        {
            if (painelTodos != null && painelTodos.activeSelf) MostrarTodos(false);
            else Fechar();
            return;
        }

        if (k.tabKey.wasPressedThisFrame && (painelTodos == null || !painelTodos.activeSelf))
        {
            TrocarModo(!modoAnonimo);
        }

        GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        // O campo pode pegar o foco de volta logo depois de escolher anônimo
        // (a ativação dele fica pro quadro seguinte): devolve pro Começar.
        if (modoAnonimo && sel == campoApelido.gameObject && Time.frameCount <= ignorarSelecaoAte + 1)
        {
            campoApelido.DeactivateInputField();
            EventSystem.current.SetSelectedGameObject(botaoComecar.gameObject);
            return;
        }

        // Enter no modo anônimo (o Começar já responde ao Enter quando está com foco).
        if (modoAnonimo && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
        {
            if (sel == null || sel == cartaoAnonimo.gameObject) Comecar();
        }
    }

    private void TrocarModo(bool anonimo)
    {
        modoAnonimo = anonimo;
        marcaApelido.SetActive(!anonimo);
        pontoApelido.SetActive(!anonimo);
        marcaAnonimo.SetActive(anonimo);
        pontoAnonimo.SetActive(anonimo);
        Avisar("");

        if (EventSystem.current == null) return;
        if (anonimo)
        {
            ignorarSelecaoAte = Time.frameCount + 2;
            campoApelido.DeactivateInputField();
            EventSystem.current.SetSelectedGameObject(botaoComecar.gameObject);
        }
        else
        {
            EventSystem.current.SetSelectedGameObject(campoApelido.gameObject);
            campoApelido.ActivateInputField();
        }
    }

    private void AtualizarContador()
    {
        if (contador != null)
        {
            contador.text = $"{campoApelido.text.Length}/{Jogadores.TamanhoMaximoApelido}";
        }
        if (!string.IsNullOrEmpty(aviso.text)) Avisar("");
        // Digitar um apelido = modo apelido.
        if (modoAnonimo && campoApelido.text.Length > 0) TrocarModo(false);
    }

    private void Comecar()
    {
        if (modoAnonimo)
        {
            Jogadores.CriarAnonimo();
            Escolhido();
            return;
        }

        string erro = Jogadores.ValidarApelido(campoApelido.text);
        if (erro != null)
        {
            Avisar(erro);
            campoApelido.ActivateInputField();
            return;
        }

        Jogadores.CriarComApelido(campoApelido.text);
        Escolhido();
    }

    private void Continuar(Jogadores.Jogador j)
    {
        Jogadores.Continuar(j);
        Escolhido();
    }

    private void Escolhido()
    {
        Action depois = aoEscolher;
        aoEscolher = null;
        gameObject.SetActive(false);
        depois?.Invoke();
    }

    private void Fechar()
    {
        aoEscolher = null;
        frameFechou = Time.frameCount;
        gameObject.SetActive(false);
    }

    private void Avisar(string texto)
    {
        aviso.text = texto;
        aviso.color = corAviso;
    }

    // ------------------------------------------------------------------
    // Jogadores salvos
    // ------------------------------------------------------------------

    private void MontarChips()
    {
        foreach (GameObject go in chipsCriados) Destroy(go);
        chipsCriados.Clear();

        List<Jogadores.Jogador> todos = Jogadores.Todos();
        linhaSalvos.SetActive(todos.Count > 0);

        // Na linha só entram os chips que cabem inteiros; o resto fica no "Ver todos".
        HorizontalLayoutGroup linha = listaSalvos.GetComponent<HorizontalLayoutGroup>();
        float espaco = linha != null ? linha.spacing : 0f;
        float disponivel = listaSalvos.rect.width;
        string textoTodos = $"Ver todos ({todos.Count})";
        float larguraTodos = LarguraChip(textoTodos);

        var larguras = new List<float>(todos.Count);
        float total = -espaco;
        foreach (Jogadores.Jogador j in todos)
        {
            larguras.Add(LarguraChip(j.Nome));
            total += larguras[larguras.Count - 1] + espaco;
        }
        bool cabemTodos = todos.Count <= chipsNaLinha && total <= disponivel;

        int naLinha = cabemTodos ? todos.Count : 0;
        if (!cabemTodos)
        {
            float usado = larguraTodos;
            while (naLinha < chipsNaLinha - 1 && usado + espaco + larguras[naLinha] <= disponivel)
            {
                usado += espaco + larguras[naLinha];
                naLinha++;
            }
        }

        for (int i = 0; i < naLinha; i++)
        {
            Jogadores.Jogador j = todos[i];
            FixarLargura(CriarChip(listaSalvos, j.Nome, () => Continuar(j)), larguras[i]);
        }
        if (todos.Count > naLinha)
        {
            FixarLargura(CriarChip(listaSalvos, textoTodos, () => MostrarTodos(true)), larguraTodos);
        }

        if (gradeTodos != null)
        {
            foreach (Jogadores.Jogador j in todos)
            {
                Jogadores.Jogador jj = j;
                CriarChip(gradeTodos, j.Nome, () => Continuar(jj));
            }
        }
    }

    private Button CriarChip(RectTransform pai, string texto, Action aoClicar)
    {
        Button chip = Instantiate(chipModelo, pai);
        chip.gameObject.SetActive(true);
        chip.name = "Chip_" + texto;
        TMP_Text t = chip.GetComponentInChildren<TMP_Text>(true);
        if (t != null) t.text = texto;
        chip.onClick.AddListener(() => aoClicar());
        chipsCriados.Add(chip.gameObject);
        return chip;
    }

    // Largura do chip pelo texto (na letra cheia), sem passar de larguraMaxChip.
    // O chip-modelo liga só durante a medida: o TMP desligado pode não medir.
    private float LarguraChip(string texto)
    {
        TMP_Text t = chipModelo.GetComponentInChildren<TMP_Text>(true);
        HorizontalLayoutGroup hl = chipModelo.GetComponent<HorizontalLayoutGroup>();
        float margem = hl != null ? hl.padding.horizontal : 40f;
        bool ligado = chipModelo.gameObject.activeSelf;
        chipModelo.gameObject.SetActive(true);
        float w = t != null ? t.GetPreferredValues(texto, float.PositiveInfinity, float.PositiveInfinity).x : 0f;
        chipModelo.gameObject.SetActive(ligado);
        return Mathf.Min(Mathf.Ceil(w + margem), larguraMaxChip);
    }

    // O layout da linha não espreme o chip: ele fica com a largura calculada.
    private static void FixarLargura(Button chip, float largura)
    {
        LayoutElement le = chip.GetComponent<LayoutElement>();
        if (le == null) le = chip.gameObject.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = largura;
        le.flexibleWidth = 0f;
    }

    private void MostrarTodos(bool mostrar)
    {
        if (painelTodos == null) return;
        painelTodos.SetActive(mostrar);
        if (EventSystem.current == null) return;

        if (mostrar && gradeTodos != null && gradeTodos.childCount > 0)
        {
            foreach (Transform c in gradeTodos)
            {
                if (c.gameObject.activeSelf)
                {
                    EventSystem.current.SetSelectedGameObject(c.gameObject);
                    break;
                }
            }
        }
        else if (!mostrar && gameObject.activeSelf)
        {
            TrocarModo(modoAnonimo);
        }
    }
}
