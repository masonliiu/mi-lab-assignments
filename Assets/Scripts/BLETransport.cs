using System;
using System.Text;
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

    private float _nextRetryTime;
    private float _lastWriteTime;
    private int _pendingPwm = -1;
    private int _lastWrittenPwm = -1;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _activity;
    private AndroidJavaObject _adapter;
    private AndroidJavaObject _scanner;
    private AndroidJavaObject _gatt;
    private AndroidJavaObject _writeCharacteristic;
    private AndroidJavaObject _uuidClass;

    private BleScanCallback _scanCallback;
    private BleGattCallback _gattCallback;
#endif

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (autoConnectOnStart)
        {
            StartScanAndConnect();
        }
    }

    void Update()
    {
        if (autoConnectOnStart && !IsConnected && Time.unscaledTime >= _nextRetryTime)
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
            if (_scanner == null)
            {
                return;
            }

            _scanCallback ??= new BleScanCallback(this);
            _scanner.Call("startScan", _scanCallback);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("BLE scan/connect start failed: " + ex.Message);
        }
#else
        Debug.Log("BLETransport scan skipped (Editor/non-Android). Target=" + targetDeviceName + " Service=" + serviceUuid + " Char=" + characteristicUuid);
#endif
    }

    public void Disconnect()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (_gatt != null)
            {
                _gatt.Call("disconnect");
                _gatt.Call("close");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("BLE disconnect failed: " + ex.Message);
        }
#endif
        IsConnected = false;
#if UNITY_ANDROID && !UNITY_EDITOR
        _writeCharacteristic = null;
        _gatt = null;
#endif
    }

    public void SendIntensity(int pwm)
    {
        _pendingPwm = Mathf.Clamp(pwm, 0, 255);
    }

    private void WritePendingIfPossible()
    {
        if (_pendingPwm == _lastWrittenPwm)
        {
            _pendingPwm = -1;
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!IsConnected || _gatt == null || _writeCharacteristic == null)
        {
            return;
        }

        try
        {
            int toWrite = _pendingPwm;
            _pendingPwm = -1;

            byte[] data = Encoding.ASCII.GetBytes(toWrite.ToString());
            _writeCharacteristic.Call<bool>("setValue", data);
            bool ok = _gatt.Call<bool>("writeCharacteristic", _writeCharacteristic);

            if (ok)
            {
                _lastWrittenPwm = toWrite;
                _lastWriteTime = Time.unscaledTime;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("BLE write failed: " + ex.Message);
            IsConnected = false;
        }
#else
        _lastWrittenPwm = _pendingPwm;
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

        RequestPermissions();

        if (_adapter == null)
        {
            AndroidJavaClass bluetoothAdapter = new AndroidJavaClass("android.bluetooth.BluetoothAdapter");
            _adapter = bluetoothAdapter.CallStatic<AndroidJavaObject>("getDefaultAdapter");
        }

        if (_adapter == null)
        {
            Debug.LogWarning("BLE adapter unavailable.");
            return;
        }

        if (!_adapter.Call<bool>("isEnabled"))
        {
            Debug.LogWarning("Bluetooth is disabled on device.");
            return;
        }

        if (_scanner == null)
        {
            _scanner = _adapter.Call<AndroidJavaObject>("getBluetoothLeScanner");
        }

        if (_uuidClass == null)
        {
            _uuidClass = new AndroidJavaClass("java.util.UUID");
        }
    }

    private void RequestPermissions()
    {
        const string fineLocation = "android.permission.ACCESS_FINE_LOCATION";
        const string bleScan = "android.permission.BLUETOOTH_SCAN";
        const string bleConnect = "android.permission.BLUETOOTH_CONNECT";

        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(fineLocation))
        {
            UnityEngine.Android.Permission.RequestUserPermission(fineLocation);
        }
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(bleScan))
        {
            UnityEngine.Android.Permission.RequestUserPermission(bleScan);
        }
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(bleConnect))
        {
            UnityEngine.Android.Permission.RequestUserPermission(bleConnect);
        }
    }

    private void OnScanResult(AndroidJavaObject result)
    {
        if (result == null)
        {
            return;
        }

        AndroidJavaObject device = result.Call<AndroidJavaObject>("getDevice");
        if (device == null)
        {
            return;
        }

        string name = device.Call<string>("getName");
        if (!string.Equals(name, targetDeviceName, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            _scanner?.Call("stopScan", _scanCallback);
        }
        catch (Exception)
        {
            // Intentionally ignored.
        }

        _gattCallback ??= new BleGattCallback(this);
        _gatt = device.Call<AndroidJavaObject>("connectGatt", _activity, false, _gattCallback);
    }

    private void OnConnectionStateChanged(AndroidJavaObject gatt, int newState)
    {
        // Android BluetoothProfile.STATE_CONNECTED == 2
        if (newState == 2 && gatt != null)
        {
            _gatt = gatt;
            _gatt.Call<bool>("discoverServices");
            return;
        }

        IsConnected = false;
        _writeCharacteristic = null;
        _gatt = null;
    }

    private void OnServicesDiscovered(AndroidJavaObject gatt)
    {
        if (gatt == null || _uuidClass == null)
        {
            return;
        }

        try
        {
            AndroidJavaObject serviceId = _uuidClass.CallStatic<AndroidJavaObject>("fromString", serviceUuid);
            AndroidJavaObject charId = _uuidClass.CallStatic<AndroidJavaObject>("fromString", characteristicUuid);

            AndroidJavaObject service = gatt.Call<AndroidJavaObject>("getService", serviceId);
            if (service == null)
            {
                Debug.LogWarning("BLE service UUID not found: " + serviceUuid);
                IsConnected = false;
                return;
            }

            _writeCharacteristic = service.Call<AndroidJavaObject>("getCharacteristic", charId);
            if (_writeCharacteristic == null)
            {
                Debug.LogWarning("BLE characteristic UUID not found: " + characteristicUuid);
                IsConnected = false;
                return;
            }

            IsConnected = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("BLE service discovery failed: " + ex.Message);
            IsConnected = false;
        }
    }

    private class BleScanCallback : AndroidJavaProxy
    {
        private readonly BLETransport _owner;

        public BleScanCallback(BLETransport owner) : base("android.bluetooth.le.ScanCallback")
        {
            _owner = owner;
        }

        // Signature: onScanResult(int callbackType, ScanResult result)
        void onScanResult(int callbackType, AndroidJavaObject result)
        {
            _owner?.OnScanResult(result);
        }
    }

    private class BleGattCallback : AndroidJavaProxy
    {
        private readonly BLETransport _owner;

        public BleGattCallback(BLETransport owner) : base("android.bluetooth.BluetoothGattCallback")
        {
            _owner = owner;
        }

        // Signature: onConnectionStateChange(BluetoothGatt gatt, int status, int newState)
        void onConnectionStateChange(AndroidJavaObject gatt, int status, int newState)
        {
            _owner?.OnConnectionStateChanged(gatt, newState);
        }

        // Signature: onServicesDiscovered(BluetoothGatt gatt, int status)
        void onServicesDiscovered(AndroidJavaObject gatt, int status)
        {
            _owner?.OnServicesDiscovered(gatt);
        }
    }
#endif
}

