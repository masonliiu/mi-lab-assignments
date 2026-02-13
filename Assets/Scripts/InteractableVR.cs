using UnityEngine;

public class InteractableVR : MonoBehaviour
{
    public enum InteractionType {
        Translate,
        Rotation,
        ColorChange,
        Scaling,
        Teleportation
    }

    public InteractionType interactionType;

    PointHandler _pointHandler;
    Renderer _renderer;
    Color _originalColor;
    Color _altColor = Color.red;
    bool _usingAltColor;

    void Awake() {
        _pointHandler = GetComponent<PointHandler>();
        _renderer = GetComponent<Renderer>();
        if (_renderer != null) {
            _originalColor = _renderer.material.color;
        }
    }

    void Start() {
        if (SceneResetManager.Instance != null) {
            SceneResetManager.Instance.Register(transform);
        }
    }
    bool IsHovered() {
        return _pointHandler != null && _pointHandler.IsHovered;
    }

    void Update() {
        // if outline on an object is active and if object is being hovered
            // then use enum and switch cases
            // to call respective function.
        if (!IsHovered()) return;
        
        switch (interactionType) {
            case InteractionType.Translate:
                // X held: move along Y while hovered
                if (OVRInput.Get(OVRInput.Button.Three)) {
                    TranslateObject();
                }
                break;
            case InteractionType.Rotation:
                // X held: rotate while hovered
                if (OVRInput.Get(OVRInput.Button.Three)) {
                    RotateObject();
                }
                break;
            case InteractionType.ColorChange:
            // have a stored color (can make random later), and if outline is enabled and x
        // is pressed down,
        // replace current color (image component doesnt work), with stored color and 
        // activate a bool and 
        // store the prev color in a temp variable. check bool at 
        // beginning of if statement, and if bool then change color to temp color
                if (OVRInput.GetDown(OVRInput.Button.Three)) {
                    ColorChangeObject();
                }
                break;
            case InteractionType.Scaling:
                // X pressed once: scale up by 0.1
                if (OVRInput.GetDown(OVRInput.Button.Three)) {
                    ScaleObject();
                }
                break;
            case InteractionType.Teleportation:
                // Y pressed once: teleport to this object, then hide it
                if (OVRInput.GetDown(OVRInput.Button.Four)) {
                    TeleportObject();
                }
                break;
        }
    }

    void TranslateObject() {
        float speed = 0.5f;
        transform.Translate(Vector3.up * speed * Time.deltaTime, Space.World);
    }

    void RotateObject() {
        float speed = 60f;
        transform.Rotate(Vector3.up * speed * Time.deltaTime, Space.Self);
    }

    void ColorChangeObject() {
        if (_renderer == null) return;

        if (_usingAltColor) {
            _renderer.material.color = _originalColor;
            _usingAltColor = false;
        } else {
            _renderer.material.color = _altColor;
            _usingAltColor = true;
        }
    }

    void ScaleObject() {
        var renderer = GetComponent<Renderer>();
        if (renderer != null) {
            Vector3 centerBefore = renderer.bounds.center;
            transform.localScale += Vector3.one * 0.1f;
            Vector3 centerAfter = renderer.bounds.center;
            transform.position += (centerBefore - centerAfter);
        } else {
            transform.localScale += Vector3.one * 0.1f;
        }
    }

    void TeleportObject() {
        var movement = FindFirstObjectByType<Movement>();
        if (movement == null || movement.rigRoot == null) {
            gameObject.SetActive(false);
            return;
        }

        float targetSurfaceY = _renderer != null ? _renderer.bounds.max.y : transform.position.y;
        Vector3 targetPos = _renderer != null ? _renderer.bounds.center : transform.position;

        var capsule = movement.rigRoot.GetComponentInChildren<CapsuleCollider>();
        if (capsule != null) {
            float bottomOffset = capsule.center.y - capsule.height * 0.5f;
            targetPos.y = targetSurfaceY - bottomOffset;
        } else {
            targetPos.y = targetSurfaceY + 0.1f;
        }

        movement.rigRoot.position = targetPos;
        gameObject.SetActive(false);
    }
}
