using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Turns where the right controller points into a point on the floor ("here").
/// Uses Meta's visible controller ray when available, so the target matches what the player sees.
/// Wall hit: point on the floor in front of the wall. Furniture hit: floor point under the hit.
/// Nothing hit: intersection with the floor plane.
/// </summary>
public sealed class PointingTarget : MonoBehaviour
{
    public enum HitKind { None, Floor, Wall, Furniture, Other }

    [Tooltip("Fallback pointer when Meta's ray is not available (RightControllerAnchor).")]
    public Transform pointer;

    [Tooltip("Follow Meta's controller RayInteractor closest to the pointer (matches the visible ray).")]
    public bool useVisibleRay = true;

    [Min(1f)] public float maxDistance = 12f;

    [Tooltip("When pointing at a wall, the target is moved this far into the room.")]
    [Min(0f)] public float wallStandOff = 0.3f;

    public LayerMask layers = ~0;

    private RayInteractor _ray;
    private float _nextRayLookup;

    public bool TryGetTarget(out Vector3 point, out HitKind kind)
    {
        point = Vector3.zero;
        kind = HitKind.None;
        if (!TryGetRay(out Ray ray)) return false;

        Transform rig = pointer != null ? pointer.root : null;
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, layers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // Ignore the player's own hands / controllers.
            if (rig != null && hit.collider.transform.IsChildOf(rig)) continue;

            if (FurnitureCollisionHandler.IsFloor(hit.collider))
            {
                kind = HitKind.Floor;
                point = hit.point;
            }
            else if (IsWall(hit.collider))
            {
                kind = HitKind.Wall;
                Vector3 normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
                if (normal.sqrMagnitude < 0.01f) normal = -Vector3.ProjectOnPlane(ray.direction, Vector3.up);
                point = hit.point + normal.normalized * wallStandOff;
            }
            else
            {
                kind = hit.collider.GetComponentInParent<FurnitureInstanceInfo>() != null ? HitKind.Furniture : HitKind.Other;
                point = hit.point;
            }

            return DropToFloor(ref point);
        }

        // Nothing hit: use the floor plane (useful when the floor collider is thin or missing).
        float floorY = FloorHeight();
        if (ray.direction.y < -0.05f)
        {
            float distance = (floorY - ray.origin.y) / ray.direction.y;
            if (distance > 0f && distance <= maxDistance)
            {
                point = ray.origin + ray.direction * distance;
                kind = HitKind.Floor;
                return DropToFloor(ref point);
            }
        }

        return false;
    }

    private bool TryGetRay(out Ray ray)
    {
        ray = default;
        if (useVisibleRay)
        {
            RayInteractor interactor = FindRayInteractor();
            if (interactor != null && interactor.isActiveAndEnabled && interactor.Ray.direction.sqrMagnitude > 0.5f)
            {
                ray = interactor.Ray;
                return true;
            }
        }

        if (pointer == null) return false;
        ray = new Ray(pointer.position, pointer.forward);
        return true;
    }

    private RayInteractor FindRayInteractor()
    {
        if (_ray != null || pointer == null || Time.unscaledTime < _nextRayLookup) return _ray;
        _nextRayLookup = Time.unscaledTime + 1f;

        float best = 0.3f;
        foreach (RayInteractor candidate in FindObjectsByType<RayInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            // Origin is where the visible ray starts (the controller tip).
            float distance = Vector3.Distance(candidate.Origin, pointer.position);
            if (distance < best)
            {
                best = distance;
                _ray = candidate;
            }
        }

        return _ray;
    }

    /// <summary>Snaps the point onto the floor below it. False when there is no floor (outside the room).</summary>
    private static bool DropToFloor(ref Vector3 point)
    {
        Vector3 origin = new Vector3(point.x, point.y + 2.5f, point.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (!FurnitureCollisionHandler.IsFloor(hit.collider)) continue;
            point = new Vector3(point.x, hit.point.y, point.z);
            return true;
        }

        return false;
    }

    private RoomBuilder _room;

    private float FloorHeight()
    {
        if (_room == null) _room = FindFirstObjectByType<RoomBuilder>();
        return _room != null ? _room.roomCentre.y : 0f;
    }

    private static bool IsWall(Collider collider)
    {
        for (Transform current = collider.transform; current != null; current = current.parent)
        {
            if (current.name.IndexOf("wall", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }
}
