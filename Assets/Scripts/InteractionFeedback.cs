using UnityEngine;

public class InteractionFeedback : MonoBehaviour
{
    private Material mat;
    private Color originalColor;
    public Color highlightColor = Color.yellow;
    public Color hoverColor = new Color(0.5f, 0.85f, 1f, 1f);
    private AudioSource audioSource;
    private bool isBeingHovered = false;
    private bool isSelected = false;

    private void Awake()
    {
        Renderer targetRenderer = GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            mat = targetRenderer.material;
            originalColor = mat.color;
        }

        audioSource = GetComponent<AudioSource>();
    }

    public void HighlightAndPlaySound()
    {
        isSelected = true;
        UpdateColor();
        if (audioSource != null)
            audioSource.Play();
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
    }

    private void UpdateColor()
    {
        if (mat == null)
            return;

        if (isSelected)
            mat.color = highlightColor;
        else if (isBeingHovered)
            mat.color = Color.Lerp(originalColor, hoverColor, 0.25f);
        else
            mat.color = originalColor;
    }
}
