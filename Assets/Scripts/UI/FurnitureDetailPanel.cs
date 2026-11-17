using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Product sheet shown over the furniture grid: picture, price, real dimensions,
/// available sizes and colours, and a "Place in room" button.
/// Built from code inside the existing menu canvas, so it is ray-pointable like the grid.
/// </summary>
public sealed class FurnitureDetailPanel : MonoBehaviour
{
    private const float Margin = 12f;
    private const float RightColumnX = 196f;

    /// <summary>Raised when the user confirms. Size / colour may be null when the item has none.</summary>
    public event Action<FurnitureItem, FurnitureSizeOption, FurnitureColorOption> PlaceRequested;

    public bool IsOpen => gameObject.activeSelf;

    private float _width;
    private TextMeshProUGUI _title;
    private Image _icon;
    private TextMeshProUGUI _category;
    private TextMeshProUGUI _price;
    private TextMeshProUGUI _dimensions;
    private TextMeshProUGUI _description;
    private TextMeshProUGUI _sizeHeader;
    private RectTransform _sizeRow;
    private TextMeshProUGUI _colorHeader;
    private RectTransform _colorRow;

    private readonly List<Button> _sizeButtons = new List<Button>();
    private readonly List<Outline> _colorOutlines = new List<Outline>();

    private FurnitureItem _item;
    private int _sizeIndex;
    private int _colorIndex;

    /// <summary>Creates the panel as an overlay of <paramref name="canvas"/>, leaving <paramref name="topInset"/> free (the tab bar).</summary>
    public static FurnitureDetailPanel Create(RectTransform canvas, float topInset)
    {
        Image background = VRUIFactory.CreatePanel("FurnitureDetailPanel", canvas, VRUIFactory.PanelColor);
        VRUIFactory.Stretch(background.rectTransform, 0f, 0f, topInset, 0f);

        var panel = background.gameObject.AddComponent<FurnitureDetailPanel>();
        panel._width = canvas.rect.width > 1f ? canvas.rect.width : 500f;
        panel.Build();
        background.gameObject.SetActive(false);
        return panel;
    }

    private void Build()
    {
        Transform root = transform;
        float contentWidth = _width - 2f * Margin;
        float rightWidth = _width - RightColumnX - Margin;

        Button back = VRUIFactory.CreateButton("BackButton", root, "< Back", Hide, VRUIFactory.CardColor, 16f);
        VRUIFactory.PlaceTopLeft((RectTransform)back.transform, Margin, 10f, 80f, 34f);

        _title = VRUIFactory.CreateText("Title", root, string.Empty, 26f, VRUIFactory.TextColor,
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        _title.textWrappingMode = TextWrappingModes.NoWrap;
        VRUIFactory.PlaceTopLeft(_title.rectTransform, 102f, 8f, _width - 102f - Margin, 40f);

        Image iconFrame = VRUIFactory.CreatePanel("IconFrame", root, VRUIFactory.CardColor, false);
        VRUIFactory.PlaceTopLeft(iconFrame.rectTransform, Margin, 56f, 170f, 170f);
        _icon = VRUIFactory.CreatePanel("Icon", iconFrame.transform, Color.white, false);
        _icon.preserveAspect = true;
        VRUIFactory.Stretch(_icon.rectTransform, 8f, 8f, 8f, 8f);

        _category = VRUIFactory.CreateText("Category", root, string.Empty, 15f, VRUIFactory.MutedTextColor,
            TextAlignmentOptions.Center);
        VRUIFactory.PlaceTopLeft(_category.rectTransform, Margin, 230f, 170f, 24f);

        _price = VRUIFactory.CreateText("Price", root, string.Empty, 30f, VRUIFactory.AccentColor,
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        VRUIFactory.PlaceTopLeft(_price.rectTransform, RightColumnX, 56f, rightWidth, 38f);

        _dimensions = VRUIFactory.CreateText("Dimensions", root, string.Empty, 16f, VRUIFactory.TextColor,
            TextAlignmentOptions.MidlineLeft);
        VRUIFactory.PlaceTopLeft(_dimensions.rectTransform, RightColumnX, 96f, rightWidth, 24f);

        _description = VRUIFactory.CreateText("Description", root, string.Empty, 14f, VRUIFactory.MutedTextColor);
        VRUIFactory.PlaceTopLeft(_description.rectTransform, RightColumnX, 126f, rightWidth, 124f);

        _sizeHeader = VRUIFactory.CreateText("SizeHeader", root, "Size", 15f, VRUIFactory.MutedTextColor,
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        VRUIFactory.PlaceTopLeft(_sizeHeader.rectTransform, Margin, 262f, contentWidth, 22f);

        _sizeRow = VRUIFactory.CreateRect("SizeRow", root);
        VRUIFactory.PlaceTopLeft(_sizeRow, Margin, 286f, contentWidth, 38f);
        VRUIFactory.AddHorizontalLayout(_sizeRow.gameObject, 8f);

        _colorHeader = VRUIFactory.CreateText("ColorHeader", root, "Colour", 15f, VRUIFactory.MutedTextColor,
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        VRUIFactory.PlaceTopLeft(_colorHeader.rectTransform, Margin, 332f, contentWidth, 22f);

        _colorRow = VRUIFactory.CreateRect("ColorRow", root);
        VRUIFactory.PlaceTopLeft(_colorRow, Margin, 356f, contentWidth, 34f);
        VRUIFactory.AddHorizontalLayout(_colorRow.gameObject, 10f);

        Button place = VRUIFactory.CreateButton("PlaceButton", root, "Place in room", Place, VRUIFactory.ConfirmColor, 20f);
        VRUIFactory.PlaceTopLeft((RectTransform)place.transform, Margin, 400f, contentWidth, 42f);
    }

    public void Show(FurnitureItem item)
    {
        if (item == null) return;

        _item = item;
        _sizeIndex = 0;
        _colorIndex = 0;

        _title.text = FurnitureCatalogUtility.PrettyName(item.itemName);
        _category.text = string.IsNullOrEmpty(item.category) ? string.Empty : item.category;
        _description.text = string.IsNullOrEmpty(item.description) ? "No description yet." : item.description;
        _icon.sprite = item.icon;
        _icon.enabled = item.icon != null;

        BuildSizeButtons();
        BuildColorSwatches();
        RefreshSelection();

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void BuildSizeButtons()
    {
        VRUIFactory.ClearChildren(_sizeRow);
        _sizeButtons.Clear();

        int count = _item.sizes != null ? _item.sizes.Count : 0;
        bool show = count > 0;
        _sizeHeader.gameObject.SetActive(show);
        _sizeRow.gameObject.SetActive(show);
        if (!show) return;

        float available = _width - 2f * Margin;
        float buttonWidth = Mathf.Min(150f, (available - 8f * (count - 1)) / count);
        for (int i = 0; i < count; i++)
        {
            int index = i;
            Button button = VRUIFactory.CreateButton(
                "Size_" + i, _sizeRow, _item.sizes[i].label, () => SelectSize(index),
                VRUIFactory.CardColor, 15f);
            VRUIFactory.SetLayoutSize(button, buttonWidth, 38f);
            _sizeButtons.Add(button);
        }
    }

    private void BuildColorSwatches()
    {
        VRUIFactory.ClearChildren(_colorRow);
        _colorOutlines.Clear();

        int count = _item.colors != null ? _item.colors.Count : 0;
        bool show = count > 0;
        _colorHeader.gameObject.SetActive(show);
        _colorRow.gameObject.SetActive(show);
        if (!show) return;

        for (int i = 0; i < count; i++)
        {
            int index = i;
            FurnitureColorOption option = _item.colors[i];
            Button swatch = VRUIFactory.CreateButton(
                "Color_" + i, _colorRow, null, () => SelectColor(index), option.color);
            VRUIFactory.SetLayoutSize(swatch, 34f, 34f);

            if (option.mode == FurnitureColorMode.Original)
            {
                // "Original" swatch: show a small label so it isn't mistaken for white.
                TextMeshProUGUI mark = VRUIFactory.CreateText("Mark", swatch.transform, "Orig.", 10f,
                    new Color(0.1f, 0.1f, 0.1f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
                VRUIFactory.Stretch(mark.rectTransform);
            }

            var outline = swatch.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3f, -3f);
            _colorOutlines.Add(outline);
        }
    }

    private void SelectSize(int index)
    {
        _sizeIndex = index;
        RefreshSelection();
    }

    private void SelectColor(int index)
    {
        _colorIndex = index;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        FurnitureSizeOption size = SelectedSize;
        FurnitureColorOption color = SelectedColor;

        for (int i = 0; i < _sizeButtons.Count; i++)
        {
            Image image = _sizeButtons[i].targetGraphic as Image;
            if (image != null) image.color = i == _sizeIndex ? VRUIFactory.AccentColor : VRUIFactory.CardColor;
        }

        for (int i = 0; i < _colorOutlines.Count; i++)
        {
            _colorOutlines[i].enabled = i == _colorIndex;
        }

        _colorHeader.text = color != null ? "Colour: " + color.label : "Colour";

        if (size != null)
        {
            _price.text = FurnitureCatalogUtility.FormatPrice(size.price);
            _dimensions.text = FurnitureCatalogUtility.FormatDimensions(size.widthCm, size.depthCm, size.heightCm);
        }
        else
        {
            _price.text = FurnitureCatalogUtility.FormatPrice(_item.basePrice);
            _dimensions.text = FurnitureCatalogUtility.TryGetModelSize(_item.prefab, out Vector3 meters)
                ? FurnitureCatalogUtility.FormatDimensions(meters.x * 100f, meters.z * 100f, meters.y * 100f)
                : "Dimensions not set";
        }
    }

    private FurnitureSizeOption SelectedSize =>
        _item?.sizes != null && _sizeIndex >= 0 && _sizeIndex < _item.sizes.Count ? _item.sizes[_sizeIndex] : null;

    private FurnitureColorOption SelectedColor =>
        _item?.colors != null && _colorIndex >= 0 && _colorIndex < _item.colors.Count ? _item.colors[_colorIndex] : null;

    private void Place()
    {
        if (_item == null) return;
        PlaceRequested?.Invoke(_item, SelectedSize, SelectedColor);
        Hide();
    }
}
