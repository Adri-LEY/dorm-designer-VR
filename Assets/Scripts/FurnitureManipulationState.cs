using Oculus.Interaction;
using UnityEngine;

[RequireComponent(typeof(RayInteractable))]
public sealed class FurnitureManipulationState : MonoBehaviour
{
    private static int _selectedCount;

    /// <summary>True while at least one furniture is selected.</summary>
    public static bool AnyFurnitureSelected => _selectedCount > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _selectedCount = 0;

    private RayInteractable _rayInteractable;
    private InteractionFeedback _feedback;
    private bool _wasSelected;
    private bool _wasHovered;

    public bool IsSelected =>
        _rayInteractable != null &&
        _rayInteractable.State == InteractableState.Select;

    private bool IsHovered =>
        _rayInteractable != null &&
        (_rayInteractable.State == InteractableState.Hover ||
         _rayInteractable.State == InteractableState.Select);

    private void Awake()
    {
        _rayInteractable = GetComponent<RayInteractable>();
        _feedback = GetComponentInParent<InteractionFeedback>();

        if (_feedback == null)
            Debug.LogWarning("No InteractionFeedback found on this object or its parents.", this);
    }

    private void Update()
    {
        bool hovered = IsHovered;
        bool selected = IsSelected;

        if (_feedback != null)
        {
            if (hovered && !_wasHovered)
                _feedback.SetHovered();
            else if (!hovered && _wasHovered)
                _feedback.SetUnhovered();

            if (selected && !_wasSelected)
                _feedback.HighlightAndPlaySound();
            else if (!selected && _wasSelected)
                _feedback.ClearSelection();
        }

        if (selected && !_wasSelected) _selectedCount++;
        else if (!selected && _wasSelected) _selectedCount--;

        _wasHovered = hovered;
        _wasSelected = selected;
    }

    private void OnDisable()
    {
        // Also runs when the furniture is deleted (A button), so the count never gets stuck.
        if (_wasSelected) _selectedCount = Mathf.Max(0, _selectedCount - 1);

        if (_feedback != null)
        {
            if (_wasSelected) _feedback.ClearSelection();
            if (_wasHovered) _feedback.SetUnhovered();
        }

        _wasSelected = false;
        _wasHovered = false;
    }
}