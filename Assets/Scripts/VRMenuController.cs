using UnityEngine;

public class VRMenuController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Drag your Canvas GameObject here")]
    public GameObject uiMenu;

    [Header("Tracking Anchors")]
    [Tooltip("Drag the LeftHandAnchor from OVRCameraRig")]
    public Transform leftController;
    [Tooltip("Drag the RightHandAnchor from OVRCameraRig")]
    public Transform rightController;
    [Tooltip("Drag the CenterEyeAnchor from OVRCameraRig")]
    public Transform headCamera;

    [Header("Menu Positioning")]
    [Tooltip("How far in front (Z) and up (Y) from the controller the menu spawns")]
    public Vector3 menuOffset = new Vector3(0f, 0.2f, 0.4f);

    void Start()
    {
        // Ensure the menu is closed when the app starts
        if (uiMenu != null)
        {
            uiMenu.SetActive(false);
        }
    }

    void Update()
    {
        // Check Left Controller: 'Y' Button (Two)
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch))
        {
            ToggleMenu(leftController);
        }

        // Check Right Controller: 'B' Button (Two) 
        // Note: The right Oculus button is reserved, so B or Joystick Click is standard
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            ToggleMenu(rightController);
        }
    }

    private void ToggleMenu(Transform activeController)
    {
        if (uiMenu == null) return;

        bool isOpening = !uiMenu.activeSelf;
        uiMenu.SetActive(isOpening);

        // Position and rotate the menu if we are opening it
        if (isOpening)
        {
            // Position the menu relative to the controller's current position and forward direction
            uiMenu.transform.position = activeController.position
                                      + (activeController.forward * menuOffset.z)
                                      + (activeController.up * menuOffset.y);

            // Make the UI canvas face the player's headset
            Vector3 lookDirection = uiMenu.transform.position - headCamera.position;

            // Keep the menu vertically straight (ignore looking up/down tilt)
            lookDirection.y = 0;

            uiMenu.transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }
}
