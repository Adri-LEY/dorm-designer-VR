using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

public class InteractionFeedback : MonoBehaviour, IGameObjectFilter
{
    private sealed class MaterialColorState
    {
        public Material Material;
        public int PropertyId;
        public Color OriginalColor;
    }

    private static readonly int[] ColorPropertyIds =
    {
        Shader.PropertyToID("_BaseColor"),
        Shader.PropertyToID("_Color"),
        Shader.PropertyToID("_OutlineColor")
    };

    private readonly List<MaterialColorState> _materialColorStates =
        new List<MaterialColorState>();

    public Color highlightColor = new Color(1f, 0.45f, 0.05f, 1f);
    public Color hoverColor = new Color(0.5f, 0.85f, 1f, 1f);
    private AudioManager audioManager;
    private bool isBeingHovered = false;
    private bool isSelected = false;

    public bool Filter(GameObject interactor)
    {
        if (interactor == null)
            return false;

        RayInteractor rayInteractor = interactor.GetComponent<RayInteractor>();
        if (rayInteractor == null)
            return false;

        ControllerSelector selector =
            rayInteractor.GetComponentInChildren<ControllerSelector>(true);
        return selector != null &&
               selector.Controller != null &&
               selector.Controller.Handedness == Handedness.Right;
    }

    private void Awake()
    {
        Renderer[] targetRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer targetRenderer in targetRenderers)
        {
            foreach (Material material in targetRenderer.materials)
            {
                if (material == null)
                    continue;

                foreach (int propertyId in ColorPropertyIds)
                {
                    if (!material.HasProperty(propertyId))
                        continue;

                    _materialColorStates.Add(new MaterialColorState
                    {
                        Material = material,
                        PropertyId = propertyId,
                        OriginalColor = material.GetColor(propertyId)
                    });
                }
            }
        }

        if (_materialColorStates.Count == 0)
        {
            Debug.LogWarning(
                "Furniture feedback found no renderer materials with a supported color property.",
                this);
        }

        audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            Debug.LogError("AudioManager is unavailable; furniture feedback sounds cannot play.", this);
        }
    }

    public void HighlightAndPlaySound()
    {
        isSelected = true;
        UpdateColor();
        if (audioManager != null)
            audioManager.PlaySelection();
    }

    public void SetHovered()
    {
        isBeingHovered = true;
        UpdateColor();
    }

    public void SetUnhovered()
    {
        isBeingHovered = false;
        UpdateColor();
    }

    public void ClearSelection()
    {
        isSelected = false;
        UpdateColor();
        if (audioManager != null)
            audioManager.PlayDeselection();
    }

    private void UpdateColor()
    {
        foreach (MaterialColorState colorState in _materialColorStates)
        {
            if (colorState.Material == null)
                continue;

            Color color = colorState.OriginalColor;
            if (isSelected)
                color = highlightColor;
            else if (isBeingHovered)
                color = Color.Lerp(colorState.OriginalColor, hoverColor, 0.45f);

            colorState.Material.SetColor(colorState.PropertyId, color);
        }
    }
}
