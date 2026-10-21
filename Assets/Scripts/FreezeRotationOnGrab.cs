using UnityEngine;

/// <summary>
/// Freezes the Y-axis rotation (and optionally X/Z) of a furniture object
/// even when grabbed by the Meta XR grab system.
///
/// The Meta XR grab interactable directly writes to transform.rotation each frame,
/// bypassing Rigidbody constraints. This script counters that by restoring the
/// locked axes every LateUpdate (which runs after the grab system updates).
///
/// Attach this to the same GameObject as your GrabInteractable / OVRGrabbable.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FreezeRotationOnGrab : MonoBehaviour
{
    [Header("Freeze Axes During Grab")]
    public bool freezeX = true;
    public bool freezeY = true;  // Most useful for furniture: keep it upright
    public bool freezeZ = true;

    private Quaternion _frozenRotation;
    private bool _isGrabbed = false;

    void Start()
    {
        // Record the initial rotation as the "locked" reference
        _frozenRotation = transform.rotation;
    }

    /// <summary>
    /// Call this from your GrabInteractable's WhenSelectingInteractorAdded event,
    /// or hook it to OVRGrabbable's GrabBegin callback.
    /// </summary>
    public void OnGrabBegin()
    {
        _isGrabbed = true;
        // Snapshot the rotation at the moment the grab begins
        _frozenRotation = transform.rotation;
    }

    /// <summary>
    /// Call this from your GrabInteractable's WhenSelectingInteractorRemoved event,
    /// or hook it to OVRGrabbable's GrabEnd callback.
    /// </summary>
    public void OnGrabEnd()
    {
        _isGrabbed = false;
    }

    void LateUpdate()
    {
        if (!_isGrabbed) return;

        // Decompose the current rotation applied by the grab system
        Vector3 currentEuler = transform.rotation.eulerAngles;
        Vector3 frozenEuler  = _frozenRotation.eulerAngles;

        // Replace only the axes we want to freeze
        float x = freezeX ? frozenEuler.x : currentEuler.x;
        float y = freezeY ? frozenEuler.y : currentEuler.y;
        float z = freezeZ ? frozenEuler.z : currentEuler.z;

        transform.rotation = Quaternion.Euler(x, y, z);
    }
}

