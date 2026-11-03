using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tela "Seleção de fases" do Menu: um cartão por fase mostrando se ela está
/// bloqueada, disponível, é a próxima a jogar ou já foi concluída. Fases
/// bloqueadas não abrem: é preciso concluir a anterior (ver
/// ProgressoJogo.Desbloqueada).
///
/// O visual é o prefab Assets/Resources/SeletorFases.prefab — edite à vontade
/// (posições, sprites, fontes, cores). Este script só cuida da lógica: ele
/// clona o cartão-modelo ("CartaoFase", com CartaoFaseUI) uma vez por fase e
/// liga os botões sozinho, então não é preciso configurar OnClick no
/// Inspector.
///
/// Dois layouts:
/// - Tema Laboratório (atual): "Vagas" preenchidas = um cartão em cada vaga
///   (as 8 fases em 4 colunas de módulo), linhas dos questionários acima e
///   abaixo, pontos do caminho entre as fases e o painel de detalhes da fase
///   selecionada com o botão "Jogar fase".
/// - Antigo: "Vagas" vazias = todos os cartões (questionários incluídos) na
///   "Grade", em sequência. Rode Tools > Menu > Recriar Prefab do Seletor de
///   Fases pra voltar a ele.
///
/// Atalho de professor: Ctrl+Shift+U libera todas as fases (fica salvo até
/// "Apagar progresso").
/// </summary>
public class SeletorFasesUI : MonoBehaviour
{
    private const string CaminhoPrefab = "SeletorFases";

    [Serializable]
    public class InfoFase
    {
        public string cena;
        public string modulo;
        public Color corModulo = Color.white;
        [TextArea(2, 4)] public string descricao;
        [Tooltip("Chips \"Você vai usar\" do painel de detalhes.")]
        public string[] conceitos;
    }

    [Serializable]
    public class TrechoCaminho
    {
        [Tooltip("Índice da fase (0 = primeira) que acende este trecho quando concluída.")]
        public int depoisDaFase;
        public Image[] pontos;
    }

    [Header("Partes da tela (ligadas pelo prefab)")]
    [Tooltip("Layout antigo: onde os cartões são criados — normalmente com um GridLayoutGroup.")]
    [SerializeField] private RectTransform grade;
    [Tooltip("Layout Laboratório: uma vaga por fase, na ordem do caminho. Vazio = usa a Grade (layout antigo).")]
    [SerializeField] private RectTransform[] vagas;
    [Tooltip("Cartão-modelo, clonado uma vez por fase. Fica desligado; edite o visual dele.")]
    [SerializeField] private CartaoFaseUI cartaoModelo;
    [SerializeField] private TMP_Text subtitulo;
    [Tooltip("Mensagem temporária (ex: por que uma fase está bloqueada).")]
    [SerializeField] private TMP_Text aviso;
    [SerializeField] private Button botaoVoltar;
    [SerializeField] private Button botaoApagar;

    [Header("Confirmação de 'Apagar progresso'")]
    [SerializeField] private GameObject confirmacao;
    [SerializeField] private Button botaoCancelar;
    [SerializeField] private Button botaoConfirmarApagar;

    [Header("Questionários (layout Laboratório)")]
    [SerializeField] private Button linhaPreTeste;
    [SerializeField] private TMP_Text textoPreTeste;
    [SerializeField] private Image iconePreTeste;
    [SerializeField] private Button linhaPosTeste;
    [SerializeField] private TMP_Text textoPosTeste;
    [SerializeField] private Image iconePosTeste;
    [SerializeField] private Sprite spriteFeito;
    [SerializeField] private Sprite spritePendente;
    [SerializeField] private Sprite spriteBloqueado;
    [Tooltip("Questionário pulado pelo atalho do professor: ícone e cor neutros, pra não parecer respondido.")]
    [SerializeField] private Sprite spritePulado;

    [Header("Caminho entre as fases (layout Laboratório)")]
    [SerializeField] private TrechoCaminho[] caminho;
    [SerializeField] private Sprite spritePonto;
    [SerializeField] private Sprite spritePontoFeito;

    [Header("Detalhes da fase selecionada (layout Laboratório)")]
    [SerializeField] private TMP_Text detalheModulo;
    [SerializeField] private TMP_Text detalheTitulo;
    [SerializeField] private TMP_Text detalheDescricao;
    [SerializeField] private RectTransform detalheConceitos;
    [Tooltip("Chip-modelo (desligado), clonado uma vez por conceito dentro de 'Detalhe Conceitos'.")]
    [SerializeField] private GameObject chipModelo;
    [SerializeField] private TMP_Text detalheTentativas;
    [SerializeField] private TMP_Text detalheMelhorTempo;
    [SerializeField] private TMP_Text detalheStatus;
    [SerializeField] private Button botaoJogarFase;
    [SerializeField] private TMP_Text rotuloJogarFase;

    [Header("Textos")]
    [Tooltip("{0} = fases concluídas, {1} = total.")]
    [SerializeField] private string formatoSubtitulo = "{0} de {1} fases concluídas";
    [SerializeField] private string textoModoProfessor = "   ·   <color=#F5C542>modo professor: tudo liberado</color>";
    [SerializeField] private string textoBloqueada = "Bloqueada";
    [SerializeField] private string textoDisponivel = "Disponível";
    [SerializeField] private string textoEmAndamento = "Em andamento";
    [SerializeField] private string textoConcluida = "Concluída";
    [SerializeField] private string tituloPreTeste = "Questionário inicial";
    [SerializeField] private string conceitoPreTeste = "Perguntas rápidas, antes do jogo";
    [SerializeField] private string tituloPosTeste = "Questionário final";
    [SerializeField] private string conceitoPosTeste = "As mesmas perguntas, pra comparar";
    [SerializeField] private string numeroPreTeste = "0";
    [SerializeField] private string numeroPosTeste = "Fim";
    [Tooltip("Cor das estatísticas (tempo, erros) embaixo de 'Concluída'.")]
    [SerializeField] private Color corEstatisticas = new Color32(0x9A, 0xA3, 0xB2, 0xFF);
    [SerializeField] private float segundosDoAviso = 4f;

    [Header("Conteúdo do painel de detalhes (um por fase)")]
    [SerializeField] private InfoFase[] infoFases =
    {
        new InfoFase { cena = "Fisica", modulo = "Física", corModulo = new Color32(0x4A, 0x8C, 0xF7, 0xFF),
            descricao = "Monte o personagem com física de verdade: andar, pular, cair e a câmera seguindo.",
            conceitos = new[] { "Rigidbody2D", "Collider2D", "linearVelocity", "Câmera" } },
        new InfoFase { cena = "Aplicacao_Fisica", modulo = "Física", corModulo = new Color32(0x4A, 0x8C, 0xF7, 0xFF),
            descricao = "Use os componentes de física nos objetos do cenário pra abrir caminho até a saída.",
            conceitos = new[] { "Rigidbody2D", "Collider2D", "Movimento", "Pulo" } },
        new InfoFase { cena = "Interacao", modulo = "Interação", corModulo = new Color32(0x52, 0xC9, 0x8A, 0xFF),
            descricao = "Ação e consequência: descubra o que acontece quando o jogador encosta, pula em cima ou chega perto.",
            conceitos = new[] { "OnTriggerEnter2D", "OnCollisionEnter2D", "Is Trigger", "Tag Player" } },
        new InfoFase { cena = "Interacao_Vida", modulo = "Interação", corModulo = new Color32(0x52, 0xC9, 0x8A, 0xFF),
            descricao = "Dê e tire vida do jogador com gatilhos e mostre isso na tela.",
            conceitos = new[] { "OnTriggerEnter2D", "Evento de vida", "UI Image", "Game Over" } },
        new InfoFase { cena = "Tutorial_Anim", modulo = "Animação", corModulo = new Color32(0x9B, 0x7B, 0xF0, 0xFF),
            descricao = "Coloque os quadros na ordem e acerte a velocidade (FPS) e o loop da animação.",
            conceitos = new[] { "Sprites", "Animation Clip", "FPS", "Loop Time" } },
        new InfoFase { cena = "Animacao", modulo = "Animação", corModulo = new Color32(0x9B, 0x7B, 0xF0, 0xFF),
            descricao = "Monte as transições do Animator com parâmetros Bool, Trigger e Int.",
            conceitos = new[] { "Animator", "velocidadeX", "noChao", "Transições" } },
        new InfoFase { cena = "IA", modulo = "IA", corModulo = new Color32(0xF5, 0xC5, 0x42, 0xFF),
            descricao = "Monte o cérebro do guarda: estados, transições e a visão com Raycast.",
            conceitos = new[] { "Máquina de estados", "Transições", "Physics2D.Raycast", "LayerMask" } },
        new InfoFase { cena = "IA_Aplicacao", modulo = "IA", corModulo = new Color32(0xF5, 0xC5, 0x42, 0xFF),
            descricao = "Configure o guarda inimigo e o aliado usando a mesma estrutura de IA.",
            conceitos = new[] { "Estados", "Alvo", "Raycast", "Aliado" } },
    };

    private static SeletorFasesUI instancia;
    private static int frameFechou = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => frameFechou = -1;

    private readonly List<GameObject> cartoes = new List<GameObject>();
    private readonly List<GameObject> chips = new List<GameObject>();
    private GameObject primeiroSelecionavel;
    private int faseSelecionada = -1;
    private float avisoAte;

    private bool LayoutLaboratorio => vagas != null && vagas.Length > 0;

    /// <summary>Abre a tela (instanciando o prefab na primeira vez).</summary>
    public static void Abrir()
    {
        if (instancia == null)
        {
            GameObject prefab = Resources.Load<GameObject>(CaminhoPrefab);
            if (prefab == null)
            {
                Debug.LogError("[SeletorFasesUI] Prefab 'Resources/SeletorFases' não encontrado. Rode Tools > Menu > Criar Prefab do Seletor de Fases.");
                return;
            }

            GameObject go = Instantiate(prefab);
            go.name = prefab.name;
            instancia = go.GetComponent<SeletorFasesUI>();
            if (instancia == null)
            {
                Debug.LogError("[SeletorFasesUI] O prefab SeletorFases não tem o componente SeletorFasesUI na raiz.");
                Destroy(go);
                return;
            }
        }

        instancia.gameObject.SetActive(true);
        instancia.Atualizar();
    }

    public static void Fechar()
    {
        if (instancia != null)
        {
            instancia.gameObject.SetActive(false);
        }
    }

    public static bool Aberto => instancia != null && instancia.gameObject.activeSelf;

    /// <summary>True também no frame em que a tela fechou com Esc — pra o Esc não abrir as Configurações junto.</summary>
    public static bool SegurandoEsc => Aberto || Time.frameCount == frameFechou;

    private void Awake()
    {
        if (cartaoModelo != null) cartaoModelo.gameObject.SetActive(false);
        if (chipModelo != null) chipModelo.SetActive(false);
        if (confirmacao != null) confirmacao.SetActive(false);
        if (aviso != null) aviso.text = "";

        if (botaoVoltar != null) botaoVoltar.onClick.AddListener(Fechar);
        if (botaoApagar != null) botaoApagar.onClick.AddListener(AbrirConfirmacao);
        if (botaoCancelar != null) botaoCancelar.onClick.AddListener(FecharConfirmacao);
        if (botaoConfirmarApagar != null) botaoConfirmarApagar.onClick.AddListener(ApagarProgresso);
        if (botaoJogarFase != null) botaoJogarFase.onClick.AddListener(JogarFaseSelecionada);
        if (linhaPreTeste != null) linhaPreTeste.onClick.AddListener(ClicarPreTeste);
        if (linhaPosTeste != null) linhaPosTeste.onClick.AddListener(ClicarPosTeste);
    }

    private void OnEnable() => ProgressoJogo.OnMudou += Atualizar;
    private void OnDisable() => ProgressoJogo.OnMudou -= Atualizar;

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    private void Update()
    {
        if (aviso != null && avisoAte > 0f && Time.unscaledTime > avisoAte)
        {
            aviso.text = "";
            avisoAte = 0f;
        }

        var teclado = Keyboard.current;
        if (teclado == null)
        {
            return;
        }

        // Esc volta um passo: fecha a confirmação; senão volta ao Menu.
        if (teclado.escapeKey.wasPressedThisFrame)
        {
            frameFechou = Time.frameCount;
            if (confirmacao != null && confirmacao.activeSelf) FecharConfirmacao();
            else Fechar();
        }

        bool ctrl = teclado.leftCtrlKey.isPressed || teclado.rightCtrlKey.isPressed;
        bool shift = teclado.leftShiftKey.isPressed || teclado.rightShiftKey.isPressed;
        // Ctrl+Shift+U liga e desliga o modo professor (desligar não apaga o progresso).
        if (ctrl && shift && teclado.uKey.wasPressedThisFrame)
        {
            if (!ProgressoJogo.TodasDesbloqueadas)
            {
                ProgressoJogo.DesbloquearTodas();
                RegistroAprendizagem.Registrar("fases_desbloqueadas", "atalho do professor");
                Avisar("Modo professor ligado. Ctrl+Shift+E exporta os registros. Ctrl+Shift+U desliga.");
            }
            else
            {
                ProgressoJogo.DesligarModoProfessor();
                RegistroAprendizagem.Registrar("fases_bloqueadas", "atalho do professor");
                Avisar("Modo professor desligado. O progresso continua salvo.");
            }
        }

        // Ctrl+Shift+E (só no modo professor) copia os CSVs e um resumo por
        // jogador pra Área de Trabalho (ver ExportarRegistros).
        if (ctrl && shift && teclado.eKey.wasPressedThisFrame && ProgressoJogo.TodasDesbloqueadas)
        {
            ExportarRegistrosDoPc();
        }
    }

    private void ExportarRegistrosDoPc()
    {
        if (ExportarRegistros.Exportar(out string pasta, out int csvs, out int jogadores, out string erro))
        {
            RegistroAprendizagem.Registrar("registros_exportados", csvs + " CSVs, " + jogadores + " jogadores");
            Avisar($"Exportado para a Área de Trabalho: {csvs} sessões e {jogadores} jogadores.");
            Application.OpenURL("file:///" + pasta.Replace('\\', '/'));
        }
        else
        {
            // O motivo completo vai pro Player.log (ver ExportarRegistros).
            Avisar("Não deu pra exportar os registros. Tente de novo.");
        }
    }

    // Com o modo professor ligado, o rodapé ganha "Ctrl+Shift+U  Sair do modo
    // professor" ao lado do botão Apagar progresso (cópia da primeira tecla da
    // legenda; na legenda não cabe). Desligado, não aparece nada.
    private RectTransform legendaProfessor;
    private RectTransform legendaTeclas;
    private TMP_Text rotuloProfessor;

    private const string TextoSairProfessor = "Sair do modo professor";
    private const string TextoSairProfessorCurto = "Desligar";

    private void AtualizarLegendaProfessor()
    {
        if (legendaProfessor == null && !CriarLegendaProfessor())
        {
            return;
        }

        bool ligado = ProgressoJogo.TodasDesbloqueadas;
        legendaProfessor.gameObject.SetActive(ligado);
        if (!ligado)
        {
            return;
        }

        PosicionarLegendaProfessor();

        // Em telas mais estreitas (4:3) o texto longo encostaria na legenda.
        rotuloProfessor.gameObject.SetActive(true);
        rotuloProfessor.text = TextoSairProfessor;
        LayoutRebuilder.ForceRebuildLayoutImmediate(legendaProfessor);
        if (EncostaNaLegenda())
        {
            rotuloProfessor.text = TextoSairProfessorCurto;
            LayoutRebuilder.ForceRebuildLayoutImmediate(legendaProfessor);
            if (EncostaNaLegenda())
            {
                rotuloProfessor.gameObject.SetActive(false);
            }
        }
    }

    private bool CriarLegendaProfessor()
    {
        legendaTeclas = AcharFilho(transform, "Legenda") as RectTransform;
        if (legendaTeclas == null || legendaTeclas.childCount < 2 || botaoApagar == null)
        {
            return false;
        }

        RectTransform apagar = (RectTransform)botaoApagar.transform;
        var go = new GameObject("Legenda_Professor", typeof(RectTransform));
        legendaProfessor = (RectTransform)go.transform;
        legendaProfessor.SetParent(apagar.parent, false);
        legendaProfessor.anchorMin = legendaProfessor.anchorMax = Vector2.zero;
        legendaProfessor.pivot = new Vector2(0f, 0.5f);
        legendaProfessor.sizeDelta = new Vector2(0f, 52f);

        var origem = legendaTeclas.GetComponent<HorizontalLayoutGroup>();
        var fileira = go.AddComponent<HorizontalLayoutGroup>();
        fileira.spacing = origem != null ? origem.spacing : 12f;
        fileira.childAlignment = TextAnchor.MiddleLeft;
        fileira.childControlWidth = origem == null || origem.childControlWidth;
        fileira.childControlHeight = origem != null && origem.childControlHeight;
        fileira.childForceExpandWidth = false;
        fileira.childForceExpandHeight = false;
        go.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject tecla = Instantiate(legendaTeclas.GetChild(0).gameObject, legendaProfessor);
        tecla.name = "Tecla_Professor";
        tecla.SetActive(true);
        TMP_Text textoTecla = tecla.GetComponentInChildren<TMP_Text>(true);
        if (textoTecla != null) textoTecla.text = "Ctrl+Shift+U";
        GameObject rotulo = Instantiate(legendaTeclas.GetChild(1).gameObject, legendaProfessor);
        rotulo.name = "Rotulo_Professor";
        rotuloProfessor = rotulo.GetComponent<TMP_Text>();
        return rotuloProfessor != null;
    }

    // 40 px depois do botão Apagar progresso, na mesma altura da legenda.
    private void PosicionarLegendaProfessor()
    {
        RectTransform apagar = (RectTransform)botaoApagar.transform;
        Vector3[] cantos = new Vector3[4];
        apagar.GetWorldCorners(cantos);
        float x = cantos[2].x + 40f * apagar.lossyScale.x;
        legendaTeclas.GetWorldCorners(cantos);
        legendaProfessor.position = new Vector3(x, (cantos[0].y + cantos[1].y) * 0.5f, legendaProfessor.position.z);
    }

    // A ponta direita da legenda do professor chega perto da primeira tecla da legenda normal?
    private bool EncostaNaLegenda()
    {
        Vector3[] cantos = new Vector3[4];
        legendaProfessor.GetWorldCorners(cantos);
        float direita = cantos[2].x;
        foreach (RectTransform filho in legendaTeclas)
        {
            if (!filho.gameObject.activeSelf) continue;
            filho.GetWorldCorners(cantos);
            return direita > cantos[0].x - 32f * legendaTeclas.lossyScale.x;
        }
        return false;
    }

    private static Transform AcharFilho(Transform pai, string nome)
    {
        foreach (Transform t in pai.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == nome) return t;
        }
        return null;
    }

    // ------------------------------------------------------------------
    // Conteúdo
    // ------------------------------------------------------------------

    private void Atualizar()
    {
        if ((grade == null && !LayoutLaboratorio) || cartaoModelo == null)
        {
            Debug.LogError("[SeletorFasesUI] 'Grade'/'Vagas' ou 'Cartao Modelo' não ligados no prefab.");
            return;
        }

        foreach (GameObject c in cartoes)
        {
            if (c != null) Destroy(c);
        }
        cartoes.Clear();
        primeiroSelecionavel = null;

        if (subtitulo != null)
        {
            subtitulo.text = string.Format(formatoSubtitulo, ProgressoJogo.QuantidadeConcluidas, ProgressoJogo.Fases.Length) +
                (ProgressoJogo.TodasDesbloqueadas ? textoModoProfessor : "");
        }
        AtualizarLegendaProfessor();

        if (LayoutLaboratorio)
        {
            AtualizarLaboratorio();
        }
        else
        {
            AtualizarGrade();
        }

        if (primeiroSelecionavel != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(primeiroSelecionavel);
        }
    }

    // Layout Laboratório: um cartão por vaga, questionários em linhas separadas.
    private void AtualizarLaboratorio()
    {
        string proxima = ProgressoJogo.ProximaCena();
        CartaoFaseUI cartaoInicial = null;
        CartaoFaseUI primeiroCartao = null;

        for (int i = 0; i < ProgressoJogo.Fases.Length && i < vagas.Length; i++)
        {
            ProgressoJogo.Fase f = ProgressoJogo.Fases[i];
            ProgressoJogo.FaseSalva info = ProgressoJogo.Info(f.cena);
            int indice = i;

            CartaoFaseUI.Estado estado;
            string status;
            if (ProgressoJogo.Concluida(f.cena))
            {
                estado = CartaoFaseUI.Estado.Concluida;
                status = textoConcluida;
            }
            else if (ProgressoJogo.Desbloqueada(f.cena))
            {
                estado = f.cena == proxima ? CartaoFaseUI.Estado.Atual : CartaoFaseUI.Estado.Disponivel;
                status = info != null && info.tentativas > 0 ? textoEmAndamento : textoDisponivel;
            }
            else
            {
                estado = CartaoFaseUI.Estado.Bloqueada;
                status = textoBloqueada;
            }

            CartaoFaseUI cartao = Instantiate(cartaoModelo, vagas[i]);
            cartao.name = "Cartao_" + f.cena;
            RectTransform rt = (RectTransform)cartao.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            cartao.gameObject.SetActive(true);
            cartao.Preencher((i + 1).ToString(), f.titulo, f.conceito, status, estado, () => ClicarFase(indice));
            cartao.AoSelecionar += _ => MostrarDetalhes(indice);
            cartoes.Add(cartao.gameObject);

            if (primeiroCartao == null) primeiroCartao = cartao;
            if (cartaoInicial == null && estado == CartaoFaseUI.Estado.Atual) cartaoInicial = cartao;
        }

        // Nenhuma "atual" (ex: modo professor com tudo concluído ou falta o questionário): primeira não concluída desbloqueada.
        if (cartaoInicial == null)
        {
            for (int i = 0; i < cartoes.Count; i++)
            {
                CartaoFaseUI c = cartoes[i].GetComponent<CartaoFaseUI>();
                if (c.EstadoAtual == CartaoFaseUI.Estado.Disponivel) { cartaoInicial = c; break; }
            }
        }

        AtualizarQuestionarios();
        AtualizarCaminho();

        if (cartaoInicial != null)
        {
            primeiroSelecionavel = cartaoInicial.Botao != null ? cartaoInicial.Botao.gameObject : cartaoInicial.gameObject;
            MostrarDetalhes(ProgressoJogo.IndiceDaFase(cartaoInicial.name.Substring("Cartao_".Length)));
        }
        else if (!ProgressoJogo.PreTesteFeito && linhaPreTeste != null)
        {
            primeiroSelecionavel = linhaPreTeste.gameObject;
            MostrarDetalhes(0);
        }
        else if (primeiroCartao != null)
        {
            primeiroSelecionavel = primeiroCartao.Botao != null ? primeiroCartao.Botao.gameObject : primeiroCartao.gameObject;
            MostrarDetalhes(0);
        }
    }

    private void AtualizarQuestionarios()
    {
        // Pulado (só pelo atalho do professor) fica cinza com a seta de pular, pra não parecer respondido.
        Color corFeito = new Color32(0x52, 0xC9, 0x8A, 0xFF);
        Color corPendente = new Color32(0xF5, 0xC5, 0x42, 0xFF);
        Color corNeutra = new Color32(0x9A, 0xA8, 0xC4, 0xFF);
        Sprite iconePulado = spritePulado != null ? spritePulado : spriteBloqueado;

        ProgressoJogo.QuestionarioSalvo pre = ProgressoJogo.Atual.preTeste;
        Color corPre = pre.feito ? (pre.pulado ? corNeutra : corFeito) : corPendente;
        if (textoPreTeste != null)
        {
            textoPreTeste.text = pre.feito
                ? (pre.pulado ? $"{tituloPreTeste} pulado pelo professor · sem respostas" : $"{tituloPreTeste} respondido · {NotaTexto(pre)}")
                : $"{tituloPreTeste} pendente · comece por aqui";
            textoPreTeste.color = corPre;
        }
        if (iconePreTeste != null)
        {
            iconePreTeste.sprite = pre.feito ? (pre.pulado ? iconePulado : spriteFeito) : spritePendente;
            iconePreTeste.color = corPre;
        }

        ProgressoJogo.QuestionarioSalvo pos = ProgressoJogo.Atual.posTeste;
        bool posLiberado = ProgressoJogo.PosTesteDesbloqueado && ProgressoJogo.PreTesteFeito;
        Color corPos = pos.feito ? (pos.pulado ? corNeutra : corFeito) : posLiberado ? corPendente : corNeutra;
        if (textoPosTeste != null)
        {
            textoPosTeste.text = pos.feito
                ? (pos.pulado ? $"{tituloPosTeste} pulado pelo professor · sem respostas" : $"{tituloPosTeste} respondido · {NotaTexto(pos)}")
                : posLiberado ? $"{tituloPosTeste} liberado · responder agora"
                : $"{tituloPosTeste} · libera depois da fase {ProgressoJogo.Fases.Length}";
            textoPosTeste.color = corPos;
        }
        if (iconePosTeste != null)
        {
            iconePosTeste.sprite = pos.feito ? (pos.pulado ? iconePulado : spriteFeito) : posLiberado ? spritePendente : spriteBloqueado;
            iconePosTeste.color = corPos;
        }
    }

    private void AtualizarCaminho()
    {
        if (caminho == null)
        {
            return;
        }

        foreach (TrechoCaminho t in caminho)
        {
            if (t == null || t.pontos == null) continue;
            bool feito = t.depoisDaFase >= 0 && t.depoisDaFase < ProgressoJogo.Fases.Length &&
                         ProgressoJogo.Concluida(ProgressoJogo.Fases[t.depoisDaFase].cena);
            foreach (Image p in t.pontos)
            {
                if (p == null) continue;
                Sprite s = feito ? spritePontoFeito : spritePonto;
                if (s != null) p.sprite = s;
            }
        }
    }

    private void MostrarDetalhes(int indice)
    {
        if (indice < 0 || indice >= ProgressoJogo.Fases.Length)
        {
            return;
        }

        faseSelecionada = indice;
        ProgressoJogo.Fase f = ProgressoJogo.Fases[indice];
        ProgressoJogo.FaseSalva salvo = ProgressoJogo.Info(f.cena);
        InfoFase info = BuscarInfo(f.cena);

        if (detalheModulo != null)
        {
            string modulo = info != null ? info.modulo : "";
            detalheModulo.text = $"MÓDULO {modulo.ToUpperInvariant()} · FASE {indice + 1}";
            if (info != null) detalheModulo.color = info.corModulo;
        }

        if (detalheTitulo != null) detalheTitulo.text = f.titulo;
        if (detalheDescricao != null) detalheDescricao.text = info != null && !string.IsNullOrEmpty(info.descricao) ? info.descricao : f.conceito;

        foreach (GameObject c in chips)
        {
            if (c == null) continue;
            c.SetActive(false); // Destroy só acontece no fim do quadro: tira do layout já
            Destroy(c);
        }
        chips.Clear();
        if (detalheConceitos != null && chipModelo != null && info != null && info.conceitos != null)
        {
            foreach (string conceito in info.conceitos)
            {
                GameObject chip = Instantiate(chipModelo, detalheConceitos);
                chip.SetActive(true);
                TMP_Text t = chip.GetComponentInChildren<TMP_Text>(true);
                if (t != null) t.text = conceito;
                chips.Add(chip);
            }
        }

        if (detalheTentativas != null) detalheTentativas.text = (salvo != null ? salvo.tentativas : 0).ToString();
        if (detalheMelhorTempo != null)
        {
            if (salvo != null && salvo.melhorTempo >= 0f)
            {
                int s = Mathf.RoundToInt(salvo.melhorTempo);
                detalheMelhorTempo.text = $"{s / 60}:{s % 60:00}";
            }
            else
            {
                detalheMelhorTempo.text = "--:--";
            }
        }

        bool concluida = ProgressoJogo.Concluida(f.cena);
        bool liberada = ProgressoJogo.Desbloqueada(f.cena);

        if (detalheStatus != null)
        {
            detalheStatus.text = concluida ? "<color=#52C98A>Concluída</color>"
                : liberada ? "<color=#5FD4E8>Liberada</color>"
                : $"<color=#9AA8C4>Bloqueada · {MotivoBloqueio(indice)}</color>";
        }

        if (botaoJogarFase != null) botaoJogarFase.interactable = liberada;
        if (rotuloJogarFase != null)
        {
            rotuloJogarFase.text = concluida ? "Jogar de novo" : "Jogar fase";
            EncostarIconeNoRotulo();
        }
    }

    // O ícone ▶ fica a uma folga fixa do começo do texto, que muda de tamanho
    // ("Jogar fase" / "Jogar de novo"); com posição fixa ele colava no texto longo.
    private void EncostarIconeNoRotulo()
    {
        Transform icone = botaoJogarFase != null ? botaoJogarFase.transform.Find("Icone") : null;
        if (icone == null)
        {
            return;
        }

        var rotulo = rotuloJogarFase.rectTransform;
        var ic = (RectTransform)icone;
        float larguraTexto = rotuloJogarFase.GetPreferredValues(rotuloJogarFase.text).x;
        float inicioTexto = rotulo.localPosition.x + rotulo.rect.center.x - larguraTexto * 0.5f;
        Vector3 p = ic.localPosition;
        p.x = inicioTexto - 16f - ic.rect.width * 0.5f;
        ic.localPosition = p;
    }

    private InfoFase BuscarInfo(string cena)
    {
        if (infoFases == null) return null;
        foreach (InfoFase i in infoFases)
        {
            if (i != null && i.cena == cena) return i;
        }
        return null;
    }

    private string MotivoBloqueio(int i)
    {
        string anterior = i == 0 ? "o questionário inicial" : $"\"{ProgressoJogo.Fases[i - 1].titulo}\"";
        return $"conclua {anterior}";
    }

    private void ClicarFase(int indice)
    {
        ProgressoJogo.Fase f = ProgressoJogo.Fases[indice];
        if (ProgressoJogo.Concluida(f.cena) || ProgressoJogo.Desbloqueada(f.cena))
        {
            Carregar(f.cena);
            return;
        }

        string anterior = indice == 0 ? "o questionário inicial" : $"\"{ProgressoJogo.Fases[indice - 1].titulo}\"";
        Avisar($"\"{f.titulo}\" abre quando você concluir {anterior}.");
    }

    private void JogarFaseSelecionada()
    {
        if (faseSelecionada >= 0)
        {
            ClicarFase(faseSelecionada);
        }
    }

    private void ClicarPreTeste()
    {
        if (ProgressoJogo.PreTesteFeito)
        {
            Avisar("O questionário inicial só é feito uma vez. Pra refazer, apague o progresso.");
            return;
        }

        Carregar(ProgressoJogo.CenaQuestionario);
    }

    private void ClicarPosTeste()
    {
        if (ProgressoJogo.PosTesteFeito)
        {
            Avisar("O questionário final já foi respondido.");
        }
        else if (ProgressoJogo.PosTesteDesbloqueado && ProgressoJogo.PreTesteFeito)
        {
            Carregar(ProgressoJogo.CenaQuestionario);
        }
        else
        {
            Avisar("O questionário final abre depois de todas as fases.");
        }
    }

    // Layout antigo: todos os cartões em sequência na Grade.
    private void AtualizarGrade()
    {
        // Questionário inicial
        ProgressoJogo.QuestionarioSalvo pre = ProgressoJogo.Atual.preTeste;
        if (pre.feito)
        {
            CriarCartao(numeroPreTeste, tituloPreTeste, conceitoPreTeste, pre.pulado ? "Pulado" : $"Feito · {NotaTexto(pre)}",
                CartaoFaseUI.Estado.Concluida, () => Avisar("O questionário inicial só é feito uma vez. Pra refazer, apague o progresso."));
        }
        else
        {
            CriarCartao(numeroPreTeste, tituloPreTeste, conceitoPreTeste, "Comece por aqui",
                CartaoFaseUI.Estado.Disponivel, () => Carregar(ProgressoJogo.CenaQuestionario));
        }

        // Fases
        for (int i = 0; i < ProgressoJogo.Fases.Length; i++)
        {
            ProgressoJogo.Fase f = ProgressoJogo.Fases[i];
            ProgressoJogo.FaseSalva info = ProgressoJogo.Info(f.cena);
            string numero = (i + 1).ToString();

            if (ProgressoJogo.Concluida(f.cena))
            {
                CriarCartao(numero, f.titulo, f.conceito, textoConcluida + Estatisticas(info), CartaoFaseUI.Estado.Concluida, () => Carregar(f.cena));
            }
            else if (ProgressoJogo.Desbloqueada(f.cena))
            {
                string status = info != null && info.tentativas > 0 ? textoEmAndamento : textoDisponivel;
                CriarCartao(numero, f.titulo, f.conceito, status, CartaoFaseUI.Estado.Disponivel, () => Carregar(f.cena));
            }
            else
            {
                string anterior = i == 0 ? "o questionário inicial" : $"\"{ProgressoJogo.Fases[i - 1].titulo}\"";
                CriarCartao(numero, f.titulo, f.conceito, textoBloqueada, CartaoFaseUI.Estado.Bloqueada,
                    () => Avisar($"\"{f.titulo}\" abre quando você concluir {anterior}."));
            }
        }

        // Questionário final
        ProgressoJogo.QuestionarioSalvo pos = ProgressoJogo.Atual.posTeste;
        if (pos.feito)
        {
            CriarCartao(numeroPosTeste, tituloPosTeste, conceitoPosTeste, pos.pulado ? "Pulado" : $"Feito · {NotaTexto(pos)}",
                CartaoFaseUI.Estado.Concluida, () => Avisar("O questionário final já foi respondido."));
        }
        else if (ProgressoJogo.PosTesteDesbloqueado && ProgressoJogo.PreTesteFeito)
        {
            CriarCartao(numeroPosTeste, tituloPosTeste, conceitoPosTeste, textoDisponivel,
                CartaoFaseUI.Estado.Disponivel, () => Carregar(ProgressoJogo.CenaQuestionario));
        }
        else
        {
            CriarCartao(numeroPosTeste, tituloPosTeste, conceitoPosTeste, textoBloqueada,
                CartaoFaseUI.Estado.Bloqueada, () => Avisar("O questionário final abre depois de todas as fases."));
        }
    }

    private void CriarCartao(string numero, string titulo, string conceito, string status, CartaoFaseUI.Estado estado, Action acao)
    {
        CartaoFaseUI cartao = Instantiate(cartaoModelo, grade);
        cartao.name = "Cartao_" + titulo;
        cartao.gameObject.SetActive(true);
        cartao.Preencher(numero, titulo, conceito, status, estado, acao);
        cartoes.Add(cartao.gameObject);

        if (primeiroSelecionavel == null && estado == CartaoFaseUI.Estado.Disponivel && cartao.Botao != null)
        {
            primeiroSelecionavel = cartao.Botao.gameObject;
        }
    }

    private static string NotaTexto(ProgressoJogo.QuestionarioSalvo q)
    {
        return q.nota >= 0 ? $"{q.nota}/{q.total} acertos" : "sem nota";
    }

    private string Estatisticas(ProgressoJogo.FaseSalva info)
    {
        if (info == null)
        {
            return "";
        }

        var partes = new List<string>();
        if (info.melhorTempo >= 0f)
        {
            int s = Mathf.RoundToInt(info.melhorTempo);
            partes.Add($"melhor tempo {s / 60}:{s % 60:00}");
        }
        if (info.menosErros >= 0) partes.Add($"{info.menosErros} erro{(info.menosErros == 1 ? "" : "s")}");
        if (info.menosFalhas >= 0) partes.Add($"{info.menosFalhas} queda{(info.menosFalhas == 1 ? "" : "s")}/morte{(info.menosFalhas == 1 ? "" : "s")}");

        if (partes.Count == 0)
        {
            return "";
        }

        string cor = ColorUtility.ToHtmlStringRGB(corEstatisticas);
        return $"\n<size=85%><color=#{cor}>" + string.Join(" · ", partes) + "</color></size>";
    }

    private void Carregar(string cena)
    {
        RegistroAprendizagem.Registrar("fase_escolhida", cena);
        Fechar();
        TrocaDeCena.Carregar(cena);
    }

    private void AbrirConfirmacao()
    {
        if (confirmacao == null) return;
        confirmacao.SetActive(true);
        if (botaoCancelar != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(botaoCancelar.gameObject);
        }
    }

    private void FecharConfirmacao()
    {
        if (confirmacao == null) return;
        confirmacao.SetActive(false);
        if (botaoApagar != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(botaoApagar.gameObject);
        }
    }

    private void ApagarProgresso()
    {
        RegistroAprendizagem.Registrar("progresso_apagado");
        ProgressoJogo.ApagarTudo();
        if (confirmacao != null) confirmacao.SetActive(false);
        Avisar("Progresso apagado.");
    }

    private void Avisar(string texto)
    {
        if (aviso == null)
        {
            return;
        }

        aviso.text = texto;
        avisoAte = Time.unscaledTime + segundosDoAviso;
    }
}
