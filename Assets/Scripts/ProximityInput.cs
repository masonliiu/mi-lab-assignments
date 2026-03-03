using UnityEngine;

public class ProximityInput : MonoBehaviour
{
    [Header("Input Source")]
    [SerializeField] private bool useRaycastMock = true;
    [SerializeField] private Transform sensorOrigin;
    [SerializeField] private float maxDistance = 2f;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float smoothing = 10f;

    private float _externalProximity01;
    private float _smoothedProximity01;

    void Update()
    {
        float targetLevel = useRaycastMock ? GetRaycastProximityLevel() : Mathf.Clamp01(_externalProximity01);
        _smoothedProximity01 = Mathf.Lerp(_smoothedProximity01, targetLevel, 1f - Mathf.Exp(-Mathf.Max(0.01f, smoothing) * Time.deltaTime));
        HapticsManager.Instance?.UpdateProximityLevel(_smoothedProximity01);
    }

    public void SetExternalProximity01(float level)
    {
        _externalProximity01 = Mathf.Clamp01(level);
    }

    private float GetRaycastProximityLevel()
    {
        if (sensorOrigin == null)
        {
            return 0f;
        }

        Vector3 origin = sensorOrigin.position;
        Vector3 direction = sensorOrigin.forward;
        if (!Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return 0f;
        }

        return 1f - Mathf.Clamp01(hit.distance / Mathf.Max(0.001f, maxDistance));
    }
}

