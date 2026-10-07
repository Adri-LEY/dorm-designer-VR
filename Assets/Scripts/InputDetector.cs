using UnityEngine;

public class InputDetector : MonoBehaviour
{
    void Update()
    {
        OVRInput.Controller activeInput = OVRInput.GetActiveController();

        if (activeInput == OVRInput.Controller.Hands ||
            activeInput == OVRInput.Controller.LHand ||
            activeInput == OVRInput.Controller.RHand)
        {
            Debug.Log("User is currently using Hand Tracking.");
        }
        else if (activeInput == OVRInput.Controller.Touch ||
                 activeInput == OVRInput.Controller.LTouch ||
                 activeInput == OVRInput.Controller.RTouch)
        {
            Debug.Log("User is currently using Controllers.");
        }
    }
}