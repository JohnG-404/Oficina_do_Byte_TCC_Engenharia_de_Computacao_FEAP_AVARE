using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Monta a cena "Interacao_Vida": reaproveita o HUD do assistente Ação+
/// Consequência (via prefab salvo a partir de Interacao.unity), o diálogo,
/// o painel de "Fase Concluída", e configura os perigos/itens já colocados
/// manualmente na cena (Laser, Espinhos, Plataforma falsa, Vida, Vida Falsa)
/// com as Ações/Consequências certas.
///
/// Uso: Tools > Interação > Montar Cena de Interação (Vida), com
/// Interacao_Vida.unity aberta. Idempotente — pode rodar de novo sem duplicar.
/// </summary>
public static class InteracaoVidaSceneBuilder
{
    private const string PanelPrefabPath = "Assets/Prefabs/InteractionConfigPanel.prefab";

    [MenuItem("Tools/Interação/Montar Cena de Interação (Vida)")]
    public static void BuildScene()
    {
        EnsureEventSystem();
        Canvas canvas = EnsureCanvas();

        InteractionConfigUI configUI = EnsureInteractionConfigPanel(canvas);
        DialogueManager dialogueManager = DialogueBoxBuilder.EnsureDialogueBox(canvas);
        LevelCompleteUI completeUI = BuildLevelCompletePanel(canvas);

        GameObject player = BuildPlayer();
        BuildCamera(player);
        BuildHeartHealthUI(canvas, player);
        BuildLevelFlow(completeUI, dialogueManager);

        AddGroundColliders();
        ConfigureTraps(player);
        EnsureSpikes();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[InteracaoVidaSceneBuilder] Cena montada. Confira posições/textos e salve (Ctrl+S).");
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(go, "Criar EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    private static Canvas EnsureCanvas()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null) return canvas;

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

    // Reaproveita o painel redesenhado da cena Interacao via prefab, em vez
    // de montar um novo do zero — é literalmente o mesmo HUD.
    private static InteractionConfigUI EnsureInteractionConfigPanel(Canvas canvas)
    {
        Transform existing = canvas.transform.Find("InteractionConfigPanel");
        if (existing != null)
        {
            return existing.GetComponent<InteractionConfigUI>();
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[InteracaoVidaSceneBuilder] Prefab não encontrado em {PanelPrefabPath}.");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
        Undo.RegisterCreatedObjectUndo(instance, "Instanciar InteractionConfigPanel");
        return instance.GetComponent<InteractionConfigUI>();
    }

    private static GameObject BuildPlayer()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("[InteracaoVidaSceneBuilder] GameObject 'Player' não encontrado na cena.");
            return null;
        }

        if (player.GetComponent<Collider2D>() == null)
        {
            var col = player.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            Physics2DTuning.PreventCornerSnag(col);
        }

        if (player.GetComponent<Rigidbody2D>() == null)
        {
            player.AddComponent<Rigidbody2D>();
        }

        if (player.GetComponent<InteracaoPlayerController>() == null)
        {
            player.AddComponent<InteracaoPlayerController>();
        }

        if (player.GetComponent<PlayerHealth>() == null)
        {
            var health = player.AddComponent<PlayerHealth>();
            var so = new SerializedObject(health);
            so.FindProperty("maxHealth").intValue = 3;
            so.FindProperty("startingHealth").intValue = 3;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Os itens interativos só reagem à tag Player (ver InteractableItem.IsPlayer).
        if (!player.CompareTag(InteractableItem.PlayerTag))
        {
            Undo.RecordObject(player, "Tag Player");
            player.tag = InteractableItem.PlayerTag;
        }

        return player;
    }

    private static void BuildCamera(GameObject player)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null || player == null) return;

        CameraFollow follow = mainCam.GetComponent<CameraFollow>();
        if (follow == null)
        {
            follow = mainCam.gameObject.AddComponent<CameraFollow>();
        }

        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Liga os 3 "quadrados" de vida (Canvas/Life/Image, Image (1), Image (2))
    // já posicionados manualmente na cena ao PlayerHealth.
    private static void BuildHeartHealthUI(Canvas canvas, GameObject player)
    {
        Transform life = canvas.transform.Find("Life");
        if (life == null)
        {
            Debug.LogError("[InteracaoVidaSceneBuilder] Canvas/Life não encontrado.");
            return;
        }

        HeartHealthUI heartUI = life.GetComponent<HeartHealthUI>();
        bool isNew = heartUI == null;
        if (heartUI == null)
        {
            heartUI = life.gameObject.AddComponent<HeartHealthUI>();
        }

        Image[] hearts =
        {
            life.Find("Image")?.GetComponent<Image>(),
            life.Find("Image (1)")?.GetComponent<Image>(),
            life.Find("Image (2)")?.GetComponent<Image>(),
        };

        var so = new SerializedObject(heartUI);
        var heartsProp = so.FindProperty("hearts");
        heartsProp.arraySize = hearts.Length;
        for (int i = 0; i < hearts.Length; i++)
        {
            heartsProp.GetArrayElementAtIndex(i).objectReferenceValue = hearts[i];
        }

        if (isNew && player != null)
        {
            so.FindProperty("playerHealth").objectReferenceValue = player.GetComponent<PlayerHealth>();
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

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

        if (isNew)
        {
            var introProp = so.FindProperty("introDialogue");
            string[] lines =
            {
                "Agora vamos dar uma olhada em como a Interação funciona com a vida do personagem.",
                "Você reparou os quadrados no canto superior da tela? Isso é a sua barra de vida — cada quadrado é um ponto de vida.",
                "Tudo nessa sala pode ser configurado do mesmo jeito de sempre: clique, escolha a Ação e a Consequência.",
                "Cuidado: nem tudo aqui é o que parece — até um item de cura pode ser configurado pra fazer o contrário. Preste atenção no que você escolhe!",
            };
            introProp.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                introProp.GetArrayElementAtIndex(i).stringValue = lines[i];
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Chão/paredes/plataformas de "Cenario/Ground" ainda não tinham
    // collider nenhum — sem isso o jogador atravessa tudo.
    private static void AddGroundColliders()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null) return;

        int added = 0;
        foreach (Transform child in ground.transform)
        {
            if (child.GetComponent<Collider2D>() != null) continue;

            var sr = child.GetComponent<SpriteRenderer>();
            var col = child.gameObject.AddComponent<BoxCollider2D>();
            if (sr != null && sr.sprite != null)
            {
                col.size = sr.sprite.bounds.size;
            }
            Physics2DTuning.PreventCornerSnag(col);
            added++;
        }

        Debug.Log($"[InteracaoVidaSceneBuilder] {added} colliders de chão adicionados.");
    }

    // Configura os perigos/itens já colocados manualmente em "Cenario/Traps"
    // com o InteractableItem certo pra cada tipo, reconhecido pelo nome.
    private static void ConfigureTraps(GameObject player)
    {
        GameObject traps = GameObject.Find("Traps");
        if (traps == null)
        {
            Debug.LogError("[InteracaoVidaSceneBuilder] GameObject 'Traps' não encontrado.");
            return;
        }

        foreach (Transform child in traps.transform)
        {
            GameObject go = child.gameObject;
            string name = go.name;

            if (name.StartsWith("trap_laser"))
            {
                ConfigureHazard(go, "Laser", "Um feixe de laser. Escolha como ele reage ao jogador.",
                    new[] { ActionType.Touch, ActionType.WalkNear, ActionType.LeaveArea },
                    new[] { ConsequenceType.DamageOneHeart, ConsequenceType.DamageTwoHearts, ConsequenceType.Repel,
                            ConsequenceType.DisableForSeconds, ConsequenceType.TurnOnBehindPlayer });
            }
            else if (name.StartsWith("fake_floor"))
            {
                ConfigureFakePlatform(go);
            }
            else if (name.StartsWith("plus_red_heal"))
            {
                ConfigureHazard(go, "Vida", "Parece um item de cura... mas você decide o que ele realmente faz.",
                    new[] { ActionType.Touch, ActionType.PressButton, ActionType.WalkNear, ActionType.RequiresMissingHealth },
                    new[] { ConsequenceType.DamageOneHeart, ConsequenceType.DamageTwoHearts, ConsequenceType.HealPlayer,
                            ConsequenceType.ShieldForSeconds, ConsequenceType.IncreaseMaxHealth });
            }
            else if (name.StartsWith("plus_black_debuff"))
            {
                ConfigureHazard(go, "Vida Falsa", "Parece um perigo... mas você decide o que ele realmente faz.",
                    new[] { ActionType.Touch, ActionType.PressButton, ActionType.WalkNear, ActionType.RequiresMissingHealth },
                    new[] { ConsequenceType.DamageOneHeart, ConsequenceType.DamageTwoHearts, ConsequenceType.HealPlayer,
                            ConsequenceType.SlowPlayer, ConsequenceType.InvertControls });
            }
        }
    }

    private static void ConfigureHazard(GameObject go, string displayName, string description, ActionType[] actions, ConsequenceType[] consequences)
    {
        if (go.GetComponent<Collider2D>() == null)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            var col = go.AddComponent<BoxCollider2D>();
            if (sr != null && sr.sprite != null)
            {
                col.size = sr.sprite.bounds.size;
            }
            Physics2DTuning.PreventCornerSnag(col);
        }

        InteractableItem item = go.GetComponent<InteractableItem>();
        bool isNew = item == null;
        if (item == null) item = go.AddComponent<InteractableItem>();

        var so = new SerializedObject(item);
        if (isNew)
        {
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
        }
        SetEnumArray(so.FindProperty("allowedActions"), actions.Select(a => (int)a).ToArray());
        SetEnumArray(so.FindProperty("allowedConsequences"), consequences.Select(c => (int)c).ToArray());
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureFakePlatform(GameObject go)
    {
        if (go.GetComponent<Rigidbody2D>() == null)
        {
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (go.GetComponent<Collider2D>() == null)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            var col = go.AddComponent<BoxCollider2D>();
            if (sr != null && sr.sprite != null)
            {
                col.size = sr.sprite.bounds.size;
            }
            Physics2DTuning.PreventCornerSnag(col);
        }

        if (go.GetComponent<FakePlatform>() == null)
        {
            go.AddComponent<FakePlatform>();
        }

        InteractableItem item = go.GetComponent<InteractableItem>();
        bool isNew = item == null;
        if (item == null) item = go.AddComponent<InteractableItem>();

        var so = new SerializedObject(item);
        if (isNew)
        {
            so.FindProperty("displayName").stringValue = "Plataforma Falsa";
            so.FindProperty("description").stringValue = "Escolha o que acontece quando o jogador pisar nela.";
        }
        SetEnumArray(so.FindProperty("allowedActions"), new[] { (int)ActionType.Touch, (int)ActionType.WalkNear });
        SetEnumArray(so.FindProperty("allowedConsequences"), new[] { (int)ConsequenceType.BreakAndRespawn, (int)ConsequenceType.BounceAndReturn,
            (int)ConsequenceType.FallAfterDelay, (int)ConsequenceType.MakeSlippery, (int)ConsequenceType.OneWayFromBelow });
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Não havia nenhum espinho colocado na cena ainda — cria 2, em pontos
    // livres do chão, com o mesmo comportamento do Laser.
    private static void EnsureSpikes()
    {
        GameObject traps = GameObject.Find("Traps");
        if (traps == null || traps.transform.Find("trap_spikes_01") != null) return;

        Sprite spikeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Trap/trap_spikes.png");
        Vector3[] positions = { new Vector3(6.3f, -4.3f, 0f), new Vector3(17.9f, -4.3f, 0f) };

        for (int i = 0; i < positions.Length; i++)
        {
            var go = new GameObject($"trap_spikes_{i + 1:00}");
            Undo.RegisterCreatedObjectUndo(go, "Criar espinhos");
            go.transform.SetParent(traps.transform, false);
            go.transform.position = positions[i];

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spikeSprite;

            ConfigureHazard(go, "Espinhos", "Fileira de espinhos. Escolha como reagem ao jogador.",
                new[] { ActionType.Touch, ActionType.WalkNear },
                new[] { ConsequenceType.DamageOneHeart, ConsequenceType.DamageTwoHearts, ConsequenceType.Repel,
                        ConsequenceType.ReturnToSafePoint, ConsequenceType.SinkIntoGround });
        }
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
}
