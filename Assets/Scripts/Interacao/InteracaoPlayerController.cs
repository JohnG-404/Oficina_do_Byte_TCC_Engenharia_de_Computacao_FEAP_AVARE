using UnityEngine;

/// <summary>
/// Player da fase "Interacao": diferente de Inicio/Aplicacao, aqui o
/// movimento e o pulo já começam liberados — a lição desta fase é sobre
/// Ação+Consequência dos objetos, não sobre destravar física/movimento.
///
/// Gravidade "assimétrica" (queda mais forte que a subida, pulo curto ao
/// soltar o botão cedo) + coyote time + buffer de pulo: mesma sensação
/// ajustada em AplicacaoPlayerController, copiada aqui porque as duas cenas
/// não compartilham uma classe base de movimento.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class InteracaoPlayerController : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float velocidade = 4f;

    [Header("Pulo")]
    [SerializeField] private float forcaPulo = 14f;
    [SerializeField] private float gravidadeBase = 4f;
    [SerializeField] private float multiplicadorQueda = 2.5f;
    [SerializeField] private float multiplicadorPuloCurto = 2f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    public bool IsGrounded => chao.NoChao;

    private Rigidbody2D rb;
    private readonly ContatosChao chao = new ContatosChao();
    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool jumpConsumedSinceGrounded;

    // Tempo restante do empurrão de "Repelir o jogador" (ver ApplyKnockback).
    // Enquanto > 0, o controle horizontal e o pulo ficam suspensos — senão o
    // HandleMovement sobrescreve a velocidade X no frame seguinte e o
    // empurrão vira só um impulso pra cima.
    private float knockbackTimer;

    [Header("Efeitos das Consequências novas")]
    [Tooltip("Multiplicador da velocidade enquanto o jogador está lento (Vida Falsa: \"Deixar o jogador lento\").")]
    [SerializeField] private float fatorLento = 0.45f;
    [Tooltip("Multiplicador do pulo enquanto o jogador está lento.")]
    [SerializeField] private float fatorPuloLento = 0.75f;
    [Tooltip("Aceleração em chão escorregadio (Plataforma Falsa: \"Ficar escorregadia\"). Menor = desliza mais.")]
    [SerializeField] private float aceleracaoEscorregadia = 3f;

    private float lentoAte = -1f;
    private float invertidoAte = -1f;
    private Collider2D chaoAtual;
    private SpriteRenderer spriteJogador;
    private PlayerHealth vida;
    private bool corAlterada;

    // Pontos de chão firme dos últimos segundos (ver VoltarAoPontoSeguro).
    private readonly System.Collections.Generic.List<Vector3> pontosSeguros = new System.Collections.Generic.List<Vector3>();
    private float proximoPontoSeguro;
    private Vector2 posicaoInicial;
    private Vector2? ultimoPontoSeguroUsado;

    public bool EstaLento => Time.time < lentoAte;
    public bool ControlesInvertidos => Time.time < invertidoAte;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        posicaoInicial = rb.position;
        spriteJogador = GetComponentInChildren<SpriteRenderer>();
        vida = GetComponent<PlayerHealth>();
        rb.gravityScale = gravidadeBase;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        Collider2D col = GetComponent<Collider2D>();
        Physics2DTuning.PreventCornerSnag(col);
    }

    private void Update()
    {
        HandleMovement();
        UpdateJumpTimers();
        HandleJump();
        ApplyJumpGravity();
    }

    private void FixedUpdate()
    {
        GuardarPontoSeguro();
    }

    private void LateUpdate()
    {
        PintarEfeitos();
    }

    private void HandleMovement()
    {
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.deltaTime;
            return;
        }

        float moveInput = !DialogueManager.IsAnyDialogueActive && !JogoBloqueado.Ativo && InputManager.Instance != null
            ? InputManager.Instance.MoveAction.ReadValue<float>()
            : 0f;
        if (ControlesInvertidos)
        {
            moveInput = -moveInput;
        }

        float alvo = moveInput * velocidade * (EstaLento ? fatorLento : 1f);
        float vx = alvo;
        if (IsGrounded && ChaoEscorregadio())
        {
            // Sem atrito: a velocidade só muda aos poucos, então o jogador
            // continua deslizando depois de soltar a tecla.
            vx = Mathf.MoveTowards(rb.linearVelocity.x, alvo, aceleracaoEscorregadia * Time.deltaTime);
        }
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    private void UpdateJumpTimers()
    {
        if (IsGrounded)
        {
            coyoteTimer = coyoteTime;
            jumpConsumedSinceGrounded = false;
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.deltaTime);
        }

        // Janela aberta (pausa, painel, Game Over...): o Espaço não chega no
        // robô e o pulo guardado é apagado, senão ele pula sozinho ao fechar.
        if (JogoBloqueado.Ativo)
        {
            jumpBufferTimer = 0f;
            return;
        }

        bool jumpPressedThisFrame = !DialogueManager.IsAnyDialogueActive &&
            InputManager.Instance != null && InputManager.Instance.JumpAction.WasPressedThisFrame();
        jumpBufferTimer = jumpPressedThisFrame ? jumpBufferTime : Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
    }

    private void HandleJump()
    {
        if (knockbackTimer > 0f || jumpConsumedSinceGrounded || jumpBufferTimer <= 0f || coyoteTimer <= 0f)
        {
            return;
        }

        jumpConsumedSinceGrounded = true;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        // Zera na hora, não espera o OnCollisionExit2D: o evento de saída só
        // chega no próximo passo de física, e nesse intervalo IsGrounded
        // ainda lia true — dava pra apertar pular de novo bem rápido e ganhar
        // um pulo extra "de graça" antes do chão ser perdido de verdade.
        chao.Limpar();
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, forcaPulo * (EstaLento ? fatorPuloLento : 1f));
        AudioManager.TocarEfeito(AudioManager.Efeito.Pulo);
    }

    private void ApplyJumpGravity()
    {
        if (rb.linearVelocity.y < 0f)
        {
            rb.gravityScale = gravidadeBase * multiplicadorQueda;
        }
        else if (rb.linearVelocity.y > 0f && !JumpHeld())
        {
            rb.gravityScale = gravidadeBase * multiplicadorPuloCurto;
        }
        else
        {
            rb.gravityScale = gravidadeBase;
        }
    }

    private bool JumpHeld()
    {
        return InputManager.Instance != null && InputManager.Instance.JumpAction.IsPressed();
    }

    /// <summary>Aplica uma velocidade imediata no player — usado pela Consequência "Lançar o jogador".</summary>
    public void ApplyLaunch(Vector2 velocity)
    {
        rb.linearVelocity = velocity;
    }

    /// <summary>
    /// Empurrão de "Repelir o jogador": aplica a velocidade e segura o
    /// controle por alguns instantes, pra o jogador deslizar pra trás rente ao
    /// chão em vez de só subir.
    /// </summary>
    public void ApplyKnockback(Vector2 velocity, float duration)
    {
        rb.linearVelocity = velocity;
        knockbackTimer = duration;
    }

    /// <summary>Vida Falsa: "Deixar o jogador lento" — anda e pula menos por alguns segundos.</summary>
    public void AplicarLentidao(float segundos)
    {
        lentoAte = Time.time + segundos;
    }

    /// <summary>Vida Falsa: "Inverter os controles" — esquerda vira direita por alguns segundos.</summary>
    public void InverterControles(float segundos)
    {
        invertidoAte = Time.time + segundos;
    }

    /// <summary>
    /// Espinhos: "Voltar ao ponto seguro" — leva o jogador ao chão firme onde
    /// ele estava pouco antes (sem ser em cima de um objeto interativo). Sem
    /// nenhum registro ainda, volta ao começo da fase.
    /// </summary>
    public void VoltarAoPontoSeguro()
    {
        // Sem registro novo (ex: voltou e caiu de novo logo em seguida), usa o
        // mesmo ponto da última volta; sem nenhum, o começo da fase.
        Vector2 destino = ultimoPontoSeguroUsado ?? posicaoInicial;
        // O último ponto pode estar colado no perigo; pega um um pouco mais antigo.
        if (pontosSeguros.Count > 0)
        {
            float limite = Time.time - 0.6f;
            int escolhido = 0;
            for (int i = pontosSeguros.Count - 1; i >= 0; i--)
            {
                if (pontosSeguros[i].z <= limite)
                {
                    escolhido = i;
                    break;
                }
            }
            destino = pontosSeguros[escolhido];
            // Os pontos depois do escolhido ficam mais perto do perigo: descarta.
            pontosSeguros.RemoveRange(escolhido + 1, pontosSeguros.Count - escolhido - 1);
        }
        ultimoPontoSeguroUsado = destino;

        rb.position = destino;
        transform.position = new Vector3(destino.x, destino.y, transform.position.z);
        rb.linearVelocity = Vector2.zero;
        // O chão de antes ficou pra trás: o de destino entra no próximo passo de física.
        chao.Limpar();
        knockbackTimer = 0.25f;
    }

    // Guarda (x, y, tempo) a cada 0,1 s em que o jogador está parado em chão
    // comum — nunca em cima de um objeto interativo (espinho, plataforma
    // falsa...). Mantém só os últimos 3 segundos.
    private void GuardarPontoSeguro()
    {
        if (Time.time < proximoPontoSeguro || !IsGrounded || chaoAtual == null || knockbackTimer > 0f)
        {
            return;
        }

        if (chaoAtual.GetComponentInParent<InteractableItem>() != null || chaoAtual.isTrigger)
        {
            return;
        }

        proximoPontoSeguro = Time.time + 0.1f;
        pontosSeguros.Add(new Vector3(rb.position.x, rb.position.y, Time.time));
        while (pontosSeguros.Count > 0 && pontosSeguros[0].z < Time.time - 3f)
        {
            pontosSeguros.RemoveAt(0);
        }
    }

    private bool ChaoEscorregadio()
    {
        if (chaoAtual == null)
        {
            return false;
        }

        var plataforma = chaoAtual.GetComponentInParent<FakePlatform>();
        return plataforma != null && plataforma.Escorregadia;
    }

    // Cor do jogador conforme o efeito ativo: roxo = lento, laranja =
    // controles invertidos, piscando em azul = escudo.
    private void PintarEfeitos()
    {
        if (spriteJogador == null)
        {
            return;
        }

        bool escudo = vida != null && vida.TemEscudo;
        if (!escudo && !EstaLento && !ControlesInvertidos)
        {
            if (corAlterada)
            {
                spriteJogador.color = Color.white;
                corAlterada = false;
            }
            return;
        }

        Color cor = Color.white;
        if (EstaLento) cor = new Color(0.72f, 0.55f, 1f, 1f);
        if (ControlesInvertidos) cor = new Color(1f, 0.62f, 0.3f, 1f);
        if (escudo)
        {
            // Pisca mais rápido no último segundo, avisando que vai acabar.
            float ritmo = vida.EscudoRestante < 1f ? 14f : 7f;
            if (Mathf.Repeat(Time.time * ritmo, 2f) < 1f) cor = new Color(0.45f, 0.82f, 1f, 1f);
        }

        spriteJogador.color = cor;
        corAlterada = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (chao.Entrar(collision))
        {
            AudioManager.TocarAterrissagem(collision.relativeVelocity.y);
        }
        UpdateChaoAtual(collision);
    }

    // Mantém o chão atual certo quando o jogador passa de um collider pro
    // outro (a saída do anterior chega depois da entrada no novo).
    private void OnCollisionStay2D(Collision2D collision)
    {
        chao.Permanecer(collision, rb.linearVelocity.y);

        if (chaoAtual != null && chaoAtual != collision.collider && chaoAtual.enabled && !chaoAtual.isTrigger)
        {
            return;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                chaoAtual = collision.collider;
                return;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        // Na saída o Collision2D pode vir sem pontos de contato: o chão
        // atual é limpo pelo collider, sem depender da normal.
        if (collision.collider == chaoAtual)
        {
            chaoAtual = null;
        }
        chao.Sair(collision);
    }

    private void UpdateChaoAtual(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                chaoAtual = collision.collider;
                return;
            }
        }
    }
}
