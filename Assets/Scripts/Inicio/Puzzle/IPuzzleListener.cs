/// <summary>
/// Quem quer ser avisado quando um PuzzleControl é resolvido. O
/// GameFlowManager (cena Fisica) já é ligado direto pelo campo flowManager;
/// cenas novas (ex: IA) usam o campo "listener" do PuzzleControl com qualquer
/// MonoBehaviour que implemente esta interface.
/// </summary>
public interface IPuzzleListener
{
    void OnPuzzleCompleted(PuzzleControl completedPuzzle);
}
