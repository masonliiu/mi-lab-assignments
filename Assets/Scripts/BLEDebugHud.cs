using System.Text;
using UnityEngine;

[DefaultExecutionOrder(20000)]
public class BLEDebugHud : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] private bool showHud = true;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, -0.06f, 0.45f);
    [SerializeField, Range(0.05f, 1f)] private float refreshIntervalSeconds = 0.1f;

    [Header("Text Style")]
    [SerializeField, Min(8)] private int fontSize = 52;
    [SerializeField, Min(0.0001f)] private float characterSize = 0.002f;
    [SerializeField] private Color textColor = new Color(0.95f, 0.95f, 0.2f, 1f);

    private TextMesh _textMesh;
    private readonly StringBuilder _sb = new StringBuilder(512);
    private Transform _head;
    private float _nextRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<BLEDebugHud>() != null)
        {
            return;
        }

        GameObject go = new GameObject("BLEDebugHud");
        go.AddComponent<BLEDebugHud>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        BuildTextMesh();
        ResolveHeadAnchor(force: true);
    }

    private void Update()
    {
        if (_textMesh == null)
        {
            return;
        }

        if (OVRInput.GetDown(OVRInput.RawButton.RThumbstick))
        {
            showHud = !showHud;
        }

        _textMesh.gameObject.SetActive(showHud);
        if (!showHud)
        {
            return;
        }

        ResolveHeadAnchor(force: false);

        if (Time.unscaledTime >= _nextRefreshTime)
        {
            RefreshHudText();
            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshIntervalSeconds);
        }
    }

    private void BuildTextMesh()
    {
        GameObject go = new GameObject("BLEDebugText");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        _textMesh = go.AddComponent<TextMesh>();
        _textMesh.anchor = TextAnchor.UpperLeft;
        _textMesh.alignment = TextAlignment.Left;
        _textMesh.fontSize = Mathf.Max(8, fontSize);
        _textMesh.characterSize = Mathf.Max(0.0001f, characterSize);
        _textMesh.color = textColor;
        _textMesh.text = "BLE HUD booting...";
        _textMesh.richText = false;

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 32767;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void ResolveHeadAnchor(bool force)
    {
        if (!force && _head != null && _head.gameObject.activeInHierarchy)
        {
            return;
        }

        Transform candidate = null;

        GameObject centerEye = GameObject.Find("CenterEyeAnchor");
        if (centerEye != null)
        {
            candidate = centerEye.transform;
        }

        if (candidate == null && Camera.main != null)
        {
            candidate = Camera.main.transform;
        }

        if (candidate == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (!cameras[i].enabled)
                {
                    continue;
                }

                candidate = cameras[i].transform;
                break;
            }
        }

        if (candidate == null || candidate == _head)
        {
            return;
        }

        _head = candidate;
        transform.SetParent(_head, false);
        transform.localPosition = localOffset;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private void RefreshHudText()
    {
        _sb.Clear();
        _sb.Append("BLE Debug HUD\n");

        BLETransport ble = BLETransport.Instance;
        if (ble == null)
        {
            _sb.Append("Transport: missing\n");
        }
        else
        {
            _sb.Append("Perm: ").Append(ble.PermissionsGranted ? "OK" : "MISSING").Append('\n');
            _sb.Append("Conn: ").Append(ble.IsConnected ? "CONNECTED" : "DISCONNECTED")
                .Append(" | Scan: ").Append(ble.IsScanning ? "Y" : "N")
                .Append(" | Busy: ").Append(ble.IsConnecting ? "Y" : "N")
                .Append('\n');
            _sb.Append("PWM req/pending/wrote: ")
                .Append(ble.LastRequestedPwm).Append('/')
                .Append(ble.PendingPwm).Append('/')
                .Append(ble.LastWrittenPwm).Append('\n');

            _sb.Append("Event: ").Append(ble.DebugLastEvent);
            float age = ble.DebugLastEventAgeSeconds;
            if (age >= 0f)
            {
                _sb.Append(" (").Append(age.ToString("F1")).Append("s)");
            }
            _sb.Append('\n');
            _sb.Append("Error: ").Append(string.IsNullOrEmpty(ble.DebugLastError) ? "-" : ble.DebugLastError).Append('\n');
        }

        HapticsManager haptics = HapticsManager.Instance;
        if (haptics == null)
        {
            _sb.Append("Haptics: missing");
        }
        else
        {
            _sb.Append("Haptics: ").Append(haptics.State)
                .Append(" pwm=").Append(haptics.OutputPwm);
        }

        _textMesh.text = _sb.ToString();
    }
}
