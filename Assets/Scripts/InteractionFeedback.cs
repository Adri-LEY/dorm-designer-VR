using UnityEngine;

public class InteractionFeedback : MonoBehaviour
{
    [Header("Outline")]
    [Tooltip("Material using the Custom/OutlineHull shader")]
    public Material outlineMaterial;

    [Header("Selected")]
    public Color highlightColor = Color.yellow;
    public float selectedWidth = 0.012f;

    [Header("Hover")]
    public Color hoverColor = new Color(0.5f, 0.85f, 1f, 1f);
    public float hoverWidth = 0.006f;

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    private Renderer targetRenderer;
    private Material[] originalMaterials;
    private Material[] outlinedMaterials;
    private Material outlineInstance;   // per-object copy so colors/widths don't leak between furniture
    private bool outlineShown = false;

    private AudioManager audioManager;
    private bool isBeingHovered = false;
    private bool isSelected = false;

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();

        audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            Debug.LogError("AudioManager is unavailable; furniture feedback sounds cannot play.", this);
        }
    }

    public void HighlightAndPlaySound()
    {
        if (isSelected)
            return; // avoid replaying the sound if already selected

        isSelected = true;
        UpdateOutline();
        if (audioManager != null)
            audioManager.PlaySelection();
    }

    public void SetHovered()
    {
        isBeingHovered = true;
        UpdateOutline();
    }

    public void SetUnhovered()
    {
        isBeingHovered = false;
        UpdateOutline();
    }

    public void ClearSelection()
    {
        if (!isSelected)
            return; // nothing to deselect, no sound

        isSelected = false;
        UpdateOutline();
        if (audioManager != null)
            audioManager.PlayDeselection();
    }

    private void UpdateOutline()
    {
        if (targetRenderer == null || outlineMaterial == null)
            return;

        if (isSelected)
            ShowOutline(highlightColor, selectedWidth);
        else if (isBeingHovered)
            ShowOutline(hoverColor, hoverWidth);
        else
            HideOutline();
    }

    private void ShowOutline(Color color, float width)
    {
        if (outlinedMaterials == null)
            BuildMaterialArrays();

        outlineInstance.SetColor(OutlineColorId, color);
        outlineInstance.SetFloat(OutlineWidthId, width);

        if (!outlineShown)
        {
            targetRenderer.sharedMaterials = outlinedMaterials;
            outlineShown = true;
        }
    }

    private void HideOutline()
    {
        if (!outlineShown)
            return;

        targetRenderer.sharedMaterials = originalMaterials;
        outlineShown = false;
    }

    // Built lazily because ObjectSpawner swaps the mesh/materials AFTER Instantiate,
    // so caching in Awake would capture the base prefab's materials.
    private void BuildMaterialArrays()
    {
        originalMaterials = targetRenderer.sharedMaterials;
        outlineInstance = new Material(outlineMaterial);

        outlinedMaterials = new Material[originalMaterials.Length + 1];
        originalMaterials.CopyTo(outlinedMaterials, 0);
        outlinedMaterials[outlinedMaterials.Length - 1] = outlineInstance;
    }

    private void OnDestroy()
    {
        if (outlineInstance != null)
            Destroy(outlineInstance);
    }
}