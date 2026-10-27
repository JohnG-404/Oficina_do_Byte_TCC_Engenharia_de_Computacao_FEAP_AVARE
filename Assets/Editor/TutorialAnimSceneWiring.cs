using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Liga a mecânica de montar a sequência de animação na cena "Tutorial_Anim"
/// já montada visualmente (Encaixe = slots + prévia, Peças = bandeja de
/// frames soltos). Não recria nada que já existe — só adiciona os
/// componentes que faltam (Collider2D + SequencePiece em cada peça, o
/// AnimationSequencePuzzle, a caixa de diálogo de erro).
///
/// Uso: Tools > Tutorial Animação > Configurar Puzzle de Sequência, com
/// Tutorial_Anim.unity aberta. Idempotente.
/// </summary>
public static class TutorialAnimSceneWiring
{
    [MenuItem("Tools/Tutorial Animação/Configurar Puzzle de Sequência")]
    public static void ConfigurePuzzle()
    {
        GameObject encaixe = GameObject.Find("Encaixe");
        GameObject pecas = GameObject.Find("Peças");

        if (encaixe == null || pecas == null)
        {
            Debug.LogError("[TutorialAnimSceneWiring] Não encontrei \"Encaixe\" e/ou \"Peças\" na cena aberta. Abra Tutorial_Anim e rode de novo.");
            return;
        }

        Transform[] slots = FindSlotsLeftToRight(encaixe.transform);
        if (slots.Length == 0)
        {
            Debug.LogError("[TutorialAnimSceneWiring] Não encontrei nenhum filho \"Slot\"/\"Slot (N)\" dentro de \"Encaixe\".");
            return;
        }

        SpriteRenderer previewRenderer = EnsurePreviewRenderer(encaixe.transform);
        SequencePiece[] trayPieces = ConfigurePieces(pecas.transform, slots.Length);
        DialogueManager dialogueManager = EnsureDialogueBox();

        AnimationSequencePuzzle puzzle = encaixe.GetComponent<AnimationSequencePuzzle>();
        bool isNewPuzzle = puzzle == null;
        if (isNewPuzzle)
        {
            puzzle = encaixe.AddComponent<AnimationSequencePuzzle>();
        }

        var so = new SerializedObject(puzzle);
        SerializedProperty slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        }

        SerializedProperty trayProp = so.FindProperty("trayPieces");
        trayProp.arraySize = trayPieces.Length;
        for (int i = 0; i < trayPieces.Length; i++)
        {
            trayProp.GetArrayElementAtIndex(i).objectReferenceValue = trayPieces[i];
        }

        SerializedProperty roundsProp = so.FindProperty("rounds");
        BuildRounds(roundsProp, slots.Length);

        so.FindProperty("previewRenderer").objectReferenceValue = previewRenderer;
        if (so.FindProperty("dialogueManager").objectReferenceValue == null)
        {
            so.FindProperty("dialogueManager").objectReferenceValue = dialogueManager;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[TutorialAnimSceneWiring] Puzzle configurado com {slots.Length} slots e {roundsProp.arraySize} rodada(s). " +
                   "Cada rodada troca os frames embaralhados na bandeja sozinha ao acertar. Salve (Ctrl+S).");
    }

    // Lista de rodadas conhecidas (nome, pasta, prefixo do arquivo). Pra
    // adicionar uma animação nova ao tutorial: adicione uma linha aqui
    // (frames precisam se chamar "prefixo_01.png", "prefixo_02.png"...) e
    // rode a ferramenta de novo — só a rodada NOVA é criada; rodadas que já
    // existem (pelo nome) não são tocadas, mesmo que você já tenha
    // reordenado os frames certos à mão no Inspector.
    private static readonly (string name, string folder, string prefix)[] KnownRoundDefinitions =
    {
        ("Pulo", "Assets/Anim/Player/Jump", "player_jump"),
        ("Corrida", "Assets/Anim/Player/Run", "player_run_dust"),
        ("Alavanca", "Assets/Anim/Alavanca", "lever_right"),
    };

    // Só ACRESCENTA rodadas cujo nome ainda não está na lista — nunca edita
    // nem reordena uma rodada já existente (pra não apagar ajustes manuais
    // feitos no Inspector).
    private static void BuildRounds(SerializedProperty roundsProp, int slotCount)
    {
        var existingNames = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < roundsProp.arraySize; i++)
        {
            existingNames.Add(roundsProp.GetArrayElementAtIndex(i).FindPropertyRelative("roundName").stringValue);
        }

        foreach (var def in KnownRoundDefinitions)
        {
            if (existingNames.Contains(def.name))
            {
                continue;
            }

            Sprite[] frames = AnimationClipBuilder.LoadFramesInOrder(def.folder, def.prefix);
            if (frames.Length < slotCount)
            {
                Debug.LogWarning($"[TutorialAnimSceneWiring] Pulei a rodada \"{def.name}\": só achei {frames.Length} frame(s) em {def.folder} (precisa de pelo menos {slotCount}).");
                continue;
            }

            int newIndex = roundsProp.arraySize;
            roundsProp.arraySize = newIndex + 1;

            SerializedProperty roundProp = roundsProp.GetArrayElementAtIndex(newIndex);
            roundProp.FindPropertyRelative("roundName").stringValue = def.name;

            SerializedProperty correctProp = roundProp.FindPropertyRelative("correctFrames");
            correctProp.arraySize = slotCount;
            for (int f = 0; f < slotCount; f++)
            {
                correctProp.GetArrayElementAtIndex(f).objectReferenceValue = frames[f];
            }

            SerializedProperty decoyProp = roundProp.FindPropertyRelative("decoyFrames");
            int decoyCount = frames.Length - slotCount;
            decoyProp.arraySize = decoyCount;
            for (int d = 0; d < decoyCount; d++)
            {
                decoyProp.GetArrayElementAtIndex(d).objectReferenceValue = frames[slotCount + d];
            }
        }
    }

    // Acha os filhos "Slot"/"Slot (N)" de Encaixe (ignora "equals_bordered" e
    // "asset_item_slot_red") e devolve ordenados da esquerda pra direita.
    private static Transform[] FindSlotsLeftToRight(Transform encaixe)
    {
        var slots = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in encaixe)
        {
            if (Regex.IsMatch(child.name, @"^Slot(\s*\(\d+\))?$"))
            {
                slots.Add(child);
            }
        }

        slots.Sort((a, b) => a.position.x.CompareTo(b.position.x));
        return slots.ToArray();
    }

    // Adiciona um filho "Preview" dentro de "asset_item_slot_red" — a
    // prévia troca o sprite DESSE filho, não o do próprio slot vermelho,
    // pra não apagar a moldura/borda vermelha desenhada nele.
    private static SpriteRenderer EnsurePreviewRenderer(Transform encaixe)
    {
        Transform redSlot = encaixe.Find("asset_item_slot_red");
        if (redSlot == null)
        {
            Debug.LogWarning("[TutorialAnimSceneWiring] Não encontrei \"asset_item_slot_red\" — a prévia não vai ter onde aparecer.");
            return null;
        }

        Transform existing = redSlot.Find("Preview");
        if (existing != null)
        {
            return existing.GetComponent<SpriteRenderer>();
        }

        var previewGO = new GameObject("Preview");
        Undo.RegisterCreatedObjectUndo(previewGO, "Criar Preview");
        previewGO.transform.SetParent(redSlot, false);
        previewGO.transform.localPosition = Vector3.zero;

        SpriteRenderer redSr = redSlot.GetComponent<SpriteRenderer>();
        var previewSr = previewGO.AddComponent<SpriteRenderer>();
        if (redSr != null)
        {
            previewSr.sortingLayerID = redSr.sortingLayerID;
            previewSr.sortingOrder = redSr.sortingOrder + 1;
        }

        return previewSr;
    }

    // Garante Collider2D + SequencePiece em cada frame solto da bandeja
    // (posições fixas — o que muda a cada rodada é o sprite/posição certa
    // de cada uma, ver AnimationSequencePuzzle.StartRound). O valor inicial
    // de CorrectPosition aqui é só um fallback caso nenhuma rodada esteja
    // configurada: deduzido do número no final do nome do sprite (ex:
    // "player_jump_03" -> posição 3); frames além de slotCount viram isca.
    private static SequencePiece[] ConfigurePieces(Transform pecas, int slotCount)
    {
        var pieces = new System.Collections.Generic.List<SequencePiece>();

        foreach (Transform trayGO in pecas)
        {
            SpriteRenderer pieceRenderer = FindPieceRenderer(trayGO);
            if (pieceRenderer == null)
            {
                continue;
            }

            GameObject pieceObject = pieceRenderer.gameObject;

            BoxCollider2D col = pieceObject.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = pieceObject.AddComponent<BoxCollider2D>();
                if (pieceRenderer.sprite != null)
                {
                    col.size = pieceRenderer.sprite.bounds.size;
                }
            }

            SequencePiece piece = pieceObject.GetComponent<SequencePiece>();
            if (piece == null)
            {
                piece = pieceObject.AddComponent<SequencePiece>();

                int frameNumber = ParseFrameNumber(pieceRenderer.sprite);
                int correctPosition = frameNumber >= 1 && frameNumber <= slotCount ? frameNumber : 0;

                var so = new SerializedObject(piece);
                so.FindProperty("correctPosition").intValue = correctPosition;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            pieces.Add(piece);
        }

        return pieces.ToArray();
    }

    // A peça arrastável é o filho com o sprite de verdade (ex:
    // "player_jump_01" dentro de "asset_item_slot_purple (N)") — não o
    // próprio frame/moldura da bandeja.
    private static SpriteRenderer FindPieceRenderer(Transform trayGO)
    {
        foreach (Transform child in trayGO)
        {
            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                return sr;
            }
        }

        return null;
    }

    private static int ParseFrameNumber(Sprite sprite)
    {
        if (sprite == null)
        {
            return 0;
        }

        Match match = Regex.Match(sprite.name, @"(\d+)$");
        return match.Success ? int.Parse(match.Value) : 0;
    }

    // Caixa de diálogo — mesmo design em toda cena, ver DialogueBoxBuilder.
    // Usada tanto pro diálogo de intro (ver AnimationSequencePuzzle.Start)
    // quanto pro aviso "acho que não é assim..." de sequência errada.
    private static DialogueManager EnsureDialogueBox()
    {
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(esGO, "Criar EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();
        }

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGO = new GameObject("Canvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Criar Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGO.AddComponent<GraphicRaycaster>();
        }

        return DialogueBoxBuilder.EnsureDialogueBox(canvas);
    }
}
