using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gerenciador central do Input System: mantém referência ao InputActionAsset,
/// expõe as ações usadas pelo gameplay, controla o rebinding em tempo de execução
/// e persiste/carrega os binding overrides via PlayerPrefs.
///
/// Segue o mesmo padrão de singleton persistente já usado por GameManager.
/// </summary>
[DisallowMultipleComponent]
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    [Header("Input Actions Asset")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Ações exibidas na tela de Configuração de Controles")]
    [SerializeField]
    private List<RebindActionInfo> rebindableActions = new List<RebindActionInfo>
    {
        new RebindActionInfo { actionName = "Move", compositePart = "negative", displayName = "Mover Esquerda" },
        new RebindActionInfo { actionName = "Move", compositePart = "positive", displayName = "Mover Direita" },
        new RebindActionInfo { actionName = "Jump", compositePart = "", displayName = "Pular" },
        new RebindActionInfo { actionName = CrouchActionName, compositePart = "", displayName = "Agachar" },
        new RebindActionInfo { actionName = "Interact", compositePart = "", displayName = "Interagir" },
        new RebindActionInfo { actionName = "Sprint", compositePart = "", displayName = "Correr" },
        new RebindActionInfo { actionName = RestartActionName, compositePart = "", displayName = "Reiniciar fase" },
    };

    /// <summary>Ação "Reiniciar fase" (padrão R): segurar reinicia, na pausa abre a confirmação (ver ReinicioFase).</summary>
    public const string RestartActionName = "Restart";

    /// <summary>Ação "Agachar" (padrão C), usada na Animação (ver PlayerAgachar).</summary>
    public const string CrouchActionName = "Crouch";

    private const string BindingsPlayerPrefsKey = "InputBindingOverrides";

    /// <summary>Disparado sempre que um binding muda (rebind aplicado, reset ou load).</summary>
    public event Action OnBindingsChanged;

    /// <summary>
    /// Aviso curto para uma linha da aba Controles (actionName, compositePart, texto),
    /// ex: depois que duas ações trocaram de tecla.
    /// </summary>
    public event Action<string, string, string> OnAvisoLinha;

    private InputActionRebindingExtensions.RebindingOperation activeRebindOperation;

    public IReadOnlyList<RebindActionInfo> RebindableActions => rebindableActions;
    public bool IsRebinding => activeRebindOperation != null;

    public InputAction MoveAction { get; private set; }
    public InputAction JumpAction { get; private set; }
    public InputAction InteractAction { get; private set; }
    public InputAction SprintAction { get; private set; }
    public InputAction CrouchAction { get; private set; }
    public InputAction RestartAction { get; private set; }
    public InputAction SubmitAction { get; private set; }
    public InputAction UICancelAction { get; private set; }

    /// <summary>
    /// Garante que exista um InputManager mesmo em cenas testadas isoladamente
    /// (sem passar pelo Menu). Roda DEPOIS que a primeira cena e seus Awake()
    /// já executaram, então uma instância colocada manualmente na cena (com uma
    /// lista de rebindableActions customizada no Inspector) sempre tem prioridade
    /// sobre esta instância automática de fallback.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        var asset = Resources.Load<InputActionAsset>("InputSystem_Actions");
        if (asset == null)
        {
            Debug.LogError("[InputManager] Não encontrei Resources/InputSystem_Actions.inputactions para o bootstrap automático.");
            return;
        }

        // O GameObject é criado inativo para adiar o Awake() até o campo
        // inputActions estar atribuído — AddComponent() chama Awake() de
        // forma síncrona e imediata, então atribuir o campo depois seria tarde demais.
        var go = new GameObject("InputManager (Auto)");
        go.SetActive(false);

        var manager = go.AddComponent<InputManager>();
        manager.inputActions = asset;

        go.SetActive(true);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (inputActions == null)
        {
            Debug.LogError("[InputManager] Nenhum InputActionAsset atribuído no Inspector.");
            return;
        }

        ResolveActionReferences();
        GarantirLinhasNovas();
        LoadBindings();
        EnableActionMaps();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            activeRebindOperation?.Dispose();
        }
    }

    private void ResolveActionReferences()
    {
        var player = inputActions.FindActionMap("Player", throwIfNotFound: true);
        var ui = inputActions.FindActionMap("UI", throwIfNotFound: true);

        MoveAction = player.FindAction("Move", throwIfNotFound: true);
        JumpAction = player.FindAction("Jump", throwIfNotFound: true);
        InteractAction = player.FindAction("Interact", throwIfNotFound: true);
        SprintAction = player.FindAction("Sprint", throwIfNotFound: true);
        CrouchAction = player.FindAction(CrouchActionName, throwIfNotFound: true);
        RestartAction = player.FindAction(RestartActionName, throwIfNotFound: true);
        SubmitAction = ui.FindAction("Submit", throwIfNotFound: true);
        UICancelAction = ui.FindAction("Cancel", throwIfNotFound: true);
    }

    /// <summary>
    /// Instâncias salvas em cena antes de "Agachar" e "Reiniciar fase" estarem na
    /// lista têm a lista antiga no Inspector: completa aqui pra a aba Controles mostrar as linhas.
    /// </summary>
    private void GarantirLinhasNovas()
    {
        if (!rebindableActions.Exists(info => info.actionName == CrouchActionName))
        {
            int pular = rebindableActions.FindIndex(info => info.actionName == "Jump");
            rebindableActions.Insert(pular >= 0 ? pular + 1 : rebindableActions.Count,
                new RebindActionInfo { actionName = CrouchActionName, compositePart = "", displayName = "Agachar" });
        }

        if (!rebindableActions.Exists(info => info.actionName == RestartActionName))
        {
            rebindableActions.Add(new RebindActionInfo { actionName = RestartActionName, compositePart = "", displayName = "Reiniciar fase" });
        }
    }

    private void EnableActionMaps()
    {
        inputActions.FindActionMap("Player").Enable();
        inputActions.FindActionMap("UI").Enable();
    }

    /// <summary>
    /// Retorna o nome amigável da tecla/botão atualmente vinculado a uma ação,
    /// em português (ver TextoTeclas.Traduzir).
    /// Ex: GetBindingName("Jump") -> "Espaço", "W" ou "Button South".
    /// </summary>
    public string GetBindingName(string actionName, string compositePart = "")
    {
        InputAction action = inputActions != null ? inputActions.FindAction(actionName) : null;
        if (action == null)
        {
            return string.Empty;
        }

        int bindingIndex = FindBindingIndex(action, compositePart);
        if (bindingIndex < 0)
        {
            return string.Empty;
        }

        return TextoTeclas.Traduzir(action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions));
    }

    /// <summary>
    /// Inicia o rebinding interativo de uma ação (ou parte de composite).
    /// Usa PerformInteractiveRebinding — nada de Input.GetKey manual.
    /// Se a tecla escolhida já pertence a outra ação, as duas trocam de tecla
    /// (ver TrocarSeEmUso) e as linhas recebem um aviso por OnAvisoLinha.
    /// </summary>
    public void StartRebind(string actionName, string compositePart, Action<string> onComplete, Action onCancel)
    {
        InputAction action = inputActions != null ? inputActions.FindAction(actionName) : null;
        if (action == null)
        {
            onCancel?.Invoke();
            return;
        }

        int bindingIndex = FindBindingIndex(action, compositePart);
        if (bindingIndex < 0)
        {
            onCancel?.Invoke();
            return;
        }

        // Cancela qualquer rebind anterior ainda em andamento antes de iniciar um novo.
        activeRebindOperation?.Cancel();

        // A tecla de antes vai para a outra ação se a nova já estiver em uso.
        string pathAnterior = action.bindings[bindingIndex].effectivePath;

        action.Disable();

        activeRebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("Mouse")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.05f)
            .OnCancel(operation =>
            {
                CleanupRebindOperation();
                action.Enable();
                onCancel?.Invoke();
            })
            .OnComplete(operation =>
            {
                CleanupRebindOperation();

                var avisos = TrocarSeEmUso(action, bindingIndex, pathAnterior);

                action.Enable();
                SaveBindings();
                OnBindingsChanged?.Invoke();
                onComplete?.Invoke(GetBindingName(actionName, compositePart));

                // Depois do onComplete, que devolve o nome da linha ao normal.
                if (avisos != null)
                {
                    foreach (var aviso in avisos)
                    {
                        OnAvisoLinha?.Invoke(aviso.info.actionName, aviso.info.compositePart, aviso.texto);
                    }
                }
            })
            .Start();
    }

    /// <summary>Cancela o rebind em andamento, se houver (ex: botão Cancelar da UI).</summary>
    public void CancelActiveRebind()
    {
        activeRebindOperation?.Cancel();
    }

    private void CleanupRebindOperation()
    {
        activeRebindOperation?.Dispose();
        activeRebindOperation = null;
    }

    private static int FindBindingIndex(InputAction action, string compositePart)
    {
        var bindings = action.bindings;

        for (int i = 0; i < bindings.Count; i++)
        {
            InputBinding binding = bindings[i];

            if (!string.IsNullOrEmpty(compositePart))
            {
                if (binding.isPartOfComposite &&
                    string.Equals(binding.name, compositePart, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            else if (!binding.isComposite && !binding.isPartOfComposite)
            {
                // Prioriza o binding do esquema Teclado/Mouse — é o alvo do rebind na UI.
                if (string.IsNullOrEmpty(binding.groups) || binding.groups.Contains("Keyboard&Mouse"))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Se a tecla nova já pertence a outra ação, a outra fica com a tecla de antes
    /// (troca), igual na tela e no save. Retorna os avisos das linhas, ou null.
    /// </summary>
    private List<(RebindActionInfo info, string texto)> TrocarSeEmUso(InputAction action, int bindingIndex, string pathAnterior)
    {
        InputAction outra = FindDuplicateBindingAction(action, bindingIndex, out int outroIndex);
        if (outra == null)
        {
            return null;
        }

        InputBinding esta = action.bindings[bindingIndex];
        InputBinding outroBinding = outra.bindings[outroIndex];

        // Ex: ← em Mover Esquerda, que já é a segunda tecla dela: nada a trocar.
        if (outra == action && esta.isPartOfComposite &&
            string.Equals(outroBinding.name, esta.name, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        RebindActionInfo linhaEsta = LinhaDoBinding(action, bindingIndex);
        RebindActionInfo linhaOutra = LinhaDoBinding(outra, outroIndex);
        var avisos = new List<(RebindActionInfo info, string texto)>();

        if (linhaOutra != null)
        {
            outra.ApplyBindingOverride(outroIndex, pathAnterior);
            if (linhaEsta != null)
            {
                avisos.Add((linhaEsta, "trocou com " + linhaOutra.displayName));
            }
            avisos.Add((linhaOutra, "agora usa " + GetBindingName(linhaOutra.actionName, linhaOutra.compositePart)));
        }
        else
        {
            // Segunda tecla que não aparece na lista (as setas do Mover): sai daquela
            // ação em vez de receber a tecla de antes, que ficaria escondida.
            outra.ApplyBindingOverride(outroIndex, string.Empty);
            if (linhaEsta != null)
            {
                RebindActionInfo dona = rebindableActions.Find(info => info.actionName == outra.name &&
                    string.Equals(info.compositePart ?? "", outroBinding.isPartOfComposite ? outroBinding.name : "", StringComparison.OrdinalIgnoreCase));
                string nomeDona = dona != null ? dona.displayName : outra.name;
                avisos.Add((linhaEsta, GetBindingName(linhaEsta.actionName, linhaEsta.compositePart) + " saiu de " + nomeDona));
            }
        }

        return avisos;
    }

    /// <summary>A linha da aba Controles que mostra este binding, ou null se ele não aparece na lista.</summary>
    private RebindActionInfo LinhaDoBinding(InputAction action, int bindingIndex)
    {
        foreach (RebindActionInfo info in rebindableActions)
        {
            if (info.actionName == action.name && FindBindingIndex(action, info.compositePart) == bindingIndex)
            {
                return info;
            }
        }

        return null;
    }

    /// <summary>Retorna a ação que já usa a mesma tecla/botão (e o índice do binding), ou null se não houver conflito.</summary>
    private static InputAction FindDuplicateBindingAction(InputAction action, int bindingIndex, out int duplicateIndex)
    {
        InputBinding newBinding = action.bindings[bindingIndex];
        duplicateIndex = -1;

        foreach (InputAction otherAction in action.actionMap.actions)
        {
            var bindings = otherAction.bindings;

            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];

                if (binding.isComposite)
                {
                    continue;
                }

                if (otherAction == action && i == bindingIndex)
                {
                    continue;
                }

                if (binding.effectivePath == newBinding.effectivePath)
                {
                    duplicateIndex = i;
                    return otherAction;
                }
            }
        }

        return null;
    }

    public void SaveBindings()
    {
        if (inputActions == null) return;

        string json = inputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(BindingsPlayerPrefsKey, json);
        PlayerPrefs.Save();
    }

    public void LoadBindings()
    {
        if (inputActions == null) return;

        if (PlayerPrefs.HasKey(BindingsPlayerPrefsKey))
        {
            string json = PlayerPrefs.GetString(BindingsPlayerPrefsKey);
            if (!string.IsNullOrEmpty(json))
            {
                inputActions.LoadBindingOverridesFromJson(json);
            }
        }

        OnBindingsChanged?.Invoke();
    }

    public void ResetBindings()
    {
        if (inputActions == null) return;

        // Uma troca esperando tecla seria aplicada por cima do padrão restaurado.
        CancelActiveRebind();

        foreach (InputActionMap map in inputActions.actionMaps)
        {
            map.RemoveAllBindingOverrides();
        }

        PlayerPrefs.DeleteKey(BindingsPlayerPrefsKey);
        PlayerPrefs.Save();

        OnBindingsChanged?.Invoke();
    }
}
