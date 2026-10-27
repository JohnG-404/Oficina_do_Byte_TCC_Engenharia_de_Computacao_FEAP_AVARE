/// <summary>Ferramentas que o jogador pode arrastar do inventário até um objeto (ou até o Player).</summary>
public enum ToolType
{
    Fisica,
    Colisao,

    /// <summary>Só tem efeito no Player: libera o movimento horizontal.</summary>
    Movimento,

    /// <summary>Só tem efeito no Player: libera o pulo.</summary>
    Pulo
}
