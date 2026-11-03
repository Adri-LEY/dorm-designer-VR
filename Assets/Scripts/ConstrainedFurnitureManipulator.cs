using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Sole writer of a furniture root's world position and rotation.
/// The object moves on the room's X/Z floor plane and turns only around world Y.
/// </summary>
[RequireComponent(typeof(FurnitureManipulationState), typeof(FurnitureControllerInput))]
public sealed class ConstrainedFurnitureManipulator : MonoBehaviour, ITransformer
{
    [Header("Room movement")]
    [Tooltip("Horizontal travel speed in world units per second.")]
    [SerializeField, Min(0f)] private float movementSpeed = 1.5f;

    [Tooltip("Yaw change for each left-stick snap, in degrees.")]
    [SerializeField, Min(1f)] private float rotationSnapAngle = 15f;

    [Tooltip("Stick values below this magnitude are ignored.")]
    [SerializeField, Range(0f, 0.95f)] private float inputDeadzone = 0.2f;

    private FurnitureManipulationState _interactionState;
    private FurnitureControllerInput _input;
    private Transform _furnitureTransform;
    private Rigidbody _rigidbody;
    private float _fixedWorldY;
    private float _worldYaw;
    private Vector3 _grabStartPosition;
    private float _grabStartYaw;
    private bool _grabActive;
    private bool _cancelledUntilDeselected;
    private bool _rotationSnapArmed = true;

    private void Awake()
    {
        _interactionState = GetComponent<FurnitureManipulationState>();
        _input = GetComponent<FurnitureControllerInput>();

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
        if (!enabled)
        {
            return;
        }

        bool isSelected = _interactionState.IsSelected;
        if (isSelected && _input.DeletePressed)
        {
            Destroy(_furnitureTransform.gameObject);
            return;
        }

        if (!isSelected)
        {
            _cancelledUntilDeselected = false;
            _grabActive = false;
        }

        if (isSelected && _input.CancelPressed)
        {
            if (_grabActive)
            {
                _furnitureTransform.position = _grabStartPosition;
                _worldYaw = _grabStartYaw;
            }

            _grabActive = false;
            _cancelledUntilDeselected = true;
        }

        if (isSelected && !_cancelledUntilDeselected && _input.RightGripHeld)
        {
            if (!_grabActive)
            {
                BeginGrab();
            }

            ApplyHorizontalMovement();
            ApplyYawSnap();
        }
        else
        {
            _grabActive = false;
        }

        EnforceRoomConstraints();
    }

    private void BeginGrab()
    {
        _grabActive = true;
        _grabStartPosition = _furnitureTransform.position;
        _grabStartYaw = _worldYaw;
        _rotationSnapArmed = true;
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
        position.x += direction.x * movementSpeed * Time.deltaTime;
        position.z += direction.y * movementSpeed * Time.deltaTime;
        position.y = _fixedWorldY;
        _furnitureTransform.position = position;
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

        _worldYaw += Mathf.Sign(horizontal) * rotationSnapAngle;
        _rotationSnapArmed = false;
    }

    private void EnforceRoomConstraints()
    {
        Vector3 position = _furnitureTransform.position;
        position.y = _fixedWorldY;
        _furnitureTransform.SetPositionAndRotation(
            position,
            Quaternion.Euler(0f, _worldYaw, 0f));
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
