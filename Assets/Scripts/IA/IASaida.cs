using System;
using UnityEngine;

/// <summary>
/// Saída das fases IA e IA_Aplicacao: um gatilho que avisa quando o jogador
/// chega. Pode começar fechada (na fase IA só abre depois do puzzle 3): a
/// "Barreira" (collider sólido + sprite) impede a passagem até
/// <see cref="Aberta"/> virar true.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IASaida : MonoBehaviour
{
    public event Action OnJogadorChegou;

    [SerializeField] private bool aberta = true;
    [Tooltip("Ativo enquanto a saída está fechada (collider sólido + sprite).")]
    [SerializeField] private GameObject barreira;
    [Tooltip("Opcional: ativo só com a saída aberta (ex: luz verde).")]
    [SerializeField] private GameObject visualAberta;

    private bool disparou;

    /// <summary>Collider sólido que segura o jogador enquanto a saída está fechada (pode ser null). Usado pela PortaSaida.</summary>
    public GameObject Barreira => barreira;

    public bool Aberta
    {
        get => aberta;
        set
        {
            if (value != aberta)
            {
                AudioManager.TocarEfeito(value ? AudioManager.Efeito.PortaAbrir : AudioManager.Efeito.PortaFechar);
            }
            aberta = value;
            AtualizarVisual();
        }
    }

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        AtualizarVisual();
    }

    private void AtualizarVisual()
    {
        if (barreira != null) barreira.SetActive(!aberta);
        if (visualAberta != null) visualAberta.SetActive(aberta);
    }

    private void OnTriggerEnter2D(Collider2D other) => Checar(other);

    // Cobre o caso de o jogador já estar dentro da área quando a saída abre.
    private void OnTriggerStay2D(Collider2D other) => Checar(other);

    private void Checar(Collider2D other)
    {
        if (!aberta || disparou)
        {
            return;
        }

        bool ehJogador = other.GetComponentInParent<InteracaoPlayerController>() != null || other.CompareTag("Player");
        if (!ehJogador)
        {
            return;
        }

        disparou = true;
        OnJogadorChegou?.Invoke();
    }
}
