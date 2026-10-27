using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Monta a cena "Aplicacao" (Canvas + inventário arrastável + alguns objetos
/// de teste). Rode com a cena Aplicacao.unity aberta.
///
/// Uso: Tools > Aplicação > Montar Cena de Aplicação.
/// Pode rodar de novo sem duplicar o que já existe (idempotente).
/// </summary>
public static class AplicacaoSceneBuilder
{
    private const string PanelSpritePath = "Assets/Images/inventory_panel_blue_4.png";

    [MenuItem("Tools/Aplicação/Montar Cena de Aplicação")]
    public static void BuildScene()
    {
        EnsureEventSystem();
        Canvas canvas = EnsureCanvas();
        GameObject inventoryPanel = BuildInventory(canvas);
        BuildLevelCompletePanel(canvas, out LevelCompleteUI completeUI);
        DialogueManager dialogueManager = BuildDialogueBox(canvas);
        BuildPlayer();
        LevelGoal goal = BuildGoal();
        EnsurePlaceholderTargets();
        EnsureLevelFlow(completeUI, goal, dialogueManager, inventoryPanel);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[AplicacaoSceneBuilder] Cena montada. Ajuste posições/sprites dos objetos de teste à vontade e salve (Ctrl+S).");
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

    private static GameObject BuildInventory(Canvas canvas)
    {
        Transform existingPanel = canvas.transform.Find("InventoryPanel");
        if (existingPanel != null)
        {
            return existingPanel.gameObject;
        }

        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
        if (panelSprite == null)
        {
            Debug.LogError($"[AplicacaoSceneBuilder] Não encontrei o sprite em {PanelSpritePath}.");
        }

        // Tamanho do painel mantendo a proporção real do sprite (476x158).
        const float panelWidth = 380f;
        float panelHeight = panelSprite != null
            ? panelWidth * (panelSprite.rect.height / panelSprite.rect.width)
            : panelWidth * (158f / 476f);

        var panelGO = new GameObject("InventoryPanel", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(panelGO, "Criar InventoryPanel");
        var panelRect = (RectTransform)panelGO.transform;
        panelRect.SetParent(canvas.transform, false);
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        panelRect.anchoredPosition = new Vector2(-30f, -30f);

        var panelImage = panelGO.AddComponent<Image>();
        panelImage.sprite = panelSprite;
        panelImage.type = Image.Type.Simple;
        panelImage.raycastTarget = false;

        // 4 ferramentas numa linha só: Física/Colisão valem pra qualquer objeto,
        // Mover/Pular só têm efeito ao soltar em cima do Player.
        CreateToolIcon(panelRect, "PhysicsIcon", "Física", ToolType.Fisica, ToolColors.Fisica, new Vector2(0.14f, 0.5f));
        CreateToolIcon(panelRect, "CollisionIcon", "Colisão", ToolType.Colisao, ToolColors.Colisao, new Vector2(0.38f, 0.5f));
        CreateToolIcon(panelRect, "MovementIcon", "Mover", ToolType.Movimento, ToolColors.Movimento, new Vector2(0.62f, 0.5f));
        CreateToolIcon(panelRect, "JumpIcon", "Pular", ToolType.Pulo, ToolColors.Pulo, new Vector2(0.86f, 0.5f));

        return panelGO;
    }

    private static void CreateToolIcon(RectTransform parent, string name, string label, ToolType tool, Color color, Vector2 anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Criar ícone do inventário");
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(64f, 64f);
        rect.anchoredPosition = Vector2.zero;

        var image = go.AddComponent<Image>();
        image.color = color;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        var labelRect = (RectTransform)labelGO.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        var draggable = go.AddComponent<DraggableToolIcon>();
        var so = new SerializedObject(draggable);
        so.FindProperty("toolType").enumValueIndex = (int)tool;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Mesmo design em toda cena — ver DialogueBoxBuilder.
    private static DialogueManager BuildDialogueBox(Canvas canvas)
    {
        return DialogueBoxBuilder.EnsureDialogueBox(canvas);
    }

    private static void BuildLevelCompletePanel(Canvas canvas, out LevelCompleteUI completeUI)
    {
        Transform existing = canvas.transform.Find("LevelCompletePanel");
        if (existing != null)
        {
            completeUI = existing.GetComponent<LevelCompleteUI>();
            return;
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

        var textGO = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textGO.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Fase Concluída!";
        tmp.fontSize = 64;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        completeUI = panelGO.AddComponent<LevelCompleteUI>();
        panelGO.SetActive(false);
    }

    private static List<PhysicsTarget> EnsurePlaceholderTargets()
    {
        var results = new List<PhysicsTarget>();
        var existingRoot = GameObject.Find("ObjetosDaFase");
        if (existingRoot != null)
        {
            results.AddRange(existingRoot.GetComponentsInChildren<PhysicsTarget>(true));
            return results;
        }

        var root = new GameObject("ObjetosDaFase");
        Undo.RegisterCreatedObjectUndo(root, "Criar ObjetosDaFase");

        Sprite squareSprite = CreateSolidSprite();

        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject($"Objeto{i + 1}");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(-3f + i * 3f, 0f, 0f);
            go.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = squareSprite;
            sr.color = new Color(0.8f, 0.8f, 0.85f);

            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;

            var target = go.AddComponent<PhysicsTarget>();
            results.Add(target);
        }

        return results;
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

    private static void BuildPlayer()
    {
        if (Object.FindAnyObjectByType<AplicacaoPlayerController>() != null)
        {
            return;
        }

        var go = new GameObject("Player");
        Undo.RegisterCreatedObjectUndo(go, "Criar Player");
        go.transform.position = new Vector3(-6f, 1f, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSolidSprite();
        sr.color = new Color(0.3f, 0.8f, 1f);
        sr.sortingOrder = 1;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        go.AddComponent<Rigidbody2D>();
        go.AddComponent<AplicacaoPlayerController>();
    }

    // Objetivo da fase: por enquanto um círculo — ao encostar nele o Player conclui a fase.
    private static LevelGoal BuildGoal()
    {
        LevelGoal existing = Object.FindAnyObjectByType<LevelGoal>();
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject("Goal");
        Undo.RegisterCreatedObjectUndo(go, "Criar Goal");
        go.transform.position = new Vector3(7f, 0f, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(1f, 0.85f, 0.2f);

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        return go.AddComponent<LevelGoal>();
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 64;
        const float pixelsPerUnit = 32f;

        var texture = new Texture2D(size, size);
        var center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1f;

        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                pixels[y * size + x] = dist <= radius ? Color.white : new Color(0f, 0f, 0f, 0f);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    // Liga o Player, o Goal, o diálogo de intro e o inventário já criados ao
    // orquestrador da fase (mostra o diálogo do Prof Byte antes de liberar o
    // inventário, reinicia ao cair sem colisão, conclui e troca de cena ao
    // alcançar o objetivo).
    private static void EnsureLevelFlow(LevelCompleteUI completeUI, LevelGoal goal, DialogueManager dialogueManager, GameObject inventoryPanel)
    {
        if (Object.FindAnyObjectByType<AplicacaoLevelFlow>() != null)
        {
            return;
        }

        var go = new GameObject("AplicacaoLevelFlow");
        Undo.RegisterCreatedObjectUndo(go, "Criar AplicacaoLevelFlow");
        AplicacaoLevelFlow flow = go.AddComponent<AplicacaoLevelFlow>();

        var so = new SerializedObject(flow);
        so.FindProperty("player").objectReferenceValue = Object.FindAnyObjectByType<AplicacaoPlayerController>();
        so.FindProperty("goal").objectReferenceValue = goal;
        so.FindProperty("completeUI").objectReferenceValue = completeUI;
        so.FindProperty("dialogueManager").objectReferenceValue = dialogueManager;
        so.FindProperty("inventoryPanel").objectReferenceValue = inventoryPanel;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private const string InteracaoScenePath = "Assets/Scenes/Interacao.unity";

    /// <summary>
    /// Cria a cena "Interacao" (placeholder vazio, só com Canvas/EventSystem
    /// e um texto "em construção") e a registra em Build Settings, pra o
    /// AplicacaoLevelFlow ter uma cena de verdade pra carregar ao concluir a
    /// fase Aplicacao. Rode uma vez só; se o arquivo já existir, só garante
    /// que esteja em Build Settings.
    ///
    /// Atenção: isso troca a cena aberta no momento (pede pra salvar antes,
    /// se houver alterações). Reabra Aplicacao.unity manualmente depois.
    /// </summary>
    [MenuItem("Tools/Aplicação/Criar Cena de Interação (placeholder)")]
    public static void CreateInteracaoScenePlaceholder()
    {
        if (System.IO.File.Exists(InteracaoScenePath))
        {
            Debug.Log("[AplicacaoSceneBuilder] Interacao.unity já existe — só conferindo Build Settings.");
            RegisterSceneInBuildSettings(InteracaoScenePath);
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[AplicacaoSceneBuilder] Cancelado: havia alterações não salvas na cena aberta.");
            return;
        }

        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        var textGO = new GameObject("PlaceholderText", typeof(RectTransform));
        textGO.transform.SetParent(canvasGO.transform, false);
        var textRect = (RectTransform)textGO.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Tutorial de Interação com Objetos\n(em construção)";
        tmp.fontSize = 48;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();

        EditorSceneManager.SaveScene(newScene, InteracaoScenePath);
        RegisterSceneInBuildSettings(InteracaoScenePath);

        Debug.Log($"[AplicacaoSceneBuilder] Cena placeholder criada em {InteracaoScenePath} e adicionada ao Build Settings.");
    }

    private static void RegisterSceneInBuildSettings(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == scenePath))
        {
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
