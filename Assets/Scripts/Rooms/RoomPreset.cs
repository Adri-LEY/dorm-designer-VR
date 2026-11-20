using UnityEngine;

/// <summary>
/// An empty room the student can furnish. Create new ones with
/// Assets > Create > Dorm Designer > Room Preset, then add them to RoomSelectionMenu.
/// </summary>
[CreateAssetMenu(fileName = "Room", menuName = "Dorm Designer/Room Preset", order = 0)]
public sealed class RoomPreset : ScriptableObject
{
    [Tooltip("Name shown in the room selection menu.")]
    public string displayName = "New room";

    [TextArea(2, 3)]
    public string description;

    [Header("Inner dimensions (metres)")]
    [Tooltip("Side to side (X axis).")]
    [Min(1f)] public float width = 3f;

    [Tooltip("Front to back (Z axis). The player starts near the front and looks toward the back.")]
    [Min(1f)] public float depth = 4f;

    [Min(1.8f)] public float height = 2.5f;

    [Min(0.02f)] public float wallThickness = 0.1f;

    [Header("Look (optional)")]
    [Tooltip("Leave alpha at 0 to keep the walls' current material colour.")]
    public Color wallColor = new Color(1f, 1f, 1f, 0f);

    [Tooltip("Leave alpha at 0 to keep the floor's current material colour.")]
    public Color floorColor = new Color(1f, 1f, 1f, 0f);

    public float Area => width * depth;
}
