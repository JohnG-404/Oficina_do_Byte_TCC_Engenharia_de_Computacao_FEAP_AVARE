using UnityEngine;

/// <summary>
/// Esconde objetos enquanto o Prof está falando (DialogueManager.IsAnyDialogueActive)
/// e mostra de novo quando a fala acaba. Na Oficina de Animação a caixa
/// do Prof fica no canto de baixo, em cima da faixa "como jogar" e da barra de
/// teclas; elas somem durante a fala em vez de aparecerem cortadas atrás dela.
/// No Editor os objetos continuam ligados (o script só age em Play).
/// </summary>
public class EsconderDuranteFala : MonoBehaviour
{
    [SerializeField] private GameObject[] alvos;

    private bool escondidos;

    private void Update()
    {
        bool falando = DialogueManager.IsAnyDialogueActive;
        if (falando == escondidos)
        {
            return;
        }
        escondidos = falando;
        foreach (GameObject alvo in alvos)
        {
            if (alvo != null)
            {
                alvo.SetActive(!falando);
            }
        }
    }

    private void OnDisable()
    {
        // Ao sair (troca de cena, desligar o Cenario), deixa tudo visível de novo.
        if (escondidos)
        {
            escondidos = false;
            foreach (GameObject alvo in alvos)
            {
                if (alvo != null)
                {
                    alvo.SetActive(true);
                }
            }
        }
    }
}
