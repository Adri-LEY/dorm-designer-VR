using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FurnitureCollisionHandler : MonoBehaviour
{
    private int overlappingCount = 0;
    private BoxCollider furnitureCollider;

    public bool HasCollision => overlappingCount > 0;

    private void Awake()
    {
        furnitureCollider = GetComponent<BoxCollider>();
    }

    /// <summary>
    /// Returns false when the furniture box would overlap another solid collider
    /// at the proposed pose. The pose is checked before the Transform is moved.
    /// </summary>
    public bool IsPoseValid(Vector3 position, Quaternion rotation)
    {
        if (furnitureCollider == null || !furnitureCollider.enabled)
        {
            return true;
        }

        Vector3 lossyScale = transform.lossyScale;
        Vector3 centerOffset = Vector3.Scale(furnitureCollider.center, lossyScale);
        Vector3 halfExtents = Vector3.Scale(
            furnitureCollider.size * 0.5f,
            new Vector3(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)));

        Vector3 worldCenter = position + rotation * centerOffset;
        Collider[] overlaps = Physics.OverlapBox(
            worldCenter,
            halfExtents,
            rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        foreach (Collider other in overlaps)
        {
            if (other == furnitureCollider || other.transform == transform || other.transform.IsChildOf(transform))
            {
                continue;
            }

            // The furniture is allowed to rest on the floor.
            if (other.gameObject.name.ToLowerInvariant().Contains("floor"))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Ignore ground collision if you only want to detect wall/furniture overlap
        if (!collision.gameObject.name.ToLower().Contains("floor"))
        {
            overlappingCount++;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!collision.gameObject.name.ToLower().Contains("floor"))
        {
            overlappingCount = Mathf.Max(0, overlappingCount - 1);
        }
    }

    public void OnGrabStart()
    {
    }

    public void OnGrabEnd()
    {
        // Collision reporting remains available, but this component no longer
        // writes the furniture Transform during release.
        overlappingCount = 0;
    }
}
