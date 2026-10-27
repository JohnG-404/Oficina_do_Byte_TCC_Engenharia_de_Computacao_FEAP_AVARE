using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Puzzle de montar a sequência de animação (cena "Tutorial_Anim", a
/// "Oficina de Animação"): o jogador arrasta os frames soltos
/// (SequencePiece) da bandeja até os encaixes da linha do tempo, em
/// qualquer ordem e podendo trocar de ideia (soltar uma peça nova em cima
/// de um encaixe já ocupado substitui a antiga, que volta pra bandeja). Um
/// clique também funciona: na bandeja manda a peça pro próximo encaixe
/// livre, no encaixe devolve pra bandeja.
///
/// Cada rodada é uma animação com 4 ou 5 frames (ver RoundData): os
/// encaixes que sobram somem e os que ficam se centralizam; as peças da
/// bandeja que sobram também somem. O resto da bandeja vira isca (1 ou 2).
/// Em animações em loop (ex: Corrida), o 1º frame pode vir travado no
/// encaixe 1, porque um ciclo não tem começo óbvio.
///
/// Quando os encaixes estiverem todos preenchidos, toca uma prévia com os
/// sprites juntos na ordem em que foram colocados — mesmo que errada.
/// Depois da prévia, valida (comparando os sprites, então frames iguais
/// podem trocar de lugar sem problema): sequência certa avança pra próxima
/// RODADA; errada mostra um diálogo com dica progressiva e reseta.
///
/// Numa rodada escolhida (ver "ajustes", por padrão a Corrida), depois da
/// ordem certa vem o passo "Ajuste a animação": o jogador escolhe a
/// velocidade (Sample Rate) e o Loop Time, como faria no Animation Clip do
/// Unity (ver AjusteAnimacao e AjusteAnimacaoUI).
///
/// A parte visual (barra de rodadas, rótulos, cor dos encaixes, botões
/// Tocar e Dica) fica no OficinaAnimacaoHUD; este script só avisa ele.
/// </summary>
public class AnimationSequencePuzzle : MonoBehaviour
{
    public static AnimationSequencePuzzle Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    /// <summary>
    /// Uma rodada = uma animação. correctFrames define quantos encaixes a
    /// rodada usa (até o número de "slots"); correctFrames + decoyFrames não
    /// pode passar do número de "trayPieces" (o que sobrar some da bandeja).
    /// Para mais animações, é só acrescentar rodadas aqui.
    /// </summary>
    [Serializable]
    public struct RoundData
    {
        [Tooltip("Nome curto da rodada (aparece na barra do topo e liga o passo de ajuste, ver AjusteAnimacao.roundName).")]
        public string roundName;

        [Tooltip("Nome completo mostrado na linha do tempo (ex: \"Lâmpada acendendo\"). Vazio = usa roundName.")]
        public string titulo;

        [Tooltip("Frames na ordem certa (4 ou 5).")]
        public Sprite[] correctFrames;

        [Tooltip("Iscas: frames que não fazem parte (1 ou 2). Use outro objeto ou outro jeito, nunca um frame da mesma sequência.")]
        public Sprite[] decoyFrames;

        [Tooltip("Animação em loop: o 1º frame já vem preso no encaixe 1.")]
        public bool primeiroFrameTravado;

        [Tooltip("Encaixes (1, 2, 3...) que já vêm preenchidos e travados. Ex: {1, 2, 4} numa rodada de 4 frames = \"falta um frame\", o jogador só acha o 3.")]
        public int[] encaixesFixos;

        [Tooltip("Falas do Prof logo depois de anunciar esta rodada (explicam o conceito novo). Vazio = só o anúncio.")]
        [TextArea(1, 3)]
        public string[] falaInicio;

        [Tooltip("Fala do Prof no botão Dica e na 3ª tentativa errada.")]
        [TextArea(1, 3)]
        public string dica;

        [Tooltip("Quadros por segundo da prévia desta rodada (0 = usa previewFrameDuration).")]
        public int fpsPrevia;

        public string Titulo => string.IsNullOrEmpty(titulo) ? roundName : titulo;

        public bool TemFixos => encaixesFixos != null && encaixesFixos.Length > 0;

        /// <summary>Encaixe (começando em 1) que já vem travado nesta rodada?</summary>
        public bool EncaixeTravado(int posicao)
        {
            return (primeiroFrameTravado && posicao == 1) || (encaixesFixos != null && Array.IndexOf(encaixesFixos, posicao) >= 0);
        }
    }

    /// <summary>
    /// Passo "Evento de animação": depois de uma rodada (e do ajuste dela, se
    /// tiver), o jogador clica no frame em que um som deve tocar, como um
    /// Animation Event do Unity. A prévia toca em loop com o som no frame
    /// marcado, e o Prof diz se acertou.
    /// </summary>
    [Serializable]
    public class EventoAnimacao
    {
        [Tooltip("Nome da rodada (RoundData.roundName) depois da qual este passo aparece.")]
        public string roundName = "Corrida";

        [Tooltip("Frame certo, contando do 1.")]
        public int frameCerto = 1;

        [Tooltip("Texto da marca que aparece embaixo do frame escolhido.")]
        public string rotulo = "passo";

        public AudioManager.Efeito efeito = AudioManager.Efeito.Passo;

        [TextArea(2, 4)]
        public string[] introDialogue =
        {
            "Falta o som! No Unity, um Animation Event chama uma função num frame escolhido do clipe, por exemplo, tocar o som do passo.",
            "Clique no frame em que o pé encosta no chão. A prévia vai tocar o passo nesse frame."
        };

        [TextArea(2, 4)]
        public string[] falasAcerto =
        {
            "Isso! O som bate junto com o pé no chão.",
            "No Unity, o evento fica na linha de cima da janela Animation. É assim que se sincroniza passo, golpe e efeito com a animação."
        };

        [TextArea(2, 4)]
        public string[] falasErro =
        {
            "Ouviu? O passo toca com o pé no ar, fora do tempo.",
            "Procure o frame em que o pé da frente acabou de encostar no chão."
        };

        [TextArea(1, 3)]
        public string textoComoJogar = "<b>Evento de animação</b>\nClique no frame em que o pé toca o chão.";
    }

    [Tooltip("Uma rodada por animação — a ordem aqui é a ordem em que aparecem no tutorial.")]
    [SerializeField] private RoundData[] rounds;

    [Tooltip("As peças fixas da bandeja (sempre as mesmas GameObjects — o que muda a cada rodada é o sprite/posição certa de cada uma).")]
    [SerializeField] private SequencePiece[] trayPieces;

    [Tooltip("Os encaixes da linha do tempo, da esquerda pra direita (posição 1 = slots[0], etc). Rodadas com menos frames usam só os primeiros, recentralizados.")]
    [SerializeField] private Transform[] slots;

    [Tooltip("Distância (unidades de mundo) considerada 'dentro' de um encaixe ao soltar uma peça.")]
    [SerializeField] private float snapRadius = 1.1f;

    [Header("Tamanhos (maior lado do sprite, em unidades de mundo)")]
    [SerializeField] private float tamanhoPecaBandeja = 1.3f;
    [SerializeField] private float tamanhoPecaEncaixe = 1.6f;
    [SerializeField] private float tamanhoPrevia = 2.4f;

    [Header("Prévia (toca quando todos os encaixes estão preenchidos)")]
    [SerializeField] private SpriteRenderer previewRenderer;
    [SerializeField] private float previewFrameDuration = 0.15f;
    [SerializeField] private int previewLoopCount = 2;

    [Header("Visual (opcional)")]
    [SerializeField] private OficinaAnimacaoHUD hud;

    [Tooltip("Casca de cebola: mostra o 1º frame em transparência no encaixe 1 quando o jogador pede Dica.")]
    [SerializeField] private SpriteRenderer fantasma;

    [Header("Diálogos (opcional)")]
    [SerializeField] private DialogueManager dialogueManager;

    [Tooltip("Mostrado uma vez, antes da primeira rodada, explicando o puzzle.")]
    [TextArea(2, 4)]
    [SerializeField]
    private string[] introDialogue =
    {
        "Bem-vindo à Oficina de Animação! Uma animação quadro a quadro é uma sequência de imagens (sprites) trocadas bem rápido. Cada imagem é um frame.",
        "Os frames estão soltos na bandeja. Arraste cada um até a linha do tempo, do encaixe 1 ao último, ou só clique nele.",
        "Com a linha do tempo cheia, a animação toca no monitor. Cuidado: sempre tem frame que não faz parte.",
        "Travou? Clique em Dica."
    };

    [Header("Dica de erro (progressiva: fica mais concreta a cada tentativa errada na mesma rodada)")]
    [TextArea(1, 3)]
    [SerializeField]
    private string[] wrongOrderDialogueStage1 = { "Hmm, não foi dessa vez. Olhe a prévia de novo." };

    [TextArea(1, 3)]
    [SerializeField]
    private string[] wrongOrderDialogueStage2 = { "Quase! Pense no movimento: o que vem primeiro, e o que vem depois?" };

    [Tooltip("Usado na 3ª tentativa em diante, só se a rodada não tiver dica própria.")]
    [TextArea(1, 3)]
    [SerializeField]
    private string[] wrongOrderDialogueStage3 = { "Procure o frame em que nada começou ainda: é por ele que a sequência começa." };

    [Header("Troca de rodada e fechamento")]
    [Tooltip("Prefixo mostrado ao acertar uma rodada, seguido do título da próxima (ver RoundData.Titulo).")]
    [SerializeField] private string nextRoundLinePrefix = "Boa! Próxima animação: ";

    [Tooltip("Nome da cena carregada ao concluir todas as rodadas (precisa estar em Build Settings). Deixe vazio pra não trocar de cena.")]
    [SerializeField] private string nextSceneName = "Animacao";

    [Tooltip("Mostrado uma vez, ao concluir a última rodada, antes de trocar de cena.")]
    [TextArea(2, 4)]
    [SerializeField]
    private string[] finalDialogue =
    {
        "Todas as animações prontas! Guarda elas bem: você vai precisar.",
        "Na próxima fase você vai decidir quando cada uma delas toca."
    };

    [Header("Ajuste de velocidade (FPS) e Loop")]
    [Tooltip("Passo extra depois de montar a ordem certa de uma rodada: o jogador escolhe o Sample Rate (FPS) e o Loop Time do clipe, com a prévia mudando na hora. Uma entrada por rodada que deve ter o ajuste (ligada pelo nome da rodada). Deixe vazio pra não ter o passo.")]
    [SerializeField] private AjusteAnimacao[] ajustes = { new AjusteAnimacao() };

    [Header("Evento de animação (som num frame)")]
    [Tooltip("Passo extra depois de uma rodada (e do ajuste dela): o jogador escolhe em qual frame o som toca. Ligado pelo nome da rodada.")]
    [SerializeField] private EventoAnimacao[] eventos;

    [Tooltip("Marca mostrada embaixo do frame escolhido no passo de evento (objeto da cena, começa desligado).")]
    [SerializeField] private GameObject marcaEvento;

    [Tooltip("Posição da marca em relação ao centro do encaixe.")]
    [SerializeField] private Vector3 deslocamentoMarca = new Vector3(0f, -1.05f, 0f);

    /// <summary>Disparado quando TODAS as rodadas (animações) já foram montadas corretamente.</summary>
    public event Action OnAllRoundsComplete;

    public int CurrentRoundIndex { get; private set; } = -1;

    /// <summary>
    /// True quando o jogador não deve mexer nas peças: painel de ajuste
    /// aberto, prévia de validação tocando, Prof falando (ou acabou de
    /// fechar a fala — o mesmo clique não pode virar clique na peça).
    /// </summary>
    public bool BloqueiaArrasto => ajusteUI != null || validando || solved || DialogueManager.IsAnyDialogueActive || Time.unscaledTime < liberaEm;

    public IReadOnlyList<RoundData> Rounds => rounds;

    /// <summary>No passo de evento, um clique num frame da linha do tempo marca o frame (sem arrastar).</summary>
    public bool AceitaCliqueEvento => eventoAtual != null && !validandoEvento && !DialogueManager.IsAnyDialogueActive && Time.unscaledTime >= liberaEm;

    public bool EmEvento => eventoAtual != null;

    private SequencePiece[] slotOccupants;
    private Vector3[] slotBase;
    private int slotsAtivos;
    private Coroutine previewRoutine;
    private bool solved;
    private bool validando;
    private float liberaEm;
    private int wrongAttemptsThisRound;

    private AjusteAnimacao ajusteAtual;
    private AjusteAnimacaoUI ajusteUI;
    private int errosAjuste;

    private EventoAnimacao eventoAtual;
    private Coroutine eventoRoutine;
    private int frameMarcado = -1;
    private bool validandoEvento;
    private int errosEvento;

    private void Awake()
    {
        Instance = this;
        slotOccupants = new SequencePiece[slots.Length];
        slotBase = new Vector3[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            slotBase[i] = slots[i].position;
        }
        slotsAtivos = slots.Length;
    }

    // Recompilar com o jogo rodando zera os estáticos; o OnEnable roda de novo.
    private void OnEnable()
    {
        Instance = this;
    }

    private void Start()
    {
        if (rounds == null || rounds.Length == 0)
        {
            return;
        }

        AvisarAjustesSemRodada();
        hud?.Preparar(this);

        if (dialogueManager != null && introDialogue.Length > 0)
        {
            dialogueManager.SetListener(new StartRoundOnDialogueEnd(this, 0));
            dialogueManager.StartDialogue(introDialogue);
            StartRound(0); // a tela já mostra a rodada montada; o Prof falando trava as peças
        }
        else
        {
            StartRound(0);
        }
    }

    private void Update()
    {
        if (DialogueManager.IsAnyDialogueActive)
        {
            liberaEm = Time.unscaledTime + 0.2f;
        }
    }

    /// <summary>
    /// Prepara a bandeja pra rodada N: ajusta quantos encaixes a rodada
    /// usa, embaralha os frames certos + iscas entre as peças da bandeja e
    /// limpa os encaixes. Chamado sozinho ao iniciar e após cada acerto.
    /// </summary>
    public void StartRound(int index)
    {
        if (rounds == null || index >= rounds.Length)
        {
            OnAllRoundsComplete?.Invoke();
            return;
        }

        ResetPuzzle(true);

        CurrentRoundIndex = index;
        solved = false;
        validando = false;
        wrongAttemptsThisRound = 0;
        RoundData round = rounds[index];

        int frames = Mathf.Clamp(round.correctFrames != null ? round.correctFrames.Length : 0, 1, slots.Length);
        ArrumarEncaixes(frames);

        var pool = new List<(Sprite sprite, int position)>();
        for (int i = 0; i < frames; i++)
        {
            pool.Add((round.correctFrames[i], i + 1));
        }
        if (round.decoyFrames != null)
        {
            foreach (Sprite decoy in round.decoyFrames)
            {
                if (decoy != null)
                {
                    pool.Add((decoy, 0));
                }
            }
        }

        if (pool.Count > trayPieces.Length)
        {
            Debug.LogWarning($"[AnimationSequencePuzzle] Rodada \"{round.roundName}\" tem {pool.Count} frames + iscas, mas a bandeja só tem {trayPieces.Length} peças. As últimas ficam de fora.");
        }

        Shuffle(pool);

        for (int i = 0; i < trayPieces.Length; i++)
        {
            bool usada = i < pool.Count;
            trayPieces[i].gameObject.SetActive(usada);
            if (usada)
            {
                trayPieces[i].Configure(pool[i].sprite, pool[i].position, tamanhoPecaBandeja);
            }
        }

        // Frames que já vêm no lugar: o 1º de um loop e/ou os encaixes fixos
        // ("falta um frame").
        for (int pos = 1; pos <= frames; pos++)
        {
            if (!round.EncaixeTravado(pos))
            {
                continue;
            }

            foreach (SequencePiece piece in trayPieces)
            {
                if (piece.gameObject.activeSelf && piece.CorrectPosition == pos && !piece.Travada)
                {
                    slotOccupants[pos - 1] = piece;
                    piece.SnapTo(slots[pos - 1].position, tamanhoPecaEncaixe);
                    piece.Travada = true;
                    break;
                }
            }
        }

        if (fantasma != null)
        {
            fantasma.enabled = false;
        }

        SetPreviewSprite(null);
        hud?.IniciarRodada(index, round, frames, pool.Count - frames, FpsDaRodada(round));
        AtualizarEncaixes();
    }

    // Liga só os encaixes que a rodada usa e centraliza eles na linha do tempo.
    private void ArrumarEncaixes(int quantos)
    {
        slotsAtivos = quantos;

        float centro = 0f;
        foreach (Vector3 p in slotBase)
        {
            centro += p.x;
        }
        centro /= Mathf.Max(1, slotBase.Length);
        float passo = slotBase.Length > 1 ? slotBase[1].x - slotBase[0].x : 0f;

        for (int i = 0; i < slots.Length; i++)
        {
            bool ativo = i < quantos;
            slots[i].gameObject.SetActive(ativo);
            if (ativo)
            {
                Vector3 p = slotBase[i];
                p.x = centro + (i - (quantos - 1) * 0.5f) * passo;
                slots[i].position = p;
            }
        }
    }

    private float FpsDaRodada(RoundData round)
    {
        return round.fpsPrevia > 0 ? round.fpsPrevia : 1f / Mathf.Max(0.01f, previewFrameDuration);
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ---------------------------------------------------------------------
    // Peças: arrastar, soltar e clicar
    // ---------------------------------------------------------------------

    /// <summary>Chamado pela SequencePiece a cada frame de arrasto: acende o encaixe que vai receber a peça.</summary>
    public void NotifyDragging(SequencePiece piece)
    {
        AtualizarEncaixes(FindSlotUnderPiece(piece));
    }

    /// <summary>Chamado pela SequencePiece quando o jogador solta o mouse depois de arrastá-la.</summary>
    public void NotifyDropped(SequencePiece piece)
    {
        if (BloqueiaArrasto)
        {
            piece.ReturnHome();
            AtualizarEncaixes();
            return;
        }

        LiberarEncaixeDa(piece);

        int targetSlot = FindSlotUnderPiece(piece);
        if (targetSlot < 0)
        {
            piece.ReturnHome();
            AtualizarEncaixes();
            return;
        }

        Colocar(piece, targetSlot);
    }

    /// <summary>
    /// Clique sem arrastar: peça num encaixe volta pra bandeja; peça na
    /// bandeja vai pro próximo encaixe livre.
    /// </summary>
    public void NotifyClicked(SequencePiece piece)
    {
        if (EmEvento)
        {
            MarcarFrameEvento(piece);
            return;
        }

        if (BloqueiaArrasto)
        {
            return;
        }

        if (LiberarEncaixeDa(piece))
        {
            piece.ReturnHome();
            AtualizarEncaixes();
            return;
        }

        for (int i = 0; i < slotsAtivos; i++)
        {
            if (slotOccupants[i] == null)
            {
                Colocar(piece, i);
                return;
            }
        }
    }

    // Libera qualquer encaixe que essa peça já ocupava — ela pode estar
    // sendo movida de um encaixe pra outro, ou de volta pra bandeja.
    private bool LiberarEncaixeDa(SequencePiece piece)
    {
        bool estava = false;
        for (int i = 0; i < slotOccupants.Length; i++)
        {
            if (slotOccupants[i] == piece)
            {
                slotOccupants[i] = null;
                estava = true;
            }
        }
        return estava;
    }

    private void Colocar(SequencePiece piece, int slot)
    {
        SequencePiece anterior = slotOccupants[slot];
        if (anterior != null && anterior != piece)
        {
            if (anterior.Travada)
            {
                piece.ReturnHome(); // encaixe 1 travado: não troca
                AtualizarEncaixes();
                return;
            }
            anterior.ReturnHome();
        }

        slotOccupants[slot] = piece;
        piece.SnapTo(slots[slot].position, tamanhoPecaEncaixe);

        if (fantasma != null && slot == 0)
        {
            fantasma.enabled = false;
        }

        AtualizarEncaixes();
        CheckIfComplete();
    }

    private int FindSlotUnderPiece(SequencePiece piece)
    {
        int closest = -1;
        float closestDistance = snapRadius;

        for (int i = 0; i < slotsAtivos; i++)
        {
            float distance = Vector2.Distance(piece.transform.position, slots[i].position);
            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closest = i;
            }
        }

        return closest;
    }

    private void AtualizarEncaixes(int destaque = -1, bool? resultado = null)
    {
        if (hud == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            OficinaAnimacaoHUD.EstadoEncaixe estado;
            if (resultado.HasValue && i < slotsAtivos)
            {
                estado = resultado.Value ? OficinaAnimacaoHUD.EstadoEncaixe.Certo : OficinaAnimacaoHUD.EstadoEncaixe.Errado;
            }
            else if (i == destaque)
            {
                estado = OficinaAnimacaoHUD.EstadoEncaixe.Destaque;
            }
            else if (slotOccupants[i] != null && slotOccupants[i].Travada)
            {
                estado = OficinaAnimacaoHUD.EstadoEncaixe.Travado;
            }
            else
            {
                estado = slotOccupants[i] != null ? OficinaAnimacaoHUD.EstadoEncaixe.Cheio : OficinaAnimacaoHUD.EstadoEncaixe.Vazio;
            }
            hud.PintarEncaixe(i, estado);
        }
    }

    private void CheckIfComplete()
    {
        for (int i = 0; i < slotsAtivos; i++)
        {
            if (slotOccupants[i] == null)
            {
                return; // ainda falta preencher algum encaixe
            }
        }

        if (previewRoutine != null)
        {
            StopCoroutine(previewRoutine);
        }
        validando = true;
        previewRoutine = StartCoroutine(PlayPreviewThenValidate());
    }

    // ---------------------------------------------------------------------
    // Botões do HUD: Tocar e Dica
    // ---------------------------------------------------------------------

    /// <summary>Botão Tocar: toca o que já está na linha do tempo, sem validar.
    /// No passo de evento a prévia já toca sozinha, então o botão fica apagado.</summary>
    public void TocarPrevia()
    {
        if (BloqueiaArrasto || EmEvento)
        {
            return;
        }

        var frames = new List<Sprite>();
        for (int i = 0; i < slotsAtivos; i++)
        {
            if (slotOccupants[i] != null)
            {
                frames.Add(slotOccupants[i].Sprite);
            }
        }

        if (frames.Count == 0)
        {
            Falar(new[] { "Coloque pelo menos um frame na linha do tempo pra tocar a prévia." }, null);
            return;
        }

        if (previewRoutine != null)
        {
            StopCoroutine(previewRoutine);
        }
        previewRoutine = StartCoroutine(TocarFrames(frames, previewLoopCount));
    }

    /// <summary>Botão Dica: mostra o 1º frame em transparência no encaixe 1 e o Prof dá a dica da rodada.
    /// No passo de evento, a dica é a do evento (a última fala de erro).</summary>
    public void MostrarDica()
    {
        if (EmEvento)
        {
            if (AceitaCliqueEvento && eventoAtual.falasErro != null && eventoAtual.falasErro.Length > 0)
            {
                RegistroAprendizagem.Registrar("anim_dica", eventoAtual.roundName + " | evento");
                Falar(new[] { eventoAtual.falasErro[eventoAtual.falasErro.Length - 1] }, null);
            }
            return;
        }

        if (BloqueiaArrasto || CurrentRoundIndex < 0)
        {
            return;
        }

        RoundData round = rounds[CurrentRoundIndex];
        RegistroAprendizagem.Registrar("anim_dica", round.roundName);

        if (fantasma != null && slotOccupants[0] == null && round.correctFrames.Length > 0)
        {
            fantasma.sprite = round.correctFrames[0];
            fantasma.transform.position = slots[0].position + Vector3.back * 0.01f;
            Encaixar(fantasma, tamanhoPecaEncaixe);
            fantasma.enabled = true;
        }

        string dica = string.IsNullOrEmpty(round.dica) ? "Comece pelo frame em que nada aconteceu ainda." : round.dica;
        Falar(new[] { dica }, null);
    }

    private IEnumerator TocarFrames(List<Sprite> frames, int loops)
    {
        float passo = 1f / FpsDaRodada(rounds[CurrentRoundIndex]);
        hud?.EstadoPrevia(true);
        for (int loop = 0; loop < loops; loop++)
        {
            foreach (Sprite sprite in frames)
            {
                SetPreviewSprite(sprite);
                yield return new WaitForSeconds(passo);
            }
        }
        hud?.EstadoPrevia(false);
    }

    // Toca a prévia com os sprites na ordem em que o jogador colocou —
    // mesmo que a ordem/peças estejam erradas, é assim que o jogador vê o
    // resultado antes de saber se acertou.
    private IEnumerator PlayPreviewThenValidate()
    {
        var frames = new List<Sprite>();
        for (int i = 0; i < slotsAtivos; i++)
        {
            frames.Add(slotOccupants[i].Sprite);
        }

        yield return TocarFrames(frames, previewLoopCount);
        Validate();
    }

    private void Validate()
    {
        RoundData round = rounds[CurrentRoundIndex];
        bool correct = true;
        for (int i = 0; i < slotsAtivos; i++)
        {
            // Compara o sprite, e não a peça: se a animação repete um frame,
            // qualquer uma das cópias serve naquele lugar.
            if (slotOccupants[i] == null || slotOccupants[i].Sprite != round.correctFrames[i])
            {
                correct = false;
                break;
            }
        }

        AtualizarEncaixes(-1, correct);

        if (correct)
        {
            RegistroAprendizagem.Registrar("sequencia_concluida", $"{round.roundName} | {wrongAttemptsThisRound} erros");
            hud?.MarcarConcluida(CurrentRoundIndex);
            Advance();
        }
        else
        {
            RegistroAprendizagem.RegistrarErro("sequencia_errada", $"{round.roundName} | tentativa {wrongAttemptsThisRound + 1}");
            ShowWrongDialogueThenReset();
        }
    }

    // Sequência certa: trava novas jogadas nesta rodada. Se ainda faltam
    // rodadas, avisa qual é a próxima e passa pra ela; se essa era a última,
    // fecha o tutorial (ver FinishAllRounds).
    private void Advance()
    {
        solved = true;
        validando = false;
        string finishedRound = rounds[CurrentRoundIndex].roundName;

        // Rodada com passo de ajuste (FPS/Loop): só segue depois dele.
        AjusteAnimacao ajuste = FindAjuste(finishedRound);
        if (ajuste != null)
        {
            StartAjuste(ajuste);
            return;
        }

        ContinueOrEvento();
    }

    // Depois da rodada (e do ajuste, se teve): passo de evento, se a rodada
    // tiver um; senão segue pra próxima.
    private void ContinueOrEvento()
    {
        EventoAnimacao evento = FindEvento(rounds[CurrentRoundIndex].roundName);
        if (evento != null)
        {
            StartEvento(evento);
            return;
        }

        ContinueAfterRound();
    }

    // O que acontece depois de uma rodada terminar de vez (com ou sem ajuste):
    // avisa qual é a próxima rodada ou fecha o tutorial.
    private void ContinueAfterRound()
    {
        int next = CurrentRoundIndex + 1;
        if (rounds == null || next >= rounds.Length)
        {
            FinishAllRounds();
            return;
        }

        if (dialogueManager != null)
        {
            var falas = new List<string> { nextRoundLinePrefix + rounds[next].Titulo + "." };
            if (rounds[next].falaInicio != null)
            {
                foreach (string fala in rounds[next].falaInicio)
                {
                    if (!string.IsNullOrEmpty(fala))
                    {
                        falas.Add(fala);
                    }
                }
            }
            dialogueManager.SetListener(new StartRoundOnDialogueEnd(this, next));
            dialogueManager.StartDialogue(falas.ToArray());
        }
        else
        {
            StartRound(next);
        }
    }

    // ---------------------------------------------------------------------
    // Passo "Ajuste a animação" (Sample Rate + Loop Time)
    // ---------------------------------------------------------------------

    private AjusteAnimacao FindAjuste(string roundName)
    {
        if (ajustes == null)
        {
            return null;
        }

        foreach (AjusteAnimacao ajuste in ajustes)
        {
            if (ajuste != null && ajuste.roundName == roundName)
            {
                return ajuste;
            }
        }

        return null;
    }

    // Nome digitado errado no Inspector faria o passo sumir sem aviso.
    private void AvisarAjustesSemRodada()
    {
        if (ajustes == null)
        {
            return;
        }

        foreach (AjusteAnimacao ajuste in ajustes)
        {
            if (ajuste == null)
            {
                continue;
            }

            bool achou = false;
            foreach (RoundData round in rounds)
            {
                if (round.roundName == ajuste.roundName)
                {
                    achou = true;
                    break;
                }
            }

            if (!achou)
            {
                Debug.LogWarning($"[AnimationSequencePuzzle] Ajuste de FPS/Loop ligado à rodada \"{ajuste.roundName}\", mas não existe rodada com esse nome. O passo não vai aparecer.");
            }
            else if (ajuste.fpsOpcoes == null || Array.IndexOf(ajuste.fpsOpcoes, ajuste.fpsCerto) < 0)
            {
                Debug.LogWarning($"[AnimationSequencePuzzle] Ajuste da rodada \"{ajuste.roundName}\": fpsCerto ({ajuste.fpsCerto}) não está entre as opções de botão. Ninguém vai conseguir acertar.");
            }
        }
    }

    // Abre o painel com os quadros que o jogador acabou de montar (a mesma
    // animação da prévia) e o Prof explica o que ajustar. O painel já aparece
    // durante a fala, com os botões travados até ela acabar.
    private void StartAjuste(AjusteAnimacao ajuste)
    {
        ajusteAtual = ajuste;
        errosAjuste = 0;

        RegistroAprendizagem.Registrar("anim_ajuste_inicio", ajuste.roundName);

        Sprite[] quadros = CollectPlacedFrames();
        if (ajuste.inverterQuadros)
        {
            Array.Reverse(quadros); // ex: Porta fechando mostra o clipe da porta abrindo, pra tocar com Speed -1
        }
        ajusteUI = AjusteAnimacaoUI.Show(ajuste, quadros, SetPreviewSprite, OnAjusteConfirmado);

        // Troca o listener mesmo sem fala: senão o listener anterior (ex:
        // StartRoundOnDialogueEnd) dispararia de novo no fim do próximo diálogo.
        if (dialogueManager != null)
        {
            dialogueManager.SetListener(new ActionOnDialogueEnd(null));
        }
        Falar(ajuste.introDialogue, null);
    }

    // Quadros na ordem em que estão nos encaixes (a ordem certa, já que a
    // rodada foi validada). Se algo faltar, usa os frames certos da rodada.
    private Sprite[] CollectPlacedFrames()
    {
        var frames = new List<Sprite>();
        for (int i = 0; i < slotsAtivos; i++)
        {
            Sprite sprite = slotOccupants[i] != null ? slotOccupants[i].Sprite : null;
            if (sprite == null)
            {
                frames.Clear();
                break;
            }
            frames.Add(sprite);
        }

        if (frames.Count == 0 && CurrentRoundIndex >= 0 && rounds[CurrentRoundIndex].correctFrames != null)
        {
            frames.AddRange(rounds[CurrentRoundIndex].correctFrames);
        }

        return frames.ToArray();
    }

    // A prévia do painel também toca no monitor do mundo.
    private void SetPreviewSprite(Sprite sprite)
    {
        if (previewRenderer == null)
        {
            return;
        }

        previewRenderer.sprite = sprite;
        if (sprite != null)
        {
            Encaixar(previewRenderer, tamanhoPrevia);
        }
    }

    // Escala um SpriteRenderer pra que o maior lado do sprite caiba em "tamanho".
    private static void Encaixar(SpriteRenderer renderer, float tamanho)
    {
        Vector2 size = renderer.sprite.bounds.size;
        float maior = Mathf.Max(size.x, size.y);
        float escala = maior > 0.0001f ? tamanho / maior : 1f;
        Transform pai = renderer.transform.parent;
        float escalaPai = pai != null ? pai.lossyScale.x : 1f;
        escala /= Mathf.Max(0.0001f, escalaPai);
        renderer.transform.localScale = new Vector3(escala, escala, 1f);
    }

    // Clique em Confirmar: erro não reseta nada (o jogador só ajusta de novo
    // depois de ouvir o porquê); acerto trava o painel, o Prof fecha o
    // assunto e o tutorial segue.
    private void OnAjusteConfirmado(int fps, bool loop, int speed)
    {
        if (ajusteAtual == null || ajusteUI == null)
        {
            return;
        }

        AjusteAnimacao.Resultado resultado = ajusteAtual.Avaliar(fps, loop, speed, errosAjuste, out string[] falas);
        string detalhe = $"{ajusteAtual.roundName} | fps={fps} | loop={(loop ? "sim" : "nao")}" + (ajusteAtual.usarSpeed ? $" | speed={speed}" : "");

        if (resultado == AjusteAnimacao.Resultado.Errado)
        {
            errosAjuste++;
            RegistroAprendizagem.RegistrarErro("anim_ajuste", detalhe + " | errado");

            if (dialogueManager != null && falas.Length > 0)
            {
                Falar(falas, null);
            }
            else
            {
                ajusteUI.MostrarRetorno(string.Join(" ", falas));
            }
            return;
        }

        string rotulo = resultado == AjusteAnimacao.Resultado.Certo ? "certo" : "certo_com_observacao";
        RegistroAprendizagem.Registrar("anim_ajuste", $"{detalhe} | {rotulo}");

        ajusteUI.MarcarConcluido();
        Falar(falas, FinishAjuste);
    }

    private void FinishAjuste()
    {
        if (ajusteUI != null)
        {
            ajusteUI.Fechar();
        }

        ajusteUI = null;
        ajusteAtual = null;
        ContinueOrEvento();
    }

    // ---------------------------------------------------------------------
    // Passo "Evento de animação" (som num frame)
    // ---------------------------------------------------------------------

    private EventoAnimacao FindEvento(string roundName)
    {
        if (eventos == null)
        {
            return null;
        }

        foreach (EventoAnimacao evento in eventos)
        {
            if (evento != null && evento.roundName == roundName)
            {
                return evento;
            }
        }

        return null;
    }

    // A linha do tempo continua montada; a prévia toca em loop e o jogador
    // clica no frame onde o som deve tocar.
    private void StartEvento(EventoAnimacao evento)
    {
        eventoAtual = evento;
        frameMarcado = -1;
        errosEvento = 0;
        validandoEvento = false;
        if (marcaEvento != null)
        {
            marcaEvento.SetActive(false);
        }

        RegistroAprendizagem.Registrar("anim_evento_inicio", evento.roundName);
        hud?.DefinirComoJogar(evento.textoComoJogar);
        DefinirBotaoTocar(false);
        AtualizarEncaixes();

        if (eventoRoutine != null)
        {
            StopCoroutine(eventoRoutine);
        }
        eventoRoutine = StartCoroutine(LoopEvento());

        if (dialogueManager != null)
        {
            dialogueManager.SetListener(new ActionOnDialogueEnd(null));
        }
        Falar(evento.introDialogue, null);
    }

    // Prévia sem fim do passo de evento: toca o som sempre que passa pelo
    // frame marcado, assim o jogador ouve se o passo bate com o pé.
    private IEnumerator LoopEvento()
    {
        float passo = 1f / FpsDaRodada(rounds[CurrentRoundIndex]);
        hud?.EstadoPrevia(true);
        while (eventoAtual != null)
        {
            for (int i = 0; i < slotsAtivos && eventoAtual != null; i++)
            {
                SetPreviewSprite(slotOccupants[i] != null ? slotOccupants[i].Sprite : null);
                if (i == frameMarcado)
                {
                    AudioManager.TocarEfeito(eventoAtual.efeito);
                }
                yield return new WaitForSeconds(passo);
            }
        }
    }

    private void MarcarFrameEvento(SequencePiece piece)
    {
        if (!AceitaCliqueEvento)
        {
            return;
        }

        int indice = Array.IndexOf(slotOccupants, piece);
        if (indice < 0 || indice >= slotsAtivos)
        {
            return; // só frames da linha do tempo
        }

        frameMarcado = indice;
        if (marcaEvento != null)
        {
            marcaEvento.transform.position = slots[indice].position + deslocamentoMarca;
            marcaEvento.SetActive(true);
        }

        for (int i = 0; i < slotsAtivos; i++)
        {
            hud?.PintarEncaixe(i, i == indice ? OficinaAnimacaoHUD.EstadoEncaixe.Destaque : OficinaAnimacaoHUD.EstadoEncaixe.Cheio);
        }

        StartCoroutine(OuvirEValidarEvento());
    }

    // Deixa o jogador ouvir duas voltas com o som no frame escolhido antes
    // de o Prof responder.
    private IEnumerator OuvirEValidarEvento()
    {
        validandoEvento = true;
        float passo = 1f / FpsDaRodada(rounds[CurrentRoundIndex]);
        yield return new WaitForSeconds(passo * slotsAtivos * 2f);

        EventoAnimacao evento = eventoAtual;
        if (evento == null)
        {
            validandoEvento = false;
            yield break;
        }

        bool certo = frameMarcado == evento.frameCerto - 1;
        string detalhe = $"{evento.roundName} | frame {frameMarcado + 1}";
        AtualizarEncaixes(-1, certo);

        if (certo)
        {
            RegistroAprendizagem.Registrar("anim_evento", detalhe + " | certo");
            Falar(evento.falasAcerto, FinishEvento);
        }
        else
        {
            errosEvento++;
            RegistroAprendizagem.RegistrarErro("anim_evento", detalhe + " | errado");
            Falar(evento.falasErro, () =>
            {
                validandoEvento = false;
                AtualizarEncaixes();
            });
        }
    }

    private void FinishEvento()
    {
        eventoAtual = null;
        validandoEvento = false;
        frameMarcado = -1;
        if (eventoRoutine != null)
        {
            StopCoroutine(eventoRoutine);
            eventoRoutine = null;
        }
        if (marcaEvento != null)
        {
            marcaEvento.SetActive(false);
        }
        hud?.EstadoPrevia(false);
        DefinirBotaoTocar(true);
        ContinueAfterRound();
    }

    private BotaoMundo botaoTocar;

    // O botão Tocar é um BotaoMundo da cena (montado pelo OficinaAnimacaoBuilder).
    private void DefinirBotaoTocar(bool ligado)
    {
        if (botaoTocar == null)
        {
            foreach (BotaoMundo b in FindObjectsByType<BotaoMundo>(FindObjectsInactive.Include))
            {
                if (b.name == "BotaoTocar")
                {
                    botaoTocar = b;
                    break;
                }
            }
        }
        if (botaoTocar != null)
        {
            botaoTocar.Interativo = ligado;
        }
    }

    // Mostra falas do Prof e chama aoTerminar quando o jogador terminar de
    // ler. Sem DialogueManager (ou sem falas), chama direto.
    private void Falar(string[] falas, Action aoTerminar)
    {
        if (dialogueManager != null && falas != null && falas.Length > 0)
        {
            dialogueManager.SetListener(new ActionOnDialogueEnd(aoTerminar));
            dialogueManager.StartDialogue(falas);
        }
        else
        {
            aoTerminar?.Invoke();
        }
    }

    // Todas as animações foram montadas: avisa quem estiver ouvindo o evento
    // (compatibilidade) e mostra o fechamento antes de trocar de cena.
    private void FinishAllRounds()
    {
        RegistroAprendizagem.Registrar("fase_concluida");
        OnAllRoundsComplete?.Invoke();

        if (dialogueManager != null && finalDialogue != null && finalDialogue.Length > 0)
        {
            dialogueManager.SetListener(new ActionOnDialogueEnd(MostrarFimDeFase));
            dialogueManager.StartDialogue(finalDialogue);
        }
        else
        {
            MostrarFimDeFase();
        }
    }

    // Painel "Fase Concluída!" (cartão com o resumo); a próxima cena só carrega com Enter/clique.
    private void MostrarFimDeFase()
    {
        LevelCompleteUI painel = FindAnyObjectByType<LevelCompleteUI>(FindObjectsInactive.Include);
        if (painel == null)
        {
            CarregarProximaCena();
            return;
        }

        var itens = new List<LevelCompleteUI.Item>
        {
            new LevelCompleteUI.Item("Ordem dos quadros", (rounds != null ? rounds.Length : 0) + " animações montadas, quadro a quadro"),
        };
        if (ajustes != null && ajustes.Length > 0)
        {
            itens.Add(new LevelCompleteUI.Item("Sample Rate e Loop", "a velocidade (FPS) e a repetição do clipe"));
        }
        if (eventos != null && eventos.Length > 0)
        {
            itens.Add(new LevelCompleteUI.Item("Animation Event", "o som do passo no quadro certo"));
        }
        painel.Mostrar(itens, true, CarregarProximaCena);
    }

    private void CarregarProximaCena()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            TrocaDeCena.Carregar(nextSceneName);
        }
    }

    // Mostra uma dica de erro cada vez mais concreta a cada tentativa errada
    // na MESMA rodada (reseta ao acertar ou trocar de rodada — ver StartRound).
    // Da 3ª em diante usa a dica da própria rodada, se tiver.
    private void ShowWrongDialogueThenReset()
    {
        string dicaRodada = rounds[CurrentRoundIndex].dica;
        string[] lines = wrongAttemptsThisRound == 0 ? wrongOrderDialogueStage1
            : wrongAttemptsThisRound == 1 ? wrongOrderDialogueStage2
            : !string.IsNullOrEmpty(dicaRodada) ? new[] { dicaRodada }
            : wrongOrderDialogueStage3;
        wrongAttemptsThisRound++;

        if (dialogueManager != null && lines != null && lines.Length > 0)
        {
            dialogueManager.SetListener(new ResetOnDialogueEnd(this));
            dialogueManager.StartDialogue(lines);
        }
        else
        {
            ResetPuzzle();
        }
    }

    /// <summary>Devolve as peças pra bandeja. O frame travado (loop) continua no encaixe 1, a não ser que <paramref name="tudo"/>.</summary>
    public void ResetPuzzle() => ResetPuzzle(false);

    private void ResetPuzzle(bool tudo)
    {
        validando = false;
        for (int i = 0; i < slotOccupants.Length; i++)
        {
            SequencePiece piece = slotOccupants[i];
            if (piece == null || (piece.Travada && !tudo))
            {
                continue;
            }

            piece.Travada = false;
            piece.ReturnHome();
            slotOccupants[i] = null;
        }

        SetPreviewSprite(null);
        AtualizarEncaixes();
    }

    // Adaptador pra IDialogueListener: o reset só deve acontecer depois que
    // o jogador terminar de ler o diálogo de erro, não antes.
    private class ResetOnDialogueEnd : IDialogueListener
    {
        private readonly AnimationSequencePuzzle owner;

        public ResetOnDialogueEnd(AnimationSequencePuzzle owner)
        {
            this.owner = owner;
        }

        public void OnDialogueFinished()
        {
            owner.ResetPuzzle();
        }
    }

    // Mesma ideia, pro diálogo de intro e pra troca de rodada: só libera a
    // rodada indicada depois que o jogador terminar de ler o texto, não durante.
    private class StartRoundOnDialogueEnd : IDialogueListener
    {
        private readonly AnimationSequencePuzzle owner;
        private readonly int roundIndex;

        public StartRoundOnDialogueEnd(AnimationSequencePuzzle owner, int roundIndex)
        {
            this.owner = owner;
            this.roundIndex = roundIndex;
        }

        public void OnDialogueFinished()
        {
            // A intro já mostrou a rodada 0 montada; não embaralha de novo.
            if (owner.CurrentRoundIndex != roundIndex || owner.solved)
            {
                owner.StartRound(roundIndex);
            }
        }
    }

    // Adaptador genérico pra IDialogueListener (usado pelo passo de ajuste).
    // Com ação nula funciona como "ninguém ouvindo", o que também impede o
    // listener anterior de disparar de novo.
    private class ActionOnDialogueEnd : IDialogueListener
    {
        private readonly Action action;

        public ActionOnDialogueEnd(Action action)
        {
            this.action = action;
        }

        public void OnDialogueFinished()
        {
            action?.Invoke();
        }
    }

    // Adaptador pra IDialogueListener: troca de cena só depois que o jogador
    // terminar de ler o fechamento do tutorial, não durante.
    private class LoadSceneOnDialogueEnd : IDialogueListener
    {
        private readonly string sceneName;

        public LoadSceneOnDialogueEnd(string sceneName)
        {
            this.sceneName = sceneName;
        }

        public void OnDialogueFinished()
        {
            if (!string.IsNullOrEmpty(sceneName))
            {
                TrocaDeCena.Carregar(sceneName);
            }
        }
    }
}
