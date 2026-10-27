using UnityEngine;

/// <summary>
/// Objeto escondido (criado pelo RegistroAprendizagem, sobrevive às trocas de
/// cena) que confere a cada quadro se a fase está em pausa — menu de pausa,
/// Configurações ou janela fora de foco — e avisa quando a janela ganha ou
/// perde o foco. Também tenta de novo as linhas do CSV que ficaram guardadas
/// e encerra a cena e a sessão no registro quando o jogo fecha. Não precisa
/// ser colocado em nenhuma cena.
/// </summary>
public class RelogioPausa : MonoBehaviour
{
    private void Update()
    {
        RegistroAprendizagem.AtualizarPausa();
        RegistroAprendizagem.TentarGravarPendentes();
    }

    private void OnApplicationFocus(bool emFoco)
    {
        RegistroAprendizagem.MudouFoco(emFoco);
    }

    private void OnApplicationQuit()
    {
        RegistroAprendizagem.AoFecharJogo();
    }
}
