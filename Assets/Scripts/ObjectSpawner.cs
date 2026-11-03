using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    public Transform spawnPoint; // The point where the object will be spawned

    public GameObject furnitureBase; // The base furniture object to spawn

    public GameObject objectToSpawn; // The object to spawn

    public GameObject spawnedObject; // The currently spawned object

    public void SpawnObject()
    {
        if (furnitureBase == null || objectToSpawn == null)
        {
            Debug.LogError("Prefab Base or Object to spawn are not assigned.");
            return;
        }

        if (spawnedObject != null)
        {
            Destroy(spawnedObject); // Destroy the previously spawned object if it exists
        }

        // Spawn the new object
        spawnedObject = Instantiate(
            furnitureBase,
            spawnPoint.position,
        spawnPoint.rotation); 

        MeshFilter sourceMesh = objectToSpawn.GetComponentInChildren<MeshFilter>();

        MeshRenderer sourceRenderer = objectToSpawn.GetComponentInChildren<MeshRenderer>();

        if (sourceMesh == null || sourceRenderer == null)
        {
            Debug.LogError("MeshFilter ou MeshRenderer introuvable sur le meuble.");
            Destroy(spawnedObject);
            return;
        }

        // 3. Récupération des composants visuels du cube
        MeshFilter targetMesh = spawnedObject.GetComponent<MeshFilter>();
        MeshRenderer targetRenderer = spawnedObject.GetComponent<MeshRenderer>();

        if (targetMesh == null || targetRenderer == null)
        {
            Debug.LogError("Le prefab cube doit avoir un MeshFilter et un MeshRenderer.");
            Destroy(spawnedObject);
            return;
        }

        // Do not keep the non-uniform scale of the base prefab when replacing its mesh.
        spawnedObject.transform.localScale = sourceMesh.transform.lossyScale;

        // 4. Remplacement du mesh et du matériau
        targetMesh.sharedMesh = sourceMesh.sharedMesh;
        targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

        // 5. Adaptation du collider
        BoxCollider sourceCollider = objectToSpawn.GetComponentInChildren<BoxCollider>();
        BoxCollider targetCollider = spawnedObject.GetComponent<BoxCollider>();

        if (sourceCollider != null && targetCollider != null)
        {
            targetCollider.center = sourceCollider.center;
            targetCollider.size = sourceCollider.size;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
