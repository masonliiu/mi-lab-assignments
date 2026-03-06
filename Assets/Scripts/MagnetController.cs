using UnityEngine;

public class MagnetController : MonoBehaviour
{
    [Header("Controller / Target")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private Behaviour rightControllerRayInteractor;
    [SerializeField] private bool disableRightRayWhileMagnetizing = true;

    [Header("Input (Left Index Trigger Only)")]
    [SerializeField] private OVRInput.Axis1D holdAxis = OVRInput.Axis1D.PrimaryIndexTrigger;
    [SerializeField] private OVRInput.RawAxis1D holdRawAxis = OVRInput.RawAxis1D.LIndexTrigger;
    [SerializeField] private OVRInput.Controller leftController = OVRInput.Controller.LTouch;
    [SerializeField, Range(0.01f, 1f)] private float holdAxisThreshold = 0.25f;

    [Header("Floating Pull")]
    [SerializeField, Min(0f)] private float stopDistanceFromController = 1f;
    [SerializeField] private float targetHeightOffset = 0.25f;
    [SerializeField] private float basePullSpeed = 2.2f;
    [SerializeField] private float minPullSpeed = 0.5f;
    [SerializeField] private float massSlowdownFactor = 0.2f;
    [SerializeField] private float rotationStabilizeSpeed = 10f;
    [SerializeField] private bool disableCollisionsWhileHeld = true;

    [Header("Magnet Haptics")]
    [SerializeField, Range(0f, 1f)] private float hapticsMinLoad = 0f;
    [SerializeField, Range(0f, 1f)] private float hapticsMaxLoad = 0.85f;
    [SerializeField, Min(0.05f)] private float hapticsDistanceRange = 2.8f;
    [SerializeField, Min(0.1f)] private float hapticsMassReference = 12f;
    [SerializeField, Range(0f, 1f)] private float hapticsDistanceWeight = 0.88f;
    [SerializeField, Range(0f, 1f)] private float hapticsMassWeight = 0.12f;
    [SerializeField, Range(0f, 1f)] private float hapticsProgressWeight = 0.35f;
    [SerializeField, Min(0.1f)] private float hapticsNearnessExponent = 0.65f;
    [SerializeField, Min(0.1f)] private float hapticsProgressExponent = 0.8f;
    [SerializeField, Range(0f, 1f)] private float hapticsEarlyRampFloor = 0.65f;
    [SerializeField, Min(0.1f)] private float hapticsResponseExponent = 0.75f;
    [SerializeField, Min(0f)] private float hapticsLoadSmoothing = 8f;

    private MagnetizableBody _lockedMagnetizable;
    private Rigidbody _lockedBody;
    private Quaternion _lockedRotation;
    private float _lockedMass;
    private bool _originalDetectCollisions;
    private bool _originalUseGravity;
    private bool _originalIsKinematic;
    private bool _rightRayWasEnabled;
    private bool _rightRaySuppressed;
    private float _smoothedHapticLoad;
    private float _initialHapticDistance = 1f;

    void Update()
    {
        if (!IsLeftIndexHeld())
        {
            ReleaseLockedBody();
            return;
        }

        MagnetizableBody hoveredTarget = GetHoveredMagnetizableTarget();
        if (hoveredTarget == null)
        {
            ReleaseLockedBody();
            return;
        }

        if (_lockedMagnetizable == null)
        {
            AcquireBody(hoveredTarget);
            return;
        }

        if (_lockedMagnetizable != hoveredTarget)
        {
            ReleaseLockedBody();
            AcquireBody(hoveredTarget);
        }
    }

    void OnDisable()
    {
        ReleaseLockedBody();
    }

    void FixedUpdate()
    {
        if (_lockedBody == null || _lockedMagnetizable == null || rayOrigin == null)
        {
            return;
        }

        // Keep object a little in front of the controller to avoid clipping into player capsule.
        Vector3 target = ComputeMagnetTargetPosition();
        float speed = ComputePullSpeed(_lockedMass);

        // Pull by world center-of-mass instead of pivot so motion is global and
        // unaffected by flipped/local object orientation.
        Vector3 currentCom = _lockedBody.worldCenterOfMass;
        Vector3 nextCom = Vector3.MoveTowards(currentCom, target, speed * Time.fixedDeltaTime);
        Vector3 delta = nextCom - currentCom;

        _lockedBody.linearVelocity = Vector3.zero;
        _lockedBody.angularVelocity = Vector3.zero;
        _lockedBody.MovePosition(_lockedBody.position + delta);

        Quaternion nextRotation = Quaternion.Slerp(
            _lockedBody.rotation,
            _lockedRotation,
            Mathf.Clamp01(rotationStabilizeSpeed * Time.fixedDeltaTime));
        _lockedBody.MoveRotation(nextRotation);

        float desiredLoad = ComputeMagnetHapticLoad(currentCom, target, _lockedMass);
        float t = 1f - Mathf.Exp(-Mathf.Max(0f, hapticsLoadSmoothing) * Time.fixedDeltaTime);
        _smoothedHapticLoad = Mathf.Lerp(_smoothedHapticLoad, desiredLoad, t);
        HapticsManager.Instance?.SetMagnetActive(true, _smoothedHapticLoad);
    }

    private bool IsLeftIndexHeld()
    {
        float threshold = Mathf.Clamp01(holdAxisThreshold);
        float axisValue = OVRInput.Get(holdAxis, leftController);
        float rawAxisValue = OVRInput.Get(holdRawAxis, leftController);
        bool triggerButton = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, leftController);
        return triggerButton || axisValue >= threshold || rawAxisValue >= threshold;
    }

    private MagnetizableBody GetHoveredMagnetizableTarget()
    {
        PointHandler[] handlers = FindObjectsByType<PointHandler>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (handlers == null || handlers.Length == 0)
        {
            return null;
        }

        MagnetizableBody bestTarget = null;
        float bestSqrDistance = float.PositiveInfinity;

        for (int i = 0; i < handlers.Length; i++)
        {
            PointHandler handler = handlers[i];
            if (handler == null || !handler.IsHovered)
            {
                continue;
            }

            MagnetizableBody candidate = handler.GetComponentInParent<MagnetizableBody>();
            if (candidate == null)
            {
                continue;
            }

            Rigidbody candidateBody = FindBody(candidate);
            if (candidateBody == null)
            {
                continue;
            }

            if (rayOrigin == null)
            {
                return candidate;
            }

            float sqrDistance = (candidateBody.worldCenterOfMass - rayOrigin.position).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private static Rigidbody FindBody(MagnetizableBody magnetizable)
    {
        if (magnetizable == null)
        {
            return null;
        }

        Rigidbody body = magnetizable.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = magnetizable.GetComponentInParent<Rigidbody>();
        }
        if (body == null)
        {
            body = magnetizable.GetComponentInChildren<Rigidbody>();
        }
        return body;
    }

    private void AcquireBody(MagnetizableBody magnetizable)
    {
        if (magnetizable == null)
        {
            return;
        }

        Rigidbody body = FindBody(magnetizable);
        if (body == null)
        {
            return;
        }

        _lockedMagnetizable = magnetizable;
        _lockedBody = body;
        _lockedMass = Mathf.Max(0.1f, _lockedBody.mass);
        _lockedRotation = _lockedBody.rotation;

        _originalDetectCollisions = _lockedBody.detectCollisions;
        _originalUseGravity = _lockedBody.useGravity;
        _originalIsKinematic = _lockedBody.isKinematic;

        _lockedMagnetizable.OnMagnetGrabbed();
        SuppressRightRayIfNeeded();

        _lockedBody.useGravity = false;
        _lockedBody.isKinematic = false;
        _lockedBody.linearVelocity = Vector3.zero;
        _lockedBody.angularVelocity = Vector3.zero;
        _lockedBody.WakeUp();

        if (disableCollisionsWhileHeld)
        {
            _lockedBody.detectCollisions = false;
        }

        _initialHapticDistance = Mathf.Max(0.05f, Vector3.Distance(_lockedBody.worldCenterOfMass, ComputeMagnetTargetPosition()));
        _smoothedHapticLoad = hapticsMinLoad;
        HapticsManager.Instance?.SetMagnetActive(true, _smoothedHapticLoad);
        HapticsManager.Instance?.TriggerMagnetBurst();
    }

    private void ReleaseLockedBody()
    {
        if (_lockedBody != null)
        {
            _lockedBody.detectCollisions = _originalDetectCollisions;
            _lockedBody.useGravity = _originalUseGravity;
            _lockedBody.isKinematic = _originalIsKinematic;
        }

        if (_lockedMagnetizable != null)
        {
            _lockedMagnetizable.OnMagnetReleased();
        }

        _lockedMagnetizable = null;
        _lockedBody = null;
        _initialHapticDistance = 1f;
        _smoothedHapticLoad = 0f;
        RestoreRightRayIfNeeded();
        HapticsManager.Instance?.SetMagnetActive(false);
    }

    private void SuppressRightRayIfNeeded()
    {
        if (!disableRightRayWhileMagnetizing || rightControllerRayInteractor == null || _rightRaySuppressed)
        {
            return;
        }

        _rightRayWasEnabled = rightControllerRayInteractor.enabled;
        rightControllerRayInteractor.enabled = false;
        _rightRaySuppressed = true;
    }

    private void RestoreRightRayIfNeeded()
    {
        if (!_rightRaySuppressed || rightControllerRayInteractor == null)
        {
            return;
        }

        rightControllerRayInteractor.enabled = _rightRayWasEnabled;
        _rightRaySuppressed = false;
    }

    private float ComputePullSpeed(float mass)
    {
        float slowed = basePullSpeed / (1f + Mathf.Max(0f, mass) * Mathf.Max(0f, massSlowdownFactor));
        return Mathf.Max(minPullSpeed, slowed);
    }

    private Vector3 ComputeMagnetTargetPosition()
    {
        if (rayOrigin == null)
        {
            return Vector3.zero;
        }

        return rayOrigin.position
             + rayOrigin.forward * Mathf.Max(0f, stopDistanceFromController)
             + Vector3.up * targetHeightOffset;
    }

    private float ComputeMagnetHapticLoad(Vector3 currentCom, Vector3 targetCom, float mass)
    {
        float distance = Vector3.Distance(currentCom, targetCom);
        float dynamicRange = Mathf.Max(0.05f, hapticsDistanceRange);
        float nearFactor = 1f - Mathf.Clamp01(distance / dynamicRange);
        nearFactor = Mathf.Pow(nearFactor, Mathf.Max(0.1f, hapticsNearnessExponent));

        float startDistance = Mathf.Max(0.05f, _initialHapticDistance);
        float progressFactor = 1f - Mathf.Clamp01(distance / startDistance);
        progressFactor = Mathf.Pow(progressFactor, Mathf.Max(0.1f, hapticsProgressExponent));

        float massFactor = Mathf.Clamp01(Mathf.Max(0.01f, mass) / Mathf.Max(0.1f, hapticsMassReference));

        float distanceWeight = Mathf.Max(0f, hapticsDistanceWeight);
        float massWeight = Mathf.Max(0f, hapticsMassWeight);
        float progressWeight = Mathf.Max(0f, hapticsProgressWeight);
        float weightSum = Mathf.Max(0.0001f, distanceWeight + massWeight + progressWeight);

        float combined = (nearFactor * distanceWeight + massFactor * massWeight + progressFactor * progressWeight) / weightSum;
        float earlyFloor = progressFactor * Mathf.Clamp01(hapticsEarlyRampFloor);
        combined = Mathf.Max(combined, earlyFloor);
        combined = Mathf.Pow(Mathf.Clamp01(combined), Mathf.Max(0.1f, hapticsResponseExponent));
        return Mathf.Lerp(hapticsMinLoad, hapticsMaxLoad, Mathf.Clamp01(combined));
    }
}
