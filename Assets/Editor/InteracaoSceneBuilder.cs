using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Monta a cena "Interacao": chão, player, 4 itens interativos de exemplo
/// (cura, porta, lançador, objetivo) cobrindo as 5 Ações e as 4
/// Consequências possíveis, e a UI do assistente de configuração.
///
/// Uso: Tools > Interação > Montar Cena de Interação, com Interacao.unity aberta.
/// Idempotente — pode rodar de novo sem duplicar o que já existe.
/// </summary>
public static class InteracaoSceneBuilder
{
    [MenuItem("Tools/Interação/Montar Cena de Interação")]
    public static void BuildScene()
    {
        EnsureEventSystem();
        Canvas canvas = EnsureCanvas();

        BuildGround();
        GameObject player = BuildPlayer();

        InteractableItem healItem = BuildHealItem();
        InteractableItem doorItem = BuildDoorAndItem(healItem);
        InteractableItem launchItem = BuildLaunchItem();
        BuildLedge();
        InteractableItem goalItem = BuildGoalItem();

        InteractionConfigUI configUI = BuildConfigUI(canvas);
        BuildHealthBar(canvas);
        DialogueManager dialogueManager = DialogueBoxBuilder.EnsureDialogueBox(canvas);
        LevelCompleteUI completeUI = BuildLevelCompletePanel(canvas);
        BuildLevelFlow(completeUI, dialogueManager);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[InteracaoSceneBuilder] Cena montada. Ajuste posições/textos/velocidade de lançamento à vontade e salve (Ctrl+S).");
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        var go = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(go, "Criar EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    private static Canvas EnsureCanvas()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            return canvas;
        }

        var canvasGO = new GameObject("Canvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Criar Canvas");

        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        return canvas;
    }

    private static void BuildGround()
    {
        if (GameObject.Find("Ground") != null)
        {
            return;
        }

        var go = new GameObject("Ground");
        Undo.RegisterCreatedObjectUndo(go, "Criar Ground");
        go.transform.position = new Vector3(5f, -1f, 0f);
        go.transform.localScale = new Vector3(40f, 1f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSolidSprite();
        sr.color = new Color(0.35f, 0.3f, 0.28f);

        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        Physics2DTuning.PreventCornerSnag(col);
    }

    private static void BuildLedge()
    {
        if (GameObject.Find("Ledge") != null)
        {
            return;
        }

        var go = new GameObject("Ledge");
        Undo.RegisterCreatedObjectUndo(go, "Criar Ledge");
        go.transform.position = new Vector3(6.5f, 3f, 0f);
        go.transform.localScale = new Vector3(9f, 1f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSolidSprite();
        sr.color = new Color(0.35f, 0.3f, 0.28f);

        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        Physics2DTuning.PreventCornerSnag(col);
    }

    private static GameObject BuildPlayer()
    {
        GameObject existing = Object.FindAnyObjectByType<InteracaoPlayerController>()?.gameObject;
        if (existing != null)
        {
            if (!existing.CompareTag(InteractableItem.PlayerTag))
            {
                Undo.RecordObject(existing, "Tag Player");
                existing.tag = InteractableItem.PlayerTag;
            }
            return existing;
        }

        var go = new GameObject("Player");
        Undo.RegisterCreatedObjectUndo(go, "Criar Player");
        go.tag = InteractableItem.PlayerTag; // os itens só reagem à tag Player (ver InteractableItem.IsPlayer)
        go.transform.position = new Vector3(-12f, 0.5f, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSolidSprite();
        sr.color = new Color(0.3f, 0.8f, 1f);
        sr.sortingOrder = 1;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        go.AddComponent<Rigidbody2D>();
        go.AddComponent<InteracaoPlayerController>();
        go.AddComponent<PlayerHealth>();

        return go;
    }

    private static InteractableItem BuildHealItem()
    {
        InteractableItem existing = FindItemByName("PocaoDeCura");
        if (existing != null)
        {
            return existing;
        }

        GameObject go = CreateItemGO("PocaoDeCura", new Vector3(-6f, 0f, 0f), new Color(1f, 0.4f, 0.55f));
        InteractableItem item = go.AddComponent<InteractableItem>();

        var so = new SerializedObject(item);
        so.FindProperty("displayName").stringValue = "Poção de Cura";
        so.FindProperty("description").stringValue = "Restaura a vida do jogador.";
        SetEnumArray(so.FindProperty("allowedActions"), new[]
        {
            (int)ActionType.PressButton,
            (int)ActionType.WalkNear,
            (int)ActionType.JumpOnTop,
        });
        SetEnumArray(so.FindProperty("allowedConsequences"), new[] { (int)ConsequenceType.HealPlayer });
        so.ApplyModifiedPropertiesWithoutUndo();

        return item;
    }

    // A porta é, ao mesmo tempo, o objeto físico que bloqueia a passagem
    // (componente Door) e o item que o jogador configura (InteractableItem)
    // — clicar na porta abre o assistente; a única Consequência que faz
    // sentido pra ela é "Abrir uma porta".
    private static InteractableItem BuildDoorAndItem(InteractableItem requiredItem)
    {
        InteractableItem existing = FindItemByName("Porta");
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject("Porta");
        Undo.RegisterCreatedObjectUndo(go, "Criar Porta");
        go.transform.position = new Vector3(0f, 0.5f, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSolidSprite();
        sr.color = new Color(0.55f, 0.35f, 0.2f);
        go.transform.localScale = new Vector3(1f, 3f, 1f);

        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        var door = go.AddComponent<Door>();
        var doorSO = new SerializedObject(door);
        doorSO.FindProperty("blockingCollider").objectReferenceValue = col;
        doorSO.ApplyModifiedPropertiesWithoutUndo();

        InteractableItem item = go.AddComponent<InteractableItem>();
        var so = new SerializedObject(item);
        so.FindProperty("displayName").stringValue = "Porta";
        so.FindProperty("description").stringValue = "Bloqueia o caminho até ser aberta.";
        SetEnumArray(so.FindProperty("allowedActions"), new[]
        {
            (int)ActionType.PressButton,
            (int)ActionType.WalkNear,
            (int)ActionType.RequiresOtherItem,
        });
        SetEnumArray(so.FindProperty("allowedConsequences"), new[] { (int)ConsequenceType.OpenDoor });
        so.FindProperty("requiredItem").objectReferenceValue = requiredItem;
        so.FindProperty("doorToOpen").objectReferenceValue = door;
        so.ApplyModifiedPropertiesWithoutUndo();

        return item;
    }

    private static InteractableItem BuildLaunchItem()
    {
        InteractableItem existing = FindItemByName("Lancador");
        if (existing != null)
        {
            return existing;
        }

        GameObject go = CreateItemGO("Lancador", new Vector3(4f, 0f, 0f), new Color(0.6f, 0.4f, 1f));
        InteractableItem item = go.AddComponent<InteractableItem>();

        var so = new SerializedObject(item);
        so.FindProperty("displayName").stringValue = "Plataforma de Lançamento";
        so.FindProperty("description").stringValue = "Lança o jogador pra cima.";
        SetEnumArray(so.FindProperty("allowedActions"), new[]
        {
            (int)ActionType.JumpOnTop,
        });
        SetEnumArray(so.FindProperty("allowedConsequences"), new[] { (int)ConsequenceType.LaunchPlayer });
        so.FindProperty("launchVelocity").vector2Value = new Vector2(2f, 26f);
        so.ApplyModifiedPropertiesWithoutUndo();

        return item;
    }

    private static InteractableItem BuildGoalItem()
    {
        InteractableItem existing = FindItemByName("ObjetivoFinal");
        if (existing != null)
        {
            return existing;
        }

        GameObject go = CreateItemGO("ObjetivoFinal", new Vector3(8f, 4f, 0f), new Color(1f, 0.85f, 0.2f));
        InteractableItem item = go.AddComponent<InteractableItem>();

        var so = new SerializedObject(item);
        so.FindProperty("displayName").stringValue = "Objetivo";
        so.FindProperty("description").stringValue = "Conclui a fase.";
        SetEnumArray(so.FindProperty("allowedActions"), new[]
        {
            (int)ActionType.WalkNear,
            (int)ActionType.PressButton,
        });
        SetEnumArray(so.FindProperty("allowedConsequences"), new[] { (int)ConsequenceType.CompleteLevel, (int)ConsequenceType.RestartLevel });
        so.ApplyModifiedPropertiesWithoutUndo();

        return item;
    }

    private static GameObject CreateItemGO(string name, Vector3 position, Color color)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Criar {name}");
        go.transform.position = position;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSolidSprite();
        sr.color = color;

        return go;
    }

    private static InteractableItem FindItemByName(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<InteractableItem>() : null;
    }

    // Layout em 2 colunas lado a lado: Ação (sempre visível) e Consequência
    // (some até escolher uma Ação). Confirmar só fica clicável com as duas
    // escolhidas; Cancelar fecha sem aplicar nada.
    private static InteractionConfigUI BuildConfigUI(Canvas canvas)
    {
        Transform existing = canvas.transform.Find("InteractionConfigPanel");
        if (existing != null)
        {
            if (existing.Find("ActionButtonTemplate") != null)
            {
                return existing.GetComponent<InteractionConfigUI>(); // já tem os 2 templates
            }

            // Painel de uma versão anterior (sem templates separados por
            // coluna) — reconstrói do zero com os 2 templates.
            Object.DestroyImmediate(existing.gameObject);
        }

        var panelGO = new GameObject("InteractionConfigPanel", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(panelGO, "Criar InteractionConfigPanel");
        var panelRect = (RectTransform)panelGO.transform;
        panelRect.SetParent(canvas.transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(900f, 520f);

        var bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);

        GameObject titleGO = UIBuildUtils.CreateText(panelRect, "Title", "Item", 32, TextAlignmentOptions.Top);
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(-40f, 50f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);

        RectTransform actionOptions = BuildColumn(panelRect, "ActionColumn", "AÇÃO — como interagir", new Vector2(0f, 0.5f));
        RectTransform consequenceOptions = BuildColumn(panelRect, "ConsequenceColumn", "CONSEQUÊNCIA — o que acontece", new Vector2(0.5f, 1f));
        Transform consequenceColumnRoot = panelRect.Find("ConsequenceColumn");
        consequenceColumnRoot.gameObject.SetActive(false); // só aparece depois de escolher uma Ação

        // Um template por coluna — cada um pode ganhar uma cor/sprite/fonte
        // diferente no Editor, sem afetar o outro. Cores de base já saem
        // diferentes (azul pra Ação, laranja pra Consequência) como ponto de
        // partida; troque à vontade.
        Button actionTemplate = BuildOptionButtonTemplate(actionOptions, panelRect, "ActionButtonTemplate",
            baseColor: new Color(0.16f, 0.32f, 0.5f), indicatorColor: new Color(0.35f, 0.7f, 1f));
        Button consequenceTemplate = BuildOptionButtonTemplate(consequenceOptions, panelRect, "ConsequenceButtonTemplate",
            baseColor: new Color(0.5f, 0.32f, 0.16f), indicatorColor: new Color(1f, 0.7f, 0.35f));

        GameObject confirmGO = UIBuildUtils.CreateButtonWithLabel(panelRect, "ConfirmButton", "Salvar e Aplicar", out _);
        var confirmRect = confirmGO.GetComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(0.53f, 0f);
        confirmRect.anchorMax = new Vector2(0.97f, 0f);
        confirmRect.pivot = new Vector2(0.5f, 0f);
        confirmRect.sizeDelta = new Vector2(0f, 54f);
        confirmRect.anchoredPosition = new Vector2(0f, 20f);
        Button confirm = confirmGO.GetComponent<Button>();

        GameObject cancelGO = UIBuildUtils.CreateButtonWithLabel(panelRect, "CancelButton", "Cancelar", out _);
        var cancelRect = cancelGO.GetComponent<RectTransform>();
        cancelRect.anchorMin = new Vector2(0.03f, 0f);
        cancelRect.anchorMax = new Vector2(0.47f, 0f);
        cancelRect.pivot = new Vector2(0.5f, 0f);
        cancelRect.sizeDelta = new Vector2(0f, 54f);
        cancelRect.anchoredPosition = new Vector2(0f, 20f);
        Button cancel = cancelGO.GetComponent<Button>();

        var hintGO = new GameObject("HintToast", typeof(RectTransform));
        var hintRect = (RectTransform)hintGO.transform;
        hintRect.SetParent(canvas.transform, false);
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(900f, 60f);
        hintRect.anchoredPosition = new Vector2(0f, 40f);
        var hintBg = hintGO.AddComponent<Image>();
        hintBg.color = new Color(0f, 0f, 0f, 0.85f);

        GameObject hintTextGO = UIBuildUtils.CreateText(hintRect, "HintText", "", 22, TextAlignmentOptions.Center);
        UIBuildUtils.StretchToParent(hintTextGO.GetComponent<RectTransform>());

        InteractionConfigUI configUI = panelGO.AddComponent<InteractionConfigUI>();
        var so = new SerializedObject(configUI);
        so.FindProperty("panelRoot").objectReferenceValue = panelGO;
        so.FindProperty("titleText").objectReferenceValue = titleGO.GetComponent<TextMeshProUGUI>();
        so.FindProperty("actionOptionsContainer").objectReferenceValue = actionOptions;
        so.FindProperty("consequenceColumnRoot").objectReferenceValue = consequenceColumnRoot.gameObject;
        so.FindProperty("consequenceOptionsContainer").objectReferenceValue = consequenceOptions;
        so.FindProperty("actionButtonTemplate").objectReferenceValue = actionTemplate;
        so.FindProperty("consequenceButtonTemplate").objectReferenceValue = consequenceTemplate;
        so.FindProperty("confirmButton").objectReferenceValue = confirm;
        so.FindProperty("cancelButton").objectReferenceValue = cancel;
        so.FindProperty("hintRoot").objectReferenceValue = hintGO;
        so.FindProperty("hintText").objectReferenceValue = hintTextGO.GetComponent<TextMeshProUGUI>();
        so.ApplyModifiedPropertiesWithoutUndo();

        return configUI;
    }

    // Molde de botão de opção pra uma coluna: cor de base própria + um
    // filho "SelectedIndicator" (barra de destaque na lateral, desligada
    // por padrão) que o InteractionConfigUI liga/desliga pra marcar seleção
    // sem precisar sobrescrever a cor do botão — assim qualquer design que
    // você aplicar no template (cor, sprite, fonte) fica intacto.
    private static Button BuildOptionButtonTemplate(RectTransform column, RectTransform panelRect, string name, Color baseColor, Color indicatorColor)
    {
        GameObject templateGO = UIBuildUtils.CreateButtonWithLabel(column, name, "Opção", out _);
        templateGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 52f);
        templateGO.GetComponent<Image>().color = baseColor;

        var indicatorGO = new GameObject("SelectedIndicator", typeof(RectTransform));
        var indicatorRect = (RectTransform)indicatorGO.transform;
        indicatorRect.SetParent(templateGO.transform, false);
        indicatorRect.anchorMin = new Vector2(0f, 0f);
        indicatorRect.anchorMax = new Vector2(0f, 1f);
        indicatorRect.pivot = new Vector2(0f, 0.5f);
        indicatorRect.sizeDelta = new Vector2(8f, 0f);
        indicatorRect.anchoredPosition = Vector2.zero;

        var indicatorImage = indicatorGO.AddComponent<Image>();
        indicatorImage.color = indicatorColor;
        indicatorImage.raycastTarget = false;
        indicatorGO.SetActive(false);

        Button template = templateGO.GetComponent<Button>();
        template.transform.SetParent(panelRect, false); // fica "solto" como molde, fora das colunas
        templateGO.SetActive(false);

        return template;
    }

    // Meia coluna (título + lista vertical de botões) ancorada em uma
    // metade do painel — anchorMinX define se é a esquerda (0) ou a
    // direita (0.5) do painel.
    private static RectTransform BuildColumn(RectTransform parent, string name, string headerText, Vector2 anchorMinOverride)
    {
        var columnGO = new GameObject(name, typeof(RectTransform));
        var columnRect = (RectTransform)columnGO.transform;
        columnRect.SetParent(parent, false);
        columnRect.anchorMin = new Vector2(anchorMinOverride.x, 0f);
        columnRect.anchorMax = new Vector2(anchorMinOverride.x + 0.5f, 1f);
        columnRect.offsetMin = new Vector2(20f, 90f);
        columnRect.offsetMax = new Vector2(-20f, -90f);

        GameObject headerGO = UIBuildUtils.CreateText(columnRect, "Header", headerText, 20, TextAlignmentOptions.Top, new Color(0.75f, 0.8f, 0.9f));
        var headerRect = headerGO.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 30f);

        var optionsGO = new GameObject("OptionsContainer", typeof(RectTransform));
        var optionsRect = (RectTransform)optionsGO.transform;
        optionsRect.SetParent(columnRect, false);
        optionsRect.anchorMin = Vector2.zero;
        optionsRect.anchorMax = Vector2.one;
        optionsRect.offsetMin = Vector2.zero;
        optionsRect.offsetMax = new Vector2(0f, -40f);

        var layout = optionsGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return optionsRect;
    }

    private static void BuildHealthBar(Canvas canvas)
    {
        if (canvas.transform.Find("HealthBar") != null)
        {
            return;
        }

        Slider slider = UIBuildUtils.CreateSlider(canvas.transform, "HealthBar");
        var rect = slider.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(280f, 24f);
        rect.anchoredPosition = new Vector2(130f, -30f);
        slider.fillRect.GetComponent<Image>().color = new Color(0.85f, 0.2f, 0.25f);

        var healthUIGO = new GameObject("HealthUI");
        Undo.RegisterCreatedObjectUndo(healthUIGO, "Criar HealthUI");
        var healthUI = healthUIGO.AddComponent<InteracaoHealthUI>();

        var so = new SerializedObject(healthUI);
        so.FindProperty("healthSlider").objectReferenceValue = slider;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Mesmo painel genérico "Fase Concluída!" usado em AplicacaoSceneBuilder.
    private static LevelCompleteUI BuildLevelCompletePanel(Canvas canvas)
    {
        Transform existing = canvas.transform.Find("LevelCompletePanel");
        if (existing != null)
        {
            return existing.GetComponent<LevelCompleteUI>();
        }

        var panelGO = new GameObject("LevelCompletePanel", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(panelGO, "Criar LevelCompletePanel");
        var rect = (RectTransform)panelGO.transform;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);

        GameObject textGO = UIBuildUtils.CreateText(rect, "Text", "Fase Concluída!", 64, TextAlignmentOptions.Center);
        UIBuildUtils.StretchToParent(textGO.GetComponent<RectTransform>());

        LevelCompleteUI completeUI = panelGO.AddComponent<LevelCompleteUI>();
        panelGO.SetActive(false);
        return completeUI;
    }

    private static void BuildLevelFlow(LevelCompleteUI completeUI, DialogueManager dialogueManager)
    {
        InteracaoLevelFlow flow = Object.FindAnyObjectByType<InteracaoLevelFlow>();
        bool isNew = flow == null;
        if (isNew)
        {
            var go = new GameObject("InteracaoLevelFlow");
            Undo.RegisterCreatedObjectUndo(go, "Criar InteracaoLevelFlow");
            flow = go.AddComponent<InteracaoLevelFlow>();
        }

        var so = new SerializedObject(flow);
        so.FindProperty("completeUI").objectReferenceValue = completeUI;
        if (so.FindProperty("dialogueManager").objectReferenceValue == null)
        {
            so.FindProperty("dialogueManager").objectReferenceValue = dialogueManager;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnumArray(SerializedProperty arrayProp, int[] values)
    {
        arrayProp.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            // intValue, não enumValueIndex: ActionType pula o 4, então índice e valor não batem.
            arrayProp.GetArrayElementAtIndex(i).intValue = values[i];
        }
    }

    private static Sprite CreateSolidSprite()
    {
        var texture = new Texture2D(4, 4);
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }
}
