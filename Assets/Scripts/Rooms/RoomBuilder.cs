using System;
using Oculus.Interaction.Locomotion;
using UnityEngine;

/// <summary>
/// Resizes the scene's floor and four walls (scaled cubes) to match a <see cref="RoomPreset"/>
/// and moves the player to the room's starting point.
/// The floor's top surface stays at this object's Y (world floor level).
/// </summary>
public sealed class RoomBuilder : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Room parts (scaled cubes)")]
    public Transform floor;
    [Tooltip("Wall on the -X side.")]
    public Transform wallLeft;
    [Tooltip("Wall on the +X side.")]
    public Transform wallRight;
    [Tooltip("Wall on the -Z side (behind the player at start).")]
    public Transform wallFront;
    [Tooltip("Wall on the +Z side (facing the player at start).")]
    public Transform wallBack;

    [Header("Placement")]
    [Tooltip("Room centre on the floor plane. If unset, the floor's current centre is used.")]
    public bool useFloorCentreAsOrigin = true;
    public Vector3 roomCentre;
    [Min(0.01f)] public float floorThickness = 0.1f;

    [Header("Player")]
    [Tooltip("OVRCameraRig root. Used only if no FirstPersonLocomotor is found.")]
    public Transform playerRig;
    [Tooltip("CenterEyeAnchor.")]
    public Transform playerHead;
    [Tooltip("Where the player starts, as a fraction of the depth from the front wall (0 = front wall, 0.5 = centre).")]
    [Range(0.1f, 0.9f)] public float startDepthFraction = 0.3f;

    public RoomPreset CurrentRoom { get; private set; }

    /// <summary>Raised after the room geometry has been rebuilt.</summary>
    public event Action<RoomPreset> RoomBuilt;

    private MaterialPropertyBlock _block;

    private void Awake()
    {
        if (useFloorCentreAsOrigin && floor != null)
        {
            Vector3 floorPosition = floor.position;
            roomCentre = new Vector3(floorPosition.x, transform.position.y, floorPosition.z);
        }
    }

    public void Build(RoomPreset room)
    {
        if (room == null) return;
        CurrentRoom = room;

        float w = room.width;
        float d = room.depth;
        float h = room.height;
        float t = room.wallThickness;
        Vector3 c = roomCentre;

        if (floor != null)
        {
            SetBox(floor, new Vector3(c.x, c.y - floorThickness * 0.5f, c.z), new Vector3(w + 2f * t, floorThickness, d + 2f * t));
        }

        // Side walls span the full depth plus the corners; front/back walls fit between them.
        SetBox(wallLeft, new Vector3(c.x - (w + t) * 0.5f, c.y + h * 0.5f, c.z), new Vector3(t, h, d + 2f * t));
        SetBox(wallRight, new Vector3(c.x + (w + t) * 0.5f, c.y + h * 0.5f, c.z), new Vector3(t, h, d + 2f * t));
        SetBox(wallFront, new Vector3(c.x, c.y + h * 0.5f, c.z - (d + t) * 0.5f), new Vector3(w, h, t));
        SetBox(wallBack, new Vector3(c.x, c.y + h * 0.5f, c.z + (d + t) * 0.5f), new Vector3(w, h, t));

        ApplyColor(room.wallColor, wallLeft, wallRight, wallFront, wallBack);
        ApplyColor(room.floorColor, floor);

        Physics.SyncTransforms();
        MovePlayerToStart(room);
        RoomBuilt?.Invoke(room);
    }

    /// <summary>Puts the player near the front wall, facing the back wall (+Z).</summary>
    public void MovePlayerToStart(RoomPreset room)
    {
        Vector3 target = roomCentre + Vector3.forward * (room.depth * (startDepthFraction - 0.5f));
        Quaternion facing = Quaternion.identity;

        FirstPersonLocomotor locomotor = FindFirstObjectByType<FirstPersonLocomotor>();
        if (locomotor != null && locomotor.isActiveAndEnabled)
        {
            locomotor.HandleLocomotionEvent(new LocomotionEvent(
                GetInstanceID(),
                new Pose(target, facing),
                LocomotionEvent.TranslationType.Absolute,
                LocomotionEvent.RotationType.Absolute));
            return;
        }

        if (playerRig == null) return;

        // Fallback: move and turn the rig so the head ends up on the target, facing +Z.
        if (playerHead != null)
        {
            Vector3 headForward = Vector3.ProjectOnPlane(playerHead.forward, Vector3.up);
            if (headForward.sqrMagnitude > 0.0001f)
            {
                float yawDelta = Vector3.SignedAngle(headForward, facing * Vector3.forward, Vector3.up);
                playerRig.RotateAround(playerHead.position, Vector3.up, yawDelta);
            }

            Vector3 offset = target - playerHead.position;
            offset.y = 0f;
            playerRig.position += offset;
        }
        else
        {
            playerRig.SetPositionAndRotation(new Vector3(target.x, playerRig.position.y, target.z), facing);
        }
    }

    private static void SetBox(Transform box, Vector3 worldCentre, Vector3 size)
    {
        if (box == null) return;

        // Walls are expected to be unparented or under an unrotated, unscaled "Room" object.
        Transform parent = box.parent;
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        box.SetPositionAndRotation(worldCentre, parent != null ? parent.rotation : Quaternion.identity);
        box.localScale = new Vector3(size.x / parentScale.x, size.y / parentScale.y, size.z / parentScale.z);
    }

    private void ApplyColor(Color color, params Transform[] parts)
    {
        if (color.a <= 0f) return;
        _block ??= new MaterialPropertyBlock();

        foreach (Transform part in parts)
        {
            if (part == null || !part.TryGetComponent(out Renderer partRenderer)) continue;

            partRenderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            partRenderer.SetPropertyBlock(_block);
        }
    }
}
