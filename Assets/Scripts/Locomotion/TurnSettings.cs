using System.Collections.Generic;
using Oculus.Interaction.Locomotion;
using UnityEngine;

/// <summary>
/// Switches the Meta rig's camera turning (thumbstick left/right) between smooth and snap,
/// remembers the choice between sessions, and pauses turning while a furniture is selected
/// so the sticks only move / rotate the furniture.
/// Put it anywhere in the scene (e.g. on the camera rig).
/// </summary>
public sealed class TurnSettings : MonoBehaviour
{
    public enum TurnMode { Smooth, Snap }

    private const string PrefsKey = "DormDesigner.TurnMode";

    [SerializeField] private TurnMode defaultMode = TurnMode.Smooth;

    [Tooltip("Turning speed at full stick deflection, in degrees per second.")]
    [SerializeField, Range(20f, 240f)] private float smoothTurnSpeed = 75f;

    [Tooltip("Angle of one snap turn, in degrees.")]
    [SerializeField, Range(10f, 90f)] private float snapTurnAngle = 30f;

    [Tooltip("Disable camera turning while a furniture is selected (the left stick rotates the furniture).")]
    [SerializeField] private bool pauseWhileFurnitureSelected = true;

    [Tooltip("Remember the player's choice between sessions.")]
    [SerializeField] private bool rememberChoice = true;

    private readonly List<TurnerEventBroadcaster> _turners = new List<TurnerEventBroadcaster>();
    private TurnMode _mode;
    private bool _paused;

    public TurnMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            if (rememberChoice) PlayerPrefs.SetInt(PrefsKey, (int)value);
            Apply();
        }
    }

    public float SmoothTurnSpeed
    {
        get => smoothTurnSpeed;
        set { smoothTurnSpeed = Mathf.Max(1f, value); Apply(); }
    }

    private void Awake()
    {
        _mode = rememberChoice && PlayerPrefs.HasKey(PrefsKey)
            ? (TurnMode)PlayerPrefs.GetInt(PrefsKey)
            : defaultMode;
    }

    private void Start()
    {
        RefreshTurners();
        Apply();
    }

    private void Update()
    {
        bool shouldPause = pauseWhileFurnitureSelected && FurnitureManipulationState.AnyFurnitureSelected;
        if (shouldPause == _paused) return;

        _paused = shouldPause;
        foreach (TurnerEventBroadcaster turner in _turners)
        {
            if (turner != null) turner.enabled = !_paused;
        }
    }

    /// <summary>Finds every turner of the Meta rig (hands and controllers). Call again if the rig is replaced.</summary>
    public void RefreshTurners()
    {
        _turners.Clear();
        _turners.AddRange(FindObjectsByType<TurnerEventBroadcaster>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        if (_turners.Count == 0)
        {
            Debug.LogWarning("TurnSettings: no TurnerEventBroadcaster found in the scene; camera turning is not configured.", this);
        }
    }

    private void Apply()
    {
        // Linear response with a gentle start, so small deflections turn slowly.
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, smoothTurnSpeed, smoothTurnSpeed * 1.5f, 0f));

        foreach (TurnerEventBroadcaster turner in _turners)
        {
            if (turner == null) continue;
            turner.TurnMethod = _mode == TurnMode.Smooth
                ? TurnerEventBroadcaster.TurnMode.Smooth
                : TurnerEventBroadcaster.TurnMode.Snap;
            turner.SmoothTurnCurve = curve;
            turner.SnapTurnDegrees = snapTurnAngle;
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying && _turners.Count > 0) Apply();
    }
}
