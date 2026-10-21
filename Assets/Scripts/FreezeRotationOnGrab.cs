using UnityEngine;

/// <summary>
/// Freezes the rotation axes and/or Y position of a furniture object
/// even when grabbed by the Meta XR grab system.
///
/// The Meta XR grab interactable directly writes to transform.rotation and
/// transform.position each frame, bypassing Rigidbody constraints.
/// This script counters that by restoring locked axes every LateUpdate
/// (which runs after the grab system updates).
///
/// Attach this to the same GameObject as your GrabInteractable / OVRGrabbable.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FreezeRotationOnGrab : MonoBehaviour
{
    [Header("Freeze Rotation Axes During Grab")]
    public bool freezeRotX = true;
    public bool freezeRotY = true;  // Most useful for furniture: keep it upright
    public bool freezeRotZ = true;

    [Header("Freeze Position Axes During Grab")]
    public bool freezePosY = true;  // Prevent lifting off the floor

    private Quaternion _frozenRotation;
    private float _frozenPosY;
    private bool _isGrabbed = false;

    void Start()
    {
        // Record the initial transform as the "locked" reference
        _frozenRotation = transform.rotation;
        _frozenPosY = transform.position.y;
    }

    /// <summary>
    /// Call this from your GrabInteractable's WhenSelectingInteractorAdded event,
    /// or hook it to OVRGrabbable's GrabBegin callback.
    /// </summary>
    public void OnGrabBegin()
    {
        _isGrabbed = true;
        // Snapshot the transform at the moment the grab begins
        _frozenRotation = transform.rotation;
        _frozenPosY = transform.position.y;
    }

    /// <summary>
    /// Call this from your GrabInteractable's WhenSelectingInteractorRemoved event,
    /// or hook it to OVRGrabbable's GrabEnd callback.
    /// </summary>
    public void OnGrabEnd()
    {
        _isGrabbed = false;

        // When the grab releases, the Rigidbody goes non-kinematic and Unity
        // applies whatever angular velocity it accumulated during the grab
        // (from the controller's motion fighting against our rotation lock).
        // Zero it out so the object doesn't spin on release.
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.angularVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
    }

    void LateUpdate()
    {
        if (!_isGrabbed) return;

        // --- Rotation lock ---
        Vector3 currentEuler = transform.rotation.eulerAngles;
        Vector3 frozenEuler  = _frozenRotation.eulerAngles;

        float x = freezeRotX ? frozenEuler.x : currentEuler.x;
        float y = freezeRotY ? frozenEuler.y : currentEuler.y;
        float z = freezeRotZ ? frozenEuler.z : currentEuler.z;

        transform.rotation = Quaternion.Euler(x, y, z);

        // --- Position Y lock ---
        if (freezePosY)
        {
            Vector3 pos = transform.position;
            pos.y = _frozenPosY;
            transform.position = pos;
        }
    }
}

