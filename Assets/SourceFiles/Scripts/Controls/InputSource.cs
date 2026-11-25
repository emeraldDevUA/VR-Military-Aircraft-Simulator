using UnityEngine;

/// <summary>
/// Reads player input (keyboard or joystick such as the Thrustmaster HOTAS X)
/// and writes clean, normalized values into a FlightInput data holder.
/// The flight model only ever reads from 'input'.
/// </summary>
public class InputSource : MonoBehaviour
{
    public enum Mode { Keyboard, Joystick }

    public const int ButtonCount = 12; // buttons 0..11

    [Header("Output")]
    public FlightInput input = new FlightInput();

    [Header("Mode")]
    public Mode mode = Mode.Joystick;

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

    [Header("Joystick buttons")]
    [Tooltip("0 = any connected joystick, 1-8 = a specific joystick")]
    [Range(0, 8)] public int joystickNumber = 0;
    public bool logButtons = false;      // prints presses to the console, handy for mapping

    /// <summary>True while button i is held.</summary>
    public bool[] buttonHeld = new bool[ButtonCount];
    /// <summary>True only on the frame button i was pressed.</summary>
    public bool[] buttonPressed = new bool[ButtonCount];
    /// <summary>True only on the frame button i was released.</summary>
    public bool[] buttonReleased = new bool[ButtonCount];

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
        EnsureButtonArrays();
        ClearButtons();
    }

    void Update()
    {
        switch (mode)
        {
            case Mode.Keyboard:
                ReadKeyboard();
                ClearButtons();
                break;
            case Mode.Joystick:
                ReadJoystick();
                ReadButtons();
                break;
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

    /// <summary>Reads joystick buttons 0..11 into held / pressed / released arrays.</summary>
    void ReadButtons()
    {
        EnsureButtonArrays();

        for (int i = 0; i < ButtonCount; i++)
        {
            KeyCode key = ButtonKey(i);
            buttonHeld[i]     = Input.GetKey(key);
            buttonPressed[i]  = Input.GetKeyDown(key);
            buttonReleased[i] = Input.GetKeyUp(key);

            if (logButtons && buttonPressed[i])
                Debug.Log($"InputSource: joystick button {i} pressed", this);
        }
    }

    /// <summary>Maps a button index to the matching Unity KeyCode.</summary>
    KeyCode ButtonKey(int index)
    {
        if (joystickNumber <= 0)
            return (KeyCode)((int)KeyCode.JoystickButton0 + index);                       // any joystick

        // Joystick1Button0 = 350, Joystick2Button0 = 370, ... (20 codes per joystick)
        return (KeyCode)((int)KeyCode.Joystick1Button0 + (joystickNumber - 1) * 20 + index);
    }

    // ----------------------------------------------------------------- Helpers

    /// <summary>Safe accessors so other scripts don't have to range-check.</summary>
    public bool GetButton(int i)     => i >= 0 && i < ButtonCount && buttonHeld[i];
    public bool GetButtonDown(int i) => i >= 0 && i < ButtonCount && buttonPressed[i];
    public bool GetButtonUp(int i)   => i >= 0 && i < ButtonCount && buttonReleased[i];

    void EnsureButtonArrays()
    {
        if (buttonHeld     == null || buttonHeld.Length     != ButtonCount) buttonHeld     = new bool[ButtonCount];
        if (buttonPressed  == null || buttonPressed.Length  != ButtonCount) buttonPressed  = new bool[ButtonCount];
        if (buttonReleased == null || buttonReleased.Length != ButtonCount) buttonReleased = new bool[ButtonCount];
    }

    void ClearButtons()
    {
        EnsureButtonArrays();
        for (int i = 0; i < ButtonCount; i++)
        {
            buttonHeld[i] = false;
            buttonPressed[i] = false;
            buttonReleased[i] = false;
        }
    }

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
        ClearButtons();
    }


[Header("Debug")]
public bool showOnScreen = true;

void OnGUI()
{
    if (!showOnScreen) return;

    string held = "";
    for (int i = 0; i < ButtonCount; i++)
        if (buttonHeld[i]) held += i + " ";

    string text = "Buttons held: " + (held == "" ? "none" : held);

    const float width = 300f;
    const float height = 25f;
    const float margin = 10f;

    var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight };

    GUI.Label(new Rect(Screen.width - width - margin, margin, width, height), text, style);
}
}