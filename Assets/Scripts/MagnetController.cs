using UnityEngine;

public class MagnetController : MonoBehaviour
{
    [Header("Ray / Selection")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private LayerMask magnetizableMask = ~0;
    [SerializeField] private float maxLockDistance = 8f;
    [SerializeField] private OVRInput.Axis1D holdAxis = OVRInput.Axis1D.PrimaryIndexTrigger;
    [SerializeField, Range(0.01f, 1f)] private float holdAxisThreshold = 0.25f;
    [SerializeField] private OVRInput.RawButton holdRawButtonFallback = OVRInput.RawButton.LIndexTrigger;
    [SerializeField] private OVRInput.Button holdButtonFallback = OVRInput.Button.Three;

    [Header("Target Pull")]
    [SerializeField] private float targetDistanceFromRayOrigin = 1.5f;
    [SerializeField] private float springForce = 4f;
    [SerializeField] private float dampingForce = 3f;
    [SerializeField] private float maxAcceleration = 6f;
    [SerializeField] private float transformPullSpeed = 1.2f;

    [Header("Load Mapping")]
    [SerializeField] private float expectedMaxMass = 5f;
    [SerializeField] private float expectedMaxAcceleration = 20f;

    private Rigidbody _lockedBody;
    private Transform _lockedTransform;
    private MagnetizableBody _lockedMagnetizable;
    private bool _forcedDynamic;
    private bool _forcedDynamicOriginalIsKinematic;

    void Update()
    {
        bool heldByAxis = OVRInput.Get(holdAxis) >= holdAxisThreshold;
        bool heldByRawFallback = OVRInput.Get(holdRawButtonFallback);
        bool heldByButtonFallback = OVRInput.Get(holdButtonFallback);
        bool held = heldByAxis || heldByRawFallback || heldByButtonFallback;

        if (!held)
        {
            ReleaseLockedBody();
            return;
        }

        Transform currentHitTransform = GetCurrentHitTransform();

        if (_lockedTransform != null && currentHitTransform != _lockedTransform)
        {
            ReleaseLockedBody();
        }

        if (_lockedTransform == null && currentHitTransform != null)
        {
            AcquireBody(currentHitTransform);
        }
    }

    void FixedUpdate()
    {
        if (_lockedTransform == null)
        {
            return;
        }

        if (rayOrigin == null)
        {
            ReleaseLockedBody();
            return;
        }

        Vector3 target = rayOrigin.position + rayOrigin.forward * targetDistanceFromRayOrigin;
        if (_lockedBody != null && !_lockedBody.isKinematic)
        {
            Vector3 toTarget = target - _lockedBody.worldCenterOfMass;
            Vector3 acceleration = toTarget * springForce - _lockedBody.linearVelocity * dampingForce;
            acceleration = Vector3.ClampMagnitude(acceleration, maxAcceleration);
            _lockedBody.AddForce(acceleration, ForceMode.Acceleration);

            float load = ComputeLoad(_lockedBody, acceleration);
            HapticsManager.Instance?.SetMagnetActive(true, load);
            return;
        }

        // Fallback path for non-rigidbody or kinematic objects.
        _lockedTransform.position = Vector3.MoveTowards(
            _lockedTransform.position,
            target,
            transformPullSpeed * Time.fixedDeltaTime);
        HapticsManager.Instance?.SetMagnetActive(true, 0.2f);
    }

    private Transform GetCurrentHitTransform()
    {
        if (rayOrigin == null)
        {
            return null;
        }

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxLockDistance, magnetizableMask, QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        return hit.transform;
    }

    private void AcquireBody(Transform hitTransform)
    {
        _lockedTransform = hitTransform;
        _lockedBody = hitTransform.GetComponent<Rigidbody>() ?? hitTransform.GetComponentInParent<Rigidbody>();
        _lockedMagnetizable = _lockedBody != null ? _lockedBody.GetComponent<MagnetizableBody>() : null;
        _lockedMagnetizable?.OnMagnetGrabbed();

        _forcedDynamic = false;
        if (_lockedBody != null && _lockedMagnetizable == null && _lockedBody.isKinematic)
        {
            _forcedDynamicOriginalIsKinematic = true;
            _lockedBody.isKinematic = false;
            _forcedDynamic = true;
        }

        HapticsManager.Instance?.SetMagnetActive(true, 0.2f);
        HapticsManager.Instance?.TriggerMagnetBurst();
    }

    private void ReleaseLockedBody()
    {
        if (_lockedTransform == null)
        {
            return;
        }

        _lockedMagnetizable?.OnMagnetReleased();
        _lockedMagnetizable = null;
        if (_forcedDynamic && _lockedBody != null)
        {
            _lockedBody.isKinematic = _forcedDynamicOriginalIsKinematic;
        }
        _forcedDynamic = false;
        _lockedBody = null;
        _lockedTransform = null;
        HapticsManager.Instance?.SetMagnetActive(false);
    }

    private float ComputeLoad(Rigidbody rb, Vector3 acceleration)
    {
        float massT = Mathf.Clamp01(rb.mass / Mathf.Max(0.001f, expectedMaxMass));
        float accelT = Mathf.Clamp01(acceleration.magnitude / Mathf.Max(0.001f, expectedMaxAcceleration));
        return Mathf.Clamp01(accelT * (0.5f + 0.5f * massT));
    }
}
