/// <summary>
/// O que acontece quando um InteractableItem é ativado (ver
/// InteractionConfigUI — passo 2 da configuração: "o que acontece?").
/// </summary>
public enum ConsequenceType
{
    /// <summary>Restaura a vida do jogador (ver PlayerHealth).</summary>
    HealPlayer,

    /// <summary>Abre a Door referenciada no item.</summary>
    OpenDoor,

    /// <summary>Aplica um impulso de velocidade no jogador (ver InteracaoPlayerController.ApplyLaunch).</summary>
    LaunchPlayer,

    /// <summary>Conclui a fase (ver InteracaoLevelFlow).</summary>
    CompleteLevel,

    /// <summary>Reinicia a fase do zero (ver InteracaoLevelFlow.RestartLevel).</summary>
    RestartLevel,

    /// <summary>Liga o vaivém da MovingPlatform — antes disso ela fica parada.</summary>
    ActivateMovingPlatform,

    /// <summary>Soma 1 ao contador de cartões (ver InteracaoLevelFlow) e remove o item da cena.</summary>
    CollectCard,

    /// <summary>Tira 1 de vida do jogador (ver PlayerHealth.TakeDamage).</summary>
    DamageOneHeart,

    /// <summary>Tira 2 de vida do jogador (ver PlayerHealth.TakeDamage).</summary>
    DamageTwoHearts,

    /// <summary>Empurra o jogador pra longe do item (ver InteracaoPlayerController.ApplyLaunch).</summary>
    Repel,

    /// <summary>Some por um tempo e volta sozinha (ver FakePlatform.BreakAndRespawn).</summary>
    BreakAndRespawn,

    /// <summary>Sobe e volta pro lugar (ver FakePlatform.BounceAndReturn).</summary>
    BounceAndReturn,

    // Novas (13/10). Os números continuam na ordem, porque as cenas salvam
    // as opções como número.

    /// <summary>Laser: apaga por alguns segundos e liga de novo sozinho (ver LaserBarreira.Desligar).</summary>
    DisableForSeconds,

    /// <summary>Laser: começa desligado e liga quando o jogador sai da área, fechando o caminho de volta.</summary>
    TurnOnBehindPlayer,

    /// <summary>Espinhos: leva o jogador de volta ao último chão seguro (ver InteracaoPlayerController.VoltarAoPontoSeguro).</summary>
    ReturnToSafePoint,

    /// <summary>Espinhos: afundam no chão e deixam de machucar/bloquear. Em "Sempre", sobem de novo depois de um tempo.</summary>
    SinkIntoGround,

    /// <summary>Vida: escudo que ignora o dano por alguns segundos (ver PlayerHealth.AtivarEscudo).</summary>
    ShieldForSeconds,

    /// <summary>Vida: +1 de vida máxima, com um quadrado novo no HUD (ver PlayerHealth.AumentarVidaMaxima).</summary>
    IncreaseMaxHealth,

    /// <summary>Vida Falsa: deixa o jogador lento por alguns segundos (ver InteracaoPlayerController.AplicarLentidao).</summary>
    SlowPlayer,

    /// <summary>Vida Falsa: inverte esquerda e direita por alguns segundos (ver InteracaoPlayerController.InverterControles).</summary>
    InvertControls,

    /// <summary>Plataforma falsa: aguenta um instante, cai e depois volta (ver FakePlatform.FallAfterDelay).</summary>
    FallAfterDelay,

    /// <summary>Plataforma falsa: fica escorregadia, sem atrito (ver FakePlatform.MakeSlippery).</summary>
    MakeSlippery,

    /// <summary>Plataforma de lançamento: lança pro lado em vez de pra cima.</summary>
    LaunchSideways,

    /// <summary>Porta: abre só por alguns segundos e fecha de novo (ver Door.OpenFor).</summary>
    OpenDoorForSeconds,

    /// <summary>Parede: fica atravessável enquanto o jogador está perto e volta a bloquear quando ele se afasta (ver ParedeFalsa.DefinirAtravessavel).</summary>
    PassableWhileNear,

    /// <summary>Plataforma falsa: vira plataforma de mão única — dá pra atravessar pulando por baixo e ficar em cima (PlatformEffector2D).</summary>
    OneWayFromBelow,

    // Novas (23/10): lâmpadas do teto da Interacao_Vida.

    /// <summary>Lâmpada: acende e fica acesa (ver LampadaInteracao.Acender).</summary>
    TurnOnLamp,

    /// <summary>Lâmpada: acende por alguns segundos e apaga de novo (ver LampadaInteracao.AcenderPor).</summary>
    TurnOnLampForSeconds
}
