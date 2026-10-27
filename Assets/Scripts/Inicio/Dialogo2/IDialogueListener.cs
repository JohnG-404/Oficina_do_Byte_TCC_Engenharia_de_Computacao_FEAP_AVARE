/// <summary>
/// Quem quiser ser avisado quando um diálogo do DialogueManager terminar
/// implementa isso (ver DialogueManager.SetListener). GameFlowManager (cena
/// Inicio) e AplicacaoLevelFlow (cena Aplicacao) implementam essa interface.
/// </summary>
public interface IDialogueListener
{
    void OnDialogueFinished();
}
