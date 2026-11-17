using UnityEngine;

public class VRMenuController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Drag your Canvas GameObject here")]
    public GameObject uiMenu;

    [Header("Tracking Anchors")]
    [Tooltip("Drag the LeftHandAnchor from OVRCameraRig")]
    public Transform leftController;
    [Tooltip("Drag the CenterEyeAnchor from OVRCameraRig")]
    public Transform headCamera;

    [Header("Menu Positioning")]
    [Tooltip("How far in front (Z) and up (Y) from the controller the menu spawns")]
    public Vector3 menuOffset = new Vector3(0f, 0.2f, 0.4f);

    [Tooltip("Distance from the head when the menu is opened in front of the player (room selection).")]
    public float headDistance = 0.65f;
    [Tooltip("How far below eye level the menu sits when opened in front of the player.")]
    public float headDrop = 0.12f;

    /// <summary>
    /// While true the menu stays open and the Menu button does nothing.
    /// Used by the room selection screen until a room is chosen.
    /// </summary>
    public bool LockedOpen { get; set; }

    public bool IsOpen => uiMenu != null && uiMenu.activeSelf;

    void Start()
    {
        // Ensure the menu is closed when the app starts (unless something requires it).
        if (uiMenu != null && !LockedOpen)
        {
            uiMenu.SetActive(false);
        }
    }

    void Update()
    {
        // Check Left Controller: 'Start' Button (Start)
        if (OVRInput.GetDown(OVRInput.Button.Start, OVRInput.Controller.LTouch))
        {
            ToggleMenu(leftController);
        }
    }

    private void ToggleMenu(Transform activeController)
    {
        if (uiMenu == null) return;

        bool isOpening = !uiMenu.activeSelf;
        if (!isOpening && LockedOpen) return;

        uiMenu.SetActive(isOpening);

        // Position and rotate the menu if we are opening it
        if (isOpening && activeController != null)
        {
            // Position the menu relative to the controller's current position and forward direction
            Vector3 position = activeController.position
                             + (activeController.forward * menuOffset.z)
                             + (activeController.up * menuOffset.y);
            PlaceFacingHead(position);
        }
    }

    /// <summary>Opens the menu and places it straight in front of the player's eyes.</summary>
    public void OpenInFrontOfHead()
    {
        if (uiMenu == null) return;

        uiMenu.SetActive(true);
        PlaceFacingHead(GetInFrontOfHeadPosition());
    }

    /// <summary>Where the menu should sit when opened in front of the player.</summary>
    public Vector3 GetInFrontOfHeadPosition()
    {
        if (headCamera == null) return uiMenu != null ? uiMenu.transform.position : Vector3.zero;

        Vector3 forward = headCamera.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        return headCamera.position + forward * headDistance + Vector3.down * headDrop;
    }

    public void Close()
    {
        if (uiMenu != null && !LockedOpen) uiMenu.SetActive(false);
    }

    /// <summary>Moves the menu to <paramref name="position"/> and turns it to face the head (kept upright).</summary>
    public void PlaceFacingHead(Vector3 position)
    {
        if (uiMenu == null) return;

        uiMenu.transform.position = position;
        if (headCamera == null) return;

        // Make the UI canvas face the player's headset
        Vector3 lookDirection = uiMenu.transform.position - headCamera.position;

        // Keep the menu vertically straight (ignore looking up/down tilt)
        lookDirection.y = 0;
        if (lookDirection.sqrMagnitude < 0.0001f) return;

        uiMenu.transform.rotation = Quaternion.LookRotation(lookDirection);
    }
}
