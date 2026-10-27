using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helpers para montar elementos de UI por código em tempo de execução
/// (Canvas, texto, botão). Usado pelos painéis que se auto-constroem
/// (PauseMenuUI, SettingsPanelUI) para não depender de prefabs ou de
/// configuração manual em cada cena.
/// </summary>
public static class UIBuildUtils
{
    public static GameObject CreateText(Transform parent, string name, string text, float fontSize, TextAlignmentOptions alignment, Color? color = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color ?? Color.white;
        tmp.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }

        return go;
    }

    public static GameObject CreateButtonWithLabel(Transform parent, string name, string label, out TMP_Text labelText)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        var button = go.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(0.32f, 0.32f, 0.32f, 1f);
        colors.pressedColor = new Color(0.1f, 0.5f, 0.9f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        GameObject labelGO = CreateText(go.transform, "Label", label, 22, TextAlignmentOptions.Midline);
        StretchToParent(labelGO.GetComponent<RectTransform>());
        labelText = labelGO.GetComponent<TMP_Text>();

        return go;
    }

    public static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static RectTransform CreatePanel(Transform parent, string name, Color backgroundColor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        StretchToParent(rect);

        var image = go.AddComponent<Image>();
        image.color = backgroundColor;

        return rect;
    }

    public static Slider CreateSlider(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(0, 24);

        var slider = go.AddComponent<Slider>();

        var background = CreatePanel(rect, "Background", new Color(1f, 1f, 1f, 0.15f));
        background.GetComponent<Image>().type = Image.Type.Sliced;

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        var fillAreaRect = (RectTransform)fillArea.transform;
        fillAreaRect.SetParent(rect, false);
        StretchToParent(fillAreaRect);

        var fillGO = new GameObject("Fill", typeof(RectTransform));
        var fillRect = (RectTransform)fillGO.transform;
        fillRect.SetParent(fillAreaRect, false);
        var fillImage = fillGO.AddComponent<Image>();
        fillImage.color = new Color(0.1f, 0.6f, 1f, 1f);
        StretchToParent(fillRect);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        var handleAreaRect = (RectTransform)handleArea.transform;
        handleAreaRect.SetParent(rect, false);
        StretchToParent(handleAreaRect);

        var handleGO = new GameObject("Handle", typeof(RectTransform));
        var handleRect = (RectTransform)handleGO.transform;
        handleRect.SetParent(handleAreaRect, false);
        handleRect.sizeDelta = new Vector2(16, 0);
        var handleImage = handleGO.AddComponent<Image>();
        handleImage.color = Color.white;

        slider.targetGraphic = handleImage;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        return slider;
    }
}
