using System.Collections.Generic;
using TMPro; // Assuming you use TextMeshPro for text
using UnityEngine;
using UnityEngine.UI;

// Defines what a furniture item is
[System.Serializable]
public class FurnitureItem
{
    public string itemName;
    public string category; // e.g., "Bed", "Chair", "Desk", "Wardrobe"
    public Sprite icon; // The icon to display on the button
    public GameObject prefab; // The 3D model to spawn

    [Header("Product details")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Price in euros, used only when the item has no size options.")]
    [Min(0f)] public float basePrice;

    [Tooltip("Purchasable sizes. The first one is selected by default. Leave empty to use the model's own size.")]
    public List<FurnitureSizeOption> sizes = new List<FurnitureSizeOption>();

    [Tooltip("Purchasable colours / finishes. The first one is selected by default.")]
    public List<FurnitureColorOption> colors = new List<FurnitureColorOption>();
}

public class FurnitureMenuManager : MonoBehaviour
{
    [Header("Your Database")]
    public List<FurnitureItem> allFurniture = new List<FurnitureItem>();

    [Header("UI References")]
    public Transform contentPanel;
    public GameObject itemButtonPrefab;

    [Header("Detail panel")]
    [Tooltip("Optional. Created automatically inside the menu canvas when left empty.")]
    public FurnitureDetailPanel detailPanel;
    [Tooltip("Height kept free at the top of the canvas for the tab bar, in canvas units.")]
    public float detailPanelTopInset = 50f;

    [Header("Spawning System")]
    public ObjectSpawner spawner;

    [Header("After placing")]
    [Tooltip("Close the whole menu once a furniture has been placed, so the player sees it right away.")]
    public bool closeMenuAfterPlacing = true;
    [Tooltip("Found automatically when left empty.")]
    public VRMenuController menuController;

    void Start()
    {
        if (menuController == null) menuController = FindFirstObjectByType<VRMenuController>();
        EnsureDetailPanel();
        SelectTab("All");
    }

    public void SelectTab(string category)
    {
        if (detailPanel != null) detailPanel.Hide();

        foreach (Transform child in contentPanel)
        {
            Destroy(child.gameObject);
        }

        foreach (FurnitureItem item in allFurniture)
        {
            if (category == "All" || item.category == category)
            {
                CreateButton(item);
            }
        }
    }

    private void EnsureDetailPanel()
    {
        if (detailPanel == null && contentPanel != null)
        {
            Canvas canvas = contentPanel.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                detailPanel = FurnitureDetailPanel.Create((RectTransform)canvas.transform, detailPanelTopInset);
            }
        }

        if (detailPanel != null)
        {
            detailPanel.PlaceRequested -= SpawnFurniture;
            detailPanel.PlaceRequested += SpawnFurniture;
        }
    }

    private void CreateButton(FurnitureItem item)
    {
        // The 'false' is crucial for VR Canvas elements
        GameObject newButton = Instantiate(itemButtonPrefab, contentPanel, false);

        newButton.transform.localPosition = new Vector3(newButton.transform.localPosition.x, newButton.transform.localPosition.y, 0f);
        newButton.transform.localScale = Vector3.one;
        newButton.transform.localRotation = Quaternion.identity;

        // Set the Icon
        Transform imageChild = newButton.transform.Find("Image");
        if (imageChild != null)
        {
            Image iconImage = imageChild.GetComponent<Image>();
            if (iconImage != null && item.icon != null)
            {
                iconImage.sprite = item.icon;
            }
        }

        TMP_Text btnText = newButton.GetComponentInChildren<TMP_Text>();
        if (btnText != null) btnText.text = FurnitureCatalogUtility.PrettyName(item.itemName);

        // Setup the Standard Unity Button: open the product sheet first.
        Button standardBtn = newButton.GetComponent<Button>();
        if (standardBtn != null)
        {
            FurnitureItem capturedItem = item;
            standardBtn.onClick.AddListener(() => ShowDetails(capturedItem));
        }
    }

    public void ShowDetails(FurnitureItem item)
    {
        if (detailPanel != null)
        {
            detailPanel.Show(item);
        }
        else
        {
            // No canvas found for the detail panel: keep the old one-click behaviour.
            SpawnFurniture(item, null, null);
        }
    }

    private void SpawnFurniture(FurnitureItem item, FurnitureSizeOption size, FurnitureColorOption color)
    {
        Debug.Log($"User selected to spawn: {item.itemName} ({size?.label ?? "model size"}, {color?.label ?? "original colour"})");

        if (spawner == null) return;

        GameObject spawned = spawner.Spawn(item, size, color);
        if (spawned == null)
        {
            // Keep the sheet open so the player can move, pick a smaller size, and retry.
            string message = string.IsNullOrEmpty(spawner.LastFailureMessage)
                ? "This furniture could not be placed."
                : spawner.LastFailureMessage;
            if (detailPanel != null) detailPanel.ShowMessage(message);
            return;
        }

        // Back to the grid for next time, and get the menu out of the way.
        if (detailPanel != null) detailPanel.Hide();
        if (closeMenuAfterPlacing && menuController != null) menuController.Close();
    }
}
