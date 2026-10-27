using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monta a caixa de diálogo do Prof Byte com o MESMO design em qualquer
/// cena — painel "dialog_panel_black_gradient", retrato "portuduun", nome
/// "Prof Byte", mesmo tamanho/posição. Antes disso, cada cena (Aplicacao,
/// Tutorial_Anim, Interacao, Animacao...) tinha seu próprio código quase
/// idêntico só que levemente diferente (Tutorial_Anim, por exemplo, tinha
/// 1400x220 em vez de 1429.31x205.39) — usando este único método, os
/// diálogos ficam pixel-a-pixel iguais em toda cena nova, e cenas antigas
/// convergem pro mesmo tamanho na próxima vez que a ferramenta de montagem
/// delas rodar.
///
/// A cena "Fisica" é a exceção de propósito: a caixa dela foi ajustada à
/// mão no Editor pra caber naquele layout específico e já usa o mesmo
/// sprite/retrato/nome (ver Tools > Inicio > Aplicar Design de Diálogo) —
/// não precisa passar por este builder.
/// </summary>
public static class DialogueBoxBuilder
{
    private const string PanelSpritePath = "Assets/Images/dialog_panel_black_gradient.png";
    private const string ProfPortraitPath = "Assets/Images/portuduun.png";

    private static readonly Vector2 BoxSize = new Vector2(1429.31f, 205.39f);
    private static readonly Vector2 BoxAnchoredPosition = new Vector2(0f, 40f);

    /// <summary>Idempotente: se já existir um "DialogueBox" filho do Canvas, só devolve o DialogueManager dele.</summary>
    public static DialogueManager EnsureDialogueBox(Canvas canvas)
    {
        Transform existing = canvas.transform.Find("DialogueBox");
        if (existing != null)
        {
            DialogueManager existingManager = existing.GetComponent<DialogueManager>();
            if (existingManager != null)
            {
                return existingManager;
            }
        }

        var boxGO = new GameObject("DialogueBox", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(boxGO, "Criar DialogueBox");
        var boxRect = (RectTransform)boxGO.transform;
        boxRect.SetParent(canvas.transform, false);
        boxRect.anchorMin = new Vector2(0.5f, 0f);
        boxRect.anchorMax = new Vector2(0.5f, 0f);
        boxRect.pivot = new Vector2(0.5f, 0f);
        boxRect.sizeDelta = BoxSize;
        boxRect.anchoredPosition = BoxAnchoredPosition;

        var bg = boxGO.AddComponent<Image>();
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
        if (panelSprite != null)
        {
            bg.sprite = panelSprite;
            bg.type = Image.Type.Simple;
            bg.color = Color.white;
        }
        else
        {
            Debug.LogWarning($"[DialogueBoxBuilder] Não encontrei o sprite em {PanelSpritePath} — usando cor sólida como fallback.");
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        }

        Texture portraitTexture = AssetDatabase.LoadAssetAtPath<Texture>(ProfPortraitPath);
        var portraitGO = new GameObject("Prof", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(portraitGO, "Criar retrato do Prof Byte");
        var portraitRect = (RectTransform)portraitGO.transform;
        portraitRect.SetParent(boxRect, false);
        portraitRect.anchorMin = new Vector2(0f, 0f);
        portraitRect.anchorMax = new Vector2(0f, 1f);
        portraitRect.pivot = new Vector2(0f, 0.5f);
        portraitRect.sizeDelta = new Vector2(180f, -20f);
        portraitRect.anchoredPosition = new Vector2(20f, 0f);

        var portraitImage = portraitGO.AddComponent<RawImage>();
        portraitImage.texture = portraitTexture;
        portraitImage.raycastTarget = false;

        GameObject nameGO = UIBuildUtils.CreateText(boxRect, "CaracterName", "Prof Byte", 26, TextAlignmentOptions.TopLeft);
        var nameRect = nameGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.sizeDelta = new Vector2(-230f, 40f);
        nameRect.anchoredPosition = new Vector2(220f, -14f);

        GameObject textGO = UIBuildUtils.CreateText(boxRect, "DialogueText", "", 22, TextAlignmentOptions.TopLeft);
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(220f, 20f);
        textRect.offsetMax = new Vector2(-30f, -56f);

        DialogueManager manager = boxGO.AddComponent<DialogueManager>();
        var managerSO = new SerializedObject(manager);
        managerSO.FindProperty("dialogueText").objectReferenceValue = textGO.GetComponent<TextMeshProUGUI>();
        managerSO.FindProperty("dialoguePanel").objectReferenceValue = boxGO;
        managerSO.ApplyModifiedPropertiesWithoutUndo();

        boxGO.SetActive(false);
        return manager;
    }
}
