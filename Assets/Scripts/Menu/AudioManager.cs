using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gerencia o volume, a trilha sonora e os efeitos sonoros do jogo. Segue o
/// mesmo padrão de singleton persistente do GameManager/InputManager: se
/// nenhuma instância existir na cena carregada, cria uma sozinho (funciona em
/// qualquer cena, sem precisar ser colocado manualmente).
///
/// Volume (tudo salvo em PlayerPrefs):
/// - Geral ("MasterVolume"): AudioListener.volume — multiplica TODO o áudio,
///   inclusive sons que não passam por aqui (ex: cliques do DialogueManager).
/// - Música ("MusicVolume"): SetMusicVolume / MusicVolume / OnMusicVolumeChanged.
/// - Efeitos ("SfxVolume"): SetSfxVolume / SfxVolume / OnSfxVolumeChanged.
///   Vale pros efeitos daqui e pros cliques da máquina de escrever.
/// Os três passam pela Curva (valor²) e a saída inteira é limitada por
/// TetoSaida: com tudo em 100%, AudioListener.volume = TetoSaida.
///
/// Música: dois AudioSource internos fazem crossfade entre faixas. As faixas
/// ficam em Assets/Resources/Audio/Musicas e trocam sozinhas ao carregar
/// qualquer cena (ver PlayTrackForScene).
///
/// Efeitos: AudioManager.TocarEfeito(Efeito.X) de qualquer script. Os
/// arquivos ficam em Assets/Resources/Audio/Efeitos; um efeito sem arquivo
/// definido em ArquivoDoEfeito simplesmente não toca (ponto de chamada já
/// pronto, só falta escolher o som). Os sons de UI (clique, hover, voltar,
/// abrir/fechar painel) são ligados sozinhos em runtime nos botões e painéis
/// de qualquer cena (ver SomUI e SomPainel) — nada precisa ser configurado
/// nas cenas ou prefabs.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    /// <summary>Faixas disponíveis em Assets/Resources/Audio/Musicas.</summary>
    public enum MusicTrack
    {
        None,
        Menu,
        Fisica,
        Interacao,
        Animacao,
        IA,
        Questionario,
        Final,   // tela final / créditos (quando existir) — chamar PlayMusic(MusicTrack.Final)
        Estudo   // reserva
    }

    /// <summary>Efeitos sonoros do jogo. Ver ArquivoDoEfeito pra saber qual arquivo cada um usa.</summary>
    public enum Efeito
    {
        Pulo,
        Aterrissar,
        Erro,              // peça errada / sequência errada
        Cartao,
        PortaAbrir,
        PortaFechar,
        JumpPad,
        GuardaAlerta,
        GuardaCalmo,
        UiClique,
        UiHover,
        UiVoltar,
        UiPainelAbrir,
        UiPainelFechar,

        // Ainda sem som escolhido: os pontos de chamada já existem.
        Encaixe,
        Acerto,
        Dano,
        GameOver,
        PuzzleResolvido,
        FaseCompleta,
        Coletar,
        Alavanca,
        Laser,
        FaseDesbloqueada,
        Passo              // som de passo (Oficina: evento de animação)
    }

    public const string MasterVolumeKey = "MasterVolume";
    public const string MusicVolumeKey = "MusicVolume";
    public const string SfxVolumeKey = "SfxVolume";

    private const string MusicResourcesFolder = "Audio/Musicas/";
    private const string EffectsResourcesFolder = "Audio/Efeitos/";
    private const float MusicBaseVolume = 0.55f;
    private const float DefaultCrossfadeSeconds = 1.5f;
    private const int EffectVoices = 8;
    private const float EffectCooldownSeconds = 0.06f;
    private const float UiScanIntervalSeconds = 0.2f;
    private const float MinLandingSpeed = 3f;

    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 1f;
    public float SfxVolume { get; private set; } = 1f;

    public event Action<float> OnVolumeChanged;
    public event Action<float> OnMusicVolumeChanged;
    public event Action<float> OnSfxVolumeChanged;

    public MusicTrack CurrentTrack => currentTrack;

    private AudioSource musicSourceA;
    private AudioSource musicSourceB;
    private bool usingSourceA = true;
    private Coroutine musicTransition;
    private MusicTrack currentTrack = MusicTrack.None;
    private bool musicFading;
    private AudioSource fadingInSource;
    private float fadingInStartVolume;
    private float fadeProgress;

    private readonly AudioSource[] effectSources = new AudioSource[EffectVoices];
    private int nextEffectSource;
    private readonly Dictionary<Efeito, AudioClip> effectClips = new Dictionary<Efeito, AudioClip>();
    private readonly Dictionary<Efeito, float> lastEffectTime = new Dictionary<Efeito, float>();

    private float nextUiScan;
    private Selectable[] selectableBuffer = new Selectable[64];
    private bool panelOpenedThisFrame;
    private readonly List<GameObject> panelsClosedThisFrame = new List<GameObject>();
    private int lastBackSoundFrame = -1;

    private AudioSource ActiveMusicSource => usingSourceA ? musicSourceA : musicSourceB;
    private AudioSource StandbyMusicSource => usingSourceA ? musicSourceB : musicSourceA;
    private float MusicTargetVolume => MusicBaseVolume * Curva(MusicVolume);

    /// <summary>
    /// Teto da saída com tudo em 100% (0,5 ≈ -6 dB), pra o som não sair alto
    /// demais no fone. Aplicado só no AudioListener: os valores salvos
    /// continuam de 0 a 1.
    /// </summary>
    public const float TetoSaida = 0.5f;

    /// <summary>
    /// Curva perceptual dos sliders (valor²): o ouvido sente volume em escala
    /// logarítmica, então sem a curva 50% soaria quase tão alto quanto 100%.
    /// </summary>
    public static float Curva(float valorDoSlider)
    {
        float v = Mathf.Clamp01(valorDoSlider);
        return v * v;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("AudioManager (Auto)");
        go.AddComponent<AudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        ApplyVolume();

        musicSourceA = CreateSource("Music A", loop: true);
        musicSourceB = CreateSource("Music B", loop: true);
        for (int i = 0; i < EffectVoices; i++)
        {
            effectSources[i] = CreateSource("Efeito " + (i + 1), loop: false);
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        RegistroAprendizagem.OnEvento += HandleRegistro;
        PlayTrackForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            RegistroAprendizagem.OnEvento -= HandleRegistro;
        }
    }

    private AudioSource CreateSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var source = go.AddComponent<AudioSource>();
        source.loop = loop; // as faixas têm loop perfeito, sem clique na emenda
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D
        source.volume = 0f;
        return source;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayTrackForScene(scene.name);
        nextUiScan = 0f; // liga os sons de UI da cena nova já no primeiro frame

        // Oficina da Tutorial_Anim: som de encaixe nas peças, sem editar a cena.
        foreach (SequencePiece peca in FindObjectsByType<SequencePiece>(FindObjectsInactive.Include))
        {
            SomEncaixePeca.Garantir(peca);
        }
    }

    // Escolhe a faixa certa pra cada cena automaticamente — sem precisar
    // colocar nada manualmente em cada uma. Cenas não listadas mantêm a
    // faixa que já estava tocando (ex: uma cena de transição futura).
    private void PlayTrackForScene(string sceneName)
    {
        switch (sceneName)
        {
            case "Menu":
                PlayMusic(MusicTrack.Menu);
                break;

            // "Inicio"/"Aplicacao" foram renomeadas pra "Fisica"/"Aplicacao_Fisica" no
            // Editor; mantidos aqui por segurança caso algo ainda carregue pelo nome antigo.
            case "Inicio":
            case "Aplicacao":
            case "Fisica":
            case "Aplicacao_Fisica":
                PlayMusic(MusicTrack.Fisica);
                break;

            case "Interacao":
            case "Interacao_Vida":
                PlayMusic(MusicTrack.Interacao);
                break;

            case "Tutorial_Anim":
            case "Animacao":
                PlayMusic(MusicTrack.Animacao);
                break;

            case "IA":
            case "IA_Aplicacao":
                PlayMusic(MusicTrack.IA);
                break;

            case "Questionario":
                PlayMusic(MusicTrack.Questionario);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Volume
    // ------------------------------------------------------------------

    public void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        ApplyVolume();
        SaveVolume(MasterVolumeKey, MasterVolume);
        OnVolumeChanged?.Invoke(MasterVolume);
    }

    public void SetMusicVolume(float volume)
    {
        MusicVolume = Mathf.Clamp01(volume);
        // Aplica na hora: durante um crossfade, no ponto atual do fade (que
        // também relê MusicTargetVolume a cada frame e aplica no fim); fora
        // dele, direto na faixa atual (sem olhar isPlaying: uma faixa em
        // streaming ainda carregando lê false).
        if (musicFading)
        {
            if (fadingInSource != null)
            {
                fadingInSource.volume = Mathf.Lerp(fadingInStartVolume, MusicTargetVolume, fadeProgress);
            }
        }
        else if (ActiveMusicSource.clip != null)
        {
            ActiveMusicSource.volume = MusicTargetVolume;
        }
        SaveVolume(MusicVolumeKey, MusicVolume);
        OnMusicVolumeChanged?.Invoke(MusicVolume);
    }

    public void SetSfxVolume(float volume)
    {
        SfxVolume = Mathf.Clamp01(volume);
        SaveVolume(SfxVolumeKey, SfxVolume);
        OnSfxVolumeChanged?.Invoke(SfxVolume);
    }

    /// <summary>Volume de efeitos atual (1 se o AudioManager ainda não existir) — pra sons tocados fora daqui.</summary>
    public static float VolumeEfeitos => Instance != null ? Curva(Instance.SfxVolume) : 1f;

    private static void SaveVolume(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
    }

    private void ApplyVolume()
    {
        AudioListener.volume = Curva(MasterVolume) * TetoSaida;
    }

    // ------------------------------------------------------------------
    // Música
    // ------------------------------------------------------------------

    /// <summary>Troca pra faixa informada com crossfade. Não faz nada se ela já estiver tocando.</summary>
    public void PlayMusic(MusicTrack track, float crossfadeSeconds = DefaultCrossfadeSeconds)
    {
        if (track == currentTrack && ActiveMusicSource.clip != null)
        {
            return;
        }

        if (track == MusicTrack.None)
        {
            StopMusic(crossfadeSeconds);
            return;
        }

        AudioClip clip = Resources.Load<AudioClip>(MusicResourcesFolder + TrackFileName(track));
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] Não encontrei a faixa '{track}' em Resources/{MusicResourcesFolder}.");
            return;
        }

        currentTrack = track;

        AudioSource incoming = StandbyMusicSource;
        incoming.clip = clip;
        incoming.timeSamples = 0;
        incoming.Play();

        if (musicTransition != null)
        {
            StopCoroutine(musicTransition);
            musicFading = false;
        }
        musicTransition = StartCoroutine(Crossfade(incoming, ActiveMusicSource, crossfadeSeconds));
        usingSourceA = !usingSourceA;
    }

    public void StopMusic(float fadeSeconds = DefaultCrossfadeSeconds)
    {
        currentTrack = MusicTrack.None;

        if (musicTransition != null)
        {
            StopCoroutine(musicTransition);
            musicFading = false;
        }
        musicTransition = StartCoroutine(Crossfade(null, ActiveMusicSource, fadeSeconds));
    }

    private static string TrackFileName(MusicTrack track)
    {
        switch (track)
        {
            case MusicTrack.Menu: return "musica_01_menu_manha_calma";
            case MusicTrack.Fisica: return "musica_02_fisica_engrenagens_suaves";
            case MusicTrack.Interacao: return "musica_03_interacao_jardim";
            case MusicTrack.Animacao: return "musica_04_animacao_nuvens";
            case MusicTrack.IA: return "musica_05_ia_circuitos_serenos";
            case MusicTrack.Questionario: return "musica_06_questionario_foco";
            case MusicTrack.Final: return "musica_07_final_por_do_sol";
            case MusicTrack.Estudo: return "musica_08_estudo_lofi";
            default: return null;
        }
    }

    private IEnumerator Crossfade(AudioSource fadeIn, AudioSource fadeOut, float duration)
    {
        musicFading = true;
        fadingInSource = fadeIn;
        fadeProgress = 0f;
        float startVolumeIn = fadeIn != null ? fadeIn.volume : 0f;
        fadingInStartVolume = startVolumeIn;
        float startVolumeOut = fadeOut != null ? fadeOut.volume : 0f;

        float t = 0f;
        while (t < duration)
        {
            // unscaledDeltaTime: a transição funciona mesmo com o jogo pausado (Time.timeScale = 0).
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            fadeProgress = k;

            // MusicTargetVolume lido a cada frame: mexer no slider durante a transição já vale.
            if (fadeIn != null) fadeIn.volume = Mathf.Lerp(startVolumeIn, MusicTargetVolume, k);
            if (fadeOut != null) fadeOut.volume = Mathf.Lerp(startVolumeOut, 0f, k);

            yield return null;
        }

        if (fadeIn != null) fadeIn.volume = MusicTargetVolume;
        if (fadeOut != null) { fadeOut.volume = 0f; fadeOut.Stop(); fadeOut.clip = null; }
        musicFading = false;
        fadingInSource = null;
    }

    // ------------------------------------------------------------------
    // Efeitos
    // ------------------------------------------------------------------

    /// <summary>
    /// Nome do arquivo (sem extensão) em Resources/Audio/Efeitos. null = som
    /// ainda não escolhido: a chamada fica pronta e não toca nada. Pra ligar
    /// um som novo, basta colocar o arquivo na pasta e o nome aqui.
    /// </summary>
    private static string ArquivoDoEfeito(Efeito efeito)
    {
        switch (efeito)
        {
            case Efeito.Pulo: return "sfx_pulo_1";
            case Efeito.Aterrissar: return "sfx_aterrissar_1";
            case Efeito.Erro: return "sfx_erro_2";
            case Efeito.Cartao: return "sfx_cartao_2";
            case Efeito.PortaAbrir: return "sfx_porta_abrir_1";
            case Efeito.PortaFechar: return "sfx_porta_fechar_1";
            case Efeito.JumpPad: return "sfx_jumppad_2";
            case Efeito.GuardaAlerta: return "sfx_guarda_alerta_1";
            case Efeito.GuardaCalmo: return "sfx_guarda_calmo_1";
            case Efeito.UiClique: return "sfx_ui_clique_1";
            case Efeito.UiHover: return "sfx_ui_hover_1";
            case Efeito.UiVoltar: return "sfx_ui_voltar_1";
            case Efeito.UiPainelAbrir: return "sfx_ui_painel_abrir_1";
            case Efeito.UiPainelFechar: return "sfx_ui_painel_fechar_1";
            case Efeito.Dano: return "sfx_dano_4";
            case Efeito.GameOver: return "sfx_game_over_4";
            case Efeito.Encaixe: return "sfx_encaixe_4";
            case Efeito.Acerto: return "sfx_acerto_4";
            case Efeito.PuzzleResolvido: return "sfx_puzzle_resolvido_3";
            case Efeito.Coletar: return "sfx_coletar_3";
            case Efeito.Alavanca: return "sfx_alavanca_3";
            case Efeito.Laser: return "sfx_laser_ligar_4";
            case Efeito.FaseDesbloqueada: return "sfx_desbloquear_3";
            case Efeito.Passo: return "sfx_passo_1";
            case Efeito.FaseCompleta: return null; // escolhido: sem som
            default: return null;
        }
    }

    /// <summary>Volume relativo de cada efeito (hover bem baixinho, que toca o tempo todo).</summary>
    private static float VolumeDoEfeito(Efeito efeito)
    {
        switch (efeito)
        {
            case Efeito.UiHover: return 0.45f;
            case Efeito.Aterrissar: return 0.7f;
            case Efeito.UiClique:
            case Efeito.UiVoltar: return 0.8f;
            default: return 1f;
        }
    }

    /// <summary>Toca um efeito sonoro. Seguro de chamar de qualquer lugar (sem AudioManager ou sem som definido, não faz nada).</summary>
    public static void TocarEfeito(Efeito efeito, float volume = 1f, float variacaoPitch = 0.04f)
    {
        if (Instance != null)
        {
            Instance.PlayEffect(efeito, volume, variacaoPitch);
        }
    }

    /// <summary>Som de aterrissagem, só se a queda foi rápida o bastante (não toca ao encostar num degrau).</summary>
    public static void TocarAterrissagem(float velocidadeDeImpacto)
    {
        if (Mathf.Abs(velocidadeDeImpacto) >= MinLandingSpeed)
        {
            TocarEfeito(Efeito.Aterrissar);
        }
    }

    private void PlayEffect(Efeito efeito, float volume, float pitchVariation)
    {
        AudioClip clip = GetEffectClip(efeito);
        if (clip == null)
        {
            return;
        }

        // Evita o mesmo som empilhado no mesmo instante (ex: dois contatos de chão no mesmo passo de física).
        float now = Time.unscaledTime;
        if (lastEffectTime.TryGetValue(efeito, out float last) && now - last < EffectCooldownSeconds)
        {
            return;
        }
        lastEffectTime[efeito] = now;

        AudioSource source = effectSources[nextEffectSource];
        nextEffectSource = (nextEffectSource + 1) % EffectVoices;

        source.Stop();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume * VolumeDoEfeito(efeito) * Curva(SfxVolume));
        source.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
        source.Play();

        if (efeito == Efeito.UiVoltar)
        {
            lastBackSoundFrame = Time.frameCount;
        }
    }

    private AudioClip GetEffectClip(Efeito efeito)
    {
        if (effectClips.TryGetValue(efeito, out AudioClip cached))
        {
            return cached;
        }

        string file = ArquivoDoEfeito(efeito);
        AudioClip clip = null;
        if (file != null)
        {
            clip = Resources.Load<AudioClip>(EffectsResourcesFolder + file);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] Não encontrei o efeito '{file}' em Resources/{EffectsResourcesFolder}.");
            }
        }

        effectClips[efeito] = clip;
        return clip;
    }

    // Eventos do registro de aprendizagem que já marcam momentos do jogo em
    // todas as fases (peça errada, puzzle concluído, fase concluída...) —
    // assim cada fase ganha o som sem precisar mexer em cada script.
    private void HandleRegistro(string evento, string detalhe)
    {
        switch (evento)
        {
            case "puzzle_peca_errada":
            case "sequencia_errada":
            case "anim_ajuste" when detalhe.EndsWith("errado"):
                TocarEfeito(Efeito.Erro);
                break;

            // Oficina da Tutorial_Anim: a rodada da Alavanca termina com o som da alavanca.
            case "sequencia_concluida" when detalhe.StartsWith("Alavanca"):
                TocarEfeito(Efeito.Alavanca);
                break;

            case "puzzle_concluido":
            case "sequencia_concluida":
                TocarEfeito(Efeito.PuzzleResolvido);
                break;

            case "fase_concluida":
                TocarEfeito(Efeito.FaseCompleta);
                break;

            case "game_over":
                TocarEfeito(Efeito.GameOver);
                break;

            case "dano":
            case "guarda_pegou":
            case "queda":
                TocarEfeito(Efeito.Dano);
                break;

            case "morte" when !detalhe.StartsWith("vida zerou"): // "vida zerou" já tocou o "dano"
                TocarEfeito(Efeito.Dano);
                break;

            case "animacao_aplicada" when detalhe.EndsWith("certa"):
            case "ferramenta_aplicada":
                TocarEfeito(Efeito.Acerto);
                break;

            case "puzzle_inicio":
                AvisarPainel(aberto: true);
                break;

            case "fases_desbloqueadas":
                TocarEfeito(Efeito.FaseDesbloqueada);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Sons de UI ligados em runtime
    // ------------------------------------------------------------------

    private void Update()
    {
        if (Time.unscaledTime < nextUiScan)
        {
            return;
        }
        nextUiScan = Time.unscaledTime + UiScanIntervalSeconds;

        AttachUiSounds();
        AttachPanelSounds();
    }

    // Botões criados por código (painéis, questionário...) aparecem a qualquer
    // momento, então a varredura é periódica e barata (só os Selectables ativos).
    private void AttachUiSounds()
    {
        int count = Selectable.allSelectableCount;
        if (selectableBuffer.Length < count)
        {
            selectableBuffer = new Selectable[Mathf.NextPowerOfTwo(count)];
        }
        count = Selectable.AllSelectablesNoAlloc(selectableBuffer);

        for (int i = 0; i < count; i++)
        {
            Selectable s = selectableBuffer[i];
            selectableBuffer[i] = null;

            if (s == null || !SomUI.Suporta(s) || s.TryGetComponent(out SomUI _))
            {
                continue;
            }
            s.gameObject.AddComponent<SomUI>();
        }
    }

    // Painéis de Configurações e Pausa (tema Laboratório): o som vai num
    // componente adicionado em runtime, sem mexer nos prefabs/scripts deles.
    private void AttachPanelSounds()
    {
        foreach (SettingsPanelUI settings in FindObjectsByType<SettingsPanelUI>(FindObjectsInactive.Include))
        {
            SomPainel.Garantir(settings.gameObject);
        }

        if (PauseMenuUI.Instance != null)
        {
            Transform pause = PauseMenuUI.Instance.transform.Find("PausePanel");
            if (pause != null)
            {
                SomPainel.Garantir(pause.gameObject);
            }
        }
    }

    /// <summary>
    /// "Abriu/fechou um painel" pra painéis que não usam SomPainel (montados e
    /// destruídos por código). Mesma regra do SomPainel: um "Voltar" no mesmo
    /// frame não toca o "fechar" de novo.
    /// </summary>
    public static void AvisarPainel(bool aberto)
    {
        if (Instance != null)
        {
            Instance.NotifyPanel(Instance.gameObject, aberto);
        }
    }

    /// <summary>Chamado pelo SomPainel. Resolvido no LateUpdate: trocar de um painel pro outro no mesmo frame toca só "abrir".</summary>
    internal void NotifyPanel(GameObject panel, bool opened)
    {
        if (opened)
        {
            panelOpenedThisFrame = true;
        }
        else
        {
            panelsClosedThisFrame.Add(panel);
        }
    }

    private void LateUpdate()
    {
        if (panelOpenedThisFrame)
        {
            TocarEfeito(Efeito.UiPainelAbrir);
        }
        else if (panelsClosedThisFrame.Count > 0 && lastBackSoundFrame != Time.frameCount)
        {
            // Painel destruído (troca de cena) não conta como "fechar". Botão
            // "Voltar" no mesmo frame já tocou o som dele.
            foreach (GameObject panel in panelsClosedThisFrame)
            {
                if (panel != null)
                {
                    TocarEfeito(Efeito.UiPainelFechar);
                    break;
                }
            }
        }

        panelOpenedThisFrame = false;
        panelsClosedThisFrame.Clear();
    }
}
