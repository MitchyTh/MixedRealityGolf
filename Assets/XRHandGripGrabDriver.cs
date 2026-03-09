using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
[RequireComponent(typeof(XRDirectInteractor))]
public class XRHandGripGrabDriver : MonoBehaviour
{
    public enum Handedness { Left, Right }

    [Header("Hand")]
    public Handedness handedness = Handedness.Left;

    [Header("Grip (0 open -> 1 closed)")]
    [Range(0f, 1f)] public float grabThreshold = 0.60f;
    [Range(0f, 1f)] public float releaseThreshold = 0.35f;
    [Range(0f, 30f)] public float smoothing = 14f;

    [Header("Distance Calibration (meters)")]
    [Tooltip("Typical fingertip-to-palm distance when hand is open.")]
    public float openDist = 0.14f;

    [Tooltip("Typical fingertip-to-palm distance when hand is closed (fist).")]
    public float closedDist = 0.06f;

    [Header("Targeting")]
    [Tooltip("Only attempt grab when hovering something.")]
    public bool onlyGrabWhenHovering = true;

    [Tooltip("Optional: Only grab objects with this Tag. Leave empty to allow all.")]
    public string requiredTag = "";

    [Tooltip("Optional: Only grab objects that also have XRGrabInteractable.")]
    public bool requireXRGrabInteractable = true;

    [Header("Debug")]
    public bool debugGrip = false;

    XRDirectInteractor _interactor;
    XRHandSubsystem _handSubsystem;

    float _smoothedGrip;
    bool _gripLatched;

    static readonly XRHandJointID[] s_FingerTips =
    {
        XRHandJointID.IndexTip,
        XRHandJointID.MiddleTip,
        XRHandJointID.RingTip,
        XRHandJointID.LittleTip
    };

    void Awake()
    {
        _interactor = GetComponent<XRDirectInteractor>();
    }

    void OnEnable()
    {
        _handSubsystem = FindRunningHandSubsystem();
        if (_handSubsystem == null)
            Debug.LogWarning("XRHandSubsystem not found/running. Make sure XR Hands + OpenXR hand tracking are enabled.");
    }

    void Update()
    {
        if (_handSubsystem == null) return;

        XRHand hand = (handedness == Handedness.Left) ? _handSubsystem.leftHand : _handSubsystem.rightHand;
        if (!hand.isTracked) return;

        float grip = ComputeGrip01(hand);
        _smoothedGrip = Smooth(_smoothedGrip, grip, smoothing);

        if (debugGrip)
            Debug.Log($"{handedness} grip raw={grip:F2} smooth={_smoothedGrip:F2}");

        // If holding something: release when open enough
        if (_interactor.hasSelection)
        {
            if (_smoothedGrip <= releaseThreshold)
            {
                _gripLatched = false;
                ForceRelease();
            }
            return;
        }

        // Not holding: latch when hand closes enough
        if (!_gripLatched && _smoothedGrip >= grabThreshold)
        {
            _gripLatched = true;

            if (!onlyGrabWhenHovering || _interactor.hasHover)
                ForceGrabClosestHover();
        }

        // Unlatch when hand opens sufficiently (prevents spam)
        if (_gripLatched && _smoothedGrip <= releaseThreshold)
            _gripLatched = false;
    }

    float ComputeGrip01(XRHand hand)
    {
        if (!TryGetPose(hand, XRHandJointID.Palm, out Pose palmPose))
            return 0f;

        float sum = 0f;
        int count = 0;

        for (int i = 0; i < s_FingerTips.Length; i++)
        {
            if (!TryGetPose(hand, s_FingerTips[i], out Pose tipPose))
                continue;

            float d = Vector3.Distance(tipPose.position, palmPose.position);
            // openDist -> 0, closedDist -> 1
            float g = Mathf.InverseLerp(openDist, closedDist, d);
            sum += Mathf.Clamp01(g);
            count++;
        }

        if (count == 0) return 0f;
        return sum / count;
    }

    void ForceGrabClosestHover()
    {
        var hovered = _interactor.interactablesHovered;
        if (hovered == null || hovered.Count == 0) return;

        IXRSelectInteractable best = null;
        float bestDistSq = float.PositiveInfinity;

        Vector3 handPos = transform.position;

        for (int i = 0; i < hovered.Count; i++)
        {
            var candidate = hovered[i] as IXRSelectInteractable;
            if (candidate == null) continue;

            var comp = candidate.transform;
            if (comp == null) continue;

            if (!string.IsNullOrEmpty(requiredTag) && !comp.CompareTag(requiredTag))
                continue;

            if (requireXRGrabInteractable && comp.GetComponent<XRGrabInteractable>() == null)
                continue;

            float dSq = (comp.position - handPos).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                best = candidate;
            }
        }

        if (best == null) return;

        _interactor.interactionManager.SelectEnter(_interactor, best);
    }

    void ForceRelease()
    {
        var selected = _interactor.interactablesSelected;
        if (selected == null || selected.Count == 0) return;

        var target = selected[0];
        if (target == null) return;

        _interactor.interactionManager.SelectExit(_interactor, target);
    }

    static bool TryGetPose(XRHand hand, XRHandJointID id, out Pose pose)
    {
        pose = default;
        var joint = hand.GetJoint(id);
        return joint.TryGetPose(out pose);
    }

    static XRHandSubsystem FindRunningHandSubsystem()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        for (int i = 0; i < subsystems.Count; i++)
        {
            var s = subsystems[i];
            if (s != null && s.running)
                return s;
        }

        return subsystems.Count > 0 ? subsystems[0] : null;
    }

    static float Smooth(float current, float target, float speed)
    {
        // framerate-independent smoothing
        float t = 1f - Mathf.Exp(-speed * Time.deltaTime);
        return Mathf.Lerp(current, target, t);
    }
}