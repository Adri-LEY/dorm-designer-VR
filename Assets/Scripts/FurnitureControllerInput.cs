using UnityEngine;

/// <summary>
/// Samples Quest Touch controller controls. Stick values are controller-local input
/// axes, not directions derived from the tracked controller's position or rotation.
/// </summary>
public sealed class FurnitureControllerInput : MonoBehaviour
{
    public Vector2 RightStick { get; private set; }
    public float LeftStickX { get; private set; }
    public bool RightGripHeld { get; private set; }
    public bool CancelPressed { get; private set; }

    private void Update()
    {
        RightStick = OVRInput.Get(
            OVRInput.Axis2D.PrimaryThumbstick,
            OVRInput.Controller.RTouch);
        LeftStickX = OVRInput.Get(
            OVRInput.Axis2D.SecondaryThumbstick,
            OVRInput.Controller.LTouch).x;
        RightGripHeld = OVRInput.Get(
            OVRInput.Button.PrimaryHandTrigger,
            OVRInput.Controller.RTouch);
        CancelPressed = OVRInput.GetDown(
            OVRInput.Button.Two,
            OVRInput.Controller.RTouch);
    }
}
