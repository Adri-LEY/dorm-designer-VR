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
    public GameObject spawnedObject;

    public List<GameObject> allSpawned = new List<GameObject>();
    public void SpawnObject()
    {
        if (headCamera == null || furnitureBase == null || objectToSpawn == null)
        {
            Debug.LogError("HeadCamera, prefab base, or object to spawn are not assigned.");
            return;
        }

        MeshFilter sourceMesh = objectToSpawn.GetComponentInChildren<MeshFilter>();
        MeshRenderer sourceRenderer = objectToSpawn.GetComponentInChildren<MeshRenderer>();
        BoxCollider sourceCollider = objectToSpawn.GetComponentInChildren<BoxCollider>();

        if (sourceMesh == null || sourceRenderer == null || sourceMesh.sharedMesh == null || sourceCollider == null)
        {
            Debug.LogError("MeshFilter, MeshRenderer or BoxCollider missing on target furniture.");
            return;
        }

        // --- NEW SPAWN LOGIC ---
        // 1. Get the forward direction of the user's head on a flat plane
        Vector3 forwardDir = headCamera.forward;
        forwardDir.y = 0;
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
        // -----------------------

        // Instantiate using the dynamic position and rotation
        spawnedObject = Instantiate(furnitureBase, finalSpawnPosition, finalSpawnRotation);

        MeshFilter targetMesh = spawnedObject.GetComponent<MeshFilter>();
        MeshRenderer targetRenderer = spawnedObject.GetComponent<MeshRenderer>();
        BoxCollider targetCollider = spawnedObject.GetComponent<BoxCollider>();

        if (targetMesh == null || targetRenderer == null || targetCollider == null)
        {
            Debug.LogError("The furniture prefab must have a MeshFilter, MeshRenderer, and BoxCollider on its root.");
            Destroy(spawnedObject);
            return;
        }

        spawnedObject.transform.localScale = sourceMesh.transform.lossyScale;
        targetMesh.sharedMesh = sourceMesh.sharedMesh;
        targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

        targetCollider.center = sourceCollider.center;
        targetCollider.size = sourceCollider.size;
        targetCollider.isTrigger = sourceCollider.isTrigger;
        targetCollider.sharedMaterial = sourceCollider.sharedMaterial;
        targetCollider.enabled = sourceCollider.enabled;

        allSpawned.Add(spawnedObject);
    }
}