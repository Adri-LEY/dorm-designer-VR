using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Reads the Meta Interaction SDK selection state without moving the furniture.
/// In the room, this is the ray's select state while the right trigger selects an item.
/// </summary>
[RequireComponent(typeof(RayInteractable))]
public sealed class FurnitureManipulationState : MonoBehaviour
{
    private RayInteractable _rayInteractable;

    public bool IsSelected =>
        _rayInteractable != null &&
        _rayInteractable.State == InteractableState.Select;

    private void Awake()
    {
        _rayInteractable = GetComponent<RayInteractable>();
    }
}
