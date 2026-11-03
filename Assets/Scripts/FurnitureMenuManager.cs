using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Assuming you use TextMeshPro for text

// Defines what a furniture item is
[System.Serializable]
public class FurnitureItem
{
    public string itemName;
    public string category; // e.g., "Chairs", "Tables", "Decor"; for now "Furnitures" and "Decorations" are the two categories
    // public Sprite icon; // The icon to display on the button
    // public GameObject prefab; // The 3D model to spawn
}

public class FurnitureMenuManager : MonoBehaviour
{
    [Header("Your Database")]
    public List<FurnitureItem> allFurniture = new List<FurnitureItem>();

    [Header("UI References")]
    public Transform contentPanel; // The panel holding the grid
    public GameObject itemButtonPrefab; // The button template to spawn

    void Start()
    {
        // Load everything when the menu first starts
        SelectTab("All");
    }

    // Call this method from your Tab Buttons in the Inspector
    public void SelectTab(string category)
    {
        // 1. Destroy old buttons to clean the view
        foreach (Transform child in contentPanel)
        {
            Destroy(child.gameObject);
        }

        // 2. Generate new buttons based on the chosen category
        foreach (FurnitureItem item in allFurniture)
        {
            if (category == "All" || item.category == category)
            {
                CreateButton(item);
            }
        }
    }

    private void CreateButton(FurnitureItem item)
    {
        GameObject newButton = Instantiate(itemButtonPrefab, contentPanel);

        // Set the text on the button
        TMP_Text btnText = newButton.GetComponentInChildren<TMP_Text>();
        if (btnText != null) btnText.text = item.itemName;

        // Make the button do something when clicked
        Button btnComp = newButton.GetComponent<Button>();
        btnComp.onClick.AddListener(() => SpawnFurniture(item));
    }

    private void SpawnFurniture(FurnitureItem item)
    {
        Debug.Log("User selected to spawn: " + item.itemName);
        // TODO: Instantiate your 3D furniture prefab in front of the user here
    }
}