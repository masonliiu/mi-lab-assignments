using UnityEngine;
using System.Collections.Generic;

public class InteractableVR : MonoBehaviour, IVRMenuOwner
{
    public enum InteractionType
    {
        Translate,
        Rotation,
        ColorChange,
        Scaling,
        Teleportation
    }

    public enum CubeMode { None, TranslationX, TranslationY, TranslationZ, RotationX, RotationY, RotationZ, NoAction }
    public enum SphereMode { None, ChangeColor, Scale, NoAction }
    public enum ColorChoice { Red, Green, Blue, Random }
    public enum ScaleDirection { Up, Down }

    [Header("Menu")]
    public VRMenuManager.InteractableKind kind;

    [Header("Legacy fallback")]
    public InteractionType interactionType;

    [Header("Radio")]
    [Tooltip("AudioSource: the component that plays sound. Add it to the same GameObject (or assign one). Clips: the actual sound files (MP3/WAV); assign your tracks here for Power and Change Song.")]
    public AudioSource audioSource;
    public AudioClip[] clips = new AudioClip[0];
    int _radioClipIndex;
    bool _radioOn;
    bool _radioDirectPowerEnabled;
    float _savedVolume = 1f;

    [Header("Lamp")]
    public Light lampLight;
    public float maxIntensity = 8000f;
    bool _lampOn = true;
    bool _lampDirectPowerEnabled;

    PointHandler _pointHandler;
    Renderer _renderer;
    Color _originalColor;
    Color _altColor = Color.red;
    bool _usingAltColor;

    CubeMode _currentCubeMode = CubeMode.None;
    SphereMode _currentSphereMode = SphereMode.None;
    ColorChoice _colorChoice = ColorChoice.Red;
    ScaleDirection _scaleDirection = ScaleDirection.Up;
    float _autoColorTimer;

    VRMenuManager.InteractableKind IVRMenuOwner.Kind => ResolvedKind();
    Transform IVRMenuOwner.Transform => transform;

    void Awake()
    {
        _pointHandler = GetComponent<PointHandler>();
        _renderer = GetComponent<Renderer>();
        if (_renderer == null)
            _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null)
            _originalColor = _renderer.material.color;
        if (lampLight == null)
            lampLight = GetComponentInChildren<Light>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (SceneResetManager.Instance != null)
            SceneResetManager.Instance.Register(transform);
        _radioOn = audioSource != null && audioSource.isPlaying;
        if (audioSource != null)
        {
            _savedVolume = audioSource.volume;
            audioSource.loop = true;
            if (clips != null && clips.Length > 0 && audioSource.clip == null)
            {
                audioSource.clip = clips[0];
                if (_radioOn) audioSource.Play();
            }
        }
        if (lampLight != null)
            _lampOn = lampLight.enabled;
    }

    VRMenuManager.InteractableKind ResolvedKind()
    {
        if (kind == VRMenuManager.InteractableKind.Lamp || kind == VRMenuManager.InteractableKind.Radio)
            return kind;
        switch (interactionType)
        {
            case InteractionType.Translate:
            case InteractionType.Rotation:
                return VRMenuManager.InteractableKind.Cube;
            case InteractionType.ColorChange:
            case InteractionType.Scaling:
                return VRMenuManager.InteractableKind.Sphere;
            case InteractionType.Teleportation:
                return VRMenuManager.InteractableKind.Teleportation;
            default:
                return VRMenuManager.InteractableKind.Cube;
        }
    }

    bool IsHovered() => _pointHandler != null && _pointHandler.IsHovered;

    bool IsCubeTranslationMode()
    {
        return _currentCubeMode == CubeMode.TranslationX ||
               _currentCubeMode == CubeMode.TranslationY ||
               _currentCubeMode == CubeMode.TranslationZ;
    }

    bool IsCubeRotationMode()
    {
        return _currentCubeMode == CubeMode.RotationX ||
               _currentCubeMode == CubeMode.RotationY ||
               _currentCubeMode == CubeMode.RotationZ;
    }

    string CubeModeOptionId()
    {
        switch (_currentCubeMode)
        {
            case CubeMode.TranslationX: return "Translation X";
            case CubeMode.TranslationY: return "Translation Y";
            case CubeMode.TranslationZ: return "Translation Z";
            case CubeMode.RotationX: return "Rotation X";
            case CubeMode.RotationY: return "Rotation Y";
            case CubeMode.RotationZ: return "Rotation Z";
            default: return "";
        }
    }

    bool TickTimer(float interval)
    {
        _autoColorTimer -= Time.deltaTime;
        if (_autoColorTimer > 0f) return false;
        _autoColorTimer = interval;
        return true;
    }

    void CycleRadioSong()
    {
        if (audioSource == null || clips == null || clips.Length < 2) return;
        _radioClipIndex = (_radioClipIndex + 1) % clips.Length;
        audioSource.clip = clips[_radioClipIndex];
        if (_radioOn) audioSource.Play();
    }

    void Update()
    {
        var resolved = ResolvedKind();

        if (IsHovered())
        {
            if (OVRInput.GetDown(OVRInput.Button.Two) && resolved != VRMenuManager.InteractableKind.Teleportation)
            {
                if (VRMenuManager.Instance != null)
                    VRMenuManager.Instance.OpenMenu(this);
                return;
            }

            if (VRMenuManager.Instance != null && VRMenuManager.Instance.IsMenuOpen && (Object)VRMenuManager.Instance.CurrentOwner != (Object)this)
                return;

            if (resolved == VRMenuManager.InteractableKind.Cube)
            {
                if (IsCubeTranslationMode() && OVRInput.Get(OVRInput.Button.One))
                    TranslateObject();
                else if (IsCubeRotationMode() && OVRInput.Get(OVRInput.Button.One))
                    RotateObject();
            }
            else if (resolved == VRMenuManager.InteractableKind.Sphere)
            {
                if (_currentSphereMode == SphereMode.ChangeColor && OVRInput.GetDown(OVRInput.Button.One))
                    ColorChangeObject();
                else if (_currentSphereMode == SphereMode.Scale && OVRInput.GetDown(OVRInput.Button.One))
                    ScaleObject();
            }
            else if (resolved == VRMenuManager.InteractableKind.Teleportation && OVRInput.GetDown(OVRInput.Button.Four))
            {
                TeleportObject();
            }
            else if (resolved == VRMenuManager.InteractableKind.Radio && OVRInput.GetDown(OVRInput.Button.One))
            {
                if (_radioDirectPowerEnabled)
                    ToggleRadioPower();
            }
            else if (resolved == VRMenuManager.InteractableKind.Lamp && OVRInput.GetDown(OVRInput.Button.One))
            {
                if (_lampDirectPowerEnabled)
                    ToggleLampPower();
            }
        }

        var menu = VRMenuManager.Instance;
        if (menu == null) return;
        if (resolved == VRMenuManager.InteractableKind.Cube)
        {
            string modeId = CubeModeOptionId();
            if (modeId.Length > 0 && menu.IsAutomationEnabled(this, modeId))
            {
                if (IsCubeTranslationMode())
                    TranslateObject();
                else if (IsCubeRotationMode())
                    RotateObject();
            }
        }
        else if (resolved == VRMenuManager.InteractableKind.Sphere)
        {
            string colorOptionId = "Change Color " + _colorChoice;
            string scaleOptionId = _scaleDirection == ScaleDirection.Up ? "Scale Up" : "Scale Down";
            if (menu.IsAutomationEnabled(this, colorOptionId) && _currentSphereMode == SphereMode.ChangeColor)
            {
                if (TickTimer(1.5f))
                    ColorChangeObject();
            }
            else if (menu.IsAutomationEnabled(this, scaleOptionId) && _currentSphereMode == SphereMode.Scale)
            {
                if (TickTimer(0.5f))
                    ScaleObject();
            }
        }
        else if (resolved == VRMenuManager.InteractableKind.Lamp && menu.IsAutomationEnabled(this, "Power"))
        {
            if (TickTimer(2f))
            {
                _lampOn = !_lampOn;
                if (lampLight != null) lampLight.enabled = _lampOn;
            }
        }
        else if (resolved == VRMenuManager.InteractableKind.Radio && (menu.IsAutomationEnabled(this, "Power") || menu.IsAutomationEnabled(this, "Change Song")))
        {
            if (TickTimer(2f))
            {
                if (menu.IsAutomationEnabled(this, "Power"))
                {
                    _radioOn = !_radioOn;
                    if (audioSource != null)
                    {
                        if (_radioOn) { audioSource.volume = _savedVolume; audioSource.Play(); }
                        else audioSource.Stop();
                    }
                }
                else if (menu.IsAutomationEnabled(this, "Change Song"))
                    CycleRadioSong();
            }
        }
    }

    public void OnMenuOptionSelected(string optionId)
    {
        var resolved = ResolvedKind();
        if (resolved == VRMenuManager.InteractableKind.Cube)
        {
            switch (optionId)
            {
                case "Translation X": _currentCubeMode = CubeMode.TranslationX; break;
                case "Translation Y": _currentCubeMode = CubeMode.TranslationY; break;
                case "Translation Z": _currentCubeMode = CubeMode.TranslationZ; break;
                case "Rotation X": _currentCubeMode = CubeMode.RotationX; break;
                case "Rotation Y": _currentCubeMode = CubeMode.RotationY; break;
                case "Rotation Z": _currentCubeMode = CubeMode.RotationZ; break;
                case "No Action": _currentCubeMode = CubeMode.NoAction; break;
                case "Exit": break;
            }
        }
        else if (resolved == VRMenuManager.InteractableKind.Sphere)
        {
            switch (optionId)
            {
                case "Change Color Red": _currentSphereMode = SphereMode.ChangeColor; _colorChoice = ColorChoice.Red; ApplyColorChoiceImmediate(); break;
                case "Change Color Green": _currentSphereMode = SphereMode.ChangeColor; _colorChoice = ColorChoice.Green; ApplyColorChoiceImmediate(); break;
                case "Change Color Blue": _currentSphereMode = SphereMode.ChangeColor; _colorChoice = ColorChoice.Blue; ApplyColorChoiceImmediate(); break;
                case "Change Color Random": _currentSphereMode = SphereMode.ChangeColor; _colorChoice = ColorChoice.Random; ApplyColorChoiceImmediate(); break;
                case "Scale Up": _currentSphereMode = SphereMode.Scale; _scaleDirection = ScaleDirection.Up; break;
                case "Scale Down": _currentSphereMode = SphereMode.Scale; _scaleDirection = ScaleDirection.Down; break;
                case "No Action": _currentSphereMode = SphereMode.NoAction; break;
                case "Exit": break;
            }
        }
        else if (resolved == VRMenuManager.InteractableKind.Radio)
        {
            switch (optionId)
            {
                case "Power":
                    _radioDirectPowerEnabled = true;
                    ToggleRadioPower();
                    if (!_radioOn && VRMenuManager.Instance != null)
                        VRMenuManager.Instance.SetSelectedOption(this, "No Action");
                    break;
                case "Change Song":
                    _radioDirectPowerEnabled = true;
                    CycleRadioSong();
                    if (VRMenuManager.Instance != null)
                        VRMenuManager.Instance.SetSelectedOption(this, _radioOn ? "Power" : "No Action");
                    break;
                case "No Action":
                    _radioDirectPowerEnabled = false;
                    _radioOn = false;
                    if (audioSource != null) audioSource.Stop();
                    break;
                case "Exit":
                    break;
            }
        }
        else if (resolved == VRMenuManager.InteractableKind.Lamp)
        {
            switch (optionId)
            {
                case "Power":
                    _lampDirectPowerEnabled = true;
                    ToggleLampPower();
                    if (!_lampOn && VRMenuManager.Instance != null)
                        VRMenuManager.Instance.SetSelectedOption(this, "No Action");
                    break;
                case "No Action":
                    _lampDirectPowerEnabled = false;
                    _lampOn = false;
                    if (lampLight != null) lampLight.enabled = false;
                    break;
                case "Exit":
                    break;
            }
        }
    }

    void ToggleRadioPower()
    {
        _radioOn = !_radioOn;
        if (audioSource != null)
        {
            if (_radioOn) { audioSource.volume = _savedVolume; audioSource.Play(); }
            else { audioSource.Stop(); }
        }
    }

    void ToggleLampPower()
    {
        _lampOn = !_lampOn;
        if (lampLight != null) lampLight.enabled = _lampOn;
    }

    public void ResetToNoAction()
    {
        _currentCubeMode = CubeMode.None;
        _currentSphereMode = SphereMode.None;
        _radioDirectPowerEnabled = false;
        _lampDirectPowerEnabled = false;

        var outlines = GetComponentsInChildren<Outline>(true);
        for (int i = 0; i < outlines.Length; i++)
        {
            if (outlines[i] != null)
                outlines[i].enabled = false;
        }
    }

    public void OnVolumeChanged(float value)
    {
        _savedVolume = value;
        if (audioSource != null)
            audioSource.volume = value;
    }

    public void OnIntensityChanged(float value)
    {
        if (lampLight != null)
            lampLight.intensity = value * maxIntensity;
    }

    Vector3 TranslationAxis()
    {
        switch (_currentCubeMode)
        {
            case CubeMode.TranslationX: return Vector3.right;
            case CubeMode.TranslationY: return Vector3.up;
            case CubeMode.TranslationZ: return Vector3.forward;
            default: return Vector3.up;
        }
    }

    Vector3 RotationAxis()
    {
        switch (_currentCubeMode)
        {
            case CubeMode.RotationX: return Vector3.right;
            case CubeMode.RotationY: return Vector3.up;
            case CubeMode.RotationZ: return Vector3.forward;
            default: return Vector3.up;
        }
    }

    void TranslateObject()
    {
        float speed = 0.5f;
        transform.Translate(TranslationAxis() * speed * Time.deltaTime, Space.World);
    }

    void RotateObject()
    {
        float speed = 60f;
        Bounds bounds;
        var rend = GetComponentInChildren<Renderer>();
        if (rend != null)
            bounds = rend.bounds;
        else
        {
            var col = GetComponent<Collider>();
            bounds = col != null ? col.bounds : new Bounds(transform.position, Vector3.one * 0.5f);
        }
        transform.RotateAround(bounds.center, RotationAxis(), speed * Time.deltaTime);
    }

    static readonly Color ColorRed = new Color(0.9f, 0.2f, 0.2f, 1f);
    static readonly Color ColorGreen = new Color(0.2f, 0.8f, 0.3f, 1f);
    static readonly Color ColorBlue = new Color(0.2f, 0.4f, 0.95f, 1f);

    Color ColorForChoice()
    {
        switch (_colorChoice)
        {
            case ColorChoice.Red: return ColorRed;
            case ColorChoice.Green: return ColorGreen;
            case ColorChoice.Blue: return ColorBlue;
            case ColorChoice.Random: return new Color(Random.value, Random.value, Random.value, 1f);
            default: return _altColor;
        }
    }

    void ApplyColorChoiceImmediate()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;
        Color target = ColorForChoice();
        foreach (var r in renderers)
        {
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
                mats[i].color = target;
            r.materials = mats;
        }
        _usingAltColor = true; // so next A-press toggles back to original
    }

    void ColorChangeObject()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;
        Color target = _usingAltColor ? _originalColor : ColorForChoice();
        foreach (var r in renderers)
        {
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
                mats[i].color = target;
            r.materials = mats;
        }
        _usingAltColor = !_usingAltColor;
    }

    void ScaleObject()
    {
        float delta = _scaleDirection == ScaleDirection.Up ? 0.1f : -0.1f;
        var r = GetComponent<Renderer>();
        if (r != null)
        {
            Vector3 centerBefore = r.bounds.center;
            transform.localScale += Vector3.one * delta;
            transform.localScale = Vector3.Max(transform.localScale, Vector3.one * 0.1f);
            Vector3 centerAfter = r.bounds.center;
            transform.position += (centerBefore - centerAfter);
        }
        else
        {
            transform.localScale += Vector3.one * delta;
            transform.localScale = Vector3.Max(transform.localScale, Vector3.one * 0.1f);
        }
    }

    void TeleportObject()
    {
        var movement = FindFirstObjectByType<Movement>();
        if (movement == null || movement.rigRoot == null)
        {
            gameObject.SetActive(false);
            return;
        }
        float targetSurfaceY = _renderer != null ? _renderer.bounds.max.y : transform.position.y;
        Vector3 targetPos = _renderer != null ? _renderer.bounds.center : transform.position;
        var capsule = movement.rigRoot.GetComponentInChildren<CapsuleCollider>();
        if (capsule != null)
        {
            float bottomOffset = capsule.center.y - capsule.height * 0.5f;
            targetPos.y = targetSurfaceY - bottomOffset;
        }
        else
            targetPos.y = targetSurfaceY + 0.1f;
        movement.rigRoot.position = targetPos;
        gameObject.SetActive(false);
    }
}
