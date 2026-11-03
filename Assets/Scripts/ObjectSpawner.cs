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

        if (sourceMesh == null || sourceRenderer == null || sourceMesh.sharedMesh == null)
        {
            Debug.LogError("MeshFilter ou MeshRenderer introuvable sur le meuble.");
            return;
        }

        // Includes the imported mesh child's transform and scale.
        Bounds sourceWorldBounds = sourceRenderer.bounds;
        spawnedObject = Instantiate(furnitureBase, spawnPoint.position, spawnPoint.rotation);

        MeshFilter targetMesh = spawnedObject.GetComponent<MeshFilter>();
        MeshRenderer targetRenderer = spawnedObject.GetComponent<MeshRenderer>();
        BoxCollider targetCollider = spawnedObject.GetComponent<BoxCollider>();

        if (targetMesh == null || targetRenderer == null)
        {
            Debug.LogError("Le prefab furniture doit avoir un MeshFilter et un MeshRenderer sur sa racine.");
            Destroy(spawnedObject);
            return;
        }

        targetMesh.sharedMesh = sourceMesh.sharedMesh;
        targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

        Vector3 targetMeshSize = targetMesh.sharedMesh.bounds.size;
        if (targetMeshSize.x <= Mathf.Epsilon || targetMeshSize.y <= Mathf.Epsilon || targetMeshSize.z <= Mathf.Epsilon)
        {
            Debug.LogError("Le mesh du prefab furniture a des dimensions invalides.");
            Destroy(spawnedObject);
            return;
        }

        // Match actual dimensions, rather than copying only transform.localScale.
        spawnedObject.transform.localScale = new Vector3(
            sourceWorldBounds.size.x / targetMeshSize.x,
            sourceWorldBounds.size.y / targetMeshSize.y,
            sourceWorldBounds.size.z / targetMeshSize.z);

        if (targetCollider != null)
        {
            // Keep the ray-grab surface aligned with the visible object.
            Bounds localBounds = TransformBoundsToLocal(sourceWorldBounds, spawnedObject.transform);
            targetCollider.center = localBounds.center;
            targetCollider.size = localBounds.size;
        }
    }

    private static Bounds TransformBoundsToLocal(Bounds worldBounds, Transform target)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
        };

        Bounds localBounds = new Bounds(target.InverseTransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
            localBounds.Encapsulate(target.InverseTransformPoint(corners[i]));

        return localBounds;
    }
}
