using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FurnitureCollisionHandler : MonoBehaviour
{
    private int overlappingCount = 0;

    public bool HasCollision => overlappingCount > 0;

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