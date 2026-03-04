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
    [SerializeField] private float maxTargetBoundsSize = 6f;

    [Header("Floating Pull")]
    [SerializeField] private bool pullToControllerPosition = true;
    [SerializeField] private float targetDistanceFromRayOrigin = 0.05f;
    [SerializeField] private float basePullSpeed = 2.2f;
    [SerializeField] private float minPullSpeed = 0.5f;
    [SerializeField] private float massSlowdownFactor = 0.2f;
    [SerializeField] private float rotationStabilizeSpeed = 10f;

    private Rigidbody _lockedBody;
    private Transform _lockedTransform;
    private bool _addedRuntimeBody;
    private bool _originalUseGravity;
    private bool _originalIsKinematic;
    private RigidbodyConstraints _originalConstraints;
    private Quaternion _lockedRotation;
    private float _lockedMass;

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

        Transform currentHitTransform = GetCurrentHitTransform(out Rigidbody hitBody);

        if (_lockedTransform != null && currentHitTransform != _lockedTransform)
        {
            ReleaseLockedBody();
        }

        if (_lockedTransform == null && currentHitTransform != null)
        {
            AcquireBody(currentHitTransform, hitBody);
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

        Vector3 target = pullToControllerPosition
            ? rayOrigin.position
            : rayOrigin.position + rayOrigin.forward * Mathf.Max(0f, targetDistanceFromRayOrigin);

        if (_lockedBody != null)
        {
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
        }
        else
        {
            float speed = ComputePullSpeed(1f);
            _lockedTransform.position = Vector3.MoveTowards(
                _lockedTransform.position,
                target,
                speed * Time.fixedDeltaTime);
        }

        HapticsManager.Instance?.SetMagnetActive(true, 0.2f);
    }

    private Transform GetCurrentHitTransform(out Rigidbody hitBody)
    {
        hitBody = null;
        if (rayOrigin == null)
        {
            return null;
        }

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxLockDistance, magnetizableMask, QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        Transform target = ResolveTargetTransform(hit);
        if (target == null)
        {
            return null;
        }

        if (!TryGetBoundsSize(target, out float boundsSize))
        {
            return null;
        }

        if (boundsSize > maxTargetBoundsSize)
        {
            return null; // Prevent magnetizing huge environment chunks like the whole room.
        }

        hitBody = target.GetComponent<Rigidbody>();
        return target;
    }

    private Transform ResolveTargetTransform(RaycastHit hit)
    {
        if (hit.transform == null || hit.transform.gameObject.isStatic)
        {
            return null;
        }

        MagnetizableBody magnetizable = hit.transform.GetComponentInParent<MagnetizableBody>();
        if (magnetizable != null)
        {
            return magnetizable.transform;
        }

        if (hit.rigidbody != null)
        {
            return hit.rigidbody.transform;
        }

        return hit.transform;
    }

    private bool TryGetBoundsSize(Transform target, out float size)
    {
        size = 0f;
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            return true;
        }

        var colliders = target.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
            size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            return true;
        }

        return false;
    }

    private void AcquireBody(Transform targetTransform, Rigidbody existingBody)
    {
        _lockedTransform = targetTransform;
        _lockedBody = existingBody ?? targetTransform.GetComponent<Rigidbody>();
        _addedRuntimeBody = false;

        if (_lockedBody == null)
        {
            _lockedBody = targetTransform.gameObject.AddComponent<Rigidbody>();
            _lockedBody.mass = 1f;
            _addedRuntimeBody = true;
        }

        _originalUseGravity = _lockedBody.useGravity;
        _originalIsKinematic = _lockedBody.isKinematic;
        _originalConstraints = _lockedBody.constraints;
        _lockedMass = Mathf.Max(0.1f, _lockedBody.mass);
        _lockedRotation = _lockedBody.rotation;

        _lockedBody.useGravity = false;
        _lockedBody.isKinematic = true;
        _lockedBody.constraints = RigidbodyConstraints.FreezeRotation;

        HapticsManager.Instance?.SetMagnetActive(true, 0.2f);
        HapticsManager.Instance?.TriggerMagnetBurst();
    }

    private void ReleaseLockedBody()
    {
        if (_lockedTransform == null)
        {
            return;
        }

        if (_lockedBody != null)
        {
            if (_addedRuntimeBody)
            {
                Destroy(_lockedBody);
            }
            else
            {
                _lockedBody.useGravity = _originalUseGravity;
                _lockedBody.isKinematic = _originalIsKinematic;
                _lockedBody.constraints = _originalConstraints;
            }
        }

        _lockedBody = null;
        _lockedTransform = null;
        _addedRuntimeBody = false;
        HapticsManager.Instance?.SetMagnetActive(false);
    }

    private float ComputePullSpeed(float mass)
    {
        float slowed = basePullSpeed / (1f + Mathf.Max(0f, mass) * Mathf.Max(0f, massSlowdownFactor));
        return Mathf.Max(minPullSpeed, slowed);
    }
}
