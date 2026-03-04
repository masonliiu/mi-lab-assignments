using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;

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
    readonly List<List<Color>> _materialColorsPerResettable = new List<List<Color>>();

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

        var materialColors = new List<Color>();
        var renderers = t.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null)
                    materialColors.Add(mats[i].color);
            }
        }

        var state = new ResettableState
        {
            position = t.position,
            rotation = t.rotation,
            localScale = t.localScale,
            active = t.gameObject.activeSelf,
            color = null
        };

        _resettables.Add((t, state));
        _materialColorsPerResettable.Add(materialColors);
    }

    // reset all registered objects to their initial state
    public void ResetAll()
    {
        var handlersToToggle = new List<PointHandler>();
        var allScenePointHandlers = FindObjectsByType<PointHandler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allScenePointHandlers.Length; i++)
        {
            var ph = allScenePointHandlers[i];
            if (ph == null) continue;
            ph.enabled = false; // OnDisable clears local hover state + outline.
            handlersToToggle.Add(ph);
        }

        for (int n = 0; n < _resettables.Count; n++)
        {
            var (transform, state) = _resettables[n];
            if (transform == null) continue;

            transform.position = state.position;
            transform.rotation = state.rotation;
            transform.localScale = state.localScale;
            transform.gameObject.SetActive(state.active);

            if (n < _materialColorsPerResettable.Count)
            {
                var materialColors = _materialColorsPerResettable[n];
                if (materialColors != null && materialColors.Count > 0)
                {
                    var renderers = transform.GetComponentsInChildren<Renderer>();
                    int idx = 0;
                    foreach (var r in renderers)
                    {
                        if (r == null) continue;
                        var mats = r.materials;
                        for (int i = 0; i < mats.Length && idx < materialColors.Count; i++)
                        {
                            mats[i].color = materialColors[idx];
                            idx++;
                        }
                        r.materials = mats;
                    }
                }
            }

            var iv = transform.GetComponent<InteractableVR>();
            if (iv != null)
                iv.ResetToNoAction();

            var rayInteractables = transform.GetComponentsInChildren<RayInteractable>(true);
            foreach (var ri in rayInteractables)
            {
                if (ri == null) continue;
                bool wasEnabled = ri.enabled;
                ri.enabled = false;
                ri.enabled = wasEnabled;
            }
        }

        for (int i = 0; i < handlersToToggle.Count; i++)
        {
            if (handlersToToggle[i] != null)
                handlersToToggle[i].ForceClearHover();
        }

        VRMenuManager.Instance?.ResetToNoAction();
        StartCoroutine(ReenablePointHandlersNextFrame(handlersToToggle));
    }

    System.Collections.IEnumerator ReenablePointHandlersNextFrame(List<PointHandler> handlers)
    {
        yield return null;
        for (int i = 0; i < handlers.Count; i++)
        {
            if (handlers[i] != null)
                handlers[i].enabled = true; // OnEnable resubscribes cleanly
        }
    }
}
