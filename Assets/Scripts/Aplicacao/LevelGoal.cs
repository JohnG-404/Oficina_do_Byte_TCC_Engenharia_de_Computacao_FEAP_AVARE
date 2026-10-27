using System;
using UnityEngine;

/// <summary>
/// Objetivo da fase "Aplicacao" — por enquanto, um gatilho circular no
/// cenário. Quando o Player entra nele, a fase é considerada concluída (ver
/// AplicacaoLevelFlow, que reage a OnPlayerReachedGoal).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LevelGoal : MonoBehaviour
{
    public event Action OnPlayerReachedGoal;

    private bool triggered;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || other.GetComponent<AplicacaoPlayerController>() == null)
        {
            return;
        }

        triggered = true;
        OnPlayerReachedGoal?.Invoke();
    }
}
