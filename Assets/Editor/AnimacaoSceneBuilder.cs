using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Reconfigura a cena "Animacao" para a versão Animator da fase (ver
/// AnimacaoLevelFlow): o personagem passa a andar com física e a animação é
/// escolhida pelo Animator, com as regras montadas num puzzle.
///
/// O que faz (sem apagar nada do cenário montado à mão):
/// - Clipes + AnimatorController com parâmetros e transições
///   (AnimationClipBuilder.BuildAll).
/// - Colisores: o cenário (Cenario/Ground, Parede, Teto) só tinha sprites. Gera
///   "ColisoresDaFase (gerado)" com caixas calculadas a partir dos tiles
///   (juntando tiles vizinhos) num CompositeCollider2D. Os tiles não são
///   tocados; se mexer no cenário, rode de novo pra recalcular.
/// - Espinhos (Cenario/Trap): um filho "Perigo (gerado)" com trigger +
///   AnimacaoPerigo em cada sprite.
/// - Player: adiciona Rigidbody2D + AnimacaoPlayerFisica, liga o controller novo.
/// - Fim do caminho: "FimDoCaminho" (cartão de acesso com o Animator de
///   flutuar que já existe em Assets/Anim/Card) + AnimacaoObjetivo.
/// - "AnimatorPuzzle" (AnimatorPuzzleUI) com os sprites da prévia, e liga
///   tudo no AnimacaoLevelFlow. Troca os textos antigos de diálogo salvos na
///   cena ("arraste uma animação...") pelos novos.
///
/// Uso: Tools > Animação > Montar Cena de Animação. Se a cena ativa não for a
/// Animacao, abre ela (só se a cena atual não tiver edições não salvas).
/// Idempotente — pode rodar de novo sem duplicar nada. Salva a cena no fim.
/// </summary>
public static class AnimacaoSceneBuilder
{
    private const string AnimacaoScenePath = "Assets/Scenes/Animacao.unity";
    private const string RunFolder = "Assets/Anim/Player/Run";
    private const string JumpFolder = "Assets/Anim/Player/Jump";
    private const string CardSpritePath = "Assets/Anim/Card/access_card_float_01.png";
    private const string CardControllerPath = "Assets/Anim/Card/AccessCardFloatController.controller";

    private const string CenarioName = "Cenario";
    private static readonly string[] SolidGroups = { "Ground", "Parede", "Teto" };
    private const string TrapGroup = "Trap";
    private const string CollidersRootName = "ColisoresDaFase (gerado)";
    private const string HazardChildName = "Perigo (gerado)";
    private const string GoalName = "FimDoCaminho";
    private const string PuzzleName = "AnimatorPuzzle";
    private const string GameSortingLayer = "Game";

    // X do cartão no fim do caminho, só usado ao criar (depois é só mover na
    // cena). Fica depois da passagem baixa de agachar e antes da coluna do
    // teto que desce até perto do chão.
    private const float GoalX = 7.6f;

    /// <summary>
    /// Cria a cena "Animacao" (placeholder vazio) e a registra em Build
    /// Settings. Rode uma vez só; se o arquivo já existir, só garante que
    /// esteja em Build Settings. Atenção: troca a cena aberta no momento
    /// (pede pra salvar antes, se houver alterações).
    /// </summary>
    [MenuItem("Tools/Animação/Criar Cena de Animação (placeholder)")]
    public static void CreateAnimacaoScenePlaceholder()
    {
        if (System.IO.File.Exists(AnimacaoScenePath))
        {
            Debug.Log("[AnimacaoSceneBuilder] Animacao.unity já existe — só conferindo Build Settings.");
            RegisterSceneInBuildSettings(AnimacaoScenePath);
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[AnimacaoSceneBuilder] Cancelado: havia alterações não salvas na cena aberta.");
            return;
        }

        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(newScene, AnimacaoScenePath);
        RegisterSceneInBuildSettings(AnimacaoScenePath);

        Debug.Log($"[AnimacaoSceneBuilder] Cena criada em {AnimacaoScenePath}. Abra-a e rode Tools > Animação > Montar Cena de Animação.");
    }

    [MenuItem("Tools/Animação/Montar Cena de Animação")]
    public static void BuildScene()
    {
        foreach (string line in Build())
        {
            Debug.Log("[AnimacaoSceneBuilder] " + line);
        }
    }

    public static List<string> Build()
    {
        var log = new List<string>();

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != AnimacaoScenePath)
        {
            if (scene.isDirty)
            {
                Debug.LogError($"[AnimacaoSceneBuilder] A cena ativa ({scene.name}) tem edições não salvas. Salve ou descarte antes de rodar, ou abra a Animacao manualmente.");
                log.Add("ABORTADO: cena ativa suja.");
                return log;
            }

            if (!System.IO.File.Exists(AnimacaoScenePath))
            {
                Debug.LogError($"[AnimacaoSceneBuilder] {AnimacaoScenePath} não existe. Rode antes Tools > Animação > Criar Cena de Animação (placeholder).");
                log.Add("ABORTADO: cena não existe.");
                return log;
            }

            scene = EditorSceneManager.OpenScene(AnimacaoScenePath, OpenSceneMode.Single);
            log.Add("Cena Animacao aberta.");
        }

        AnimatorController controller = AnimationClipBuilder.BuildAll(out _, out _, out _);
        if (controller == null)
        {
            log.Add("ABORTADO: não consegui montar clipes/controller (ver erros acima).");
            return log;
        }
        log.Add("AnimatorController pronto: parâmetros velocidadeX/noChao/regrasMontadas e 4 transições (Idle->Corrida, Corrida->Idle, Any State->Pulo, Pulo->Idle).");

        Sprite[] runFrames = AnimationClipBuilder.LoadFramesInOrder(RunFolder, "player_run_dust");
        Sprite[] jumpFrames = AnimationClipBuilder.LoadFramesInOrder(JumpFolder, "player_jump");
        Sprite idleSprite = AnimationClipBuilder.LoadIdleSprite();

        EnsureEventSystem(log);
        Canvas canvas = EnsureCanvas(log);

        Transform cenario = FindRoot(scene, CenarioName)?.transform;
        if (cenario == null)
        {
            log.Add($"AVISO: '{CenarioName}' não encontrado na raiz da cena — colisores e espinhos não foram gerados.");
        }

        BuildColliders(scene, cenario, log);
        BuildHazards(cenario, log);

        AnimacaoPlayerFisica player = ConfigurePlayer(scene, controller, idleSprite, log);
        BuildGoal(scene, player, log);
        DisableObsolete(scene, canvas, log);

        AnimatorPuzzleUI puzzle = BuildPuzzleConfig(scene, idleSprite, runFrames, jumpFrames, log);
        DialogueManager dialogueManager = DialogueBoxBuilder.EnsureDialogueBox(canvas);
        LevelCompleteUI completeUI = BuildLevelCompletePanel(canvas);
        ConfigureFlow(completeUI, dialogueManager, player, puzzle, log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add("Cena salva.");
        return log;
    }

    // ---------------------------------------------------------------- básicos

    private static GameObject FindRoot(Scene scene, string name)
    {
        return scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);
    }

    private static void EnsureEventSystem(List<string> log)
    {
        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        var go = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(go, "Criar EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
        log.Add("EventSystem (InputSystemUIInputModule) criado.");
    }

    private static Canvas EnsureCanvas(List<string> log)
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
        log.Add("Canvas criado.");
        return canvas;
    }

    // ---------------------------------------------------------------- colisores do cenário

    /// <summary>Só recalcula "ColisoresDaFase (gerado)" a partir dos tiles da cena ativa (após mexer no cenário).</summary>
    [MenuItem("Tools/Animação/Recalcular Colisores do Cenário")]
    public static void RecalcularColisores()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject cenario = FindRoot(scene, CenarioName);
        if (cenario == null)
        {
            Debug.LogWarning("[AnimacaoSceneBuilder] Cena ativa sem \"" + CenarioName + "\".");
            return;
        }

        var log = new List<string>();
        BuildColliders(scene, cenario.transform, log);
        foreach (string l in log)
        {
            Debug.Log("[AnimacaoSceneBuilder] " + l);
        }
    }

    private static void BuildColliders(Scene scene, Transform cenario, List<string> log)
    {
        if (cenario == null)
        {
            return;
        }

        var tiles = new List<Rect>();
        foreach (string groupName in SolidGroups)
        {
            Transform group = cenario.Find(groupName);
            if (group == null)
            {
                log.Add($"AVISO: {CenarioName}/{groupName} não encontrado — sem colisão pra ele.");
                continue;
            }

            foreach (SpriteRenderer sr in group.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!sr.enabled || !sr.gameObject.activeInHierarchy || sr.sprite == null)
                {
                    continue;
                }

                Bounds b = sr.bounds;
                tiles.Add(Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y));
            }
        }

        List<Rect> blocks = MergeVertically(MergeHorizontally(tiles));

        GameObject root = FindRoot(scene, CollidersRootName);
        if (root == null)
        {
            root = new GameObject(CollidersRootName);
            Undo.RegisterCreatedObjectUndo(root, "Criar colisores da fase");
        }
        else
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }
        }

        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        Rigidbody2D rb = root.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = root.AddComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Static;

        CompositeCollider2D composite = root.GetComponent<CompositeCollider2D>();
        if (composite == null)
        {
            composite = root.AddComponent<CompositeCollider2D>();
        }
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.generationType = CompositeCollider2D.GenerationType.Synchronous;

        for (int i = 0; i < blocks.Count; i++)
        {
            Rect r = blocks[i];
            var block = new GameObject($"Bloco_{i:00}");
            block.transform.SetParent(root.transform, false);
            block.transform.position = new Vector3(r.center.x, r.center.y, 0f);

            var box = block.AddComponent<BoxCollider2D>();
            box.size = r.size;
            box.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        composite.GenerateGeometry();
        log.Add($"Colisores: {tiles.Count} tiles de {string.Join("/", SolidGroups)} viraram {blocks.Count} caixas em '{CollidersRootName}' (CompositeCollider2D).");
    }

    // Junta tiles da mesma linha (mesma altura) que se encostam na horizontal.
    private static List<Rect> MergeHorizontally(List<Rect> rects)
    {
        const float tolerance = 0.02f;
        var result = new List<Rect>();

        foreach (IGrouping<(int, int), Rect> row in rects.GroupBy(t => (Mathf.RoundToInt(t.yMin * 100f), Mathf.RoundToInt(t.yMax * 100f))))
        {
            Rect? current = null;
            foreach (Rect r in row.OrderBy(t => t.xMin))
            {
                if (current == null)
                {
                    current = r;
                    continue;
                }

                Rect c = current.Value;
                if (r.xMin <= c.xMax + tolerance)
                {
                    current = Rect.MinMaxRect(c.xMin, c.yMin, Mathf.Max(c.xMax, r.xMax), c.yMax);
                }
                else
                {
                    result.Add(c);
                    current = r;
                }
            }

            if (current != null)
            {
                result.Add(current.Value);
            }
        }

        return result;
    }

    // Junta faixas com o mesmo X que se encostam na vertical (ex: colunas de parede).
    private static List<Rect> MergeVertically(List<Rect> rects)
    {
        const float tolerance = 0.02f;
        var result = new List<Rect>();

        foreach (IGrouping<(int, int), Rect> column in rects.GroupBy(t => (Mathf.RoundToInt(t.xMin * 100f), Mathf.RoundToInt(t.xMax * 100f))))
        {
            Rect? current = null;
            foreach (Rect r in column.OrderBy(t => t.yMin))
            {
                if (current == null)
                {
                    current = r;
                    continue;
                }

                Rect c = current.Value;
                if (r.yMin <= c.yMax + tolerance)
                {
                    current = Rect.MinMaxRect(c.xMin, c.yMin, c.xMax, Mathf.Max(c.yMax, r.yMax));
                }
                else
                {
                    result.Add(c);
                    current = r;
                }
            }

            if (current != null)
            {
                result.Add(current.Value);
            }
        }

        return result;
    }

    // ---------------------------------------------------------------- espinhos

    private static void BuildHazards(Transform cenario, List<string> log)
    {
        Transform trap = cenario != null ? cenario.Find(TrapGroup) : null;
        if (trap == null)
        {
            if (cenario != null)
            {
                log.Add($"AVISO: {CenarioName}/{TrapGroup} não encontrado — nenhum espinho configurado.");
            }
            return;
        }

        int count = 0;
        foreach (SpriteRenderer sr in trap.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null || sr.gameObject.name == HazardChildName)
            {
                continue;
            }

            Transform hazard = sr.transform.Find(HazardChildName);
            if (hazard == null)
            {
                var go = new GameObject(HazardChildName);
                Undo.RegisterCreatedObjectUndo(go, "Criar perigo");
                hazard = go.transform;
                hazard.SetParent(sr.transform, false);
            }

            // Trigger um pouco menor que o desenho, alinhado embaixo: raspar
            // a ponta no fim de um pulo não mata.
            Bounds b = sr.bounds;
            var worldSize = new Vector2(b.size.x * 0.8f, b.size.y * 0.55f);
            hazard.position = new Vector3(b.center.x, b.min.y + worldSize.y * 0.5f, sr.transform.position.z);
            hazard.rotation = Quaternion.identity;

            BoxCollider2D box = hazard.GetComponent<BoxCollider2D>();
            if (box == null)
            {
                box = hazard.gameObject.AddComponent<BoxCollider2D>();
            }

            Vector3 scale = hazard.lossyScale;
            box.size = new Vector2(worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)), worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
            box.offset = Vector2.zero;
            box.isTrigger = true;

            if (hazard.GetComponent<AnimacaoPerigo>() == null)
            {
                hazard.gameObject.AddComponent<AnimacaoPerigo>();
            }

            count++;
        }

        log.Add($"Espinhos: {count} perigo(s) configurado(s) em {CenarioName}/{TrapGroup}.");
    }

    // ---------------------------------------------------------------- player

    private static AnimacaoPlayerFisica ConfigurePlayer(Scene scene, AnimatorController controller, Sprite idleSprite, List<string> log)
    {
        GameObject go = null;

        AnimacaoPlayerFisica existing = Object.FindAnyObjectByType<AnimacaoPlayerFisica>(FindObjectsInactive.Include);
        if (existing != null)
        {
            go = existing.gameObject;
        }

        if (go == null)
        {
            go = FindRoot(scene, "Player");
        }

        if (go == null)
        {
            go = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(go, "Criar Player");
            go.transform.position = new Vector3(-7.7f, -3.8f, 0f);
            var newSr = go.AddComponent<SpriteRenderer>();
            newSr.sortingLayerName = GameSortingLayer;
            newSr.sortingOrder = 1;
            log.Add("Player não existia — criado em (-7.7, -3.8). Ajuste a posição na cena.");
        }

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = GameSortingLayer;
            sr.sortingOrder = 1;
        }
        if (idleSprite != null)
        {
            sr.sprite = idleSprite;
        }

        Collider2D col = go.GetComponent<Collider2D>();
        if (col == null)
        {
            var box = go.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
            col = box;
        }
        col.isTrigger = false;

        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = go.AddComponent<Rigidbody2D>();
            log.Add("Rigidbody2D adicionado ao Player.");
        }
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 4f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        Animator animator = go.GetComponent<Animator>();
        if (animator == null)
        {
            animator = go.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        if (go.GetComponent<PlayerHealth>() == null)
        {
            go.AddComponent<PlayerHealth>();
            log.Add("PlayerHealth adicionado ao Player.");
        }

        AnimacaoPlayerFisica player = go.GetComponent<AnimacaoPlayerFisica>();
        if (player == null)
        {
            player = go.AddComponent<AnimacaoPlayerFisica>();
            log.Add("AnimacaoPlayerFisica adicionado ao Player (movimento com física, alimenta o Animator).");
        }

        if (go.GetComponent<PlayerAgachar>() == null && go.GetComponent<BoxCollider2D>() != null)
        {
            go.AddComponent<PlayerAgachar>();
            log.Add("PlayerAgachar adicionado ao Player (agachar encolhe o collider; passagem baixa antes do cartão).");
        }

        EditorUtility.SetDirty(go);
        log.Add($"Player '{go.name}' configurado: Rigidbody2D dinâmico, collider sólido, Animator com {controller.name}.");
        return player;
    }

    // ---------------------------------------------------------------- fim do caminho

    private static void BuildGoal(Scene scene, AnimacaoPlayerFisica player, List<string> log)
    {
        GameObject goal = FindRoot(scene, GoalName);
        bool created = goal == null;
        if (created)
        {
            goal = new GameObject(GoalName);
            Undo.RegisterCreatedObjectUndo(goal, "Criar FimDoCaminho");

            // Altura do chão = pé do Player na posição inicial (ele nasce no chão).
            float floorY = -4.34f;
            if (player != null && player.TryGetComponent(out BoxCollider2D playerBox))
            {
                Transform t = player.transform;
                floorY = t.position.y + (playerBox.offset.y - playerBox.size.y * 0.5f) * Mathf.Abs(t.lossyScale.y);
            }

            goal.transform.position = new Vector3(GoalX, floorY + 0.9f, 0f);
        }

        SpriteRenderer sr = goal.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = goal.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = GameSortingLayer;
            sr.sortingOrder = 1;
        }

        if (sr.sprite == null)
        {
            sr.sprite = AssetDatabase.LoadAllAssetsAtPath(CardSpritePath).OfType<Sprite>().FirstOrDefault();
            if (sr.sprite == null)
            {
                log.Add($"AVISO: sprite do cartão não encontrado em {CardSpritePath} — o fim do caminho ficou sem desenho.");
            }
        }

        // O cartão já tem um Animator de objeto pronto (um estado "Loop"
        // flutuando), o mesmo usado na fase Interacao.
        RuntimeAnimatorController cardController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CardControllerPath);
        if (cardController != null)
        {
            Animator animator = goal.GetComponent<Animator>();
            if (animator == null)
            {
                animator = goal.AddComponent<Animator>();
            }

            if (animator.runtimeAnimatorController == null)
            {
                animator.runtimeAnimatorController = cardController;
            }
        }

        BoxCollider2D box = goal.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = goal.AddComponent<BoxCollider2D>();
            // Alto o bastante pra pegar o personagem andando no chão embaixo do cartão.
            box.size = new Vector2(1.0f, 1.6f);
            box.offset = new Vector2(0f, -0.3f);
        }
        box.isTrigger = true;

        if (goal.GetComponent<AnimacaoObjetivo>() == null)
        {
            goal.AddComponent<AnimacaoObjetivo>();
        }

        log.Add(created
            ? $"'{GoalName}' criado em {goal.transform.position} (cartão flutuando + AnimacaoObjetivo). Mova à vontade."
            : $"'{GoalName}' já existia — componentes conferidos, posição mantida.");
    }

    // ---------------------------------------------------------------- obsoletos

    private static void DisableObsolete(Scene scene, Canvas canvas, List<string> log)
    {
        // Sobras da primeira versão do builder, se existirem.
        foreach (string name in new[] { "Obstaculo", "Objetivo" })
        {
            GameObject old = FindRoot(scene, name);
            if (old != null && old.activeSelf)
            {
                Undo.RecordObject(old, "Desativar " + name);
                old.SetActive(false);
                log.Add($"'{name}' (versão antiga) desativado.");
            }
        }
    }

    // ---------------------------------------------------------------- puzzle e fluxo

    private static AnimatorPuzzleUI BuildPuzzleConfig(Scene scene, Sprite idleSprite, Sprite[] runFrames, Sprite[] jumpFrames, List<string> log)
    {
        AnimatorPuzzleUI puzzle = Object.FindAnyObjectByType<AnimatorPuzzleUI>(FindObjectsInactive.Include);
        if (puzzle == null)
        {
            GameObject go = FindRoot(scene, PuzzleName);
            if (go == null)
            {
                go = new GameObject(PuzzleName);
                Undo.RegisterCreatedObjectUndo(go, "Criar AnimatorPuzzle");
            }

            puzzle = go.AddComponent<AnimatorPuzzleUI>();
            log.Add($"'{PuzzleName}' criado (AnimatorPuzzleUI com peças e textos padrão, editáveis no Inspector).");
        }

        var so = new SerializedObject(puzzle);
        so.FindProperty("spriteIdle").objectReferenceValue = idleSprite;
        SetSpriteArray(so.FindProperty("framesCorrida"), runFrames);
        SetSpriteArray(so.FindProperty("framesPulo"), jumpFrames);
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add($"Prévia do puzzle: Idle={(idleSprite != null ? idleSprite.name : "nenhum")}, {runFrames.Length} quadros de corrida, {jumpFrames.Length} de pulo.");

        return puzzle;
    }

    private static void SetSpriteArray(SerializedProperty prop, Sprite[] sprites)
    {
        prop.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }

    private static void ConfigureFlow(LevelCompleteUI completeUI, DialogueManager dialogueManager, AnimacaoPlayerFisica player, AnimatorPuzzleUI puzzle, List<string> log)
    {
        AnimacaoLevelFlow flow = Object.FindAnyObjectByType<AnimacaoLevelFlow>(FindObjectsInactive.Include);
        if (flow == null)
        {
            var go = new GameObject("AnimacaoLevelFlow");
            Undo.RegisterCreatedObjectUndo(go, "Criar AnimacaoLevelFlow");
            flow = go.AddComponent<AnimacaoLevelFlow>();
            log.Add("AnimacaoLevelFlow criado.");
        }

        var so = new SerializedObject(flow);
        so.FindProperty("completeUI").objectReferenceValue = completeUI;
        if (so.FindProperty("dialogueManager").objectReferenceValue == null)
        {
            so.FindProperty("dialogueManager").objectReferenceValue = dialogueManager;
        }
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("puzzle").objectReferenceValue = puzzle;
        if (player != null)
        {
            so.FindProperty("playerHealth").objectReferenceValue = player.GetComponent<PlayerHealth>();
        }

        // Textos antigos (versão "arraste a animação") salvos na cena
        // sobrescrevem os padrões novos do script; troca só esses.
        if (ReplaceIfOld(so.FindProperty("introDialogue"), AnimacaoLevelFlow.IntroPadrao, "arraste", "inventário"))
        {
            log.Add("introDialogue: texto antigo (arrastar animação) trocado pelo novo.");
        }

        if (ReplaceIfOld(so.FindProperty("deathHintDialogue"), AnimacaoLevelFlow.DicaMortePadrao, "não corrida"))
        {
            log.Add("deathHintDialogue: texto antigo trocado pelo novo.");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add($"AnimacaoLevelFlow ligado (próxima cena: '{so.FindProperty("nextSceneName").stringValue}').");
    }

    private static bool ReplaceIfOld(SerializedProperty array, string[] newLines, params string[] oldMarkers)
    {
        if (array == null || !array.isArray)
        {
            return false;
        }

        bool isOld = array.arraySize == 0;
        for (int i = 0; i < array.arraySize && !isOld; i++)
        {
            string line = array.GetArrayElementAtIndex(i).stringValue ?? "";
            isOld = oldMarkers.Any(m => line.IndexOf(m, System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (!isOld)
        {
            return false;
        }

        array.arraySize = newLines.Length;
        for (int i = 0; i < newLines.Length; i++)
        {
            array.GetArrayElementAtIndex(i).stringValue = newLines[i];
        }

        return true;
    }

    // Mesmo painel genérico "Fase Concluída!" usado em Aplicacao/Interacao.
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

        GameObject textGO = UIBuildUtils.CreateText(rect, "Text", "Fase Concluída!", 64, TMPro.TextAlignmentOptions.Center);
        UIBuildUtils.StretchToParent(textGO.GetComponent<RectTransform>());

        LevelCompleteUI completeUI = panelGO.AddComponent<LevelCompleteUI>();
        panelGO.SetActive(false);
        return completeUI;
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
