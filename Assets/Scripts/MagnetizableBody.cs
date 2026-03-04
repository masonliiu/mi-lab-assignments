using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MagnetizableBody : MonoBehaviour
{
    [Header("Startup Stability")]
    [SerializeField] private bool stableAtStartup = true;
    [SerializeField] private bool startKinematic = true;
    [SerializeField] private bool freezeRotationAtStartup = true;

    [Header("After First Magnet Grab")]
    [SerializeField] private bool stayDynamicAfterFirstGrab = true;
    [SerializeField] private bool enableGravityWhenActivated = true;

    private Rigidbody _rb;
    private bool _hasBeenTamperedWith;
    private RigidbodyConstraints _originalConstraints;
    private bool _originalUseGravity;
    private bool _originalIsKinematic;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _originalConstraints = _rb.constraints;
        _originalUseGravity = _rb.useGravity;
        _originalIsKinematic = _rb.isKinematic;

        if (!stableAtStartup)
        {
            return;
        }

        if (startKinematic)
        {
            _rb.isKinematic = true;
        }

        if (freezeRotationAtStartup)
        {
            _rb.constraints = _rb.constraints | RigidbodyConstraints.FreezeRotation;
        }
    }

    public void OnMagnetGrabbed()
    {
        _hasBeenTamperedWith = true;
        _rb.isKinematic = false;

        if (enableGravityWhenActivated)
        {
            _rb.useGravity = true;
        }

        // Remove startup stabilization once user has interacted.
        _rb.constraints = _originalConstraints;
    }

    public void OnMagnetReleased()
    {
        if (!stayDynamicAfterFirstGrab || !_hasBeenTamperedWith)
        {
            return;
        }

        // Keep physically active after first interaction.
        _rb.isKinematic = false;
    }

    public void ResetToStartupStability()
    {
        _hasBeenTamperedWith = false;
        _rb.constraints = _originalConstraints;
        _rb.useGravity = _originalUseGravity;
        _rb.isKinematic = _originalIsKinematic;

        if (!stableAtStartup)
        {
            return;
        }

        if (startKinematic)
        {
            _rb.isKinematic = true;
        }

        if (freezeRotationAtStartup)
        {
            _rb.constraints = _rb.constraints | RigidbodyConstraints.FreezeRotation;
        }
    }
}

