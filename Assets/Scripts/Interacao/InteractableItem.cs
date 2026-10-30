using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Objeto interativo da cena "Interacao": o jogador clica nele pra abrir o
/// assistente de configuração (ver InteractionConfigUI), escolhe uma Ação
/// (como interagir) e uma Consequência (o que acontece) — só as opções
/// listadas em AllowedActions/AllowedConsequences aparecem, pra manter cada
/// item fazendo sentido (ex: uma porta só oferece "Abrir uma porta" como
/// consequência).
///
/// Depois de configurado, o próprio item passa a vigiar a Ação escolhida
/// (zona de proximidade, zona de "em cima", tecla de Interagir ou o drop de
/// um item-chave arrastado) e dispara a Consequência escolhida, uma vez só
/// ou sempre, conforme o "Repetir" escolhido no assistente (ChosenRepeats).
///
/// Só o jogador ativa: as zonas e a colisão checam a tag "Player"
/// (CompareTag), então inimigos, caixas e outros objetos com física passam
/// sem disparar nada (ver IsPlayer).
/// </summary>
public class InteractableItem : MonoBehaviour
{
    [Header("Identificação (mostrada no assistente de configuração)")]
    [SerializeField] private string displayName = "Item";
    [TextArea(1, 3)]
    [SerializeField] private string description = "";

    [Header("Opções válidas pra este item")]
    [SerializeField] private ActionType[] allowedActions = { ActionType.PressButton, ActionType.WalkNear };
    [SerializeField] private ConsequenceType[] allowedConsequences = { ConsequenceType.HealPlayer };

    [Header("Só usado se a Ação escolhida for 'Precisa de outro item'")]
    [SerializeField] private InteractableItem requiredItem;

    [Header("Só usado se a Consequência escolhida for 'Lançar o jogador'")]
    [SerializeField] private Vector2 launchVelocity = new Vector2(0f, 16f);

    [Header("Só usado se a Consequência escolhida for 'Abrir uma porta'")]
    [SerializeField] private Door doorToOpen;

    [Header("Só usado se a Consequência escolhida for 'Repelir o jogador'")]
    [Tooltip("Velocidade horizontal do empurrão.")]
    [SerializeField] private float repelForce = 12f;
    [Tooltip("Pulinho vertical do empurrão — baixo, pra o jogador continuar rente ao chão.")]
    [SerializeField] private float repelHop = 4f;
    [Tooltip("Por quanto tempo o controle do jogador fica suspenso durante o empurrão.")]
    [SerializeField] private float repelDuration = 0.18f;

    [Header("Só usado se a Consequência escolhida for 'Lançar para o lado'")]
    [SerializeField] private Vector2 lateralLaunchVelocity = new Vector2(9f, 9f);
    [Tooltip("Por quanto tempo o controle fica suspenso durante o lançamento lateral (senão o movimento anula o X).")]
    [SerializeField] private float lateralLaunchDuration = 0.45f;

    [Header("Tempos das Consequências novas (segundos)")]
    [SerializeField] private float laserOffSeconds = 3f;
    [SerializeField] private float doorOpenSeconds = 5f;
    [Tooltip("Lâmpada: 'Acender por 5 segundos'.")]
    [SerializeField] private float lampOnSeconds = 5f;
    [Tooltip("Escudo, lentidão e controles invertidos.")]
    [SerializeField] private float effectSeconds = 3f;
    [Tooltip("Em 'Sempre', quanto tempo os espinhos ficam afundados antes de subir de novo.")]
    [SerializeField] private float sinkSeconds = 3f;
    [Tooltip("Ação 'Ficar em cima 1 segundo'.")]
    [SerializeField] private float stayOnTopSeconds = 1f;

    [Header("Zonas de detecção (criadas sozinhas se deixar vazio)")]
    [SerializeField] private float proximityRadius = 1.6f;
    [SerializeField] private float topZoneHeight = 0.4f;

    public string DisplayName => displayName;
    public string Description => description;
    public IReadOnlyList<ActionType> AllowedActions => allowedActions;
    public IReadOnlyList<ConsequenceType> AllowedConsequences => allowedConsequences;

    public bool IsConfigured { get; private set; }
    public bool HasTriggered { get; private set; }
    public ActionType ChosenAction { get; private set; }
    public ConsequenceType ChosenConsequence { get; private set; }

    /// <summary>
    /// Escolha "Repetir" do assistente: true = "Sempre" (dispara a cada nova
    /// ativação), false = "Uma vez". Público pra InteracaoLevelFlow registrar
    /// no RegistroAprendizagem junto com a Ação/Consequência.
    /// </summary>
    public bool ChosenRepeats { get; private set; }

    /// <summary>
    /// Já foi ativado ao menos uma vez (mesmo se configurado pra repetir).
    /// É o "outroItem.ativado" da Ação "Precisar de outro item". HasTriggered
    /// não serve pra isso, porque um item em "Sempre" nunca o liga.
    /// </summary>
    public bool WasActivated { get; private set; }

    /// <summary>Única tag que ativa itens (ver IsPlayer).</summary>
    public const string PlayerTag = "Player";

    private bool playerInRange;

    // Ações por cartão com "Sempre": a checagem por frame em Update só pode
    // disparar uma vez por visita (senão dispararia todo frame com o jogador
    // parado perto). Zera quando o jogador sai da zona de proximidade.
    private bool triggeredDuringStay;

    private static bool warnedMissingTag;

    // Ação "Ficar em cima 1 segundo".
    private bool playerOnTop;
    private float onTopSince;
    private bool triggeredDuringTopStay;

    // Ação "Apertar 3 vezes".
    private int pressCount;
    private const int PressesNeeded = 3;

    // Consequência "Ligar depois que passar": o jogador ativou o laser mas
    // ainda está na área; liga quando ele sair.
    private bool turnOnWhenPlayerLeaves;
    private bool laserLigadoAtras;

    // Consequência "Afundar no chão".
    private bool sunk;
    private Vector3 sinkStartScale;
    private Vector3 sinkStartPosition;
    private Coroutine sinkRoutine;
    private Coroutine hideRoutine;

    // Reconfigurar: o collider próprio virou trigger depois de usado (ver
    // DisablePhysicalBlocking) e precisa voltar a ser sólido.
    private bool bloqueioDesligado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        warnedMissingTag = false;
    }

    /// <summary>
    /// Quem pode ativar um item: só objetos com a tag "Player". Fallback de
    /// segurança: um jogador com InteracaoPlayerController mas ainda sem a tag
    /// (cena antiga, antes de rodar Tools > Interação > Marcar Player com tag
    /// Player) continua funcionando, com um aviso no Console.
    /// </summary>
    public static bool IsPlayer(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.CompareTag(PlayerTag))
        {
            return true;
        }

        // O collider pode estar num filho do jogador: confere o dono do Rigidbody2D.
        Rigidbody2D body = other.attachedRigidbody;
        if (body != null && body.CompareTag(PlayerTag))
        {
            return true;
        }

        return IsUntaggedPlayer(other.gameObject);
    }

    private static bool IsUntaggedPlayer(GameObject other)
    {
        if (other.GetComponentInParent<InteracaoPlayerController>() == null)
        {
            return false;
        }

        if (!warnedMissingTag)
        {
            warnedMissingTag = true;
            Debug.LogWarning($"[InteractableItem] \"{other.name}\" tem InteracaoPlayerController mas não tem a tag \"{PlayerTag}\". " +
                             "Rode Tools > Interação > Marcar Player com tag Player (o painel diz que só a tag Player ativa os itens).");
        }

        return true;
    }

    private void Awake()
    {
        EnsureClickCollider();
        EnsureProximityZone();
        EnsureTopZone();
    }

    // OnMouseDown só dispara no GameObject dono do collider — como
    // ProximityZone/TopZone (os colliders "de verdade") ficam em filhos
    // separados, o próprio item precisa de um collider seu (sólido, não
    // trigger: assim também vira um pequeno obstáculo físico, o que faz a
    // Ação "Pular em cima" fazer sentido de verdade — sem isso não haveria
    // como ficar de pé em cima de um item do nível do chão). Também serve de
    // alvo de clique: OnMouseDown funciona com collider sólido ou trigger.
    private void EnsureClickCollider()
    {
        if (GetComponent<Collider2D>() != null)
        {
            // Já tem um (ex: a Porta, cujo collider sólido bloqueia a passagem
            // e também serve de alvo de clique).
            return;
        }

        var sr = GetComponent<SpriteRenderer>();
        var box = gameObject.AddComponent<BoxCollider2D>();
        if (sr != null && sr.sprite != null)
        {
            box.size = sr.sprite.bounds.size;
        }
        Physics2DTuning.PreventCornerSnag(box);
    }

    private void EnsureProximityZone()
    {
        Transform existing = transform.Find("ProximityZone");
        if (existing != null)
        {
            EnsureZoneComponent<ItemProximityZone>(existing.gameObject);
            return;
        }

        var zoneGO = new GameObject("ProximityZone");
        zoneGO.transform.SetParent(transform, false);
        // Cancela a escala do próprio item — sem isso, um item esticado
        // (ex: a Porta, escala Y=3) deixaria o círculo de proximidade oval.
        CounterScaleForParent(zoneGO.transform);

        var circle = zoneGO.AddComponent<CircleCollider2D>();
        circle.radius = proximityRadius;

        EnsureZoneComponent<ItemProximityZone>(zoneGO);
    }

    private void EnsureTopZone()
    {
        Transform existing = transform.Find("TopZone");
        if (existing != null)
        {
            EnsureZoneComponent<ItemTopZone>(existing.gameObject);
            return;
        }

        var sr = GetComponent<SpriteRenderer>();
        // sprite.bounds é em espaço LOCAL do sprite (independente da escala
        // do GameObject) — usado pra posição, que o Unity já multiplica pela
        // escala do pai sozinho ao calcular a posição final no mundo.
        Vector2 localHalfSize = sr != null && sr.sprite != null
            ? (Vector2)sr.sprite.bounds.extents
            : new Vector2(0.5f, 0.5f);

        Vector3 parentScale = transform.lossyScale;
        float worldHalfWidth = localHalfSize.x * Mathf.Abs(parentScale.x);

        var zoneGO = new GameObject("TopZone");
        zoneGO.transform.SetParent(transform, false);
        zoneGO.transform.localPosition = new Vector3(0f, localHalfSize.y, 0f);
        // Cancela a escala do item DEPOIS de posicionar — a posição usa
        // unidades locais (multiplicadas pela escala do pai automaticamente),
        // mas o tamanho do collider abaixo já precisa estar em "unidades de
        // mundo" (por isso o contra-escalamento).
        CounterScaleForParent(zoneGO.transform);

        var box = zoneGO.AddComponent<BoxCollider2D>();
        box.size = new Vector2(worldHalfWidth * 1.6f, topZoneHeight);

        EnsureZoneComponent<ItemTopZone>(zoneGO);
    }

    private void EnsureZoneComponent<T>(GameObject zoneGO) where T : Component, IItemZone
    {
        T zone = zoneGO.GetComponent<T>();
        if (zone == null)
        {
            zone = zoneGO.AddComponent<T>();
        }
        zone.Initialize(this);
    }

    // Cancela a escala herdada do pai — usado pros filhos ProximityZone/TopZone
    // não ficarem esticados quando o próprio item tem escala não-uniforme.
    private void CounterScaleForParent(Transform child)
    {
        Vector3 parentScale = transform.lossyScale;
        float x = Mathf.Approximately(parentScale.x, 0f) ? 1f : 1f / parentScale.x;
        float y = Mathf.Approximately(parentScale.y, 0f) ? 1f : 1f / parentScale.y;
        child.localScale = new Vector3(x, y, 1f);
    }

    // Chamado pelo InteractionConfigUI quando o jogador clica neste item.
    // OnMouseDown é baseado em raycast de física e NÃO sabe nada sobre a UI
    // por conta própria — sem essa checagem, clicar num botão do assistente
    // que fica por cima de outro item na tela (ex: a porta atrás do painel)
    // dispara o clique dos DOIS ao mesmo tempo. IsPointerOverGameObject()
    // garante que cliques em cima de qualquer UI nunca "vazam" pro cenário.
    private void OnMouseDown()
    {
        AbrirPainelPeloClique();
    }

    /// <summary>
    /// Abre o assistente pra este item, a não ser que o clique tenha caído
    /// em cima da UI. Público pra quem cobre o collider do item e recebe o
    /// clique no lugar dele (ex: a PortaSaida, sólida por cima do Objetivo).
    /// </summary>
    public void AbrirPainelPeloClique()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        InteractionConfigUI.Instance?.OpenFor(this);
    }

    // O OnMouseDown só chega pelo collider sólido do próprio item. Depois de
    // usado, esse collider pode ter virado trigger (DisablePhysicalBlocking,
    // laser apagado; o projeto deixa Physics2D.queriesHitTriggers desligado)
    // ou estar desligado (espinho afundado, plataforma caindo). Aqui o clique
    // em cima do desenho abre o painel do mesmo jeito, pra o item continuar
    // reconfigurável.
    private void AbrirPainelSeClicadoSemColisor()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Collider2D own = GetComponent<Collider2D>();
        Camera cam = Camera.main;
        if ((own != null && own.enabled && !own.isTrigger) || cam == null)
        {
            return;
        }

        Vector2 mundo = cam.ScreenToWorldPoint(mouse.position.ReadValue());
        if (!ContemPonto(mundo))
        {
            return;
        }

        // Um item sólido no mesmo ponto já recebe o clique pelo OnMouseDown.
        Collider2D solido = Physics2D.OverlapPoint(mundo);
        if (solido != null && (solido.GetComponent<InteractableItem>() != null || solido.GetComponent<PortaSaida>() != null))
        {
            return;
        }

        AbrirPainelPeloClique();
    }

    /// <summary>
    /// Se o ponto (no mundo) cai em cima do item: o collider próprio ou, com
    /// ele desligado, o desenho visível. Espinho afundado conta a altura de
    /// antes de afundar, pra continuar fácil de clicar. Usado também pelo
    /// destaque do mouse (DestaqueItensInteracao).
    /// </summary>
    public bool ContemPonto(Vector2 mundo)
    {
        Bounds area;
        Collider2D own = GetComponent<Collider2D>();
        if (own != null && own.enabled)
        {
            area = own.bounds;
        }
        else
        {
            Renderer desenho = GetComponent<Renderer>();
            if (desenho == null || !desenho.enabled)
            {
                return false;
            }
            area = desenho.bounds;
            if (sunk && transform.localScale.y > 0.0001f)
            {
                float alturaInteira = area.size.y * sinkStartScale.y / transform.localScale.y;
                area.SetMinMax(area.min, new Vector3(area.max.x, area.min.y + alturaInteira, area.max.z));
            }
        }

        return mundo.x >= area.min.x && mundo.x <= area.max.x && mundo.y >= area.min.y && mundo.y <= area.max.y;
    }

    /// <summary>Configura usando o "Repetir" padrão da Consequência (ver DefaultRepeats).</summary>
    public void Configure(ActionType action, ConsequenceType consequence)
    {
        Configure(action, consequence, DefaultRepeats(consequence));
    }

    /// <summary>
    /// Chamado pelo InteractionConfigUI ao fim do assistente de configuração.
    /// repeats = escolha "Repetir" do jogador (Sempre = true). Pras
    /// Consequências que não fazem sentido repetir (ver CanRepeat) vira
    /// "Uma vez" de qualquer jeito.
    /// </summary>
    public void Configure(ActionType action, ConsequenceType consequence, bool repeats)
    {
        ConsequenceType? previous = IsConfigured ? ChosenConsequence : (ConsequenceType?)null;
        if (previous.HasValue)
        {
            DesfazerConsequenciaAnterior(previous.Value);
        }

        ChosenAction = action;
        ChosenConsequence = consequence;
        ChosenRepeats = repeats && CanRepeat(consequence);
        IsConfigured = true;

        // Reconfigurar recomeça do zero: o item volta a reagir mesmo se já
        // tinha disparado na escolha anterior.
        HasTriggered = false;
        triggeredDuringStay = false;
        triggeredDuringTopStay = false;
        pressCount = 0;
        turnOnWhenPlayerLeaves = false;
        laserLigadoAtras = false;

        if (consequence == ConsequenceType.TurnOnBehindPlayer)
        {
            // Começa desligado: o caminho está aberto até o jogador passar.
            SetLaserOn(false);
        }

        // Reconfigurar é registrado pelo próprio painel ("reconfigurou", com a
        // escolha antiga e a nova); aqui só a primeira configuração.
        if (!previous.HasValue)
        {
            InteracaoLevelFlow.Instance?.NotifyItemConfigured(this);
        }
    }

    // Reconfigurar: tira do item o que a Consequência antiga deixou ligado
    // (laser apagado, espinho afundado, plataforma escorregadia ou de mão
    // única, parede atravessável, collider que virou trigger depois de usado).
    // O que já aconteceu no mundo (porta aberta, cura, cartão) fica.
    // Vale também quando a Consequência continua a mesma e só a Ação ou o
    // Repetir mudou: senão um espinho afundado ou uma Vida já pega em
    // "Uma vez" ficavam presos assim, sem clique.
    private void DesfazerConsequenciaAnterior(ConsequenceType anterior)
    {
        if (anterior == ConsequenceType.TurnOnBehindPlayer || anterior == ConsequenceType.DisableForSeconds)
        {
            if (hideRoutine != null) { StopCoroutine(hideRoutine); hideRoutine = null; }
            SetLaserOn(true);
        }

        if (anterior == ConsequenceType.SinkIntoGround && (sunk || sinkRoutine != null))
        {
            if (sinkRoutine != null) { StopCoroutine(sinkRoutine); sinkRoutine = null; }
            transform.localScale = sinkStartScale;
            transform.position = sinkStartPosition;
            var solid = GetComponent<Collider2D>();
            if (solid != null) solid.enabled = true;
            sunk = false;
        }

        // A lâmpada apaga, pra a escolha nova começar do zero.
        if (anterior == ConsequenceType.TurnOnLamp || anterior == ConsequenceType.TurnOnLampForSeconds)
        {
            GetComponent<LampadaInteracao>()?.Apagar();
        }

        var plataforma = GetComponent<FakePlatform>();
        if (plataforma != null && (anterior == ConsequenceType.MakeSlippery || anterior == ConsequenceType.OneWayFromBelow))
        {
            plataforma.Desfazer();
        }

        var parede = GetComponent<ParedeFalsa>();
        if (parede != null && anterior == ConsequenceType.PassableWhileNear && parede.Atravessavel)
        {
            StartCoroutine(SolidificarQuandoLivre(parede.DefinirAtravessavelFalso));
        }

        if (bloqueioDesligado)
        {
            bloqueioDesligado = false;
            Collider2D own = GetComponent<Collider2D>();
            if (own != null)
            {
                StartCoroutine(SolidificarQuandoLivre(() => own.isTrigger = false));
            }
        }
    }

    // Volta a ser sólido só quando o jogador não estiver dentro (senão prende).
    private System.Collections.IEnumerator SolidificarQuandoLivre(System.Action solidificar)
    {
        Collider2D own = GetComponent<Collider2D>();
        while (own != null && PlayerOverlaps(own))
        {
            yield return null;
        }
        solidificar();
    }

    private void Update()
    {
        // Balão com a tecla de Interagir sobre o jogador enquanto dá para apertar.
        BalaoTecla.Definir(this,
            IsConfigured && !HasTriggered && playerInRange && !JogoBloqueado.Ativo &&
            (ChosenAction == ActionType.PressButton || ChosenAction == ActionType.PressButtonThreeTimes),
            "Interact", "interagir", displayName);

        AbrirPainelSeClicadoSemColisor();

        if (!IsConfigured || HasTriggered)
        {
            return;
        }

        // Com uma janela aberta (pausa, painel, Game Over), a tecla de
        // Interagir não aciona o item que está atrás dela.
        bool podeApertar = playerInRange && !JogoBloqueado.Ativo;

        if (ChosenAction == ActionType.PressButton && podeApertar &&
            InputManager.Instance != null && InputManager.Instance.InteractAction.WasPressedThisFrame())
        {
            Trigger();
        }
        else if (ChosenAction == ActionType.PressButtonThreeTimes && podeApertar &&
                 InputManager.Instance != null && InputManager.Instance.InteractAction.WasPressedThisFrame())
        {
            pressCount++;
            if (pressCount >= PressesNeeded)
            {
                pressCount = 0;
                Trigger();
            }
            else
            {
                InteractionConfigUI.Instance?.ShowHint($"\"{displayName}\": {pressCount} de {PressesNeeded}. Aperte de novo!");
            }
        }
        else if (ChosenAction == ActionType.StayOnTop && playerOnTop && !triggeredDuringTopStay &&
                 Time.time - onTopSince >= stayOnTopSeconds)
        {
            triggeredDuringTopStay = true;
            Trigger();
        }
        else if (ChosenAction == ActionType.RequiresMissingHealth && playerInRange && !triggeredDuringStay &&
                 !PlayerHealthFull())
        {
            // Cobre o caso do jogador tomar dano enquanto já está perto.
            Trigger();
        }
        else if (playerInRange && !triggeredDuringStay &&
                 (ChosenAction == ActionType.RequiresOneCard || ChosenAction == ActionType.RequiresTwoCards))
        {
            // Checagem silenciosa a cada frame: cobre o caso do jogador
            // coletar o cartão que faltava enquanto já está parado perto
            // (o aviso "faltam cartões" já foi mostrado uma vez em
            // HandleProximityEnter).
            TryTriggerByCardCount(showHintIfNotEnough: false);
        }
    }

    public void HandleProximityEnter(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerInRange = true;

        if (!IsConfigured)
        {
            return;
        }

        // "Ligar depois que passar" em "Sempre": ao voltar pra área, o laser
        // abre de novo (e liga outra vez quando o jogador sair).
        if (ChosenConsequence == ConsequenceType.TurnOnBehindPlayer && ChosenRepeats && laserLigadoAtras)
        {
            laserLigadoAtras = false;
            SetLaserOn(false);
        }

        if (HasTriggered)
        {
            return;
        }

        if (ChosenAction == ActionType.WalkNear)
        {
            Trigger();
        }
        else if (ChosenAction == ActionType.RequiresOtherItem)
        {
            if (requiredItem != null && requiredItem.WasActivated)
            {
                Trigger();
            }
            else
            {
                string requirement = requiredItem != null ? requiredItem.DisplayName : "outro item";
                InteractionConfigUI.Instance?.ShowHint($"\"{displayName}\" precisa que \"{requirement}\" seja ativado primeiro.");
            }
        }
        else if (ChosenAction == ActionType.RequiresOneCard || ChosenAction == ActionType.RequiresTwoCards)
        {
            TryTriggerByCardCount(showHintIfNotEnough: true);
        }
        else if (ChosenAction == ActionType.RequiresMissingHealth)
        {
            if (!PlayerHealthFull())
            {
                Trigger();
            }
            else
            {
                InteractionConfigUI.Instance?.ShowHint($"\"{displayName}\" só funciona se você tiver perdido vida.");
            }
        }
    }

    // Ação "Ter perdido vida": vida cheia (ou sem PlayerHealth na cena) não ativa.
    private static bool PlayerHealthFull()
    {
        var health = FindAnyObjectByType<PlayerHealth>();
        return health == null || health.CurrentHealth >= health.MaxHealth;
    }

    // Usado pelas Ações "Precisa de 1/2 cartões" (ver ActionType) — o total
    // coletado vive em InteracaoLevelFlow, não aqui, pra sobreviver mesmo se
    // este item específico for reconfigurado.
    private void TryTriggerByCardCount(bool showHintIfNotEnough)
    {
        int required = ChosenAction == ActionType.RequiresOneCard ? 1 : 2;
        int collected = InteracaoLevelFlow.Instance != null ? InteracaoLevelFlow.Instance.CollectedCards : 0;

        if (collected >= required)
        {
            Trigger();
        }
        else if (showHintIfNotEnough)
        {
            string plural = required > 1 ? "cartões" : "cartão";
            InteractionConfigUI.Instance?.ShowHint($"\"{displayName}\" precisa de {required} {plural} — você tem {collected}.");
        }
    }

    public void HandleProximityExit(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerInRange = false;
        triggeredDuringStay = false;

        if (!IsConfigured)
        {
            return;
        }

        if (ChosenConsequence == ConsequenceType.PassableWhileNear)
        {
            StartCoroutine(SolidifyWallWhenClear());
        }

        if (turnOnWhenPlayerLeaves)
        {
            turnOnWhenPlayerLeaves = false;
            TurnLaserOnBehind();
        }

        if (!HasTriggered && ChosenAction == ActionType.LeaveArea)
        {
            Trigger();
        }
    }

    /// <summary>Chamado pela TopZone quando o jogador sai de cima (Ação "Ficar em cima 1 segundo").</summary>
    public void HandleLeftTop(Collider2D other)
    {
        if (IsPlayer(other))
        {
            playerOnTop = false;
            triggeredDuringTopStay = false;
        }
    }

    private void OnDisable()
    {
        playerInRange = false;
        BalaoTecla.Soltar(this);
    }

    // ActionType.Touch: toque físico direto no collider sólido do próprio
    // item (não a ProximityZone, que é uma área bem mais generosa em volta).
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsConfigured || HasTriggered || ChosenAction != ActionType.Touch)
        {
            return;
        }

        if (IsPlayer(collision.collider))
        {
            Trigger();
        }
    }

    public void HandleLandedOnTop(Collider2D other)
    {
        if (IsPlayer(other) && !playerOnTop)
        {
            playerOnTop = true;
            onTopSince = Time.time;
        }

        if (!IsConfigured || HasTriggered || ChosenAction != ActionType.JumpOnTop)
        {
            return;
        }

        if (IsPlayer(other))
        {
            Trigger();
        }
    }

    private void Trigger()
    {
        WasActivated = true;
        if (playerInRange)
        {
            triggeredDuringStay = true;
        }

        if (!ChosenRepeats)
        {
            HasTriggered = true;
        }

        ApplyConsequence();

        // Só sai do caminho o que é naturalmente de uso único (porta, cartão,
        // cura...). Um lançador, espinho ou plataforma que o jogador deixou em
        // "Uma vez" apenas para de reagir e continua sólido; se virasse
        // trigger, o jogador cairia através dele.
        if (!ChosenRepeats && !DefaultRepeats(ChosenConsequence) && KeepsBlockingAfterUse(ChosenConsequence) == false)
        {
            DisablePhysicalBlocking();
        }
    }

    // Consequências em "Uma vez" que não devem tirar o bloqueio do item depois
    // de usadas: o laser que liga atrás do jogador precisa continuar sólido, e
    // a lâmpada continua clicável pra reconfigurar (com queriesHitTriggers
    // desligado, um trigger não recebe o clique).
    private static bool KeepsBlockingAfterUse(ConsequenceType consequence)
    {
        return consequence == ConsequenceType.TurnOnBehindPlayer
            || consequence == ConsequenceType.TurnOnLamp
            || consequence == ConsequenceType.TurnOnLampForSeconds;
    }

    /// <summary>
    /// "Repetir" pré-selecionado no assistente pra cada Consequência. Algumas
    /// são feitas pra continuar funcionando toda vez (ex: um lançador de pulo
    /// é um objeto permanente do cenário, não algo que se "gasta" numa única
    /// ativação). Com "Sempre", HasTriggered nunca liga, então
    /// HandleLandedOnTop/etc. seguem chamando Trigger() a cada novo contato.
    /// </summary>
    public static bool DefaultRepeats(ConsequenceType consequence)
    {
        return ConsequenceRepeats(consequence);
    }

    /// <summary>
    /// Se o botão "Sempre" fica disponível pra essa Consequência. Passar a
    /// fase, reiniciar, pegar o cartão (o item some), abrir a porta e ligar a
    /// plataforma só fazem sentido uma vez, então ficam travadas em "Uma vez".
    /// </summary>
    public static bool CanRepeat(ConsequenceType consequence)
    {
        switch (consequence)
        {
            case ConsequenceType.CompleteLevel:
            case ConsequenceType.RestartLevel:
            case ConsequenceType.CollectCard:
            case ConsequenceType.OpenDoor:
            case ConsequenceType.ActivateMovingPlatform:
            case ConsequenceType.IncreaseMaxHealth:
            case ConsequenceType.TurnOnLamp:
                return false;
            default:
                return true;
        }
    }

    // Padrão de antes do jogador poder escolher. Continua sendo o valor
    // pré-selecionado do "Repetir" (ver DefaultRepeats).
    private static bool ConsequenceRepeats(ConsequenceType consequence)
    {
        return consequence == ConsequenceType.LaunchPlayer
            || consequence == ConsequenceType.DamageOneHeart
            || consequence == ConsequenceType.DamageTwoHearts
            || consequence == ConsequenceType.Repel
            || consequence == ConsequenceType.BreakAndRespawn
            || consequence == ConsequenceType.BounceAndReturn
            || consequence == ConsequenceType.DisableForSeconds
            || consequence == ConsequenceType.ReturnToSafePoint
            || consequence == ConsequenceType.SlowPlayer
            || consequence == ConsequenceType.InvertControls
            || consequence == ConsequenceType.FallAfterDelay
            || consequence == ConsequenceType.MakeSlippery
            || consequence == ConsequenceType.OneWayFromBelow
            || consequence == ConsequenceType.LaunchSideways
            || consequence == ConsequenceType.OpenDoorForSeconds
            || consequence == ConsequenceType.PassableWhileNear
            || consequence == ConsequenceType.TurnOnLampForSeconds;
    }

    // Depois de usado, o item não deve mais atrapalhar o caminho do jogador
    // — vira trigger (deixa de bloquear) mas continua visível no cenário, em
    // vez de precisar desaparecer/ser destruído pra parar de atrapalhar.
    //
    // Exceção: uma MovingPlatform precisa continuar sólida pra sempre (o
    // jogador anda em cima dela indefinidamente) — aqui "disparar" só liga o
    // vaivém, não deve tirar o bloqueio físico do collider que ela usa tanto
    // pra clique quanto pra sustentar o jogador.
    private void DisablePhysicalBlocking()
    {
        if (GetComponent<MovingPlatform>() != null)
        {
            return;
        }

        Collider2D ownCollider = GetComponent<Collider2D>();
        if (ownCollider != null && !ownCollider.isTrigger)
        {
            ownCollider.isTrigger = true;
            bloqueioDesligado = true;
        }
    }

    // O lançador usa o mesmo Animator do jumppad da fase Animacao
    // (JumppadFase: Repouso -> Impulso). "Bounce" fica de reserva para
    // objetos que ainda usem o controller antigo.
    private static readonly int EstadoImpulso = Animator.StringToHash("Impulso");
    private static readonly int EstadoBounce = Animator.StringToHash("Bounce");

    private void TocarAnimacaoLancador()
    {
        Animator animator = GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.Play(animator.HasState(0, EstadoImpulso) ? EstadoImpulso : EstadoBounce, 0, 0f);
    }

    private void ApplyConsequence()
    {
        var player = FindAnyObjectByType<InteracaoPlayerController>();

        switch (ChosenConsequence)
        {
            case ConsequenceType.HealPlayer:
                player?.GetComponent<PlayerHealth>()?.HealFully();
                break;

            case ConsequenceType.OpenDoor:
                if (doorToOpen != null) doorToOpen.Open();
                // Parede falsa (ex: DoorFalse): a "porta" é o próprio item.
                else GetComponent<ParedeFalsa>()?.Abrir();
                break;

            case ConsequenceType.LaunchPlayer:
                player?.ApplyLaunch(launchVelocity);
                AudioManager.TocarEfeito(AudioManager.Efeito.JumpPad);
                // Play() força reiniciar o estado na hora — um SetTrigger aqui
                // podia ficar "pendente" se o pouso disparasse duas vezes
                // seguidas antes da transição consumir o gatilho, fazendo a
                // animação entrar num loop Idle<->Bounce sozinha, sem relação
                // com o jogador tocar a plataforma de novo.
                TocarAnimacaoLancador();
                break;

            case ConsequenceType.CompleteLevel:
                // Com a porta de saída (ver PortaSaida), a fase só termina
                // quando o jogador atravessa a porta já aberta.
                var portaSaida = PortaSaida.DoItem(gameObject);
                if (portaSaida != null)
                {
                    portaSaida.Liberar(() => InteracaoLevelFlow.Instance?.CompleteLevel());
                }
                else
                {
                    InteracaoLevelFlow.Instance?.CompleteLevel();
                }
                break;

            case ConsequenceType.RestartLevel:
                InteracaoLevelFlow.Instance?.RestartLevel();
                break;

            case ConsequenceType.ActivateMovingPlatform:
                GetComponent<MovingPlatform>()?.Activate();
                break;

            case ConsequenceType.CollectCard:
                InteracaoLevelFlow.Instance?.CollectCard();
                gameObject.SetActive(false);
                break;

            case ConsequenceType.DamageOneHeart:
                player?.GetComponent<PlayerHealth>()?.TakeDamage(1);
                break;

            case ConsequenceType.DamageTwoHearts:
                player?.GetComponent<PlayerHealth>()?.TakeDamage(2);
                break;

            case ConsequenceType.Repel:
                ApplyRepel(player);
                break;

            case ConsequenceType.BreakAndRespawn:
                GetComponent<FakePlatform>()?.BreakAndRespawn();
                break;

            case ConsequenceType.BounceAndReturn:
                GetComponent<FakePlatform>()?.BounceAndReturn();
                break;

            case ConsequenceType.DisableForSeconds:
                var barreira = GetComponent<LaserBarreira>();
                if (barreira != null) barreira.Desligar(laserOffSeconds);
                else hideRoutine = StartCoroutine(HideForSeconds(laserOffSeconds));
                break;

            case ConsequenceType.TurnOnBehindPlayer:
                if (playerInRange) turnOnWhenPlayerLeaves = true;
                else TurnLaserOnBehind();
                break;

            case ConsequenceType.ReturnToSafePoint:
                player?.VoltarAoPontoSeguro();
                AudioManager.TocarEfeito(AudioManager.Efeito.Erro);
                break;

            case ConsequenceType.SinkIntoGround:
                if (!sunk) sinkRoutine = StartCoroutine(SinkRoutine());
                break;

            case ConsequenceType.ShieldForSeconds:
                player?.GetComponent<PlayerHealth>()?.AtivarEscudo(effectSeconds);
                AudioManager.TocarEfeito(AudioManager.Efeito.Acerto);
                break;

            case ConsequenceType.IncreaseMaxHealth:
                player?.GetComponent<PlayerHealth>()?.AumentarVidaMaxima(1);
                AudioManager.TocarEfeito(AudioManager.Efeito.Acerto);
                break;

            case ConsequenceType.SlowPlayer:
                player?.AplicarLentidao(effectSeconds);
                AudioManager.TocarEfeito(AudioManager.Efeito.Erro);
                break;

            case ConsequenceType.InvertControls:
                player?.InverterControles(effectSeconds);
                AudioManager.TocarEfeito(AudioManager.Efeito.Erro);
                break;

            case ConsequenceType.FallAfterDelay:
                GetComponent<FakePlatform>()?.FallAfterDelay();
                break;

            case ConsequenceType.MakeSlippery:
                GetComponent<FakePlatform>()?.MakeSlippery();
                break;

            case ConsequenceType.OneWayFromBelow:
                GetComponent<FakePlatform>()?.MakeOneWay();
                break;

            case ConsequenceType.LaunchSideways:
                player?.ApplyKnockback(lateralLaunchVelocity, lateralLaunchDuration);
                AudioManager.TocarEfeito(AudioManager.Efeito.JumpPad);
                TocarAnimacaoLancador();
                break;

            case ConsequenceType.OpenDoorForSeconds:
                if (doorToOpen != null) doorToOpen.OpenFor(doorOpenSeconds);
                break;

            case ConsequenceType.PassableWhileNear:
                GetComponent<ParedeFalsa>()?.DefinirAtravessavel(true);
                break;

            case ConsequenceType.TurnOnLamp:
                GetComponent<LampadaInteracao>()?.Acender();
                break;

            case ConsequenceType.TurnOnLampForSeconds:
                GetComponent<LampadaInteracao>()?.AcenderPor(lampOnSeconds);
                break;
        }
    }

    // ---- Laser: "Ligar depois que passar" / "Desligar por 3 segundos"

    private void TurnLaserOnBehind()
    {
        laserLigadoAtras = true;
        SetLaserOn(true);
        AudioManager.TocarEfeito(AudioManager.Efeito.Laser);
    }

    private void SetLaserOn(bool on)
    {
        var barreira = GetComponent<LaserBarreira>();
        if (barreira != null)
        {
            if (on) barreira.Ligar();
            else barreira.DesligarAteLigar();
            return;
        }

        // Laser antigo (só um sprite): liga/desliga o collider e o sprite.
        var solid = GetComponent<Collider2D>();
        if (solid != null) solid.enabled = on;
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = on;
    }

    private System.Collections.IEnumerator HideForSeconds(float seconds)
    {
        SetLaserOn(false);
        yield return new WaitForSeconds(seconds);
        SetLaserOn(true);
        hideRoutine = null;
    }

    // ---- Espinhos: "Afundar no chão"

    // Os espinhos se achatam contra o chão (a base fica no lugar) e o collider
    // sólido desliga. Em "Sempre", sobem de novo depois de sinkSeconds, mas só
    // quando o jogador não está em cima.
    private System.Collections.IEnumerator SinkRoutine()
    {
        sunk = true;
        var solid = GetComponent<Collider2D>();
        var sr = GetComponent<SpriteRenderer>();
        float height = sr != null ? sr.bounds.size.y : 0.5f;
        sinkStartScale = transform.localScale;
        sinkStartPosition = transform.position;

        if (solid != null) solid.enabled = false;
        AudioManager.TocarEfeito(AudioManager.Efeito.Alavanca);
        yield return SquashTo(0.15f, height);

        if (!ChosenRepeats)
        {
            yield break;
        }

        yield return new WaitForSeconds(sinkSeconds);
        while (playerOnTop)
        {
            yield return null;
        }

        yield return SquashTo(1f, height);
        if (solid != null) solid.enabled = true;
        sunk = false;
        sinkRoutine = null;
    }

    private System.Collections.IEnumerator SquashTo(float targetFactor, float fullHeight)
    {
        float startFactor = transform.localScale.y / sinkStartScale.y;
        float t = 0f;
        const float duration = 0.3f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Lerp(startFactor, targetFactor, Mathf.SmoothStep(0f, 1f, t / duration));
            ApplySquash(k, fullHeight);
            yield return null;
        }
        ApplySquash(targetFactor, fullHeight);
    }

    // Pivot no centro: pra base continuar no chão, desce metade do que encolheu.
    private void ApplySquash(float factor, float fullHeight)
    {
        transform.localScale = new Vector3(sinkStartScale.x, sinkStartScale.y * factor, sinkStartScale.z);
        transform.position = sinkStartPosition + Vector3.down * (fullHeight * (1f - factor) * 0.5f);
    }

    // ---- Parede: "Deixar passar quando perto"

    // Volta a bloquear quando o jogador sai de perto, mas só depois que ele
    // não estiver mais dentro da parede (senão ficaria preso nela).
    private System.Collections.IEnumerator SolidifyWallWhenClear()
    {
        var parede = GetComponent<ParedeFalsa>();
        var solid = GetComponent<Collider2D>();
        if (parede == null)
        {
            yield break;
        }

        while (!playerInRange && solid != null && PlayerOverlaps(solid))
        {
            yield return null;
        }

        if (!playerInRange)
        {
            parede.DefinirAtravessavel(false);
        }
    }

    private static bool PlayerOverlaps(Collider2D area)
    {
        Bounds b = area.bounds;
        foreach (Collider2D c in Physics2D.OverlapBoxAll(b.center, b.size, 0f))
        {
            if (c != area && !c.isTrigger && IsPlayer(c))
            {
                return true;
            }
        }
        return false;
    }

    // Empurra o jogador pra longe do próprio item — usado pela Consequência
    // "Repelir o jogador" (ex: laser, espinhos). O empurrão é sempre pro lado,
    // com um pulinho baixo: antes a direção era "centro do item -> jogador",
    // que em cima de um espinho aponta pra cima e lançava o jogador alto no ar.
    private void ApplyRepel(InteracaoPlayerController player)
    {
        if (player == null)
        {
            return;
        }

        float dx = player.transform.position.x - transform.position.x;
        float side;
        if (Mathf.Abs(dx) > 0.05f)
        {
            side = Mathf.Sign(dx);
        }
        else
        {
            // Bem no meio do item: empurra pro lado de onde o jogador veio.
            var body = player.GetComponent<Rigidbody2D>();
            float vx = body != null ? body.linearVelocity.x : 0f;
            side = vx > 0.01f ? -1f : 1f;
        }

        player.ApplyKnockback(new Vector2(side * repelForce, repelHop), repelDuration);
    }
}
