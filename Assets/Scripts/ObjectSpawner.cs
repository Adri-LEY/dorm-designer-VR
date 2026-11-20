using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates furniture in the room. Every spawn first looks for a free pose for the furniture's box
/// at its final size, using the same rule as when moving furniture (<see cref="FurnitureCollisionHandler.IsBoxFree"/>),
/// so a spawned piece can always be moved. Two searches exist:
/// in front of the player (catalogue menu) and around a pointed point (voice "place a chair here").
/// The models' front is +Z: "facing the player" means +Z points toward the head.
/// </summary>
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

    [Header("Free-space search (menu: in front of the player)")]
    [Tooltip("Minimum gap between the player's head and the near side of the furniture, in metres.")]
    public float personalSpace = 0.35f;
    [Tooltip("How much further than Spawn Distance the search may go, in metres.")]
    public float extraSearchDistance = 1.5f;
    [Tooltip("Largest angle away from where the player looks that the search may use, in degrees.")]
    [Range(0f, 180f)] public float maxSearchAngle = 90f;
    [Tooltip("Also try the furniture turned by 90 degrees (helps long items in narrow rooms).")]
    public bool allowQuarterTurn = true;

    [Header("Free-space search (voice: around a pointed point)")]
    [Tooltip("How far from the pointed point the furniture may end up, in metres.")]
    public float maxDistanceFromTarget = 1.5f;
    [Tooltip("If a wall is closer than this to the pointed point, the furniture is put with its back to that wall.")]
    public float wallAlignDistance = 0.9f;

    [Header("References")]
    public GameObject furnitureBase;
    public GameObject objectToSpawn;

    public List<GameObject> allSpawned = new List<GameObject>();

    /// <summary>Why the last spawn failed, ready to show to the player. Empty after a success.</summary>
    public string LastFailureMessage { get; private set; } = string.Empty;

    /// <summary>Number of furniture in the room (deleted ones, including soft-deleted ones kept for undo, are ignored).</summary>
    public int SpawnedCount
    {
        get
        {
            allSpawned.RemoveAll(go => go == null);
            int count = 0;
            foreach (GameObject go in allSpawned)
            {
                if (go.activeSelf) count++;
            }

            return count;
        }
    }

    /// <summary>Legacy entry point: spawns <see cref="objectToSpawn"/> at its model size and colour.</summary>
    public void SpawnObject()
    {
        Spawn(new FurnitureItem { itemName = objectToSpawn != null ? objectToSpawn.name : "furniture", prefab = objectToSpawn }, null, null);
    }

    /// <summary>Spawns a catalogue item in front of the player, with the chosen size and colour (both optional).</summary>
    public GameObject Spawn(FurnitureItem item, FurnitureSizeOption size, FurnitureColorOption color)
    {
        if (!TryPrepare(item, size, out Placement placement)) return null;

        if (!TryFindPoseInFront(placement, out Vector3 position, out Quaternion rotation))
        {
            return Fail($"No free space for {DisplayName(item)} here. Move to a more open spot or turn around, then try again.");
        }

        return Create(placement, item, size, color, position, rotation);
    }

    /// <summary>
    /// Spawns a catalogue item at the free spot closest to <paramref name="target"/> (a point on the floor),
    /// within <see cref="maxDistanceFromTarget"/>. Used by voice commands ("place a chair here").
    /// </summary>
    public GameObject SpawnNear(FurnitureItem item, FurnitureSizeOption size, FurnitureColorOption color, Vector3 target)
    {
        if (!TryPrepare(item, size, out Placement placement)) return null;

        if (!TryFindPoseNear(placement, target, out Vector3 position, out Quaternion rotation))
        {
            return Fail($"No free space for {DisplayName(item)} around that spot. Point at a clearer area and try again.");
        }

        return Create(placement, item, size, color, position, rotation);
    }

    /// <summary>
    /// Finds the free pose closest to <paramref name="target"/> for furniture already in the room (voice "move this here",
    /// undo). The furniture ignores itself. Near a wall it is put with its back to the wall; otherwise it keeps
    /// <paramref name="currentRotation"/> if possible.
    /// </summary>
    public bool TryFindFreePoseNear(GameObject furniture, Vector3 target, Quaternion currentRotation,
        out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        BoxCollider box = furniture != null ? furniture.GetComponent<BoxCollider>() : null;
        if (box == null || headCamera == null) return false;

        Placement placement = FromCollider(box, furniture.transform.lossyScale);
        return TryFindPoseNear(placement, target, out position, out rotation, furniture.transform, currentRotation);
    }

    /// <summary>Removes every spawned furniture (used when switching room).</summary>
    public void ClearSpawned()
    {
        if (FurnitureHistory.Instance != null) FurnitureHistory.Instance.Clear();

        foreach (GameObject spawned in allSpawned)
        {
            if (spawned != null) Destroy(spawned);
        }

        allSpawned.Clear();
    }

    // ------------------------------------------------------------------ Preparation / creation

    /// <summary>Everything needed to test and create a furniture box at its final size.</summary>
    private struct Placement
    {
        public MeshFilter SourceMesh;
        public MeshRenderer SourceRenderer;
        public BoxCollider SourceCollider;
        public Vector3 Scale;
        public Vector3 HalfExtents;
        public Vector3 CenterOffset;
    }

    private bool TryPrepare(FurnitureItem item, FurnitureSizeOption size, out Placement placement)
    {
        placement = default;
        GameObject prefab = item != null ? item.prefab : null;
        if (headCamera == null || furnitureBase == null || prefab == null)
        {
            Debug.LogError("HeadCamera, prefab base, or object to spawn are not assigned.");
            LastFailureMessage = "This furniture cannot be placed (setup error, see the Console).";
            return false;
        }

        placement.SourceMesh = prefab.GetComponentInChildren<MeshFilter>();
        placement.SourceRenderer = prefab.GetComponentInChildren<MeshRenderer>();
        placement.SourceCollider = prefab.GetComponentInChildren<BoxCollider>();

        if (placement.SourceMesh == null || placement.SourceRenderer == null ||
            placement.SourceMesh.sharedMesh == null || placement.SourceCollider == null)
        {
            Debug.LogError("MeshFilter, MeshRenderer or BoxCollider missing on target furniture.");
            LastFailureMessage = "This furniture cannot be placed (setup error, see the Console).";
            return false;
        }

        // Final scale first: the free-space search needs the real box size.
        Vector3 modelScale = placement.SourceMesh.transform.lossyScale;
        placement.Scale = size != null
            ? FurnitureCatalogUtility.ComputeScaleForSize(placement.SourceMesh.sharedMesh, modelScale, size)
            : modelScale;

        Placement box = FromCollider(placement.SourceCollider, placement.Scale);
        placement.HalfExtents = box.HalfExtents;
        placement.CenterOffset = box.CenterOffset;
        return true;
    }

    private static Placement FromCollider(BoxCollider collider, Vector3 scale)
    {
        Vector3 absScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return new Placement
        {
            SourceCollider = collider,
            Scale = scale,
            HalfExtents = Vector3.Scale(collider.size * 0.5f, absScale),
            CenterOffset = Vector3.Scale(collider.center, scale),
        };
    }

    private GameObject Create(Placement placement, FurnitureItem item, FurnitureSizeOption size, FurnitureColorOption color,
        Vector3 position, Quaternion rotation)
    {
        GameObject spawnedObject = Instantiate(furnitureBase, position, rotation);

        MeshFilter targetMesh = spawnedObject.GetComponent<MeshFilter>();
        MeshRenderer targetRenderer = spawnedObject.GetComponent<MeshRenderer>();
        BoxCollider targetCollider = spawnedObject.GetComponent<BoxCollider>();

        if (targetMesh == null || targetRenderer == null || targetCollider == null)
        {
            Debug.LogError("The furniture prefab must have a MeshFilter, MeshRenderer, and BoxCollider on its root.");
            Destroy(spawnedObject);
            LastFailureMessage = "This furniture cannot be placed (setup error, see the Console).";
            return null;
        }

        // Size: the model's own scale, or rescaled to the chosen real dimensions.
        spawnedObject.transform.localScale = placement.Scale;
        targetMesh.sharedMesh = placement.SourceMesh.sharedMesh;

        // Colour: tinted / replaced material copies are owned by FurnitureInstanceInfo.
        var createdMaterials = new List<Material>();
        targetRenderer.sharedMaterials = FurnitureCatalogUtility.BuildMaterials(
            placement.SourceRenderer.sharedMaterials, color, createdMaterials);

        BoxCollider sourceCollider = placement.SourceCollider;
        targetCollider.center = sourceCollider.center;
        targetCollider.size = sourceCollider.size;
        targetCollider.isTrigger = sourceCollider.isTrigger;
        targetCollider.sharedMaterial = sourceCollider.sharedMaterial;
        targetCollider.enabled = sourceCollider.enabled;

        var info = spawnedObject.AddComponent<FurnitureInstanceInfo>();
        info.Initialize(item, size, color, createdMaterials);

        allSpawned.Add(spawnedObject);
        FurnitureHistory.Instance.RecordPlaced(spawnedObject);
        LastFailureMessage = string.Empty;
        return spawnedObject;
    }

    private GameObject Fail(string message)
    {
        LastFailureMessage = message;
        Debug.Log("[ObjectSpawner] " + message);
        return null;
    }

    private static string DisplayName(FurnitureItem item) =>
        item != null ? FurnitureCatalogUtility.PrettyName(item.itemName) : "this furniture";

    // ------------------------------------------------------------------ Search: in front of the player

    private struct Candidate
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Cost;
    }

    /// <summary>
    /// Prefers straight ahead at <see cref="spawnDistance"/>, then nearer/further, then to the sides.
    /// </summary>
    private bool TryFindPoseInFront(Placement placement, out Vector3 position, out Quaternion rotation)
    {
        Vector3 halfExtents = placement.HalfExtents;
        Vector3 centerOffset = placement.CenterOffset;
        Vector3 forward = FlatForward(headCamera);
        Vector3 head = headCamera.position;

        var candidates = new List<Candidate>();
        const float angleStep = 15f;
        const float distanceStep = 0.2f;

        for (int turn = 0; turn < (allowQuarterTurn ? 2 : 1); turn++)
        {
            // Half-size of the furniture along the direction to the player.
            float halfAlongView = turn == 0 ? halfExtents.z + Mathf.Abs(centerOffset.z) : halfExtents.x + Mathf.Abs(centerOffset.x);
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

        return PickFirstValid(candidates, placement, head, null, out position, out rotation);
    }

    // ------------------------------------------------------------------ Search: around a pointed point

    /// <summary>
    /// Rings around <paramref name="target"/>, closest first. Orientation: back against a nearby wall when there
    /// is one, otherwise facing the player; the other orientations are tried too, at a small extra cost.
    /// </summary>
    private bool TryFindPoseNear(Placement placement, Vector3 target, out Vector3 position, out Quaternion rotation,
        Transform ignoreRoot = null, Quaternion? keepRotation = null)
    {
        Vector3 head = headCamera.position;
        Vector3 toHead = head - target;
        toHead.y = 0f;
        Quaternion facePlayer = toHead.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(toHead.normalized)
            : Quaternion.LookRotation(-FlatForward(headCamera));

        // Existing furniture (move/undo) prefers to keep its rotation; new furniture faces the player.
        Quaternion natural = keepRotation.HasValue
            ? Quaternion.Euler(0f, keepRotation.Value.eulerAngles.y, 0f)
            : facePlayer;

        var orientations = new List<(Quaternion rotation, float cost)>();
        if (TryFindNearbyWall(target, out Vector3 wallNormal))
        {
            Quaternion backToWall = Quaternion.LookRotation(wallNormal);
            orientations.Add((backToWall, 0f));
            orientations.Add((natural, 0.25f));
            if (keepRotation.HasValue) orientations.Add((facePlayer, 0.3f));
            if (allowQuarterTurn) orientations.Add((backToWall * Quaternion.Euler(0f, 90f, 0f), 0.4f));
        }
        else
        {
            orientations.Add((natural, 0f));
            if (keepRotation.HasValue) orientations.Add((facePlayer, 0.3f));
        }

        if (allowQuarterTurn) orientations.Add((natural * Quaternion.Euler(0f, 90f, 0f), 0.4f));

        var candidates = new List<Candidate>();
        const float ringStep = 0.1f;
        for (float radius = 0f; radius <= maxDistanceFromTarget + 0.001f; radius += ringStep)
        {
            int samples = radius < 0.001f ? 1 : Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / ringStep));
            for (int i = 0; i < samples; i++)
            {
                float angle = i * 2f * Mathf.PI / samples;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Vector3 candidate = target + offset;
                candidate.y = spawnYOffset;

                foreach ((Quaternion orientation, float orientationCost) in orientations)
                {
                    candidates.Add(new Candidate { Position = candidate, Rotation = orientation, Cost = radius + orientationCost });
                }
            }
        }

        return PickFirstValid(candidates, placement, head, target, out position, out rotation, ignoreRoot);
    }

    /// <summary>Closest wall around a floor point (horizontal rays at knee height). Normal points into the room.</summary>
    private bool TryFindNearbyWall(Vector3 point, out Vector3 normal)
    {
        normal = Vector3.zero;
        Vector3 origin = new Vector3(point.x, point.y + 0.5f, point.z);
        float best = wallAlignDistance;
        for (int i = 0; i < 8; i++)
        {
            Vector3 direction = Quaternion.AngleAxis(i * 45f, Vector3.up) * Vector3.forward;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, wallAlignDistance, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                if (!IsWall(hit.collider) || hit.distance >= best) continue;
                Vector3 flatNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
                if (flatNormal.sqrMagnitude < 0.01f) continue;
                best = hit.distance;
                normal = flatNormal.normalized;
            }
        }

        return normal != Vector3.zero;
    }

    // ------------------------------------------------------------------ Shared validity rules

    /// <summary>
    /// First candidate (lowest cost) whose box is on the floor, overlaps nothing, keeps personal space around
    /// the head, and is not separated by a wall from the player (or from the pointed target, when given).
    /// </summary>
    private bool PickFirstValid(List<Candidate> candidates, Placement placement, Vector3 head, Vector3? target,
        out Vector3 position, out Quaternion rotation, Transform ignoreRoot = null)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        candidates.Sort((a, b) => a.Cost.CompareTo(b.Cost));
        Vector3 reference = target.HasValue ? target.Value + Vector3.up : head;
        foreach (Candidate candidate in candidates)
        {
            Vector3 worldCenter = candidate.Position + candidate.Rotation * placement.CenterOffset;
            if (!IsOnFloor(worldCenter)) continue;
            if (IsWallBetween(reference, worldCenter)) continue;
            if (!KeepsPersonalSpace(head, worldCenter, candidate.Rotation, placement.HalfExtents)) continue;
            if (!FurnitureCollisionHandler.IsBoxFree(worldCenter, placement.HalfExtents, candidate.Rotation, ignoreRoot, out _)) continue;

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

    /// <summary>True if a wall stands between the two points (horizontally, at the height of <paramref name="from"/>).</summary>
    private bool IsWallBetween(Vector3 from, Vector3 to)
    {
        Vector3 target = new Vector3(to.x, from.y, to.z);
        Vector3 delta = target - from;
        float distance = delta.magnitude;
        if (distance < 0.001f) return false;

        foreach (RaycastHit hit in Physics.RaycastAll(from, delta / distance, distance, obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            if (IsWall(hit.collider)) return true;
        }

        return false;
    }

    /// <summary>The box must stay at least <see cref="personalSpace"/> away from the head (horizontally).</summary>
    private bool KeepsPersonalSpace(Vector3 head, Vector3 boxCenter, Quaternion rotation, Vector3 halfExtents)
    {
        Vector3 local = Quaternion.Inverse(rotation) * (head - boxCenter);
        float gapX = Mathf.Max(0f, Mathf.Abs(local.x) - halfExtents.x);
        float gapZ = Mathf.Max(0f, Mathf.Abs(local.z) - halfExtents.z);
        return new Vector2(gapX, gapZ).magnitude >= personalSpace;
    }

    private static bool IsWall(Collider collider)
    {
        for (Transform current = collider != null ? collider.transform : null; current != null; current = current.parent)
        {
            if (current.name.IndexOf("wall", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    private static Vector3 FlatForward(Transform t)
    {
        Vector3 forward = t.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        return forward.normalized;
    }
}
