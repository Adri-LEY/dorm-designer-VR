using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    public Transform spawnPoint;
    public GameObject furnitureBase;
    public GameObject objectToSpawn;
    public GameObject spawnedObject;

    public void SpawnObject()
    {
        if (spawnPoint == null || furnitureBase == null || objectToSpawn == null)
        {
            Debug.LogError("SpawnPoint, prefab base or object to spawn are not assigned.");
            return;
        }

        if (spawnedObject != null)
            Destroy(spawnedObject);

        MeshFilter sourceMesh = objectToSpawn.GetComponentInChildren<MeshFilter>();
        MeshRenderer sourceRenderer = objectToSpawn.GetComponentInChildren<MeshRenderer>();
        BoxCollider sourceCollider = objectToSpawn.GetComponentInChildren<BoxCollider>();

        if (sourceMesh == null || sourceRenderer == null || sourceMesh.sharedMesh == null || sourceCollider == null)
        {
            Debug.LogError("MeshFilter, MeshRenderer ou BoxCollider introuvable sur le meuble.");
            return;
        }

        spawnedObject = Instantiate(furnitureBase, spawnPoint.position, spawnPoint.rotation);

        MeshFilter targetMesh = spawnedObject.GetComponent<MeshFilter>();
        MeshRenderer targetRenderer = spawnedObject.GetComponent<MeshRenderer>();
        BoxCollider targetCollider = spawnedObject.GetComponent<BoxCollider>();

        if (targetMesh == null || targetRenderer == null || targetCollider == null)
        {
            Debug.LogError("Le prefab furniture doit avoir un MeshFilter, un MeshRenderer et un BoxCollider sur sa racine.");
            Destroy(spawnedObject);
            return;
        }

        // The source mesh already contains its dimensions. Keep the source scale
        // and replace the visual content of the furniture prefab.
        spawnedObject.transform.localScale = sourceMesh.transform.lossyScale;
        targetMesh.sharedMesh = sourceMesh.sharedMesh;
        targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

        // Reuse the collider already referenced by the grab interaction.
        targetCollider.center = sourceCollider.center;
        targetCollider.size = sourceCollider.size;
        targetCollider.isTrigger = sourceCollider.isTrigger;
        targetCollider.sharedMaterial = sourceCollider.sharedMaterial;
        targetCollider.enabled = sourceCollider.enabled;


    }
}
