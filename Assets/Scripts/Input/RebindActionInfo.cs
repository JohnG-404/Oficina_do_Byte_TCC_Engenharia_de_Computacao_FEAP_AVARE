using System;
using UnityEngine;

/// <summary>
/// Descreve uma ação (ou parte de uma ação composite) que pode ser exibida
/// e remapeada na tela de Configuração de Controles.
/// </summary>
[Serializable]
public class RebindActionInfo
{
    [Tooltip("Nome da InputAction no InputSystem_Actions (ex: Move, Jump, Interact)")]
    public string actionName;

    [Tooltip("Nome da parte do composite (ex: negative/positive). Deixe vazio para uma ação simples (botão único).")]
    public string compositePart;

    [Tooltip("Texto amigável exibido na UI (ex: Mover Esquerda, Pular)")]
    public string displayName;
}
