using System.Collections;
using UnityEngine;

/// <summary>
/// Lâmpada do teto da Interacao_Vida. Usa o mesmo Animator Controller das
/// lâmpadas da sala escura da fase Animacao (LampadaFase: Bool "ligada",
/// Apagada &lt;-&gt; Acesa). O brilho é o filho LuzLampada, que o clip
/// Lampada_Acender clareia junto com os quadros da lâmpada.
///
/// Começa apagada. Quem acende é o InteractableItem do mesmo objeto, depois
/// que o jogador escolhe "Acender a lâmpada" ou "Acender por 5 segundos" no
/// painel Configurar item. Montada na cena por Tools > Interação > Lâmpadas
/// configuráveis (Vida).
/// </summary>
[RequireComponent(typeof(Animator))]
public class LampadaInteracao : MonoBehaviour
{
    private static readonly int ParamLigada = Animator.StringToHash(LampadaSensor.ParamLigada);

    private Animator animator;
    private Coroutine apagarDepois;

    public bool Acesa => animator != null && animator.GetBool(ParamLigada);

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void Acender()
    {
        PararTempo();
        Definir(true);
    }

    public void AcenderPor(float segundos)
    {
        PararTempo();
        Definir(true);
        apagarDepois = StartCoroutine(ApagarDepois(segundos));
    }

    public void Apagar()
    {
        PararTempo();
        Definir(false);
    }

    private IEnumerator ApagarDepois(float segundos)
    {
        yield return new WaitForSeconds(segundos);
        apagarDepois = null;
        Definir(false);
    }

    private void PararTempo()
    {
        if (apagarDepois != null)
        {
            StopCoroutine(apagarDepois);
            apagarDepois = null;
        }
    }

    private void Definir(bool acesa)
    {
        if (animator == null || animator.GetBool(ParamLigada) == acesa)
        {
            return;
        }

        animator.SetBool(ParamLigada, acesa);
        AudioManager.TocarEfeito(AudioManager.Efeito.Alavanca);
    }
}
