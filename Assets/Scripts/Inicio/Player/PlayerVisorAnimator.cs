using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Animações do player com visor (Laboratório Tecno): parado, correr, pular,
/// cair, aterrissar, agachar, andar agachado, dano e vitória.
///
/// Só troca o sprite — quem move o player continua sendo a física do
/// controlador de cada fase (Player, InteracaoPlayerController,
/// AplicacaoPlayerController). O estado é escolhido aqui a cada frame e
/// tocado direto por nome (Animator.Play), então o
/// PlayerVisorController.controller não tem transições.
///
/// Fica salvo nas cenas (Tools > Player > Salvar visor nas cenas), então o
/// editor já mostra o sprite do Play; Instalar só cobre player que ficou sem.
/// As cenas de Animação ficam de fora: lá o Animator é parte
/// do puzzle (AnimacaoPlayerFisica / AnimacaoController).
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerVisorAnimator : MonoBehaviour
{
    public const string ControllerResource = "PlayerVisor/PlayerVisorController";

    // Nomes dos estados no PlayerVisorController.controller.
    public const string EstadoParado = "Visor_Parado";
    public const string EstadoCorrer = "Visor_Correr";
    public const string EstadoPular = "Visor_Pular";
    public const string EstadoCair = "Visor_Cair";
    public const string EstadoAterrissar = "Visor_Aterrissar";
    public const string EstadoAgachar = "Visor_Agachar";
    public const string EstadoAndarAgachado = "Visor_AndarAgachado";
    public const string EstadoDano = "Visor_Dano";
    public const string EstadoVitoria = "Visor_Vitoria";

    [Tooltip("Velocidade horizontal mínima pra contar como correndo.")]
    [SerializeField] private float limiarCorrida = 0.1f;
    [Tooltip("Tempo de aterrissagem (duração do clip Visor_Aterrissar).")]
    [SerializeField] private float duracaoAterrissar = 4f / 14f;
    [Tooltip("Tempo do clip Visor_Dano.")]
    [SerializeField] private float duracaoDano = 6f / 14f;

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private PlayerHealth health;
    private PlayerAgachar agachar;
    private Func<bool> noChao;

    private string estadoAtual;
    private bool estavaNoChao = true;
    private float timerAterrissar;
    private float timerDano;
    private bool comemorando;
    private int vidaAnterior = int.MaxValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegistrarInstalacao()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
        Instalar();
    }

    private static void AoCarregarCena(Scene cena, LoadSceneMode modo) => Instalar();

    /// <summary>Coloca o Animator com visor em todo player das fases que ainda não tem.</summary>
    public static void Instalar()
    {
        var controller = Resources.Load<RuntimeAnimatorController>(ControllerResource);
        if (controller == null)
        {
            Debug.LogWarning("[PlayerVisorAnimator] Não achei Resources/" + ControllerResource);
            return;
        }
        foreach (var p in FindObjectsByType<Player>()) InstalarEm(p.gameObject, controller, () => p.IsGrounded);
        foreach (var p in FindObjectsByType<InteracaoPlayerController>()) InstalarEm(p.gameObject, controller, () => p.IsGrounded);
        foreach (var p in FindObjectsByType<AplicacaoPlayerController>()) InstalarEm(p.gameObject, controller, () => p.IsGrounded);
    }

    private static void InstalarEm(GameObject go, RuntimeAnimatorController controller, Func<bool> noChao)
    {
        if (go.GetComponent<PlayerVisorAnimator>() != null) return;
        if (go.GetComponent<AnimacaoPlayerFisica>() != null) return;
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        var anim = go.GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null) return;   // já tem outra animação
        if (anim == null) anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        sr.drawMode = SpriteDrawMode.Simple;
        var visor = go.AddComponent<PlayerVisorAnimator>();
        visor.noChao = noChao;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        // Já salvo na cena (Tools > Player > Salvar visor nas cenas): pega o "no chão" do controlador daqui.
        if (noChao == null)
        {
            var p = GetComponent<Player>();
            var ip = GetComponent<InteracaoPlayerController>();
            var ap = GetComponent<AplicacaoPlayerController>();
            if (p != null) noChao = () => p.IsGrounded;
            else if (ip != null) noChao = () => ip.IsGrounded;
            else if (ap != null) noChao = () => ap.IsGrounded;
        }
    }

    private void Start()
    {
        health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            vidaAnterior = health.CurrentHealth;
            health.OnHealthChanged += AoMudarVida;
        }
        Tocar(EstadoParado);
    }

    private void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= AoMudarVida;
    }

    private void AoMudarVida(int atual, int max)
    {
        if (atual < vidaAnterior) Dano();
        vidaAnterior = atual;
    }

    /// <summary>Toca a animação de dano (já é chamado sozinho quando o PlayerHealth perde vida).</summary>
    public void Dano()
    {
        timerDano = duracaoDano;
        Tocar(EstadoDano, reiniciar: true);
    }

    /// <summary>Pulinho de vitória em loop até chamar PararVitoria (ex.: ao concluir a fase).</summary>
    public void Vitoria() => comemorando = true;
    public void PararVitoria() => comemorando = false;

    private void Update()
    {
        // O Rigidbody2D pode ser criado pelo controlador depois do nosso Awake.
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        Vector2 v = rb != null ? rb.linearVelocity : Vector2.zero;
        bool chao = noChao != null ? noChao() : Mathf.Abs(v.y) < 0.01f;
        // Antes do puzzle de Física o player flutua sem gravidade: trata como no chão.
        if (rb != null && rb.gravityScale == 0f) chao = true;

        if (v.x > limiarCorrida) spriteRenderer.flipX = false;
        else if (v.x < -limiarCorrida) spriteRenderer.flipX = true;

        if (chao && !estavaNoChao) timerAterrissar = duracaoAterrissar;
        estavaNoChao = chao;
        timerAterrissar -= Time.deltaTime;
        timerDano -= Time.deltaTime;

        if (timerDano > 0f) return;
        if (comemorando) { Tocar(EstadoVitoria); return; }

        if (!chao)
        {
            Tocar(v.y > 0.05f ? EstadoPular : EstadoCair);
            return;
        }

        // Só agacha nas fases que têm o PlayerAgachar (o collider encolhe junto).
        if (agachar == null) agachar = GetComponent<PlayerAgachar>();
        bool agachado = agachar != null && agachar.Agachado;
        bool andando = Mathf.Abs(v.x) > limiarCorrida;

        if (agachado) Tocar(andando ? EstadoAndarAgachado : EstadoAgachar);
        else if (timerAterrissar > 0f && !andando) Tocar(EstadoAterrissar);
        else Tocar(andando ? EstadoCorrer : EstadoParado);
    }

    private void Tocar(string estado, bool reiniciar = false)
    {
        if (estado == estadoAtual && !reiniciar) return;
        // Andar agachado -> parar agachado não repete a entrada no agachamento.
        if (estado == EstadoAgachar && estadoAtual == EstadoAndarAgachado)
            animator.Play(estado, 0, 1f);
        else
            animator.Play(estado, 0, 0f);
        estadoAtual = estado;
    }
}
