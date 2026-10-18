using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    public Transform spawnPoint; // The point where the object will be spawned

    public GameObject objectToSpawn; // The object to spawn

    public GameObject spawnedObject; // The currently spawned object

    public void SpawnObject()
    {
        if (objectToSpawn == null)
        {
            Debug.LogError("Object to spawn is not assigned.");
            return;
        }

        if(spawnedObject != null)
        {
            Destroy(spawnedObject); // Destroy the previously spawned object if it exists
        }

        spawnedObject = Instantiate(
            objectToSpawn, 
            spawnPoint.position, 
        spawnPoint.rotation); // Spawn the new object
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
