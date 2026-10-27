using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public TextMeshProUGUI dialogueText;
    public GameObject dialoguePanel;

    [Tooltip("Legado: usado pela cena Inicio, arrastado no Inspector. Cenas novas (sem GameFlowManager) devem chamar SetListener() em vez de preencher este campo.")]
    public GameFlowManager flowManager;

    // Alternativa a arrastar um GameFlowManager no Inspector — qualquer
    // IDialogueListener pode se registrar em runtime (ver SetListener),
    // usado por cenas como Aplicacao que não têm GameFlowManager.
    private IDialogueListener runtimeListener;

    private IDialogueListener EffectiveListener => runtimeListener ?? flowManager;

    private Queue<string> lines = new Queue<string>();
    public float typingSpeed = 0.03f;

    [Header("Som da máquina de escrever")]
    [Tooltip("Carregado sozinho de Resources/Audio/SFX — não precisa arrastar nada aqui.")]
    [SerializeField, Range(0f, 1f)] private float typeSoundVolume = 0.6f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.94f, 1.08f);
    [Tooltip("Toca o clique a cada N caracteres — 1 clique por letra costuma soar exagerado.")]
    [SerializeField, Min(1)] private int playSoundEveryNCharacters = 2;

    private AudioSource sfxSource;
    private AudioClip[] clickClips;
    private AudioClip endOfLineClip;
    private int lastClickIndex = -1;
    private int charactersSinceLastClick;

    // Controla se o Update() deve escutar o Submit pra avançar o diálogo —
    // sem isso, apertar Espaço/Enter em QUALQUER momento (mesmo com o painel
    // fechado) chamava EndDialogue() de novo e reacionava o listener fora de hora.
    private bool dialogueActive;

    // Linha sendo digitada agora: o 1º Enter/clique completa ela de uma vez
    // (quem lê rápido não perde o resto do texto), o 2º avança.
    private bool isTyping;
    private string currentLine = "";

    // O clique/Enter que abriu o diálogo (ex.: clicar num objeto) não pode
    // já completar a primeira linha no mesmo frame.
    private int startFrame = -1;

    [Header("Posição da caixa (fases com o tema Laboratório)")]
    [Tooltip("A caixa fica embaixo, encostada no canto do lado oposto ao jogador, pra não esconder o personagem.")]
    [SerializeField] private float larguraCaixa = 1340f;
    [Tooltip("Mesma distância da borda de baixo e da borda lateral — a caixa fica alinhada com o canto da tela.")]
    [SerializeField] private float margemCaixa = 30f;
    // Meia largura/altura aproximada do jogador na tela (unidades do Canvas 1920x1080).
    private const float FolgaJogador = 70f;
    // A placa com o nome sai ~40 px pra fora da borda de cima da caixa.
    private const float AlturaPlacaNome = 40f;
    private Transform player;

    /// <summary>
    /// True enquanto qualquer diálogo estiver na tela, em qualquer cena —
    /// estático de propósito, pra qualquer PlayerController checar sem
    /// precisar de referência (ver InteracaoPlayerController/
    /// AplicacaoPlayerController/Player.HandleMovement) e travar o movimento
    /// livre enquanto o jogador está lendo.
    /// </summary>
    public static bool IsAnyDialogueActive { get; private set; }

    /// <summary>
    /// True enquanto a caixa estiver no canto de cima (ver
    /// PositionBoxAwayFromPlayer): a faixa de objetivo (ObjetivoFase) some
    /// nesse tempo, senão cobre a placa com o nome do Prof Byte.
    /// </summary>
    public static bool CaixaNoTopo { get; private set; }

    // O painel começa escondido aqui, no Awake — e não no Start: quem chama
    // StartDialogue() (GameFlowManager/AplicacaoLevelFlow...) faz isso no
    // próprio Start(), e a ordem entre dois Start() não é garantida (antes, um
    // Start() daqui chegou a esconder o diálogo que acabara de ser mostrado).
    // TODOS os Awake() da cena rodam antes de QUALQUER Start(), então aqui é
    // seguro.
    //
    // Sem isso, uma fase que recomeça sem repetir a intro (depois de uma
    // morte/queda) ficava com o painel vazio na tela se o "DialogueBox"
    // estivesse salvo ativo na cena — só a intro fechava ele.
    //
    // Cuidado: quando este componente fica no próprio painel e o painel é
    // salvo desligado, este Awake só roda DENTRO do StartDialogue (no
    // SetActive(true)) — aí dialogueActive já é true e nada é escondido.
    private void Awake()
    {
        LoadTypingSounds();
        TemaLabFases.EstilizarDialogo(dialoguePanel, dialogueText);

        if (!dialogueActive && dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    // Estático: se a cena recarregar no meio de um diálogo, ficaria true e
    // travaria o movimento do jogador pra sempre. sceneLoaded roda depois dos
    // Awake e antes dos Start (onde os diálogos começam).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnSceneLoad()
    {
        IsAnyDialogueActive = false;
        CaixaNoTopo = false;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= ClearActiveFlag;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += ClearActiveFlag;
    }

    private static void ClearActiveFlag(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single)
        {
            IsAnyDialogueActive = false;
            CaixaNoTopo = false;
        }
    }

    private void LoadTypingSounds()
    {
        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        var loadedClicks = new List<AudioClip>();
        foreach (string clipName in new[] { "typewriter_click_01", "typewriter_click_02", "typewriter_click_03" })
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/SFX/" + clipName);
            if (clip != null)
            {
                loadedClicks.Add(clip);
            }
        }
        clickClips = loadedClicks.ToArray();

        endOfLineClip = Resources.Load<AudioClip>("Audio/SFX/typewriter_fim_de_linha");
    }

    /// <summary>Registra quem deve ser avisado ao fim do diálogo (ver EffectiveListener).</summary>
    public void SetListener(IDialogueListener listener)
    {
        runtimeListener = listener;
    }

    public void StartDialogue(string[] dialogueLines)
    {
        dialogueActive = true;
        IsAnyDialogueActive = true;
        startFrame = Time.frameCount;
        dialoguePanel.SetActive(true);
        PositionBoxAwayFromPlayer();
        lines.Clear();

        foreach (string line in dialogueLines)
        {
            // "{andar}" e "{pular}" viram a tecla configurada em Controles.
            lines.Enqueue(TextoTeclas.Trocar(line));
        }

        StopAllCoroutines();
        StartCoroutine(TypeLine(lines.Dequeue()));
    }

    IEnumerator TypeLine(string line)
    {
        currentLine = line;
        isTyping = true;
        dialogueText.text = "";
        charactersSinceLastClick = 0;

        // Velocidade do texto (Configurações > Jogo): 0 = linha inteira de uma vez.
        float espera = typingSpeed * ConfiguracoesJogo.FatorVelocidadeTexto;
        if (espera <= 0f)
        {
            dialogueText.text = line;
        }
        else
        {
            foreach (char letter in line.ToCharArray())
            {
                dialogueText.text += letter;
                PlayTypeSoundIfDue(letter);
                yield return new WaitForSeconds(espera);
            }
        }

        isTyping = false;
        PlayEndOfLineSound();
    }

    /// <summary>Mostra a linha atual inteira sem esperar a digitação.</summary>
    private void CompleteLine()
    {
        StopAllCoroutines();
        dialogueText.text = currentLine;
        isTyping = false;
        PlayEndOfLineSound();
    }

    private void PlayTypeSoundIfDue(char letter)
    {
        if (sfxSource == null || char.IsWhiteSpace(letter))
        {
            return;
        }

        charactersSinceLastClick++;
        if (charactersSinceLastClick < playSoundEveryNCharacters)
        {
            return;
        }
        charactersSinceLastClick = 0;

        AudioClip clip = PickClickClip();
        if (clip == null)
        {
            return;
        }

        sfxSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        sfxSource.PlayOneShot(clip, typeSoundVolume * AudioManager.VolumeEfeitos);
    }

    // Sorteia entre os cliques evitando repetir o mesmo duas vezes seguidas
    // (soa mais mecânico/variado, igual a uma máquina de escrever de verdade).
    private AudioClip PickClickClip()
    {
        if (clickClips == null || clickClips.Length == 0)
        {
            return null;
        }

        int index = Random.Range(0, clickClips.Length);
        if (clickClips.Length > 1 && index == lastClickIndex)
        {
            index = (index + 1) % clickClips.Length;
        }
        lastClickIndex = index;

        return clickClips[index];
    }

    private void PlayEndOfLineSound()
    {
        if (sfxSource != null && endOfLineClip != null)
        {
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(endOfLineClip, typeSoundVolume * 0.8f * AudioManager.VolumeEfeitos);
        }
    }

    void Update()
    {
        if (!dialogueActive || Time.frameCount == startFrame)
        {
            return;
        }

        // O Enter que escolhe um botão da pausa não avança a fala por trás.
        if (JogoBloqueado.PausaPorCima)
        {
            return;
        }

        // A tecla do Pular não avança a fala (só Enter e clique, como a caixa
        // mostra): quem chega pulando, no fim do teste do pulo na Física ou
        // logo depois do guarda na IA_Aplicação, perdia a explicação.
        bool submit = InputManager.Instance != null && InputManager.Instance.SubmitAction.WasPressedThisFrame()
            && !ApertouTeclaDoPular();
        if (submit || ClickedToAdvance())
        {
            if (isTyping)
            {
                CompleteLine();
            }
            else
            {
                NextLine();
            }
        }
    }

    // Confere as teclas do Pular em vez de JumpAction.WasPressedThisFrame():
    // segue a tecla escolhida em Controles e funciona mesmo se o mapa Player
    // estiver desligado.
    private static bool ApertouTeclaDoPular()
    {
        InputAction pular = InputManager.Instance != null ? InputManager.Instance.JumpAction : null;
        if (pular == null)
        {
            return false;
        }

        foreach (InputControl control in pular.controls)
        {
            if (control is ButtonControl botao && botao.wasPressedThisFrame)
            {
                return true;
            }
        }
        return false;
    }

    // Clique esquerdo em qualquer lugar avança — menos com o jogo pausado
    // (menu de pausa, Game Over...), em cima de um botão/campo da UI ou num
    // objeto configurável da Interação (o clique abre o painel dele e a fala
    // espera, senão a fala "clique num objeto" sumia atrás do painel).
    private bool ClickedToAdvance()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame || Time.timeScale == 0f)
        {
            return false;
        }

        Vector2 posicao = Mouse.current.position.ReadValue();
        if (EventSystem.current == null)
        {
            return !ClicouEmObjetoConfiguravel(posicao);
        }

        var pointer = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        foreach (RaycastResult hit in hits)
        {
            if (hit.gameObject.GetComponentInParent<Selectable>() != null)
            {
                return false;
            }
        }
        // Em cima da UI o item não recebe o clique (InteractableItem.OnMouseDown), então a fala avança.
        return hits.Count > 0 || !ClicouEmObjetoConfiguravel(posicao);
    }

    // Mesmo alvo do InteractableItem.OnMouseDown: o collider do próprio item.
    private static bool ClicouEmObjetoConfiguravel(Vector2 tela)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return false;
        }

        Vector2 mundo = cam.ScreenToWorldPoint(tela);
        foreach (Collider2D c in Physics2D.OverlapPointAll(mundo))
        {
            if (c != null && c.GetComponent<InteractableItem>() != null)
            {
                return true;
            }
        }
        return false;
    }

    public void NextLine()
    {
        if (lines.Count == 0)
        {
            EndDialogue();
            return;
        }

        PositionBoxAwayFromPlayer();
        StopAllCoroutines();
        StartCoroutine(TypeLine(lines.Dequeue()));
    }

    /// <summary>
    /// Encosta a caixa no canto de baixo do lado oposto ao jogador (ele nasce
    /// embaixo e ficava escondido atrás do diálogo). Se nem assim ele ficar
    /// livre — jogador no meio da tela, perto do chão — a caixa sobe pro
    /// canto de cima do mesmo lado. Só mexe na caixa do tema Laboratório
    /// (TemaLabFases.EstilizarDialogo); a do Inicio antigo fica como está.
    /// </summary>
    private void PositionBoxAwayFromPlayer()
    {
        var caixa = dialoguePanel != null ? dialoguePanel.transform as RectTransform : null;
        if (caixa == null || caixa.Find("TemaLab") == null)
        {
            return;
        }

        Canvas canvas = caixa.GetComponentInParent<Canvas>();
        RectTransform tela = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        if (tela == null)
        {
            return;
        }

        float larguraTela = tela.rect.width;
        float alturaTela = tela.rect.height;
        float alturaCaixa = caixa.sizeDelta.y;

        // Sem jogador na cena: lado esquerdo livre (onde as fases começam).
        bool jogadorAEsquerda = true;
        bool cobreJogador = false;

        if (player == null)
        {
            GameObject go = GameObject.FindWithTag("Player");
            if (go == null)
            {
                go = GameObject.Find("Player");
            }
            player = go != null ? go.transform : null;
        }

        Camera cam = Camera.main;
        if (player != null && cam != null && Screen.width > 0 && Screen.height > 0)
        {
            Vector3 ponto = cam.WorldToScreenPoint(player.position);
            float x = ponto.x / Screen.width * larguraTela;
            float y = ponto.y / Screen.height * alturaTela;

            jogadorAEsquerda = x < larguraTela * 0.5f;
            float distanciaDaBorda = jogadorAEsquerda ? x : larguraTela - x;
            float faixaLivre = larguraTela - larguraCaixa - margemCaixa;
            cobreJogador = distanciaDaBorda + FolgaJogador > faixaLivre
                && y - FolgaJogador < margemCaixa + alturaCaixa;
        }

        CaixaNoTopo = cobreJogador;
        float lado = jogadorAEsquerda ? 1f : 0f;
        float vertical = cobreJogador ? 1f : 0f;
        caixa.anchorMin = caixa.anchorMax = caixa.pivot = new Vector2(lado, vertical);
        caixa.sizeDelta = new Vector2(larguraCaixa, alturaCaixa);
        caixa.anchoredPosition = new Vector2(
            jogadorAEsquerda ? -margemCaixa : margemCaixa,
            cobreJogador ? -(margemCaixa + AlturaPlacaNome) : margemCaixa);
    }

    void EndDialogue()
    {
        dialogueActive = false;
        IsAnyDialogueActive = false;
        CaixaNoTopo = false;
        dialoguePanel.SetActive(false);

        EffectiveListener?.OnDialogueFinished();
    }
}
