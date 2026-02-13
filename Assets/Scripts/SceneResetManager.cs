using System.Collections.Generic;
using UnityEngine;

public class SceneResetManager : MonoBehaviour
{
    public static SceneResetManager Instance { get; private set; }

    [System.Serializable]
    public struct ResettableState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;
        public bool active;
        public Color? color;
    }

    readonly List<(Transform transform, ResettableState state)> _resettables = new List<(Transform, ResettableState)>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // register an object to be resetted when the scene is reset
    public void Register(Transform t)
    {
        if (t == null) return;

        var state = new ResettableState
        {
            position = t.position,
            rotation = t.rotation,
            localScale = t.localScale,
            active = t.gameObject.activeSelf,
            color = null
        };

        var r = t.GetComponent<Renderer>();
        if (r != null && r.material != null)
            state.color = r.material.color;

        _resettables.Add((t, state));
    }

    // reset all registered objects to their initial state
    public void ResetAll()
    {
        foreach (var (transform, state) in _resettables)
        {
            if (transform == null) continue;

            transform.position = state.position;
            transform.rotation = state.rotation;
            transform.localScale = state.localScale;
            transform.gameObject.SetActive(state.active);

            if (state.color.HasValue)
            {
                var r = transform.GetComponent<Renderer>();
                if (r != null && r.material != null)
                    r.material.color = state.color.Value;
            }
        }
    }
}
