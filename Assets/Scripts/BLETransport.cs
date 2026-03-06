using System;
using UnityEngine;

public class BLETransport : MonoBehaviour
{
    public static BLETransport Instance { get; private set; }

    [Header("Target Peripheral")]
    [SerializeField] private string targetDeviceName = "MagnetHand";
    [SerializeField] private string serviceUuid = "12345678-1234-1234-1234-1234567890ab";
    [SerializeField] private string characteristicUuid = "abcd1234-5678-5678-5678-abcdef123456";

    [Header("Connection")]
    [SerializeField] private bool autoConnectOnStart = true;
    [SerializeField] private float retryScanIntervalSeconds = 2f;
    [SerializeField] private float minWriteIntervalSeconds = 0.02f;

    public bool IsConnected { get; private set; }
    public bool IsScanning { get; private set; }
    public bool IsConnecting { get; private set; }
    public bool PermissionsGranted => _permissionsGranted;
    public int PendingPwm => _pendingPwm;
    public int LastRequestedPwm { get; private set; }
    public int LastWrittenPwm { get; private set; } = -1;
    public string DebugLastEvent => _lastEvent;
    public float DebugLastEventAgeSeconds => _lastEventTime < 0f ? -1f : Time.unscaledTime - _lastEventTime;
    public string DebugLastError => _lastError;

    private float _nextRetryTime;
    private float _lastWriteTime;
    private float _nextPermissionRequestTime;
    private int _pendingPwm = -1;
    private bool _permissionsGranted;
    private string _lastEvent = "Init";
    private float _lastEventTime = -1f;
    private string _lastError = string.Empty;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _activity;
    private AndroidJavaClass _bridgeClass;
#endif

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RecordEvent("Awake");
#if !(UNITY_ANDROID && !UNITY_EDITOR)
        _permissionsGranted = true;
#endif
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        if (autoConnectOnStart)
        {
            _nextRetryTime = Time.unscaledTime;
            RecordEvent("AutoConnectQueued");
        }
    }

    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        SyncBridgeState();
#endif

        if (autoConnectOnStart && !IsConnected && !IsScanning && !IsConnecting && Time.unscaledTime >= _nextRetryTime)
        {
            StartScanAndConnect();
            _nextRetryTime = Time.unscaledTime + retryScanIntervalSeconds;
        }

        if (_pendingPwm < 0)
        {
            return;
        }

        if (Time.unscaledTime - _lastWriteTime < minWriteIntervalSeconds)
        {
            return;
        }

        WritePendingIfPossible();
    }

    public void StartScanAndConnect()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            EnsureAndroidReady();
            if (_bridgeClass == null || IsConnected || IsScanning || IsConnecting)
            {
                return;
            }

            bool started = _bridgeClass.CallStatic<bool>("startScan");
            if (started)
            {
                RecordEvent("ScanRequested");
                _lastError = string.Empty;
            }

            SyncBridgeState();
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            RecordEvent("ScanStartFailed");
            Debug.LogWarning("BLE scan/connect start failed: " + ex.Message);
        }
#else
        Debug.Log("BLETransport scan skipped (Editor/non-Android). Target=" + targetDeviceName + " Service=" + serviceUuid + " Char=" + characteristicUuid);
        RecordEvent("ScanSkippedEditor");
#endif
    }

    public void Disconnect()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (_bridgeClass != null)
            {
                _bridgeClass.CallStatic("disconnect");
            }
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            Debug.LogWarning("BLE disconnect failed: " + ex.Message);
        }

        SyncBridgeState();
#endif

        IsConnected = false;
        IsScanning = false;
        IsConnecting = false;
        _pendingPwm = 0;
        LastWrittenPwm = -1;
        RecordEvent("Disconnected");
    }

    void OnDisable()
    {
        Disconnect();
    }

    public void SendIntensity(int pwm)
    {
        LastRequestedPwm = Mathf.Clamp(pwm, 0, 255);
        _pendingPwm = LastRequestedPwm;

        if (_pendingPwm > 0 && !IsConnected && !IsScanning && !IsConnecting && Time.unscaledTime >= _nextRetryTime)
        {
            StartScanAndConnect();
            _nextRetryTime = Time.unscaledTime + retryScanIntervalSeconds;
        }
    }

    private void WritePendingIfPossible()
    {
        if (_pendingPwm == LastWrittenPwm)
        {
            _pendingPwm = -1;
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!IsConnected || _bridgeClass == null)
        {
            return;
        }

        int toWrite = _pendingPwm;
        _pendingPwm = -1;

        try
        {
            bool ok = _bridgeClass.CallStatic<bool>("writePwm", toWrite);
            if (ok)
            {
                LastWrittenPwm = toWrite;
                _lastWriteTime = Time.unscaledTime;
                _lastError = string.Empty;
                RecordEvent("Write:" + toWrite);
            }
            else
            {
                // Re-queue so we retry after connection settles.
                _pendingPwm = toWrite;
                SyncBridgeState();
            }
        }
        catch (Exception ex)
        {
            _pendingPwm = toWrite;
            _lastError = ex.Message;
            RecordEvent("WriteFailed");
            Debug.LogWarning("BLE write failed: " + ex.Message);
            IsConnected = false;
        }
#else
        LastWrittenPwm = _pendingPwm;
        _pendingPwm = -1;
        _lastWriteTime = Time.unscaledTime;
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void EnsureAndroidReady()
    {
        if (_activity == null)
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            _activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }

        _permissionsGranted = HasRequiredPermissions();
        if (!_permissionsGranted && Time.unscaledTime >= _nextPermissionRequestTime)
        {
            RequestPermissions();
            _nextPermissionRequestTime = Time.unscaledTime + 2f;
            RecordEvent("RequestPerm");
        }

        if (_bridgeClass == null)
        {
            _bridgeClass = new AndroidJavaClass("com.milab.ble.BleBridge");
        }

        if (_bridgeClass != null && _activity != null)
        {
            _bridgeClass.CallStatic("initialize", _activity);
            _bridgeClass.CallStatic("setTarget", targetDeviceName, serviceUuid, characteristicUuid);
        }

        SyncBridgeState();
    }

    private void RequestPermissions()
    {
        const string fineLocation = "android.permission.ACCESS_FINE_LOCATION";
        const string bleScan = "android.permission.BLUETOOTH_SCAN";
        const string bleConnect = "android.permission.BLUETOOTH_CONNECT";
        int sdkInt = GetAndroidSdkInt();

        if (sdkInt < 31 && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(fineLocation))
        {
            UnityEngine.Android.Permission.RequestUserPermission(fineLocation);
        }
        if (sdkInt >= 31 && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(bleScan))
        {
            UnityEngine.Android.Permission.RequestUserPermission(bleScan);
        }
        if (sdkInt >= 31 && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(bleConnect))
        {
            UnityEngine.Android.Permission.RequestUserPermission(bleConnect);
        }
    }

    private bool HasRequiredPermissions()
    {
        const string fineLocation = "android.permission.ACCESS_FINE_LOCATION";
        const string bleScan = "android.permission.BLUETOOTH_SCAN";
        const string bleConnect = "android.permission.BLUETOOTH_CONNECT";
        int sdkInt = GetAndroidSdkInt();

        if (sdkInt >= 31)
        {
            bool hasScan = UnityEngine.Android.Permission.HasUserAuthorizedPermission(bleScan);
            bool hasConnect = UnityEngine.Android.Permission.HasUserAuthorizedPermission(bleConnect);
            return hasScan && hasConnect;
        }

        return UnityEngine.Android.Permission.HasUserAuthorizedPermission(fineLocation);
    }

    private int GetAndroidSdkInt()
    {
        AndroidJavaClass versionClass = new AndroidJavaClass("android.os.Build$VERSION");
        return versionClass.GetStatic<int>("SDK_INT");
    }

    private void SyncBridgeState()
    {
        if (_bridgeClass == null)
        {
            return;
        }

        try
        {
            IsConnected = _bridgeClass.CallStatic<bool>("isConnected");
            IsScanning = _bridgeClass.CallStatic<bool>("isScanning");
            IsConnecting = _bridgeClass.CallStatic<bool>("isConnecting");

            string eventText = _bridgeClass.CallStatic<string>("getLastEvent");
            if (!string.IsNullOrWhiteSpace(eventText) && !string.Equals(eventText, _lastEvent, StringComparison.Ordinal))
            {
                RecordEvent(eventText);
            }

            string errorText = _bridgeClass.CallStatic<string>("getLastError");
            _lastError = errorText ?? string.Empty;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            RecordEvent("BridgeSyncFailed");
        }
    }
#endif

    private void RecordEvent(string message)
    {
        _lastEvent = string.IsNullOrWhiteSpace(message) ? "Event" : message;
        _lastEventTime = Time.unscaledTime;
    }
}
