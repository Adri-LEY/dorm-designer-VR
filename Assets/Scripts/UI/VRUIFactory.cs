using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Small helpers to build world-space UI from code inside the existing menu canvas.
/// The menu canvas is 500 x 500 units at a 0.001 scale, so 1 unit = 1 mm.
/// </summary>
public static class VRUIFactory
{
    public static readonly Color PanelColor = new Color(0.11f, 0.12f, 0.15f, 0.97f);
    public static readonly Color CardColor = new Color(0.19f, 0.21f, 0.26f, 1f);
    public static readonly Color AccentColor = new Color(0.20f, 0.60f, 1.00f, 1f);
    public static readonly Color ConfirmColor = new Color(0.18f, 0.68f, 0.38f, 1f);
    public static readonly Color WarningColor = new Color(0.90f, 0.45f, 0.20f, 1f);
    public static readonly Color TextColor = new Color(0.95f, 0.96f, 0.98f, 1f);
    public static readonly Color MutedTextColor = new Color(0.68f, 0.72f, 0.80f, 1f);

    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent != null ? parent.gameObject.layer : 0;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition3D = Vector3.zero;
        return rect;
    }

    /// <summary>Stretch to the parent with the given insets (left, right, top, bottom).</summary>
    public static RectTransform Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    /// <summary>Anchor to the parent's top-left corner, at (x, y) from that corner, with a fixed size.</summary>
    public static RectTransform PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    public static Image CreatePanel(string name, Transform parent, Color color, bool raycastTarget = true)
    {
        RectTransform rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    public static TextMeshProUGUI CreateText(
        string name, Transform parent, string text, float fontSize,
        Color color, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft,
        FontStyles style = FontStyles.Normal)
    {
        RectTransform rect = CreateRect(name, parent);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.fontStyle = style;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    public static Button CreateButton(
        string name, Transform parent, string label, UnityAction onClick,
        Color background, float fontSize = 16f, Color? textColor = null)
    {
        Image image = CreatePanel(name, parent, background);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        // Do not keep a tint after the click; the ray pointer has no "deselect" gesture.
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        if (onClick != null) button.onClick.AddListener(onClick);

        if (!string.IsNullOrEmpty(label))
        {
            TextMeshProUGUI text = CreateText(
                "Label", image.transform, label, fontSize,
                textColor ?? TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(text.rectTransform, 6f, 6f, 2f, 2f);
        }

        return button;
    }

    public static TextMeshProUGUI GetLabel(Button button)
    {
        return button != null ? button.GetComponentInChildren<TextMeshProUGUI>(true) : null;
    }

    public static HorizontalLayoutGroup AddHorizontalLayout(GameObject target, float spacing, bool expandWidth = false)
    {
        var layout = target.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = expandWidth;
        layout.childForceExpandHeight = true;
        return layout;
    }

    public static VerticalLayoutGroup AddVerticalLayout(GameObject target, float spacing)
    {
        var layout = target.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static LayoutElement SetLayoutSize(Component target, float preferredWidth = -1f, float preferredHeight = -1f)
    {
        var element = target.GetComponent<LayoutElement>();
        if (element == null) element = target.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = preferredWidth;
        element.preferredHeight = preferredHeight;
        if (preferredWidth > 0f) element.minWidth = preferredWidth;
        if (preferredHeight > 0f) element.minHeight = preferredHeight;
        return element;
    }

    public static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            // Hide immediately so layout groups ignore it; Destroy only happens at end of frame.
            child.SetActive(false);
            Object.Destroy(child);
        }
    }
}
