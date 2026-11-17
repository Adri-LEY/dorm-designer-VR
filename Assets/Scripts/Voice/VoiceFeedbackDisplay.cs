using TMPro;
using UnityEngine;

/// <summary>
/// World-space feedback for voice commands: a disc marking the pointed spot on the floor,
/// and a short text label floating between the player and that spot (always facing the player).
/// </summary>
public sealed class VoiceFeedbackDisplay : MonoBehaviour
{
    public enum Tone { Info, Success, Error }

    [Tooltip("CenterEyeAnchor; the label faces it.")]
    public Transform headCamera;

    [Tooltip("Material of the floor marker. If empty, an unlit material is created at runtime.")]
    public Material markerMaterial;

    [Min(0.05f)] public float markerDiameter = 0.35f;
    public Color markerColor = new Color(0.25f, 0.7f, 1f, 1f);
    [Tooltip("Colour of the second marker (left controller: destination of a move).")]
    public Color secondaryMarkerColor = new Color(1f, 0.6f, 0.2f, 1f);

    [Tooltip("Distance of the label from the head, toward the target.")]
    [Min(0.3f)] public float labelDistance = 1.1f;
    public float labelFontSize = 0.45f;

    public Color infoColor = Color.white;
    public Color successColor = new Color(0.45f, 1f, 0.55f, 1f);
    public Color errorColor = new Color(1f, 0.6f, 0.35f, 1f);

    private Transform _marker;
    private Transform _secondaryMarker;
    private TextMeshPro _label;
    private float _hideLabelAt = -1f;
    private Vector3? _labelAnchor;
    private Material _ownedMaterial;

    private void Awake()
    {
        _marker = BuildMarker("VoiceTargetMarker", markerColor);
        _secondaryMarker = BuildMarker("VoiceDestinationMarker", secondaryMarkerColor);
        HideSecondaryMarker();
        BuildLabel();
        HideMarker();
        HideLabel();
    }

    // ------------------------------------------------------------------ Marker

    public void ShowMarker(Vector3 floorPoint) => PlaceMarker(_marker, floorPoint);

    /// <summary>Hides both markers.</summary>
    public void HideMarker()
    {
        HidePrimaryMarker();
        HideSecondaryMarker();
    }

    /// <summary>Hides only the main (blue, right controller) marker.</summary>
    public void HidePrimaryMarker()
    {
        if (_marker != null) _marker.gameObject.SetActive(false);
    }

    /// <summary>Second (orange) marker, for the left controller's pointed spot.</summary>
    public void ShowSecondaryMarker(Vector3 floorPoint) => PlaceMarker(_secondaryMarker, floorPoint);

    public void HideSecondaryMarker()
    {
        if (_secondaryMarker != null) _secondaryMarker.gameObject.SetActive(false);
    }

    private void PlaceMarker(Transform marker, Vector3 floorPoint)
    {
        marker.gameObject.SetActive(true);
        marker.position = floorPoint + Vector3.up * 0.012f;
        float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 8f);
        marker.localScale = new Vector3(markerDiameter * pulse, 0.005f, markerDiameter * pulse);
    }

    // ------------------------------------------------------------------ Label

    /// <summary>Shows a message. <paramref name="seconds"/> &lt;= 0 keeps it until replaced or hidden.</summary>
    public void ShowMessage(string text, Tone tone = Tone.Info, float seconds = 0f, Vector3? nearPoint = null)
    {
        _label.gameObject.SetActive(true);
        _label.text = text;
        _label.color = tone == Tone.Success ? successColor : tone == Tone.Error ? errorColor : infoColor;
        _hideLabelAt = seconds > 0f ? Time.unscaledTime + seconds : -1f;
        if (nearPoint.HasValue) _labelAnchor = nearPoint;
        UpdateLabelPose();
    }

    /// <summary>Moves the label toward a new point without changing the text.</summary>
    public void SetLabelAnchor(Vector3? point) => _labelAnchor = point;

    public void HideLabel()
    {
        if (_label != null) _label.gameObject.SetActive(false);
        _labelAnchor = null;
    }

    private void LateUpdate()
    {
        if (!_label.gameObject.activeSelf) return;

        if (_hideLabelAt > 0f && Time.unscaledTime >= _hideLabelAt)
        {
            HideLabel();
            return;
        }

        UpdateLabelPose();
    }

    private void UpdateLabelPose()
    {
        if (headCamera == null) return;

        Vector3 head = headCamera.position;
        Vector3 direction;
        if (_labelAnchor.HasValue)
        {
            direction = _labelAnchor.Value - head;
            direction.y = 0f;
        }
        else
        {
            direction = Vector3.ProjectOnPlane(headCamera.forward, Vector3.up);
        }

        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
        direction.Normalize();

        // Slightly below eye level, between the player and the target, facing the player.
        Vector3 position = head + direction * labelDistance + Vector3.down * 0.15f;
        _label.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - head, Vector3.up));
    }

    // ------------------------------------------------------------------ Building

    private Transform BuildMarker(string markerName, Color color)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = markerName;
        disc.transform.SetParent(transform, false);

        // No collider: the marker must never block spawning, rays or movement.
        Collider discCollider = disc.GetComponent<Collider>();
        if (discCollider != null) Destroy(discCollider);

        Renderer markerRenderer = disc.GetComponent<Renderer>();
        markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        markerRenderer.receiveShadows = false;

        Material material = markerMaterial;
        if (material == null && _ownedMaterial != null)
        {
            material = _ownedMaterial;
        }
        else if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                _ownedMaterial = new Material(shader);
                material = _ownedMaterial;
            }
        }

        if (material != null)
        {
            markerRenderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            markerRenderer.SetPropertyBlock(block);
        }

        return disc.transform;
    }

    private void BuildLabel()
    {
        var go = new GameObject("VoiceFeedbackLabel");
        go.transform.SetParent(transform, false);
        _label = go.AddComponent<TextMeshPro>();
        _label.fontSize = labelFontSize;
        _label.alignment = TextAlignmentOptions.Center;
        _label.textWrappingMode = TextWrappingModes.Normal;
        _label.rectTransform.sizeDelta = new Vector2(0.9f, 0.3f);
        _label.outlineColor = Color.black;
        _label.outlineWidth = 0.25f;
    }

    private void OnDestroy()
    {
        if (_ownedMaterial != null) Destroy(_ownedMaterial);
    }
}
