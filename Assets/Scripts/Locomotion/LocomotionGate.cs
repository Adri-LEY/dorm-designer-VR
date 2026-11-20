using System.Collections.Generic;
using System.Reflection;
using Oculus.Interaction;
using Oculus.Interaction.Locomotion;
using UnityEngine;

/// <summary>
/// Decides when controller locomotion (turn, teleport, slide) is allowed.
///
/// Meta's BaseInteractors prefab has a "ScrollLocomotionGate" that switches a controller's whole
/// LocomotionControllerInteractorGroup off as soon as that controller's ray points at anything
/// (menu, furniture...). This component replaces that condition at runtime with ours:
/// locomotion stays available while pointing, and is turned off on both controllers only while
/// a furniture is selected (so the sticks move / rotate the furniture instead).
///
/// The Meta prefabs live in the package cache, so they are patched at runtime rather than edited.
/// </summary>
public sealed class LocomotionGate : MonoBehaviour, IActiveState
{
    [Tooltip("Turn controller locomotion off while a furniture is selected.")]
    [SerializeField] private bool blockWhileFurnitureSelected = true;

    private static readonly FieldInfo TrackerGameObjectsField =
        typeof(ActiveStateTracker).GetField("_gameObjects", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly HashSet<object> _blockers = new HashSet<object>();

    /// <summary>True when locomotion is allowed (read by Meta's ActiveStateTracker every frame).</summary>
    public bool Active =>
        _blockers.Count == 0 &&
        !(blockWhileFurnitureSelected && FurnitureManipulationState.AnyFurnitureSelected);

    /// <summary>Lets other features block locomotion temporarily (e.g. a future measuring tool).</summary>
    public void AddBlocker(object key) => _blockers.Add(key);

    public void RemoveBlocker(object key) => _blockers.Remove(key);

    private void Start()
    {
        int patched = 0;
        foreach (ActiveStateTracker tracker in FindObjectsByType<ActiveStateTracker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ControlsControllerLocomotion(tracker))
            {
                tracker.InjectActiveState(this);
                patched++;
            }
        }

        if (patched == 0)
        {
            Debug.LogWarning(
                "LocomotionGate: no tracker controlling controller locomotion was found; " +
                "pointing at the menu or furniture may still block turning/moving (Meta SDK layout changed?).", this);
        }
        else
        {
            Debug.Log($"LocomotionGate: controller locomotion now ignores ray hover ({patched} controller(s)).", this);
        }
    }

    /// <summary>True for trackers that switch a LocomotionControllerInteractorGroup on/off.</summary>
    private static bool ControlsControllerLocomotion(ActiveStateTracker tracker)
    {
        if (TrackerGameObjectsField == null) return false;
        if (!(TrackerGameObjectsField.GetValue(tracker) is List<GameObject> targets)) return false;

        foreach (GameObject target in targets)
        {
            if (target == null) continue;

            // The controller locomotion group carries the LocomotionEventsConnection that feeds the locomotor.
            bool isLocomotionGroup =
                target.GetComponent<LocomotionEventsConnection>() != null &&
                target.GetComponent<BestSelectInteractorGroup>() != null;
            bool isControllerVariant = target.name.IndexOf("Controller", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isLocomotionGroup && isControllerVariant) return true;
        }

        return false;
    }
}
