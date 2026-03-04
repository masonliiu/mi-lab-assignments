using UnityEngine;

public class MagnetController : MonoBehaviour
{
    [Header("Ray / Selection")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private LayerMask magnetizableMask = ~0;
    [SerializeField] private float maxLockDistance = 8f;

    [Header("Input (Left Index Trigger Only)")]
    [SerializeField] private OVRInput.Axis1D holdAxis = OVRInput.Axis1D.PrimaryIndexTrigger;
    [SerializeField] private OVRInput.RawAxis1D holdRawAxis = OVRInput.RawAxis1D.LIndexTrigger;
    [SerializeField] private OVRInput.Controller leftController = OVRInput.Controller.LTouch;
    [SerializeField, Range(0.01f, 1f)] private float holdAxisThreshold = 0.55f;
    [SerializeField] private bool preferHoveredMagnetizable = true;

    [Header("Floating Pull")]
    [SerializeField] private bool pullToControllerPosition = true;
    [SerializeField] private float targetDistanceFromRayOrigin = 0.05f;
    [SerializeField] private float basePullSpeed = 2.4f;
    [SerializeField] private float minPullSpeed = 0.7f;
    [SerializeField] private float massSlowdownFactor = 0.2f;
    [SerializeField] private float rotationStabilizeSpeed = 12f;
    [SerializeField] private bool disableCollisionsWhileHeld = true;

    private MagnetizableBody _lockedMagnetizable;
    private Rigidbody _lockedBody;
    private Quaternion _lockedRotation;
    private float _lockedMass;
    private bool _originalDetectCollisions;

    void Update()
    {
        bool held = IsLeftIndexHeld();
        if (!held)
        {
            ReleaseLockedBody();
            return;
        }

        MagnetizableBody currentTarget = GetCurrentMagnetizableTarget();

        if (_lockedMagnetizable != null && currentTarget != _lockedMagnetizable)
        {
            ReleaseLockedBody();
        }

        if (_lockedMagnetizable == null && currentTarget != null)
        {
            AcquireBody(currentTarget);
        }
    }

    void FixedUpdate()
    {
        if (_lockedBody == null || _lockedMagnetizable == null)
        {
            return;
        }

        if (rayOrigin == null)
        {
            ReleaseLockedBody();
            return;
        }

        Vector3 target = pullToControllerPosition
            ? rayOrigin.position
            : rayOrigin.position + rayOrigin.forward * Mathf.Max(0f, targetDistanceFromRayOrigin);

        float speed = ComputePullSpeed(_lockedMass);
        Vector3 nextPos = Vector3.MoveTowards(_lockedBody.position, target, speed * Time.fixedDeltaTime);
        _lockedBody.linearVelocity = Vector3.zero;
        _lockedBody.angularVelocity = Vector3.zero;
        _lockedBody.MovePosition(nextPos);

        Quaternion nextRot = Quaternion.Slerp(
            _lockedBody.rotation,
            _lockedRotation,
            Mathf.Clamp01(rotationStabilizeSpeed * Time.fixedDeltaTime));
        _lockedBody.MoveRotation(nextRot);

        HapticsManager.Instance?.SetMagnetActive(true, 0.2f);
    }

    private bool IsLeftIndexHeld()
    {
        float threshold = Mathf.Max(0.55f, holdAxisThreshold);
        float axis = OVRInput.Get(holdAxis, leftController);
        float raw = OVRInput.Get(holdRawAxis);
        return axis >= threshold || raw >= threshold;
    }

    private MagnetizableBody GetCurrentMagnetizableTarget()
    {
        if (rayOrigin == null)
        {
            return null;
        }

        if (preferHoveredMagnetizable)
        {
            PointHandler[] hoveredHandlers = FindObjectsByType<PointHandler>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < hoveredHandlers.Length; i++)
            {
                PointHandler ph = hoveredHandlers[i];
                if (ph == null || !ph.IsHovered)
                {
                    continue;
                }

                MagnetizableBody hoveredMagnetizable = ph.GetComponentInParent<MagnetizableBody>();
                if (hoveredMagnetizable != null)
                {
                    return hoveredMagnetizable;
                }
            }
        }

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxLockDistance, magnetizableMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return null;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            MagnetizableBody magnetizable = hits[i].transform.GetComponentInParent<MagnetizableBody>();
            if (magnetizable != null)
            {
                return magnetizable;
            }
        }

        return null;
    }

    private void AcquireBody(MagnetizableBody magnetizable)
    {
        if (magnetizable == null)
        {
            return;
        }

        Rigidbody rb = magnetizable.GetComponent<Rigidbody>();
        if (rb == null)
        {
            return;
        }

        _lockedMagnetizable = magnetizable;
        _lockedBody = rb;
        _lockedMass = Mathf.Max(0.1f, _lockedBody.mass);
        _lockedRotation = _lockedBody.rotation;

        _lockedMagnetizable.OnMagnetGrabbed();

        _originalDetectCollisions = _lockedBody.detectCollisions;

        _lockedBody.useGravity = false;
        _lockedBody.isKinematic = true;
        if (disableCollisionsWhileHeld)
        {
            _lockedBody.detectCollisions = false;
        }

        HapticsManager.Instance?.SetMagnetActive(true, 0.2f);
        HapticsManager.Instance?.TriggerMagnetBurst();
    }

    private void ReleaseLockedBody()
    {
        if (_lockedBody == null || _lockedMagnetizable == null)
        {
            return;
        }

        _lockedBody.detectCollisions = _originalDetectCollisions;

        _lockedMagnetizable.OnMagnetReleased();
        _lockedMagnetizable = null;
        _lockedBody = null;
        HapticsManager.Instance?.SetMagnetActive(false);
    }

    private float ComputePullSpeed(float mass)
    {
        float slowed = basePullSpeed / (1f + Mathf.Max(0f, mass) * Mathf.Max(0f, massSlowdownFactor));
        return Mathf.Max(minPullSpeed, slowed);
    }
}
