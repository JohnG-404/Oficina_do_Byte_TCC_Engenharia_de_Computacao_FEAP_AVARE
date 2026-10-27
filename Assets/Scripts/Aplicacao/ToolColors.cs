using UnityEngine;

/// <summary>
/// Cor de cada ferramenta do inventário — usada nos cartões do inventário
/// (AplicacaoInventarioBuilder) e nos anéis de confirmação visual que aparecem
/// no objeto ou no Player quando a ferramenta é aplicada (ver
/// ToolOutlineFactory, PhysicsTarget, AplicacaoPlayerController). Centralizada
/// aqui pra cartão e anel nunca ficarem com cores diferentes da mesma
/// ferramenta. Os valores são as cores dos próprios ícones
/// (Images/Scene_Col_Rig/icon_*.png).
/// </summary>
public static class ToolColors
{
    public static readonly Color Fisica = new Color(0.231f, 0.514f, 0.925f);
    public static readonly Color Colisao = new Color(0.231f, 0.827f, 0.157f);
    public static readonly Color Movimento = new Color(0.996f, 0.588f, 0.169f);
    public static readonly Color Pulo = new Color(0.588f, 0.353f, 0.996f);

    public static Color For(ToolType tool)
    {
        switch (tool)
        {
            case ToolType.Fisica: return Fisica;
            case ToolType.Colisao: return Colisao;
            case ToolType.Movimento: return Movimento;
            case ToolType.Pulo: return Pulo;
            default: return Color.white;
        }
    }
}
