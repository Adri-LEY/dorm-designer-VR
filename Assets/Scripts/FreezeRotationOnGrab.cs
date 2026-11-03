using UnityEngine;

/// <summary>
/// Legacy Meta event receiver kept for existing serialized furniture callbacks.
/// Position and rotation constraints are now owned by ConstrainedFurnitureManipulator.
/// </summary>
public class FreezeRotationOnGrab : MonoBehaviour
{
    public void OnGrabBegin()
    {
    }

    public void OnGrabEnd()
    {
    }
}
