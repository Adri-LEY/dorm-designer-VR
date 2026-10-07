using UnityEngine;

public class InteractionFeedback : MonoBehaviour
{
    private Material mat;
    private Color originalColor;
    public Color highlightColor = Color.yellow;
    private AudioSource audioSource;

    // Keeps track of the currently selected object across the whole scene
    private static InteractionFeedback currentActive;
    private bool isBeingHovered = false;

    void Start()
    {
        mat = GetComponent<Renderer>().material;
        originalColor = mat.color;
        audioSource = GetComponent<AudioSource>();
    }

    public void HighlightAndPlaySound()
    {
        // If another object is already highlighted, turn it off first
        if (currentActive != null && currentActive != this)
        {
            currentActive.ResetColor();
        }

        currentActive = this;
        mat.color = highlightColor;
        if (audioSource != null) audioSource.Play();
    }

    // We will link these to the ray's hover states
    public void SetHovered() { isBeingHovered = true; }
    public void SetUnhovered() { isBeingHovered = false; }

    public void ResetColor()
    {
        mat.color = originalColor;
        if (currentActive == this) currentActive = null;
    }

    void Update()
    {
        // If this is the highlighted object, and the ray is NOT pointing at it
        if (currentActive == this && !isBeingHovered)
        {
            // Listen for a trigger pull (click) in empty space or on a non-interactable object
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) || OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
            {
                ResetColor();
            }
        }
    }
}
