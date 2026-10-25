using UnityEngine;

/// <summary>
/// Reads player input (keyboard or joystick such as the Thrustmaster HOTAS X)
/// and writes clean, normalized values into a FlightInput data holder.
/// The flight model only ever reads from 'input'.
/// </summary>
public class InputSource : MonoBehaviour
{
    public enum Mode { Keyboard, Joystick }

    [Header("Output")]
    public FlightInput input = new FlightInput();

    [Header("Mode")]
    public Mode mode = Mode.Keyboard;

    [Header("Joystick axis names (must exist in Edit > Project Settings > Input Manager)")]
    public string aileronAxis  = "Aileron";
    public string elevatorAxis = "Elevator";
    public string rudderAxis   = "Rudder";
    public string throttleAxis = "Throttle";

    [Header("Joystick tuning")]
    [Range(0f, 0.5f)] public float deadzone = 0.08f;
    public bool invertElevator = true;   // stick back = pull up (nose up) on most setups
    public bool invertThrottle = false;  // many throttles report -1 at full forward
    public bool throttleIsBipolar = true; // axis range -1..1 instead of 0..1

    [Header("Keyboard tuning")]
    public float throttleRate = 0.5f;    // throttle change per second
    public float trimRate = 0.2f;        // trim change per second
    public KeyCode throttleUp   = KeyCode.LeftShift;
    public KeyCode throttleDown = KeyCode.LeftControl;
    public KeyCode rudderLeft   = KeyCode.Q;
    public KeyCode rudderRight  = KeyCode.E;
    public KeyCode trimUp       = KeyCode.R;
    public KeyCode trimDown     = KeyCode.F;

    void Awake()
    {
        if (input == null) input = new FlightInput();
        input.Clear();
    }

    void Update()
    {
        switch (mode)
        {
            case Mode.Keyboard: ReadKeyboard(); break;
            case Mode.Joystick: ReadJoystick(); break;
        }

        // Safety: keep everything in range and free of NaN
        input.aileron  = Sanitize(input.aileron,  -1f, 1f);
        input.elevator = Sanitize(input.elevator, -1f, 1f);
        input.rudder   = Sanitize(input.rudder,   -1f, 1f);
        input.trim     = Sanitize(input.trim,     -1f, 1f);
        input.throttle = Sanitize(input.throttle,  0f, 1f);
    }

    // ---------------------------------------------------------------- Keyboard

    void ReadKeyboard()
    {
        // Read the keys directly. The built-in "Horizontal"/"Vertical" axes also read any connected
        // joystick (e.g. the HOTAS X), which would feed a constant stick offset into the flight model.
        float ailTarget = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                        - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float eleTarget = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                        - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        input.aileron  = Mathf.MoveTowards(input.aileron,  ailTarget, 4f * Time.deltaTime);
        input.elevator = Mathf.MoveTowards(input.elevator, eleTarget, 4f * Time.deltaTime);

        float rud = 0f;
        if (Input.GetKey(rudderRight)) rud += 1f;
        if (Input.GetKey(rudderLeft))  rud -= 1f;
        input.rudder = rud;

        // Throttle and trim are persistent: they stay where you leave them
        if (Input.GetKey(throttleUp))   input.throttle += throttleRate * Time.deltaTime;
        if (Input.GetKey(throttleDown)) input.throttle -= throttleRate * Time.deltaTime;

        if (Input.GetKey(trimUp))   input.trim += trimRate * Time.deltaTime;
        if (Input.GetKey(trimDown)) input.trim -= trimRate * Time.deltaTime;
    }

    // ---------------------------------------------------------------- Joystick

    void ReadJoystick()
    {
        input.aileron  = ApplyDeadzone(SafeAxis(aileronAxis));
        input.rudder   = ApplyDeadzone(SafeAxis(rudderAxis));

        float elev = ApplyDeadzone(SafeAxis(elevatorAxis));
        input.elevator = invertElevator ? -elev : elev;

        float thr = SafeAxis(throttleAxis);
        if (invertThrottle) thr = -thr;
        input.throttle = throttleIsBipolar ? (thr + 1f) * 0.5f : thr;

        // Trim still via keyboard so you can tune out any residual drift
        if (Input.GetKey(trimUp))   input.trim += trimRate * Time.deltaTime;
        if (Input.GetKey(trimDown)) input.trim -= trimRate * Time.deltaTime;
    }

    // ----------------------------------------------------------------- Helpers

    /// <summary>Returns 0 instead of throwing if the axis is not defined in the Input Manager.</summary>
    float SafeAxis(string axisName)
    {
        if (string.IsNullOrEmpty(axisName)) return 0f;
        try { return Input.GetAxis(axisName); }
        catch (System.ArgumentException)
        {
            Debug.LogWarning($"InputSource: axis '{axisName}' is not defined in the Input Manager.", this);
            return 0f;
        }
    }

    /// <summary>Removes small stick offsets and rescales so output still reaches +/-1.</summary>
    float ApplyDeadzone(float v)
    {
        float a = Mathf.Abs(v);
        if (a < deadzone) return 0f;
        return Mathf.Sign(v) * (a - deadzone) / (1f - deadzone);
    }

    static float Sanitize(float v, float min, float max)
    {
        if (float.IsNaN(v)) return 0f;
        return Mathf.Clamp(v, min, max);
    }

    void OnDisable()
    {
        // Don't leave stale input behind if this component gets disabled
        if (input != null) input.Clear();
    }
}