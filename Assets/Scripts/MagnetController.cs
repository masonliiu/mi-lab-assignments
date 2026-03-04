using UnityEngine;

public class MagnetController : MonoBehaviour
{
    [Header("Ray / Selection")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private LayerMask magnetizableMask = ~0;
    [SerializeField] private float maxLockDistance = 8f;
    [SerializeField] private OVRInput.Axis1D holdAxis = OVRInput.Axis1D.PrimaryIndexTrigger;
    [SerializeField, Range(0.01f, 1f)] private float holdAxisThreshold = 0.25f;

    [Header("Target Pull")]
    [SerializeField] private float targetDistanceFromRayOrigin = 1.5f;
    [SerializeField] private float springForce = 4f;
    [SerializeField] private float dampingForce = 3f;
    [SerializeField] private float maxAcceleration = 6f;

    [Header("Load Mapping")]
    [SerializeField] private float expectedMaxMass = 5f;
    [SerializeField] private float expectedMaxAcceleration = 20f;

    private Rigidbody _lockedBody;
    private MagnetizableBody _lockedMagnetizable;

    void Update()
    {
        bool held = OVRInput.Get(holdAxis) >= holdAxisThreshold;

        if (!held)
        {
            ReleaseLockedBody();
            return;
        }

        Rigidbody currentHitBody = GetCurrentHitBody();

        if (_lockedBody != null && currentHitBody != _lockedBody)
        {
            ReleaseLockedBody();
        }

        if (_lockedBody == null && currentHitBody != null)
        {
            AcquireBody(currentHitBody);
        }
    }

    void FixedUpdate()
    {
        if (_lockedBody == null)
        {
            return;
        }

        if (rayOrigin == null)
        {
            ReleaseLockedBody();
            return;
        }

        Vector3 target = rayOrigin.position + rayOrigin.forward * targetDistanceFromRayOrigin;
        Vector3 toTarget = target - _lockedBody.worldCenterOfMass;
        Vector3 acceleration = toTarget * springForce - _lockedBody.linearVelocity * dampingForce;
        acceleration = Vector3.ClampMagnitude(acceleration, maxAcceleration);
        _lockedBody.AddForce(acceleration, ForceMode.Acceleration);

        float load = ComputeLoad(_lockedBody, acceleration);
        HapticsManager.Instance?.SetMagnetActive(true, load);
    }

    private Rigidbody GetCurrentHitBody()
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

        Rigidbody rb = hit.rigidbody;
        if (rb == null)
        {
            return null;
        }

        return rb;
    }

    private void AcquireBody(Rigidbody rb)
    {
        _lockedBody = rb;
        _lockedMagnetizable = _lockedBody.GetComponent<MagnetizableBody>();
        _lockedMagnetizable?.OnMagnetGrabbed();
        HapticsManager.Instance?.SetMagnetActive(true, 0.2f);
        HapticsManager.Instance?.TriggerMagnetBurst();
    }

    private void ReleaseLockedBody()
    {
        if (_lockedBody == null)
        {
            return;
        }

        _lockedBody = null;
        _lockedMagnetizable?.OnMagnetReleased();
        _lockedMagnetizable = null;
        HapticsManager.Instance?.SetMagnetActive(false);
    }

    private float ComputeLoad(Rigidbody rb, Vector3 acceleration)
    {
        float massT = Mathf.Clamp01(rb.mass / Mathf.Max(0.001f, expectedMaxMass));
        float accelT = Mathf.Clamp01(acceleration.magnitude / Mathf.Max(0.001f, expectedMaxAcceleration));
        return Mathf.Clamp01(accelT * (0.5f + 0.5f * massT));
    }
}
