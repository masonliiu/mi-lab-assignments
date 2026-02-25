using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;

public class VRMenuManager : MonoBehaviour
{
    public enum InteractableKind { Cube, Sphere, Radio, Lamp, Teleportation }

    public static VRMenuManager Instance { get; private set; }

    [Header("Ray Origin for menu")]
    public Transform rayOriginFallback;

    const float MenuOffsetAbove = 0.55f;
    const float MenuOffsetAboveTable = 1.1f; // wood table, stereo table
    const float MenuOffsetInFrontY = 0.5f; // extra height for plant/painting
    const float MenuScale = 0.0032f;
    const float PanelWidth = 380f;
    const float PanelHeight = 720f;
    const float ButtonHeight = 44f;
    const float SubButtonHeight = 36f;
    const float SliderHeight = 40f;
    const float Padding = 10f;
    const float SubIndent = 20f;
    const float GapBelowHeader = 12f;
    const float ExitButtonSize = 42f;
    const float AutoButtonSize = 34f;
    static readonly Color ButtonNormal = new Color(0.35f, 0.35f, 0.45f, 1f);
    static readonly Color ButtonHighlight = new Color(0.5f, 0.55f, 0.7f, 1f);
    static readonly Color ButtonSelected = new Color(0.35f, 0.6f, 0.5f, 1f);

    Canvas _canvas;
    RectTransform _canvasRect;
    Camera _eventCamera;
    Collider _menuCollider;
    RayInteractable _menuRayInteractable;
    readonly Dictionary<InteractableKind, RectTransform> _panels = new Dictionary<InteractableKind, RectTransform>();
    readonly Dictionary<int, string> _savedSelectionByOwnerId = new Dictionary<int, string>();
    readonly Dictionary<string, bool> _automationEnabled = new Dictionary<string, bool>();
    readonly Dictionary<string, bool> _expandedGroups = new Dictionary<string, bool>();
    readonly Dictionary<string, GameObject> _groupSubContainers = new Dictionary<string, GameObject>();
    IVRMenuOwner _currentOwner;
    bool _menuOpen;
    string _selectedOptionId = "No Action";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _eventCamera = Camera.main;
        if (_eventCamera == null) _eventCamera = FindFirstObjectByType<Camera>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void BuildMenuCanvas()
    {
        var root = new GameObject("VRMenuCanvas");
        root.transform.SetParent(transform);

        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = _eventCamera;
        _canvas.sortingOrder = 100;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        scaler.referencePixelsPerUnit = 100f;
        scaler.scaleFactor = 1f;
        scaler.referenceResolution = new Vector2(420, 380);

        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<CanvasGroup>();

        _canvasRect = root.GetComponent<RectTransform>();
        _canvasRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        _canvasRect.localScale = Vector3.one * MenuScale;
        _canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
        _canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
        _canvasRect.pivot = new Vector2(0.5f, 0.5f);

        BuildPanelWithDropdowns(InteractableKind.Cube,
            new[] { ("Translation", new[] { "Translation X", "Translation Y", "Translation Z" }), ("Rotation", new[] { "Rotation X", "Rotation Y", "Rotation Z" }) },
            new[] { "No Action", "Exit" });
        BuildPanelWithDropdowns(InteractableKind.Sphere,
            new[] { ("Change Color", new[] { "Change Color Red", "Change Color Green", "Change Color Blue", "Change Color Random" }), ("Scale", new[] { "Scale Up", "Scale Down" }) },
            new[] { "No Action", "Exit" });
        BuildPanel(InteractableKind.Radio, new[] { "Power", "Change Song", "Volume", "No Action", "Exit" }, hasVolumeSlider: true);
        BuildPanel(InteractableKind.Lamp, new[] { "Power", "Intensity", "No Action", "Exit" }, hasIntensitySlider: true);

        var colliderGo = new GameObject("MenuCollider");
        colliderGo.transform.SetParent(root.transform, false);
        colliderGo.transform.localPosition = Vector3.zero;  
        colliderGo.transform.localRotation = Quaternion.identity;
        colliderGo.transform.localScale = new Vector3(PanelWidth, PanelHeight, 1f);
        var box = colliderGo.AddComponent<BoxCollider>();
        box.size = Vector3.one;
        box.isTrigger = false;
        _menuCollider = box;
        var surf = colliderGo.AddComponent<ColliderSurface>();
        surf.InjectCollider(box);
        _menuRayInteractable = colliderGo.AddComponent<RayInteractable>();
        _menuRayInteractable.InjectSurface(surf);

        root.SetActive(false);
    }

    void BuildPanelWithDropdowns(InteractableKind kind, (string groupLabel, string[] subOptionIds)[] groups, string[] simpleOptions)
    {
        var panel = new GameObject(kind + "Panel");
        panel.transform.SetParent(_canvasRect, false);

        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var image = panel.AddComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

        float y = -Padding;

        foreach (var (groupLabel, subOptionIds) in groups)
        {
            string key = kind + "_" + groupLabel;
            bool expanded = _expandedGroups.TryGetValue(key, out var v) && v;

            y -= ButtonHeight;
            var headerRow = new GameObject("Header_" + groupLabel);
            headerRow.transform.SetParent(panel.transform, false);
            var headerRect = headerRow.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            // Match width of normal rows, e.g. "No Action"
            headerRect.sizeDelta = new Vector2(PanelWidth - Padding * 2, ButtonHeight);
            headerRect.anchoredPosition = new Vector2(0, y);

            var headerBtn = CreateButton(headerRow.transform, groupLabel + (expanded ? " \u25BC" : " \u25B6"), () => ToggleGroup(kind, groupLabel), isExit: false);
            var mainRect = headerBtn.GetComponent<RectTransform>();
            // Use same non-stretch rect strategy as normal rows.
            mainRect.anchorMin = new Vector2(0f, 0.5f);
            mainRect.anchorMax = new Vector2(0f, 0.5f);
            mainRect.pivot = new Vector2(0f, 0.5f);
            mainRect.sizeDelta = new Vector2(PanelWidth - Padding * 2, ButtonHeight);
            mainRect.anchoredPosition = Vector2.zero;
            y -= ButtonHeight + GapBelowHeader;

            float subContentWidth = PanelWidth - Padding * 2 - SubIndent * 2;
            float subHeight = subOptionIds.Length * (SubButtonHeight + Padding);
            var subContainer = new GameObject("Sub_" + groupLabel);
            subContainer.transform.SetParent(panel.transform, false);
            var subRect = subContainer.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 1f);
            subRect.anchorMax = new Vector2(0.5f, 1f);
            subRect.pivot = new Vector2(0.5f, 1f);
            subRect.anchoredPosition = new Vector2(0, y);
            subRect.sizeDelta = new Vector2(subContentWidth, subHeight);
            _groupSubContainers[key] = subContainer;
            subContainer.SetActive(expanded);

            float subY = 0f;
            foreach (var subId in subOptionIds)
            {
                subY -= SubButtonHeight;
                var subRow = CreateOptionRow(subContainer.transform, subId, subY, () => OnMenuButtonClicked(subId), kind, SubButtonHeight, subContentWidth);
                var sr = subRow.GetComponent<RectTransform>();
                sr.anchoredPosition = new Vector2(0, subY);
                subY -= Padding;
            }
            y -= subHeight + Padding;
        }

        foreach (var opt in simpleOptions)
        {
            if (opt == "Exit") continue;
            y -= ButtonHeight;
            var row = CreateOptionRow(panel.transform, opt, y, () => OnMenuButtonClicked(opt), kind);
            y -= Padding;
        }

        var exitBtn = CreateButton(panel.transform, "Exit", () => OnMenuButtonClicked("Exit"), isExit: true);
        var exitRect = exitBtn.GetComponent<RectTransform>();
        exitRect.anchorMin = new Vector2(1f, 1f);
        exitRect.anchorMax = new Vector2(1f, 1f);
        exitRect.pivot = new Vector2(1f, 1f);
        exitRect.sizeDelta = new Vector2(ExitButtonSize, ExitButtonSize);
        exitRect.anchoredPosition = new Vector2(-Padding, -Padding);

        _panels[kind] = panelRect;
    }

    void ToggleGroup(InteractableKind kind, string groupLabel)
    {
        string key = kind + "_" + groupLabel;
        _expandedGroups[key] = !(_expandedGroups.TryGetValue(key, out var cur) && cur);
        if (_groupSubContainers.TryGetValue(key, out var go) && go != null)
            go.SetActive(_expandedGroups[key]);
        UpdateGroupHeaderLabel(kind, groupLabel);
    }

    void UpdateGroupHeaderLabel(InteractableKind kind, string groupLabel)
    {
        string key = kind + "_" + groupLabel;
        bool expanded = _expandedGroups.TryGetValue(key, out var ex) && ex;
        string headerName = "Header_" + groupLabel;
        foreach (var kv in _panels)
        {
            if (kv.Key != kind) continue;
            var header = kv.Value.transform.Find(headerName);
            if (header == null) continue;
            var text = header.GetComponentInChildren<Text>();
            if (text != null) text.text = groupLabel + (expanded ? " \u25BC" : " \u25B6");
            break;
        }
    }

    void BuildPanel(InteractableKind kind, string[] options, bool hasVolumeSlider = false, bool hasIntensitySlider = false)
    {
        var panel = new GameObject(kind + "Panel");
        panel.transform.SetParent(_canvasRect, false);

        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var image = panel.AddComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

        float y = -Padding;

        foreach (var opt in options)
        {
            if (opt == "Exit")
                continue;

            if (opt == "Volume" && hasVolumeSlider)
            {
                y -= SliderHeight;
                var sliderGo = CreateSlider(panel.transform, "Volume", 0f, 1f, (v) =>
                {
                    _currentOwner?.OnVolumeChanged(v);
                });
                var sliderRect = sliderGo.GetComponent<RectTransform>();
                sliderRect.anchoredPosition = new Vector2(0, y);
                y -= Padding + SliderHeight;
                continue;
            }
            if (opt == "Intensity" && hasIntensitySlider)
            {
                y -= SliderHeight;
                var sliderGo = CreateSlider(panel.transform, "Intensity", 0f, 1f, (v) =>
                {
                    _currentOwner?.OnIntensityChanged(v);
                });
                var sliderRect = sliderGo.GetComponent<RectTransform>();
                sliderRect.anchoredPosition = new Vector2(0, y);
                y -= Padding + SliderHeight;
                continue;
            }

            y -= ButtonHeight;
            var row = CreateOptionRow(panel.transform, opt, y, () => OnMenuButtonClicked(opt), kind);
            y -= Padding;
        }

        // exit button
        var exitBtn = CreateButton(panel.transform, "Exit", () => OnMenuButtonClicked("Exit"), isExit: true);
        var exitRect = exitBtn.GetComponent<RectTransform>();
        exitRect.anchorMin = new Vector2(1f, 1f);
        exitRect.anchorMax = new Vector2(1f, 1f);
        exitRect.pivot = new Vector2(1f, 1f);
        exitRect.sizeDelta = new Vector2(ExitButtonSize, ExitButtonSize);
        exitRect.anchoredPosition = new Vector2(-Padding, -Padding);

        _panels[kind] = panelRect;
    }

    GameObject CreateButton(Transform parent, string label, System.Action onClick, bool isExit = false)
    {
        var go = new GameObject("Btn_" + label);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = isExit ? new Vector2(ExitButtonSize, ExitButtonSize) : new Vector2(PanelWidth - Padding * 2, ButtonHeight);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);

        var image = go.AddComponent<Image>();
        image.color = isExit ? new Color(0.75f, 0.2f, 0.2f, 1f) : ButtonNormal;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick?.Invoke());

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textGo.AddComponent<Text>();
        text.text = isExit ? "X" : label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = isExit ? 30 : 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        return go;
    }

    GameObject CreateOptionRow(Transform parent, string optionId, float y, System.Action onOptionClick, InteractableKind kind, float rowHeight = 0f, float rowWidth = 0f)
    {
        float h = rowHeight > 0 ? rowHeight : ButtonHeight;
        float w = rowWidth > 0 ? rowWidth : (PanelWidth - Padding * 2);
        bool showAuto = optionId != "No Action" && kind != InteractableKind.Radio;
        var row = new GameObject("Row_" + optionId);
        row.transform.SetParent(parent, false);
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 1f);
        rowRect.anchorMax = new Vector2(0.5f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(w, h);
        rowRect.anchoredPosition = new Vector2(0, y);

        float mainWidth = showAuto ? (w - AutoButtonSize - 4f) : w;
        var mainBtn = CreateButton(row.transform, optionId, onOptionClick, isExit: false);
        var mainRect = mainBtn.GetComponent<RectTransform>();
        mainRect.anchorMin = new Vector2(0f, 0.5f);
        mainRect.anchorMax = new Vector2(0f, 0.5f);
        mainRect.pivot = new Vector2(0f, 0.5f);
        mainRect.sizeDelta = new Vector2(mainWidth, h);
        mainRect.anchoredPosition = new Vector2(0, 0);

        if (showAuto)
        {
            var autoGo = new GameObject("AutoBtn_" + optionId);
            autoGo.transform.SetParent(row.transform, false);
            var autoRect = autoGo.AddComponent<RectTransform>();
            autoRect.anchorMin = new Vector2(1f, 0.5f);
            autoRect.anchorMax = new Vector2(1f, 0.5f);
            autoRect.pivot = new Vector2(1f, 0.5f);
            autoRect.sizeDelta = new Vector2(AutoButtonSize, AutoButtonSize);
            autoRect.anchoredPosition = new Vector2(0, 0);
            var autoImg = autoGo.AddComponent<Image>();
            autoImg.color = ButtonNormal;
            var autoBtn = autoGo.AddComponent<Button>();
            autoBtn.targetGraphic = autoImg;
            string opt = optionId;
            autoBtn.onClick.AddListener(() =>
            {
                if (_currentOwner != null)
                {
                    bool next = !IsAutomationEnabled(_currentOwner, opt);
                    SetAutomationEnabled(_currentOwner, opt, next);
                }
            });
            var autoText = new GameObject("Text");
            autoText.transform.SetParent(autoGo.transform, false);
            var autoTextRect = autoText.AddComponent<RectTransform>();
            autoTextRect.anchorMin = Vector2.zero;
            autoTextRect.anchorMax = Vector2.one;
            autoTextRect.offsetMin = Vector2.zero;
            autoTextRect.offsetMax = Vector2.zero;
            var autoTextComp = autoText.AddComponent<Text>();
            autoTextComp.text = "A";
            autoTextComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            autoTextComp.fontSize = 18;
            autoTextComp.alignment = TextAnchor.MiddleCenter;
            autoTextComp.color = Color.white;
        }

        return row;
    }

    GameObject CreateSlider(Transform parent, string label, float min, float max, System.Action<float> onValueChanged)
    {
        var go = new GameObject("Slider_" + label);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(PanelWidth - Padding * 2, SliderHeight);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);

        var slider = go.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = label == "Volume" ? 1f : 1f;
        slider.onValueChanged.AddListener((v) => onValueChanged?.Invoke(v));

        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        var fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(5, 5);
        fillAreaRect.offsetMax = new Vector2(-5, -5);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.2f, 0.6f, 0.9f, 1f);
        slider.fillRect = fillRect;

        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(go.transform, false);
        var handleAreaRect = handleArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10, 0);
        handleAreaRect.offsetMax = new Vector2(-10, 0);

        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var handleRect = handle.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20, 0);
        handleRect.anchorMin = new Vector2(0, 0.25f);
        handleRect.anchorMax = new Vector2(0, 0.75f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        var handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;
        slider.handleRect = handleRect;

        return go;
    }

    void OnMenuButtonClicked(string optionId)
    {
        if (optionId == "Exit")
        {
            CloseMenu();
            return;
        }
        _selectedOptionId = optionId;
        _currentOwner?.OnMenuOptionSelected(optionId);
        UpdateSelectionAndHoverHighlight(null);
    }

    public void OpenMenu(IVRMenuOwner owner)
    {
        if (owner == null) return;
        CloseMenu();

        if (_canvas == null)
            BuildMenuCanvas();

        _currentOwner = owner;
        int ownerId = owner.Transform.GetInstanceID();
        _selectedOptionId = _savedSelectionByOwnerId.TryGetValue(ownerId, out var saved) ? saved : "No Action";
        _canvas.gameObject.SetActive(true);
        _menuOpen = true;

        CollapseAllGroupsForKind(owner.Kind);
        ExpandGroupContainingOption(owner.Kind, _selectedOptionId);

        foreach (var kv in _panels)
            kv.Value.gameObject.SetActive(kv.Key == owner.Kind);
        if (!_panels.ContainsKey(owner.Kind))
        {
            foreach (var kv in _panels)
                kv.Value.gameObject.SetActive(kv.Key == InteractableKind.Cube);
        }

        if (owner.Kind == InteractableKind.Lamp && _panels.TryGetValue(InteractableKind.Lamp, out var lampPanel))
        {
            var ivr = owner.Transform.GetComponent<InteractableVR>();
            if (ivr != null && ivr.lampLight != null && ivr.maxIntensity > 0f)
            {
                float currentT = Mathf.Clamp01(ivr.lampLight.intensity / ivr.maxIntensity);
                foreach (var s in lampPanel.GetComponentsInChildren<Slider>(true))
                {
                    if (s.gameObject.name == "Slider_Intensity")
                    {
                        s.value = currentT;
                        break;
                    }
                }
            }
        }

        PositionAbove(owner.Transform);
        UpdateSelectionAndHoverHighlight(null);
    }

    void CollapseAllGroupsForKind(InteractableKind kind)
    {
        string prefix = kind + "_";
        foreach (var kv in new System.Collections.Generic.Dictionary<string, bool>(_expandedGroups))
        {
            if (!kv.Key.StartsWith(prefix)) continue;
            _expandedGroups[kv.Key] = false;
            if (_groupSubContainers.TryGetValue(kv.Key, out var go) && go != null)
                go.SetActive(false);
            string groupLabel = kv.Key.Substring(prefix.Length);
            UpdateGroupHeaderLabel(kind, groupLabel);
        }
    }

    void ExpandGroupContainingOption(InteractableKind kind, string optionId)
    {
        if (optionId == null) return;
        string groupLabel = null;
        if (kind == InteractableKind.Cube)
        {
            if (optionId.StartsWith("Translation ")) groupLabel = "Translation";
            else if (optionId.StartsWith("Rotation ")) groupLabel = "Rotation";
        }
        else if (kind == InteractableKind.Sphere)
        {
            if (optionId.StartsWith("Change Color ")) groupLabel = "Change Color";
            else if (optionId.StartsWith("Scale ")) groupLabel = "Scale";
        }
        if (groupLabel != null)
        {
            string key = kind + "_" + groupLabel;
            _expandedGroups[key] = true;
            if (_groupSubContainers.TryGetValue(key, out var go) && go != null)
                go.SetActive(true);
            UpdateGroupHeaderLabel(kind, groupLabel);
        }
    }

    void PositionAbove(Transform target)
    {
        if (target == null) return;
        Bounds bounds;
        var rend = target.GetComponentInChildren<Renderer>();
        if (rend != null)
            bounds = rend.bounds;
        else
        {
            var col = target.GetComponent<Collider>();
            bounds = col != null ? col.bounds : new Bounds(target.position, Vector3.one * 0.5f);
        }
        Vector3 camPos = _eventCamera != null ? _eventCamera.transform.position : bounds.center + Vector3.forward * 2f;
        Vector3 toCamera = (camPos - bounds.center).normalized;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.01f) toCamera = Vector3.forward;
        else toCamera.Normalize();

        bool menuInFront = target.name.IndexOf("Plant", System.StringComparison.OrdinalIgnoreCase) >= 0
            || target.name.IndexOf("Painting", System.StringComparison.OrdinalIgnoreCase) >= 0;
        if (menuInFront)
        {
            float dist = Mathf.Max(bounds.extents.magnitude + 0.3f, 0.5f);
            Vector3 inFront = bounds.center + toCamera * dist;
            inFront.y = Mathf.Max(inFront.y, bounds.center.y) + MenuOffsetInFrontY;
            _canvasRect.position = inFront;
        }
        else
        {
            bool isTable = target.name.IndexOf("table", System.StringComparison.OrdinalIgnoreCase) >= 0;
            float offset = isTable ? MenuOffsetAboveTable : MenuOffsetAbove;
            Vector3 above = bounds.center + Vector3.up * (bounds.extents.y + offset);
            _canvasRect.position = above;
        }
        _canvasRect.rotation = Quaternion.LookRotation(_canvasRect.position - camPos);
    }

    public void CloseMenu()
    {
        if (_currentOwner != null)
            _savedSelectionByOwnerId[_currentOwner.Transform.GetInstanceID()] = _selectedOptionId;
        _menuOpen = false;
        _currentOwner = null;
        if (_canvas != null && _canvas.gameObject != null)
            _canvas.gameObject.SetActive(false);
    }

    public void ResetToNoAction()
    {
        _savedSelectionByOwnerId.Clear();
        CloseMenu();
    }

    public bool IsMenuOpen => _menuOpen;

    public IVRMenuOwner CurrentOwner => _currentOwner;

    public string SelectedOptionId => _selectedOptionId;

    public bool IsAutomationEnabled(IVRMenuOwner owner, string optionId)
    {
        if (owner == null) return false;
        return _automationEnabled.TryGetValue(AutomationKey(owner, optionId), out var v) && v;
    }

    public void SetAutomationEnabled(IVRMenuOwner owner, string optionId, bool enabled)
    {
        if (owner == null) return;
        _automationEnabled[AutomationKey(owner, optionId)] = enabled;
    }

    public void SetSelectedOption(IVRMenuOwner owner, string optionId)
    {
        if (owner == null || _currentOwner != owner) return;
        _selectedOptionId = optionId;
        UpdateSelectionAndHoverHighlight(null);
    }

    static string AutomationKey(IVRMenuOwner owner, string optionId) => owner.Transform.GetInstanceID() + "_" + optionId;

    void Update()
    {
        if (!_menuOpen || _currentOwner == null || _canvas == null || !_canvas.gameObject.activeInHierarchy)
            return;

        PositionAbove(_currentOwner.Transform);
        if (_eventCamera != null)
            _canvasRect.rotation = Quaternion.LookRotation(_canvasRect.position - _eventCamera.transform.position);

        Vector3 hitWorld;
        if (!TryGetMenuHitWorld(out hitWorld))
        {
            UpdateSelectionAndHoverHighlight(null);
            UpdateAutomationButtonVisuals();
            return;
        }

        Button hitButton = GetButtonUnderRay(hitWorld);
        TryGetSliderUnderRay(hitWorld, out Slider hitSlider, out float sliderT);

        UpdateSelectionAndHoverHighlight(hitButton);
        UpdateAutomationButtonVisuals();

        bool pressing = OVRInput.Get(OVRInput.Button.One);
        bool pressedDown = OVRInput.GetDown(OVRInput.Button.One);
        if (hitSlider != null && (pressedDown || pressing))
        {
            float value = Mathf.Lerp(hitSlider.minValue, hitSlider.maxValue, Mathf.Clamp01(sliderT));
            if (Mathf.Abs(hitSlider.value - value) > 0.001f)
                hitSlider.value = value;
        }
        else if (pressedDown && hitButton != null && hitButton.enabled)
        {
            hitButton.onClick.Invoke();
        }
    }

    bool TryGetMenuHitWorld(out Vector3 hitWorld)
    {
        hitWorld = default;
        if (_eventCamera == null || _canvasRect == null) return false;
        if (_menuRayInteractable != null)
        {
            var interactors = FindObjectsByType<RayInteractor>(FindObjectsSortMode.None);
            foreach (var ri in interactors)
            {
                if (ri == null || !ri.gameObject.activeInHierarchy) continue;
                if (ri.Candidate == _menuRayInteractable && ri.CollisionInfo.HasValue)
                {
                    hitWorld = ri.CollisionInfo.Value.Point;
                    return true;
                }
            }
        }
        TryGetRayInteractorRays(out var rightOrigin, out var rightForward, out var leftOrigin, out var leftForward);
        if (rightOrigin != null && rightForward != null && TryGetMenuHitWorldFromRay(rightOrigin.Value, rightForward.Value, out hitWorld)) return true;
        if (leftOrigin != null && leftForward != null && TryGetMenuHitWorldFromRay(leftOrigin.Value, leftForward.Value, out hitWorld)) return true;
        var movement = FindFirstObjectByType<Movement>();
        if (movement != null && movement.rayOriginControllerRight != null && TryGetMenuHitWorldFromRay(movement.rayOriginControllerRight.position, movement.rayOriginControllerRight.forward, out hitWorld)) return true;
        if (movement != null && movement.rayOrigin != null && TryGetMenuHitWorldFromRay(movement.rayOrigin.position, movement.rayOrigin.forward, out hitWorld)) return true;
        if (rayOriginFallback != null && TryGetMenuHitWorldFromRay(rayOriginFallback.position, rayOriginFallback.forward, out hitWorld)) return true;
        if (_eventCamera != null && TryGetMenuHitWorldFromRay(_eventCamera.transform.position, _eventCamera.transform.forward, out hitWorld)) return true;
        return false;
    }

    bool TryGetMenuHitWorldFromRay(Vector3 origin, Vector3 forward, out Vector3 hitWorld)
    {
        hitWorld = default;
        if (_menuCollider != null && _menuCollider.gameObject.activeInHierarchy &&
            Physics.Raycast(origin, forward, out RaycastHit hit, MenuRayMaxDistance) &&
            hit.collider == _menuCollider)
        {
            hitWorld = hit.point;
            return true;
        }
        var ray = new Ray(origin, forward);
        var plane = new Plane(-_canvasRect.forward, _canvasRect.position);
        if (!plane.Raycast(ray, out float enter)) return false;
        hitWorld = ray.GetPoint(enter);
        return true;
    }

    void TryGetSliderUnderRay(Vector3 hitWorld, out Slider slider, out float normalizedT)
    {
        slider = null;
        normalizedT = 0.5f;
        if (_eventCamera == null) return;
        Vector2 screenPos = _eventCamera.WorldToScreenPoint(hitWorld);
        foreach (var kv in _panels)
        {
            if (!kv.Value.gameObject.activeSelf) continue;
            foreach (var s in kv.Value.GetComponentsInChildren<Slider>(true))
            {
                var rect = s.GetComponent<RectTransform>();
                if (rect == null) continue;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, _eventCamera, out Vector2 localPoint))
                    continue;
                if (!rect.rect.Contains(localPoint)) continue;
                var trackRect = rect;
                var fillArea = s.transform.Find("Fill Area");
                if (fillArea != null)
                {
                    var fillAreaRect = fillArea.GetComponent<RectTransform>();
                    if (fillAreaRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(fillAreaRect, screenPos, _eventCamera, out Vector2 trackLocal))
                    {
                        trackRect = fillAreaRect;
                        localPoint = trackLocal;
                    }
                }
                float width = trackRect.rect.width;
                if (width < 0.001f) continue;
                normalizedT = Mathf.Clamp01(Mathf.InverseLerp(trackRect.rect.xMin, trackRect.rect.xMax, localPoint.x));
                slider = s;
                return;
            }
        }
    }

    static readonly Color ExitButtonColor = new Color(0.75f, 0.2f, 0.2f, 1f);

    void UpdateSelectionAndHoverHighlight(Button hovered)
    {
        if (_canvas == null || !_canvas.gameObject.activeInHierarchy) return;
        foreach (var kv in _panels)
        {
            if (!kv.Value.gameObject.activeSelf) continue;
            foreach (var btn in kv.Value.GetComponentsInChildren<Button>(true))
            {
                var img = btn.targetGraphic as Image;
                if (img == null) continue;
                bool isExit = btn.gameObject.name.StartsWith("Btn_Exit");
                if (isExit)
                {
                    img.color = ExitButtonColor;
                    continue;
                }
                bool isSelected = btn.gameObject.name == "Btn_" + _selectedOptionId;
                if (btn == hovered && isSelected)
                    img.color = ButtonSelected;
                else if (btn == hovered)
                    img.color = ButtonHighlight;
                else if (isSelected)
                    img.color = ButtonSelected;
                else
                    img.color = ButtonNormal;
            }
        }
    }

    void UpdateAutomationButtonVisuals()
    {
        if (_canvas == null || _currentOwner == null) return;
        foreach (var kv in _panels)
        {
            if (!kv.Value.gameObject.activeSelf) continue;
            foreach (var btn in kv.Value.GetComponentsInChildren<Button>(true))
            {
                if (!btn.gameObject.name.StartsWith("AutoBtn_")) continue;
                string optionId = btn.gameObject.name.Substring("AutoBtn_".Length);
                var img = btn.targetGraphic as Image;
                if (img != null)
                    img.color = IsAutomationEnabled(_currentOwner, optionId) ? ButtonSelected : ButtonNormal;
            }
        }
    }

    void TryGetRayInteractorRays(out Vector3? rightOrigin, out Vector3? rightForward, out Vector3? leftOrigin, out Vector3? leftForward)
    {
        rightOrigin = null;
        rightForward = null;
        leftOrigin = null;
        leftForward = null;
        var interactors = FindObjectsByType<RayInteractor>(FindObjectsSortMode.None);
        foreach (var ri in interactors)
        {
            if (ri == null || !ri.gameObject.activeInHierarchy) continue;
            bool isLeft = false;
            var p = ri.transform.parent;
            while (p != null)
            {
                if (p.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0) { isLeft = true; break; }
                if (p.name.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0) break;
                p = p.parent;
            }
            if (isLeft)
            {
                leftOrigin = ri.Origin;
                leftForward = ri.Forward;
            }
            else
            {
                rightOrigin = ri.Origin;
                rightForward = ri.Forward;
            }
        }
    }

    const float MenuRayMaxDistance = 10f;

    Button GetButtonUnderRay(Vector3 hitWorld)
    {
        if (_eventCamera == null) return null;
        Vector2 screenPos = _eventCamera.WorldToScreenPoint(hitWorld);
        foreach (var kv in _panels)
        {
            if (!kv.Value.gameObject.activeSelf) continue;
            foreach (var rect in kv.Value.GetComponentsInChildren<RectTransform>(true))
            {
                var button = rect.GetComponent<Button>();
                if (button == null) continue;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, _eventCamera, out Vector2 localPoint))
                    continue;
                if (rect.rect.Contains(localPoint))
                    return button;
            }
        }
        return null;
    }
}
