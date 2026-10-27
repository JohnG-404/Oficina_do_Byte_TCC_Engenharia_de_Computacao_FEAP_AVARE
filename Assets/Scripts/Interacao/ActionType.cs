/// <summary>
/// Formas de interação que o jogador pode escolher pra um InteractableItem
/// (ver InteractionConfigUI — passo 1 da configuração: "como interagir?").
/// </summary>
public enum ActionType
{
    /// <summary>Precisa estar perto e apertar o botão de Interagir.</summary>
    PressButton,

    /// <summary>Dispara assim que o jogador entra na área do item, sem apertar nada.</summary>
    WalkNear,

    /// <summary>Dispara quando o jogador pousa em cima do item (ver ItemTopZone).</summary>
    JumpOnTop,

    /// <summary>Só dispara (ao passar perto) se outro item (RequiredItem) já tiver sido ativado antes.</summary>
    RequiresOtherItem,

    // O valor 4 era "arrastar um item-chave" (removido). Os números abaixo
    // ficam fixos porque as cenas salvam a escolha como número.

    /// <summary>Só dispara se o jogador já tiver coletado ao menos 1 cartão de acesso (ConsequenceType.CollectCard).</summary>
    RequiresOneCard = 5,

    /// <summary>Só dispara se o jogador já tiver coletado ao menos 2 cartões de acesso (ConsequenceType.CollectCard).</summary>
    RequiresTwoCards = 6,

    /// <summary>Dispara no toque físico direto (colisão sólida com o próprio corpo do item), diferente de WalkNear (zona de proximidade, mais generosa).</summary>
    Touch = 7,
    /// <summary>Dispara depois que o jogador fica 1 segundo em cima do item (TopZone + OnTriggerStay2D).</summary>
    StayOnTop = 8,
    /// <summary>Dispara quando o jogador sai da área do item (OnTriggerExit2D).</summary>
    LeaveArea = 9,
    /// <summary>Só dispara (ao passar perto) se a vida do jogador não estiver cheia.</summary>
    RequiresMissingHealth = 10,
    /// <summary>Precisa apertar o botão de Interagir 3 vezes perto do item.</summary>
    PressButtonThreeTimes = 11
}
