using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FurnitureCollisionHandler : MonoBehaviour
{
    private int overlappingCount = 0;
    private Vector3 lastValidPosition;
    private Quaternion lastValidRotation;

    public bool HasCollision => overlappingCount > 0;

    void Start()
    {
        lastValidPosition = transform.position;
        lastValidRotation = transform.rotation;
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
        // Remember position where the object started
        if (overlappingCount == 0)
        {
            lastValidPosition = transform.position;
            lastValidRotation = transform.rotation;
        }
    }

    public void OnGrabEnd()
    {
        // If released inside a wall or another furniture piece, snap back!
        if (overlappingCount > 0)
        {
            transform.position = lastValidPosition;
            transform.rotation = lastValidRotation;
            overlappingCount = 0;
        }
        else
        {
            lastValidPosition = transform.position;
            lastValidRotation = transform.rotation;
        }
    }
}