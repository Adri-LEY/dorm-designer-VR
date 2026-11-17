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

    [Header("Free-space search")]
    [Tooltip("Minimum gap between the player's head and the near side of the furniture, in metres.")]
    public float personalSpace = 0.35f;
    [Tooltip("How much further than Spawn Distance the search may go, in metres.")]
    public float extraSearchDistance = 1.5f;
    [Tooltip("Largest angle away from where the player looks that the search may use, in degrees.")]
    [Range(0f, 180f)] public float maxSearchAngle = 90f;
    [Tooltip("Also try the furniture turned by 90 degrees (helps long items in narrow rooms).")]
    public bool allowQuarterTurn = true;

    /// <summary>Why the last spawn failed, ready to show to the player. Empty after a success.</summary>
    public string LastFailureMessage { get; private set; } = string.Empty;

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

        // Final scale first: the free-space search needs the real box size.
        Vector3 modelScale = sourceMesh.transform.lossyScale;
        Vector3 finalScale = size != null
            ? FurnitureCatalogUtility.ComputeScaleForSize(sourceMesh.sharedMesh, modelScale, size)
            : modelScale;

        if (!TryFindFreePose(sourceCollider, finalScale, out Vector3 finalSpawnPosition, out Quaternion finalSpawnRotation))
        {
            string itemName = item != null ? FurnitureCatalogUtility.PrettyName(item.itemName) : "this furniture";
            LastFailureMessage =
                $"No free space for {itemName} here. Move to a more open spot or turn around, then try again.";
            Debug.Log("[ObjectSpawner] " + LastFailureMessage);
            return null;
        }

        // Instantiate using the free position and rotation
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
        spawnedObject.transform.localScale = finalScale;

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
        LastFailureMessage = string.Empty;
        return spawnedObject;
    }

    private struct Candidate
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Cost;
    }

    /// <summary>
    /// Looks for a pose in front of the player where the furniture box is on the floor,
    /// not behind a wall, and overlaps nothing (same rule as when moving furniture).
    /// Prefers straight ahead at <see cref="spawnDistance"/>, then nearer/further, then to the sides.
    /// </summary>
    private bool TryFindFreePose(BoxCollider box, Vector3 scale, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        Vector3 absScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, absScale);
        Vector3 centerOffset = Vector3.Scale(box.center, scale);

        Vector3 forward = headCamera.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 head = headCamera.position;
        var candidates = new List<Candidate>();
        const float angleStep = 15f;
        const float distanceStep = 0.2f;

        for (int turn = 0; turn < (allowQuarterTurn ? 2 : 1); turn++)
        {
            // Half-size of the furniture along the direction to the player.
            float halfAlongView = (turn == 0 ? halfExtents.z + Mathf.Abs(centerOffset.z) : halfExtents.x + Mathf.Abs(centerOffset.x));
            float minDistance = halfAlongView + personalSpace;
            float preferred = Mathf.Max(spawnDistance, minDistance);
            float maxDistance = preferred + extraSearchDistance;

            for (float angle = 0f; angle <= maxSearchAngle + 0.01f; angle += angleStep)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (angle == 0f && side == 1) continue;
                    Vector3 direction = Quaternion.AngleAxis(angle * side, Vector3.up) * forward;
                    Quaternion facing = Quaternion.LookRotation(-direction) * Quaternion.Euler(0f, turn * 90f, 0f);

                    for (float distance = minDistance; distance <= maxDistance + 0.01f; distance += distanceStep)
                    {
                        Vector3 candidate = head + direction * distance;
                        candidate.y = spawnYOffset;
                        candidates.Add(new Candidate
                        {
                            Position = candidate,
                            Rotation = facing,
                            Cost = angle / angleStep + Mathf.Abs(distance - preferred) / 0.3f + turn * 2.5f,
                        });
                    }
                }
            }
        }

        candidates.Sort((a, b) => a.Cost.CompareTo(b.Cost));
        foreach (Candidate candidate in candidates)
        {
            Vector3 worldCenter = candidate.Position + candidate.Rotation * centerOffset;
            if (!IsOnFloor(worldCenter)) continue;
            if (IsBehindWall(head, worldCenter)) continue;
            if (!FurnitureCollisionHandler.IsBoxFree(worldCenter, halfExtents, candidate.Rotation, null, out _)) continue;

            position = candidate.Position;
            rotation = candidate.Rotation;
            return true;
        }

        return false;
    }

    private static bool IsOnFloor(Vector3 point)
    {
        Vector3 origin = new Vector3(point.x, point.y + 3f, point.z);
        foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (FurnitureCollisionHandler.IsFloor(hit.collider)) return true;
        }

        return false;
    }

    /// <summary>True if a wall stands between the player and the point (so the spot is in another room / outside).</summary>
    private bool IsBehindWall(Vector3 head, Vector3 point)
    {
        Vector3 target = new Vector3(point.x, head.y, point.z);
        Vector3 toTarget = target - head;
        float distance = toTarget.magnitude;
        if (distance < 0.001f) return false;

        foreach (RaycastHit hit in Physics.RaycastAll(head, toTarget / distance, distance, obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            for (Transform current = hit.collider.transform; current != null; current = current.parent)
            {
                if (current.name.IndexOf("wall", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
        }

        return false;
    }
}
