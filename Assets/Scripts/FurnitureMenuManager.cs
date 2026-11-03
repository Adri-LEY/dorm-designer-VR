using System.Collections.Generic;
using TMPro; // Assuming you use TextMeshPro for text
using UnityEngine;
using UnityEngine.UI;
using Oculus.Interaction;

// Defines what a furniture item is
[System.Serializable]
public class FurnitureItem
{
    public string itemName;
    public string category; // e.g., "Chairs", "Tables", "Decor"; for now "Furnitures" and "Decorations" are the two categories
    public Sprite icon; // The icon to display on the button
    public GameObject prefab; // The 3D model to spawn
}

public class FurnitureMenuManager : MonoBehaviour
{
    [Header("Your Database")]
    public List<FurnitureItem> allFurniture = new List<FurnitureItem>();

    [Header("UI References")]
    public Transform contentPanel;
    public GameObject itemButtonPrefab;

    [Header("Spawning System")]
    public ObjectSpawner spawner;
    void Start()
    {
        SelectTab("All");
    }

    public void SelectTab(string category)
    {
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
        if (btnText != null) btnText.text = item.itemName;

        // Setup the Standard Unity Button
        Button standardBtn = newButton.GetComponent<Button>();
        if (standardBtn != null)
        {
            FurnitureItem capturedItem = item;
            standardBtn.onClick.AddListener(() => SpawnFurniture(capturedItem));
        }
    }

    private void SpawnFurniture(FurnitureItem item)
    {
        Debug.Log("User selected to spawn: " + item.itemName);

        if (spawner != null)
        {
            spawner.objectToSpawn = item.prefab;
            spawner.SpawnObject();
        }
    }
}