using System.Collections.Generic;
using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("The camera representing the player's head")]
    public Transform headCamera;
    [Tooltip("How far away from the player the object should spawn")]
    public float spawnDistance = 1.5f;
    [Tooltip("Slight lift from the absolute 0 floor to prevent collision drag")]
    public float spawnYOffset = 0.05f;
    [Tooltip("Layers to treat as walls to avoid spawning inside them")]
    public LayerMask obstacleLayers = ~0;

    [Header("References")]
    public GameObject furnitureBase;
    public GameObject objectToSpawn;

    public List<GameObject> allSpawned = new List<GameObject>();

    /// <summary>Number of spawned furniture still in the room (deleted ones are ignored).</summary>
    public int SpawnedCount
    {
        get
        {
            allSpawned.RemoveAll(go => go == null);
            return allSpawned.Count;
        }
    }

    /// <summary>Legacy entry point: spawns <see cref="objectToSpawn"/> at its model size and colour.</summary>
    public void SpawnObject()
    {
        SpawnPrefab(objectToSpawn, null, null, null);
    }

    /// <summary>Spawns a catalogue item with the chosen size and colour (both optional).</summary>
    public GameObject Spawn(FurnitureItem item, FurnitureSizeOption size, FurnitureColorOption color)
    {
        if (item == null)
        {
            Debug.LogError("Cannot spawn a null furniture item.");
            return null;
        }

        objectToSpawn = item.prefab;
        return SpawnPrefab(item.prefab, item, size, color);
    }

    /// <summary>Removes every spawned furniture (used when switching room).</summary>
    public void ClearSpawned()
    {
        foreach (GameObject spawned in allSpawned)
        {
            if (spawned != null) Destroy(spawned);
        }

        allSpawned.Clear();
    }

    private GameObject SpawnPrefab(GameObject prefab, FurnitureItem item, FurnitureSizeOption size, FurnitureColorOption color)
    {
        if (headCamera == null || furnitureBase == null || prefab == null)
        {
            Debug.LogError("HeadCamera, prefab base, or object to spawn are not assigned.");
            return null;
        }

        MeshFilter sourceMesh = prefab.GetComponentInChildren<MeshFilter>();
        MeshRenderer sourceRenderer = prefab.GetComponentInChildren<MeshRenderer>();
        BoxCollider sourceCollider = prefab.GetComponentInChildren<BoxCollider>();

        if (sourceMesh == null || sourceRenderer == null || sourceMesh.sharedMesh == null || sourceCollider == null)
        {
            Debug.LogError("MeshFilter, MeshRenderer or BoxCollider missing on target furniture.");
            return null;
        }

        // 1. Get the forward direction of the user's head on a flat plane
        Vector3 forwardDir = headCamera.forward;
        forwardDir.y = 0;
        if (forwardDir.sqrMagnitude < 0.0001f) forwardDir = Vector3.forward;
        forwardDir.Normalize();

        Vector3 finalSpawnPosition = headCamera.position;

        // 2. Raycast to prevent spawning inside walls
        // We cast from the head position forward. If we hit something, we spawn slightly in front of it.
        if (Physics.Raycast(headCamera.position, forwardDir, out RaycastHit hit, spawnDistance, obstacleLayers))
        {
            // Stop 30cm short of the wall to give the furniture's bounding box room
            float adjustedDistance = Mathf.Max(0.5f, hit.distance - 0.3f);
            finalSpawnPosition += forwardDir * adjustedDistance;
        }
        else
        {
            // Path is clear, spawn at the standard distance
            finalSpawnPosition += forwardDir * spawnDistance;
        }

        // 3. Apply the fixed Y offset for the floor (matches your old 0.044 setup)
        finalSpawnPosition.y = spawnYOffset;

        // 4. Make the furniture face the player
        Quaternion finalSpawnRotation = Quaternion.LookRotation(-forwardDir);

        // Instantiate using the dynamic position and rotation
        GameObject spawnedObject = Instantiate(furnitureBase, finalSpawnPosition, finalSpawnRotation);

        MeshFilter targetMesh = spawnedObject.GetComponent<MeshFilter>();
        MeshRenderer targetRenderer = spawnedObject.GetComponent<MeshRenderer>();
        BoxCollider targetCollider = spawnedObject.GetComponent<BoxCollider>();

        if (targetMesh == null || targetRenderer == null || targetCollider == null)
        {
            Debug.LogError("The furniture prefab must have a MeshFilter, MeshRenderer, and BoxCollider on its root.");
            Destroy(spawnedObject);
            return null;
        }

        // Size: the model's own scale, or rescaled to the chosen real dimensions.
        Vector3 modelScale = sourceMesh.transform.lossyScale;
        spawnedObject.transform.localScale = size != null
            ? FurnitureCatalogUtility.ComputeScaleForSize(sourceMesh.sharedMesh, modelScale, size)
            : modelScale;

        targetMesh.sharedMesh = sourceMesh.sharedMesh;

        // Colour: tinted / replaced material copies are owned by FurnitureInstanceInfo.
        var createdMaterials = new List<Material>();
        targetRenderer.sharedMaterials = FurnitureCatalogUtility.BuildMaterials(
            sourceRenderer.sharedMaterials, color, createdMaterials);

        targetCollider.center = sourceCollider.center;
        targetCollider.size = sourceCollider.size;
        targetCollider.isTrigger = sourceCollider.isTrigger;
        targetCollider.sharedMaterial = sourceCollider.sharedMaterial;
        targetCollider.enabled = sourceCollider.enabled;

        var info = spawnedObject.AddComponent<FurnitureInstanceInfo>();
        info.Initialize(item, size, color, createdMaterials);

        allSpawned.Add(spawnedObject);
        return spawnedObject;
    }
}
