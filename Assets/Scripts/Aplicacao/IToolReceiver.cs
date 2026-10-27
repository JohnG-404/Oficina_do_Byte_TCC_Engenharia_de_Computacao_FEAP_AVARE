/// <summary>
/// Qualquer coisa que pode receber uma ferramenta arrastada do inventário
/// (ver DraggableToolIcon). Implementado por PhysicsTarget (objetos comuns
/// da fase) e por AplicacaoPlayerController (o Player, que além de
/// Física/Colisão também aceita Movimento/Pulo).
/// </summary>
public interface IToolReceiver
{
    void ApplyTool(ToolType tool);

    /// <summary>Se esta ferramenta faz sentido aqui (ex: Movimento e Pulo só valem no Player).</summary>
    bool CanReceive(ToolType tool);

    /// <summary>Se esta ferramenta já foi aplicada aqui — aplicar de novo não gasta um uso.</summary>
    bool HasTool(ToolType tool);
}
