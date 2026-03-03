using UnityEngine;

public class HapticsManager : MonoBehaviour
{
    public static HapticsManager Instance { get; private set; }

    public enum HapticState
    {
        Idle,
        Magnet,
        ProximityWarn
    }

    [Header("State Thresholds")]
    [Range(0f, 1f)]
    [SerializeField] private float dangerThreshold = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float safeThreshold = 0.5f;

    [Header("Magnet Output")]
    [Range(0, 255)]
    [SerializeField] private int magnetMinPwm = 30;
    [Range(0, 255)]
    [SerializeField] private int magnetMaxPwm = 180;
    [SerializeField] private float magnetPulseDepth = 0.15f;
    [SerializeField] private float magnetPulseRateHz = 5f;

    [Header("Magnet Burst (Lock Feedback)")]
    [SerializeField] private float magnetBurstDuration = 0.08f;
    [Range(0, 255)]
    [SerializeField] private int magnetBurstPwm = 220;

    [Header("Proximity Warning Pulses")]
    [Range(0, 255)]
    [SerializeField] private int proximityWarnPwm = 230;
    [SerializeField] private float proximityPulseOnSeconds = 0.05f;
    [SerializeField] private float proximityMinOffSeconds = 0.04f;
    [SerializeField] private float proximityMaxOffSeconds = 0.22f;

    [Header("BLE Send Throttling")]
    [SerializeField] private float minSendIntervalSeconds = 0.03f;
    [SerializeField] private int minPwmDeltaToSend = 3;

    public HapticState State { get; private set; } = HapticState.Idle;
    public int OutputPwm { get; private set; }

    private bool _magnetActive;
    private float _magnetLoad;
    float _proximityLevel;
    private float _phase;
    private float _burstTimeRemaining;
    private float _proximityPulseTimer;
    private bool _proximityPulseOn;
    private float _lastSendTime = -999f;
    private int _lastSentPwm = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        if (_burstTimeRemaining > 0f)
        {
            _burstTimeRemaining -= dt;
        }

        HapticState nextState = ComputeNextState();
        if (nextState != State)
        {
            State = nextState;
            _phase = 0f;
            _proximityPulseTimer = 0f;
            _proximityPulseOn = false;
        }

        _phase += dt;
        OutputPwm = ComputeOutputPwm(State, dt);

        SendToActuator(OutputPwm);
    }

    public void SetMagnetActive(bool active)
    {
        _magnetActive = active;
    }

    public void SetMagnetActive(bool active, float normalizedLoad)
    {
        _magnetLoad = Mathf.Clamp01(normalizedLoad);
        SetMagnetActive(active);
    }

    public void TriggerMagnetBurst()
    {
        _burstTimeRemaining = Mathf.Max(_burstTimeRemaining, magnetBurstDuration);
    }

    public void UpdateProximityLevel(float level)
    {
        _proximityLevel = Mathf.Clamp01(level);
    }

    HapticState ComputeNextState()
    {
        if (State == HapticState.ProximityWarn)
        {
            if (_proximityLevel <= safeThreshold)
            {
                return _magnetActive ? HapticState.Magnet : HapticState.Idle;
            }

            return HapticState.ProximityWarn;
        }

        if (_proximityLevel >= dangerThreshold)
        {
            return HapticState.ProximityWarn;
        }

        if (_magnetActive)
        {
            return HapticState.Magnet;
        }

        return HapticState.Idle;
    }

    int ComputeOutputPwm(HapticState state, float dt)
    {
        switch (state)
        {
            case HapticState.Idle:
                return 0;

            case HapticState.Magnet:
                return ComputeMagnetPwm();

            case HapticState.ProximityWarn:
                return ComputeProximityWarnPwm(dt);

            default:
                return 0;
        }
    }

    int ComputeMagnetPwm()
    {
        if (_burstTimeRemaining > 0f)
        {
            return magnetBurstPwm;
        }

        float strength = Mathf.Clamp01(_magnetLoad);
        float basePwm = Mathf.Lerp(magnetMinPwm, magnetMaxPwm, strength);
        float pulse = 1f - magnetPulseDepth + (Mathf.Sin(_phase * Mathf.PI * 2f * Mathf.Max(0.01f, magnetPulseRateHz)) * 0.5f + 0.5f) * magnetPulseDepth;
        return Mathf.Clamp(Mathf.RoundToInt(basePwm * pulse), 0, 255);
    }

    int ComputeProximityWarnPwm(float dt)
    {
        float t = Mathf.Clamp01((_proximityLevel - safeThreshold) / Mathf.Max(0.001f, (1f - safeThreshold)));
        float offDuration = Mathf.Lerp(proximityMaxOffSeconds, proximityMinOffSeconds, t);

        _proximityPulseTimer += dt;
        if (_proximityPulseOn && _proximityPulseTimer >= proximityPulseOnSeconds)
        {
            _proximityPulseOn = false;
            _proximityPulseTimer = 0f;
        }
        else if (!_proximityPulseOn && _proximityPulseTimer >= offDuration)
        {
            _proximityPulseOn = true;
            _proximityPulseTimer = 0f;
        }

        return _proximityPulseOn ? proximityWarnPwm : 0;
    }

    void SendToActuator(int pwm)
    {
        BLETransport transport = BLETransport.Instance;
        int safePwm = transport != null && transport.IsConnected ? Mathf.Clamp(pwm, 0, 255) : 0;

        float now = Time.unscaledTime;
        bool intervalElapsed = now - _lastSendTime >= minSendIntervalSeconds;
        bool changedEnough = _lastSentPwm < 0 || Mathf.Abs(safePwm - _lastSentPwm) >= minPwmDeltaToSend;
        bool forceZero = safePwm == 0 && _lastSentPwm != 0;

        if ((intervalElapsed && changedEnough) || forceZero)
        {
            if (transport != null)
            {
                transport.SendIntensity(safePwm);
            }
            _lastSentPwm = safePwm;
            _lastSendTime = now;
        }
    }
}

