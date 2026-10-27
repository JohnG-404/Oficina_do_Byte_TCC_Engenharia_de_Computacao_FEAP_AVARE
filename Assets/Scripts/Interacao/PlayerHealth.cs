using System;
using UnityEngine;

/// <summary>
/// Vida do jogador na cena "Interacao" — usada pela Consequência "Curar o
/// jogador" (ver InteractableItem/ConsequenceType.HealPlayer).
///
/// Começa machucado de propósito (não no máximo), pra "curar" ter um efeito
/// visível de verdade quando o jogador testar essa consequência.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int startingHealth = 35;

    [Tooltip("Segundos de invencibilidade após tomar dano — evita perder toda a vida de uma vez encostado num perigo (ex: laser) que dispara toda hora.")]
    [SerializeField] private float damageCooldown = 1f;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    /// <summary>
    /// Enquanto true, TakeDamage é ignorado. Usado pela mini-lição de HUD da
    /// Interacao_Vida (ver HudLicao) pra nenhuma armadilha tirar vida com a
    /// lição na tela. Padrão: false.
    /// </summary>
    public bool Invulneravel { get; set; }

    /// <summary>Disparado sempre que a vida muda: (atual, máximo) — usado pela InteracaoHealthUI.</summary>
    public event Action<int, int> OnHealthChanged;

    /// <summary>Disparado uma única vez, quando a vida chega a 0.</summary>
    public event Action OnDeath;

    private float lastDamageTime = -999f;
    private float escudoAte = -1f;

    /// <summary>Limite da Consequência "Aumentar a vida máxima" (o HUD não cabe muito mais).</summary>
    public const int VidaMaximaLimite = 6;

    /// <summary>Escudo da Consequência "Dar escudo" ligado agora (o InteracaoPlayerController faz o jogador piscar).</summary>
    public bool TemEscudo => Time.time < escudoAte;

    /// <summary>Segundos que faltam de escudo (0 sem escudo).</summary>
    public float EscudoRestante => Mathf.Max(0f, escudoAte - Time.time);

    private void Awake()
    {
        CurrentHealth = Mathf.Clamp(startingHealth, 0, maxHealth);
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void Heal(int amount)
    {
        CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void HealFully()
    {
        CurrentHealth = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>Consequência "Dar escudo": ignora todo dano por alguns segundos.</summary>
    public void AtivarEscudo(float segundos)
    {
        escudoAte = Time.time + segundos;
    }

    /// <summary>
    /// Consequência "Aumentar a vida máxima": +1 no máximo (e na vida atual),
    /// e o HUD ganha um quadrado novo pelo OnHealthChanged.
    /// </summary>
    public void AumentarVidaMaxima(int quanto)
    {
        if (IsDead || maxHealth >= VidaMaximaLimite)
        {
            return;
        }

        maxHealth = Mathf.Min(VidaMaximaLimite, maxHealth + quanto);
        CurrentHealth = Mathf.Clamp(CurrentHealth + quanto, 0, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || Invulneravel || TemEscudo || Time.time - lastDamageTime < damageCooldown)
        {
            return;
        }

        lastDamageTime = Time.time;
        CurrentHealth = Mathf.Clamp(CurrentHealth - amount, 0, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0)
        {
            IsDead = true;
            OnDeath?.Invoke();
        }
    }
}
