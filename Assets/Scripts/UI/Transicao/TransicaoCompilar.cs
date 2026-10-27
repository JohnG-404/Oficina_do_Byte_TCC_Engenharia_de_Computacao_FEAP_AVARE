using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Transição "Compilando a fase" entre cenas: a tela vira a grade azul do editor, célula por
/// célula, a partir da porta de saída (ou do jogador); um cartão mostra o
/// módulo, a fase e a barra de carregamento; na cena nova a grade se desfaz a
/// partir do jogador.
///
/// O visual (cartão, cores, tamanho da célula, tempos, módulo de cada cena)
/// fica no prefab Assets/Resources/TransicaoCompilar.prefab, criado por
/// Tools > Transição > Criar prefab Compilando a fase. As células são
/// clonadas da "CelulaModelo" do prefab conforme o tamanho da tela.
/// Use pelo TrocaDeCena.
/// </summary>
public class TransicaoCompilar : MonoBehaviour
{
    private const string CaminhoPrefab = "TransicaoCompilar";

    [Serializable]
    public class InfoCena
    {
        public string cena;
        [Tooltip("Rótulo pequeno em cima do título (ex.: INTERAÇÃO).")]
        public string modulo;
        [Tooltip("Título no cartão. Vazio = \"Fase N · título\" do ProgressoJogo.")]
        public string titulo;
        public Color cor = Color.white;
    }

    [Header("Partes (ligadas pelo prefab)")]
    [SerializeField] private RectTransform grade;
    [SerializeField] private Image celulaModelo;
    [SerializeField] private Image bloqueio;
    [SerializeField] private CanvasGroup cartao;
    [SerializeField] private Image faixaModulo;
    [SerializeField] private TMP_Text textoModulo;
    [SerializeField] private TMP_Text textoTitulo;
    [SerializeField] private TMP_Text textoArquivo;
    [SerializeField] private Image barraPreenchimento;

    [Header("Grade")]
    [Tooltip("Lado de cada célula, em px da tela de referência (1920x1080).")]
    [SerializeField] private float tamanhoCelula = 60f;
    [Tooltip("Quanto do tempo cada célula leva pra aparecer/sumir (fração da duração).")]
    [Range(0.05f, 0.6f)] [SerializeField] private float duracaoCelula = 0.22f;
    [Tooltip("Mistura aleatória na ordem das células (0 = onda perfeita a partir da origem).")]
    [Range(0f, 0.6f)] [SerializeField] private float ruido = 0.22f;
    [Tooltip("Escala da célula quando começa a aparecer.")]
    [SerializeField] private float escalaInicial = 0.55f;

    [Header("Tempos (segundos, sem depender do Time.timeScale)")]
    [SerializeField] private float duracaoSaida = 0.35f;
    [SerializeField] private float duracaoEntrada = 0.4f;
    [Tooltip("Tempo mínimo do cartão na tela, mesmo se a cena carregar antes.")]
    [SerializeField] private float tempoMinimoCartao = 0.45f;
    [SerializeField] private float fadeCartao = 0.12f;
    [Tooltip("Recarregar a mesma cena (morte, reinício): sem cartão, só a grade.")]
    [SerializeField] private float tempoMinimoRecarregar = 0.08f;

    [Header("Cenas")]
    [SerializeField] private Color corPadrao = new Color32(0x5F, 0xD4, 0xE8, 0xFF);
    [SerializeField] private InfoCena[] cenas = Array.Empty<InfoCena>();

    private static TransicaoCompilar instancia;
    public static bool EmAndamento { get; private set; }

    private readonly List<Image> celulas = new List<Image>();
    private readonly List<Vector2> centros = new List<Vector2>();
    private float[] ordem = Array.Empty<float>();
    private Vector2 tamanhoMontado;
    private Color corAtual;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instancia = null;
        EmAndamento = false;
    }

    /// <summary>A instância persistente (criada do prefab na 1ª vez), ou null sem prefab.</summary>
    public static TransicaoCompilar Garantir()
    {
        if (instancia != null)
        {
            return instancia;
        }

        TransicaoCompilar prefab = Resources.Load<TransicaoCompilar>(CaminhoPrefab);
        if (prefab == null)
        {
            return null;
        }

        instancia = Instantiate(prefab);
        instancia.name = prefab.name;
        DontDestroyOnLoad(instancia.gameObject);
        instancia.Esconder();
        return instancia;
    }

    private void Awake()
    {
        if (celulaModelo != null)
        {
            celulaModelo.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (instancia == this)
        {
            instancia = null;
            EmAndamento = false;
        }
    }

    public void Iniciar(string cena, bool recarregar)
    {
        EmAndamento = true;
        gameObject.SetActive(true);
        StartCoroutine(Rodar(cena, recarregar));
    }

    private IEnumerator Rodar(string cena, bool recarregar)
    {
        gameObject.SetActive(true);
        if (bloqueio != null) bloqueio.raycastTarget = true;
        cartao.alpha = 0f;
        cartao.gameObject.SetActive(false);
        Canvas.ForceUpdateCanvases();
        MontarCelulas();

        InfoCena info = Info(cena);
        corAtual = info != null ? info.cor : corPadrao;

        // 1. A cena atual vira grade, a partir da porta de saída (ou do jogador).
        Ordenar(OrigemNaTela(!recarregar));
        yield return Animar(duracaoSaida, aparecer: true);
        PintarTudo(1f);

        // 2. Cartão + carregamento.
        float inicio = Time.realtimeSinceStartup;
        if (!recarregar)
        {
            PreencherCartao(cena, info);
            yield return FadeCartao(1f);
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(cena);
        if (op == null)
        {
            Esconder();
            yield break;
        }

        op.allowSceneActivation = false;
        float minimo = recarregar ? tempoMinimoRecarregar : tempoMinimoCartao;
        while (op.progress < 0.9f || Time.realtimeSinceStartup - inicio < minimo)
        {
            float carga = Mathf.Clamp01(op.progress / 0.9f);
            float tempo = Mathf.Clamp01((Time.realtimeSinceStartup - inicio) / Mathf.Max(0.01f, minimo));
            Barra(Mathf.Min(carga, tempo));
            yield return null;
        }

        Barra(1f);
        op.allowSceneActivation = true;
        while (!op.isDone)
        {
            yield return null;
        }

        // Deixa a cena nova rodar Start e a câmera chegar no jogador.
        yield return null;
        yield return null;

        if (!recarregar)
        {
            yield return FadeCartao(0f);
        }

        // 3. A grade se desfaz a partir do jogador.
        Ordenar(OrigemNaTela(false));
        if (bloqueio != null) bloqueio.raycastTarget = false;
        yield return Animar(duracaoEntrada, aparecer: false);
        Esconder();
    }

    private void Esconder()
    {
        PintarTudo(0f);
        if (cartao != null)
        {
            cartao.alpha = 0f;
            cartao.gameObject.SetActive(false);
        }
        gameObject.SetActive(false);
        EmAndamento = false;
    }

    // ------------------------------------------------------------ grade

    private void MontarCelulas()
    {
        Vector2 tamanho = grade.rect.size;
        if (celulas.Count > 0 && tamanho == tamanhoMontado)
        {
            return;
        }

        foreach (Image c in celulas)
        {
            if (c != null) Destroy(c.gameObject);
        }
        celulas.Clear();
        centros.Clear();
        tamanhoMontado = tamanho;

        int colunas = Mathf.CeilToInt(tamanho.x / tamanhoCelula);
        int linhas = Mathf.CeilToInt(tamanho.y / tamanhoCelula);
        // Centraliza a grade: a sobra fica dividida nas bordas.
        Vector2 inicio = new Vector2((tamanho.x - colunas * tamanhoCelula) / 2f, (tamanho.y - linhas * tamanhoCelula) / 2f);

        for (int y = 0; y < linhas; y++)
        {
            for (int x = 0; x < colunas; x++)
            {
                Image c = Instantiate(celulaModelo, grade);
                c.name = "Celula";
                c.gameObject.SetActive(true);
                c.raycastTarget = false;
                RectTransform rt = c.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(tamanhoCelula, tamanhoCelula);
                Vector2 centro = inicio + new Vector2((x + 0.5f) * tamanhoCelula, (y + 0.5f) * tamanhoCelula);
                rt.anchoredPosition = centro;
                celulas.Add(c);
                centros.Add(centro);
            }
        }

        ordem = new float[celulas.Count];
    }

    private void Ordenar(Vector2 origem)
    {
        float maior = 0.01f;
        for (int i = 0; i < centros.Count; i++)
        {
            maior = Mathf.Max(maior, Vector2.Distance(origem, centros[i]));
        }

        var rnd = new System.Random();
        float faixa = 1f - duracaoCelula;
        for (int i = 0; i < centros.Count; i++)
        {
            float d = Vector2.Distance(origem, centros[i]) / maior;
            float o = d * (1f - ruido) + (float)rnd.NextDouble() * ruido;
            ordem[i] = o * faixa;
        }
    }

    private IEnumerator Animar(float duracao, bool aparecer)
    {
        float t0 = Time.realtimeSinceStartup;
        while (true)
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - t0) / Mathf.Max(0.01f, duracao));
            for (int i = 0; i < celulas.Count; i++)
            {
                float k = Mathf.Clamp01((t - ordem[i]) / duracaoCelula);
                Pintar(i, aparecer ? k : 1f - k, aparecer);
            }

            if (t >= 1f)
            {
                yield break;
            }
            yield return null;
        }
    }

    // k = 0 sem célula, 1 célula cheia. Na borda da onda a célula brilha na cor do módulo.
    private void Pintar(int i, float k, bool aparecendo)
    {
        Image c = celulas[i];
        float suave = 1f - (1f - k) * (1f - k);
        float brilho = aparecendo ? 1f - k : k * (1f - k) * 4f * 0.6f;
        Color cor = Color.Lerp(Color.white, corAtual * 1.6f, Mathf.Clamp01(brilho) * 0.85f);
        // A cor aparece logo: a célula fica opaca no 1º terço e só depois "apaga" para o azul do editor.
        cor.a = Mathf.Clamp01(k * 3f);
        c.color = cor;
        float s = Mathf.Lerp(escalaInicial, 1f, suave);
        c.rectTransform.localScale = new Vector3(s, s, 1f);
    }

    private void PintarTudo(float k)
    {
        for (int i = 0; i < celulas.Count; i++)
        {
            Pintar(i, k, true);
        }
    }

    /// <summary>Ponto na grade de onde a onda parte: porta de saída, jogador ou o meio da tela.</summary>
    private Vector2 OrigemNaTela(bool preferirPorta)
    {
        Camera cam = Camera.main;
        Vector2 meio = grade.rect.size / 2f;
        if (cam == null)
        {
            return meio;
        }

        Transform alvo = null;
        if (preferirPorta)
        {
            GameObject porta = GameObject.Find("PortaSaida");
            if (porta != null && NaTela(cam, porta.transform.position))
            {
                alvo = porta.transform;
            }
        }

        if (alvo == null)
        {
            GameObject jogador = GameObject.FindWithTag("Player");
            if (jogador != null && NaTela(cam, jogador.transform.position))
            {
                alvo = jogador.transform;
            }
        }

        if (alvo == null)
        {
            return meio;
        }

        Vector3 tela = cam.WorldToScreenPoint(alvo.position);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(grade, tela, null, out Vector2 local)
            ? local - grade.rect.min
            : meio;
    }

    private static bool NaTela(Camera cam, Vector3 posicao)
    {
        Vector3 v = cam.WorldToViewportPoint(posicao);
        return v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
    }

    // ------------------------------------------------------------ cartão

    private InfoCena Info(string cena)
    {
        foreach (InfoCena i in cenas)
        {
            if (i != null && i.cena == cena) return i;
        }
        return null;
    }

    private void PreencherCartao(string cena, InfoCena info)
    {
        cartao.gameObject.SetActive(true);
        if (faixaModulo != null) faixaModulo.color = corAtual;
        if (barraPreenchimento != null) barraPreenchimento.color = corAtual;

        if (textoModulo != null)
        {
            textoModulo.text = info != null ? info.modulo : "";
            textoModulo.color = corAtual;
        }

        if (textoTitulo != null)
        {
            string titulo = info != null ? info.titulo : null;
            if (string.IsNullOrEmpty(titulo))
            {
                int i = ProgressoJogo.IndiceDaFase(cena);
                titulo = i >= 0 ? $"Fase {i + 1} · {ProgressoJogo.Fases[i].titulo}" : cena;
            }
            textoTitulo.text = titulo;
        }

        if (textoArquivo != null)
        {
            textoArquivo.text = $"Carregando {cena}.unity";
        }

        Barra(0f);
    }

    private void Barra(float f)
    {
        if (barraPreenchimento == null)
        {
            return;
        }

        RectTransform r = barraPreenchimento.rectTransform;
        r.anchorMax = new Vector2(Mathf.Clamp01(f), r.anchorMax.y);
    }

    private IEnumerator FadeCartao(float alvo)
    {
        float de = cartao.alpha;
        float t0 = Time.realtimeSinceStartup;
        while (true)
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - t0) / Mathf.Max(0.01f, fadeCartao));
            cartao.alpha = Mathf.Lerp(de, alvo, t);
            float s = Mathf.Lerp(0.94f, 1f, cartao.alpha);
            cartao.transform.localScale = new Vector3(s, s, 1f);
            if (t >= 1f) yield break;
            yield return null;
        }
    }
}
