using System.Collections;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Sole writer of a furniture root's world position and rotation.
/// The object moves on the room's X/Z floor plane and turns only around world Y
/// (smoothly with the left stick by default, or in snap steps).
/// </summary>
[RequireComponent(typeof(FurnitureManipulationState), typeof(FurnitureControllerInput))]
public sealed class ConstrainedFurnitureManipulator : MonoBehaviour, ITransformer
{
    [Header("Room movement")]
    [Tooltip("Horizontal travel speed in world units per second.")]
    [SerializeField, Min(0f)] private float movementSpeed = 1.5f;

    public enum RotationMode { Smooth, Snap }

    [Header("Rotation (left stick)")]
    [Tooltip("Smooth: turns continuously while the stick is held. Snap: one step per deflection.")]
    [SerializeField] private RotationMode rotationMode = RotationMode.Smooth;

    [Tooltip("Smooth mode: turning speed at full stick deflection, in degrees per second.")]
    [SerializeField, Range(10f, 360f)] private float rotationSpeed = 90f;

    [Tooltip("Smooth mode: >1 makes small deflections turn slower for precise alignment.")]
    [SerializeField, Range(1f, 3f)] private float rotationResponseExponent = 2f;

    [Tooltip("Snap mode: yaw change for each left-stick snap, in degrees.")]
    [SerializeField, Min(1f)] private float rotationSnapAngle = 15f;

    [Tooltip("Stick values below this magnitude are ignored.")]
    [SerializeField, Range(0f, 0.95f)] private float inputDeadzone = 0.2f;

    private FurnitureManipulationState _interactionState;
    private FurnitureControllerInput _input;
    private FurnitureCollisionHandler _collisionHandler;
    private AudioManager _audioManager;
    private Transform _furnitureTransform;
    private Rigidbody _rigidbody;
    private float _fixedWorldY;
    private float _worldYaw;
    private Vector3 _grabStartPosition;
    private float _grabStartYaw;
    private bool _grabActive;
    private bool _cancelledUntilDeselected;
    private bool _rotationSnapArmed = true;
    private bool _scriptedMove;
    private Coroutine _moveRoutine;

    /// <summary>The furniture root this component moves.</summary>
    public Transform FurnitureRoot => _furnitureTransform;

    /// <summary>Current yaw around world Y, in degrees.</summary>
    public float Yaw => _worldYaw;

    private void Awake()
    {
        _interactionState = GetComponent<FurnitureManipulationState>();
        _input = GetComponent<FurnitureControllerInput>();
        _collisionHandler = GetComponentInParent<FurnitureCollisionHandler>();
        _audioManager = AudioManager.Instance;
        if (_audioManager == null)
        {
            Debug.LogError("AudioManager is unavailable; furniture feedback sounds cannot play.", this);
        }

        _rigidbody = GetComponentInParent<Rigidbody>();
        _furnitureTransform = _rigidbody != null
            ? _rigidbody.transform
            : transform.parent;

        if (_furnitureTransform == null)
        {
            Debug.LogError(
                "Furniture manipulation needs a parent furniture Transform.",
                this);
            enabled = false;
            return;
        }

        _fixedWorldY = _furnitureTransform.position.y;
        _worldYaw = _furnitureTransform.eulerAngles.y;

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        Grabbable grabbable = GetComponentInParent<Grabbable>();
        if (grabbable != null)
        {
            // Keep Meta's pointer/grab lifecycle, but replace its pose-following
            // transformer with this no-op implementation.
            grabbable.InjectOptionalOneGrabTransformer(this);
            grabbable.InjectOptionalTwoGrabTransformer(this);
            grabbable.InjectOptionalThrowWhenUnselected(false);
        }
    }

    private void Start()
    {
        // Keep ray hover/select, but remove any serialized Meta pose-follow provider.
        RayInteractable rayInteractable = GetComponent<RayInteractable>();
        if (rayInteractable != null)
        {
            rayInteractable.InjectOptionalMovementProvider(null);
        }
    }

    private void Update()
    {
        if (!enabled || _scriptedMove)
        {
            return;
        }

        bool isSelected = _interactionState.IsSelected;
        if (isSelected && _input.DeletePressed)
        {
            Delete();
            return;
        }

        if (!isSelected)
        {
            _cancelledUntilDeselected = false;
            EndGrab();
        }

        if (isSelected && _input.CancelPressed)
        {
            if (_grabActive)
            {
                _furnitureTransform.position = _grabStartPosition;
                _worldYaw = _grabStartYaw;
            }

            EndGrab();
            _cancelledUntilDeselected = true;
        }

        if (isSelected && !_cancelledUntilDeselected && _input.RightGripHeld)
        {
            if (!_grabActive)
            {
                BeginGrab();
            }

            ApplyHorizontalMovement();
            ApplyRotation();
        }
        else
        {
            EndGrab();
        }

        EnforceRoomConstraints();
    }

    /// <summary>
    /// Deletes this furniture (A button or voice "delete this"). It is a soft delete through
    /// <see cref="FurnitureHistory"/>, so "undo" can bring it back.
    /// </summary>
    public void Delete()
    {
        EndGrab();
        if (_audioManager != null)
        {
            _audioManager.PlayDeletion();
        }

        FurnitureHistory.Instance.Delete(_furnitureTransform.gameObject);
    }

    /// <summary>
    /// Moves the furniture to a pose chosen by code (voice "move this here", undo), with a short glide.
    /// The pose must already be valid (checked by the caller); the floor height of the furniture is kept.
    /// </summary>
    public void MoveTo(Vector3 position, float yaw, float duration = 0.3f)
    {
        if (_furnitureTransform == null) return;
        EndGrab();

        position.y = _fixedWorldY;
        if (_moveRoutine != null) StopCoroutine(_moveRoutine);

        if (!isActiveAndEnabled || duration <= 0f)
        {
            FinishMove(position, yaw);
            return;
        }

        _moveRoutine = StartCoroutine(GlideTo(position, yaw, duration));
    }

    private IEnumerator GlideTo(Vector3 position, float yaw, float duration)
    {
        _scriptedMove = true;
        Vector3 startPosition = _furnitureTransform.position;
        Quaternion startRotation = _furnitureTransform.rotation;
        Quaternion endRotation = Quaternion.Euler(0f, yaw, 0f);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            _furnitureTransform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, position, k),
                Quaternion.Slerp(startRotation, endRotation, k));
            yield return null;
        }

        FinishMove(position, yaw);
    }

    private void FinishMove(Vector3 position, float yaw)
    {
        _furnitureTransform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        _worldYaw = yaw;
        _scriptedMove = false;
        _moveRoutine = null;
        Physics.SyncTransforms();
        if (_collisionHandler != null) _collisionHandler.NotifyPoseChanged();
    }

    private void OnDisable()
    {
        // Soft delete / room change during a glide: never stay stuck in scripted mode.
        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }

        _scriptedMove = false;
    }

    private void BeginGrab()
    {
        _grabActive = true;
        _grabStartPosition = _furnitureTransform.position;
        _grabStartYaw = _worldYaw;
        _rotationSnapArmed = true;
        if (_audioManager != null)
        {
            _audioManager.PlayMovementStart();
        }
    }

    private void EndGrab()
    {
        if (!_grabActive)
        {
            return;
        }

        _grabActive = false;
        if (_audioManager != null)
        {
            _audioManager.PlayMovementStop();
        }

        // Make joystick moves undoable too (B-cancel restores the start pose, so nothing is recorded then).
        bool moved = (_furnitureTransform.position - _grabStartPosition).sqrMagnitude > 0.0001f ||
                     Mathf.Abs(Mathf.DeltaAngle(_worldYaw, _grabStartYaw)) > 0.5f;
        if (moved)
        {
            FurnitureHistory.Instance.RecordMoved(
                _furnitureTransform.gameObject, _grabStartPosition, Quaternion.Euler(0f, _grabStartYaw, 0f));
        }
    }

    private void ApplyHorizontalMovement()
    {
        Vector2 stick = _input.RightStick;
        float magnitude = stick.magnitude;
        if (magnitude <= inputDeadzone)
        {
            return;
        }

        float adjustedMagnitude = Mathf.Clamp01(
            (magnitude - inputDeadzone) / (1f - inputDeadzone));
        Vector2 direction = stick / magnitude * adjustedMagnitude;

        Vector3 position = _furnitureTransform.position;

        // --- NEW: Camera-Relative Movement ---
        Camera playerCamera = Camera.main;

        if (playerCamera != null)
        {
            // Get the camera's forward and right directions, ignoring looking up/down
            Vector3 camForward = playerCamera.transform.forward;
            camForward.y = 0;
            camForward.Normalize();

            Vector3 camRight = playerCamera.transform.right;
            camRight.y = 0;
            camRight.Normalize();

            // Move the furniture relative to the user's perspective
            position += (camRight * direction.x + camForward * direction.y) * movementSpeed * Time.deltaTime;
        }
        else
        {
            // Fallback to global world coordinates if no MainCamera is found
            position.x += direction.x * movementSpeed * Time.deltaTime;
            position.z += direction.y * movementSpeed * Time.deltaTime;
        }
        // -------------------------------------

        TryApplyPose(position, _worldYaw, true);
    }

    private void ApplyRotation()
    {
        if (rotationMode == RotationMode.Snap)
        {
            ApplyYawSnap();
        }
        else
        {
            ApplySmoothYaw();
        }
    }

    private void ApplySmoothYaw()
    {
        float horizontal = _input.LeftStickX;
        float magnitude = Mathf.Abs(horizontal);
        if (magnitude <= inputDeadzone)
        {
            return;
        }

        float adjusted = Mathf.Clamp01((magnitude - inputDeadzone) / (1f - inputDeadzone));
        float speed = Mathf.Pow(adjusted, rotationResponseExponent) * rotationSpeed;
        float candidateYaw = _worldYaw + Mathf.Sign(horizontal) * speed * Time.deltaTime;

        // If the full step would hit a wall or another furniture, the furniture simply stops turning.
        TryApplyPose(_furnitureTransform.position, candidateYaw, true);
    }

    private void ApplyYawSnap()
    {
        float horizontal = _input.LeftStickX;
        if (Mathf.Abs(horizontal) <= inputDeadzone)
        {
            _rotationSnapArmed = true;
            return;
        }

        if (!_rotationSnapArmed)
        {
            return;
        }

        float candidateYaw = _worldYaw + Mathf.Sign(horizontal) * rotationSnapAngle;
        if (TryApplyPose(_furnitureTransform.position, candidateYaw, true))
        {
            _rotationSnapArmed = false;
        }
    }

    private void EnforceRoomConstraints()
    {
        TryApplyPose(_furnitureTransform.position, _worldYaw);
    }

    private bool TryApplyPose(Vector3 position, float yaw, bool reportBlockedPose = false)
    {
        position.y = _fixedWorldY;
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

        if (_collisionHandler != null &&
            !_collisionHandler.IsPoseValid(position, rotation, out Collider blockingCollider))
        {
            if (reportBlockedPose)
            {
                _collisionHandler.ReportBlockedPose(blockingCollider, _furnitureTransform.position);
            }

            return false;
        }

        bool poseChanged =
            (_furnitureTransform.position - position).sqrMagnitude > 0.00000001f ||
            Quaternion.Angle(_furnitureTransform.rotation, rotation) > 0.01f;
        _furnitureTransform.SetPositionAndRotation(position, rotation);
        _worldYaw = yaw;
        if (poseChanged && _collisionHandler != null)
        {
            _collisionHandler.NotifyPoseChanged();
        }

        return true;
    }

    public void Initialize(IGrabbable grabbable)
    {
    }

    public void BeginTransform()
    {
    }

    public void UpdateTransform()
    {
    }

    public void EndTransform()
    {
    }
}
