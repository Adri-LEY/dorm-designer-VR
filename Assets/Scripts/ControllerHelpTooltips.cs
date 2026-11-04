using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ControllerHelpTooltips : MonoBehaviour
{
    public enum Hand { Left, Right }

    [Serializable]
    public class Tip
    {
        public string label;
        public Hand hand;
        [Tooltip("Position in the controller anchor's local space (meters).")]
        public Vector3 localPosition;
    }

    [Header("References")]
    [Tooltip("OVRCameraRig > TrackingSpace > LeftControllerAnchor")]
    public Transform leftControllerAnchor;
    [Tooltip("OVRCameraRig > TrackingSpace > RightControllerAnchor")]
    public Transform rightControllerAnchor;
    [Tooltip("CenterEyeAnchor, used to make labels face the player")]
    public Transform headCamera;

    [Header("Look")]
    public float fontSize = 0.25f;
    public Color textColor = Color.white;
    public Color outlineColor = Color.black;

    [Tooltip("Show labels all the time, to tune positions. Turn off when done.")]
    public bool alwaysShow = false;

    [Header("Tips")]
    public List<Tip> tips = new List<Tip>
{
    // Right controller (labels stack on the outer side, growing to the right)
    new Tip { hand = Hand.Right, label = "B: cancel move",                localPosition = new Vector3( 0.06f,  0.030f,  0.020f) },
    new Tip { hand = Hand.Right, label = "A: delete",                     localPosition = new Vector3( 0.06f,  0.015f,  0.020f) },
    new Tip { hand = Hand.Right, label = "Stick: move (hold grip)",       localPosition = new Vector3( 0.06f,  0.000f, -0.010f) },
    new Tip { hand = Hand.Right, label = "Grip: hold to move/rotate",     localPosition = new Vector3( 0.06f, -0.025f, -0.030f) },
    new Tip { hand = Hand.Right, label = "Trigger: keep selected",        localPosition = new Vector3( 0.06f, -0.045f,  0.040f) },

    // Left controller (labels stack on the outer side, growing to the left)
    new Tip { hand = Hand.Left,  label = "Stick: rotate 15° (hold right grip)", localPosition = new Vector3(-0.06f, 0.000f, -0.010f) },
};

    private readonly List<Transform> _labels = new List<Transform>();

    private void Start()
    {
        foreach (Tip tip in tips)
        {
            Transform anchor = tip.hand == Hand.Left ? leftControllerAnchor : rightControllerAnchor;
            if (anchor == null) { _labels.Add(null); continue; }

            var go = new GameObject("Tip_" + tip.label);
            go.transform.SetParent(anchor, false);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = tip.label;
            tmp.fontSize = fontSize;
            tmp.color = textColor;
            tmp.outlineColor = outlineColor;
            tmp.outlineWidth = 0.25f;
            tmp.rectTransform.sizeDelta = new Vector2(0.5f, 0.05f);

            // Labels grow away from the controller: left hand to the left, right hand to the right.
            bool left = tip.hand == Hand.Left;
            tmp.alignment = left ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            tmp.rectTransform.pivot = new Vector2(left ? 1f : 0f, 0.5f);

            go.SetActive(false);
            _labels.Add(go.transform);
        }
    }

    private void LateUpdate()
    {
        bool controllersActive =
            (OVRInput.GetActiveController() & OVRInput.Controller.Touch) != 0;
        bool show = alwaysShow || (controllersActive && FurnitureManipulationState.AnyFurnitureSelected);

        for (int i = 0; i < _labels.Count; i++)
        {
            Transform label = _labels[i];
            if (label == null) continue;

            if (label.gameObject.activeSelf != show)
                label.gameObject.SetActive(show);
            if (!show) continue;

            label.localPosition = tips[i].localPosition; // lets you tweak positions live in Play mode

            if (headCamera != null)
            {
                // Face the player (text reads correctly when the forward points away from the head).
                label.rotation = Quaternion.LookRotation(label.position - headCamera.position, Vector3.up);
            }
        }
    }
}