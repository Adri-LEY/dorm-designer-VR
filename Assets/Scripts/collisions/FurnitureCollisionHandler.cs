using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FurnitureCollisionHandler : MonoBehaviour
{
    private int overlappingCount = 0;
    private BoxCollider furnitureCollider;
    private bool wallImpactDebounced;
    private AudioManager audioManager;

    public bool HasCollision => overlappingCount > 0;

    private void Awake()
    {
        furnitureCollider = GetComponent<BoxCollider>();
        audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            Debug.LogError("AudioManager is unavailable; wall impact sounds cannot play.", this);
        }
    }

    /// <summary>
    /// Returns false when the furniture box would overlap another solid collider
    /// at the proposed pose. The pose is checked before the Transform is moved.
    /// </summary>
    public bool IsPoseValid(Vector3 position, Quaternion rotation)
    {
        return IsPoseValid(position, rotation, out _);
    }

    public bool IsPoseValid(Vector3 position, Quaternion rotation, out Collider blockingCollider)
    {
        blockingCollider = null;
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

            blockingCollider = other;
            return false;
        }

        return true;
    }

    public void ReportBlockedPose(Collider obstacle, Vector3 furniturePosition)
    {
        if (!IsRoomWall(obstacle) || wallImpactDebounced)
        {
            return;
        }

        wallImpactDebounced = true;
        if (audioManager != null)
        {
            audioManager.PlayWallImpact(furniturePosition);
        }
    }

    private bool IsRoomWall(Collider obstacle)
    {
        if (obstacle == null)
        {
            return false;
        }

        Transform current = obstacle.transform;
        while (current != null)
        {
            if (current.name.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    public void NotifyPoseChanged()
    {
        wallImpactDebounced = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        ReportBlockedPose(collision.collider, transform.position);

        // Ignore ground collision if you only want to detect wall/furniture overlap
        if (!collision.gameObject.name.ToLower().Contains("floor"))
        {
            overlappingCount++;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (IsRoomWall(collision.collider))
        {
            wallImpactDebounced = false;
        }

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
        wallImpactDebounced = false;
    }
}
