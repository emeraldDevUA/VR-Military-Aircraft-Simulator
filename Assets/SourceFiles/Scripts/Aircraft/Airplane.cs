using UnityEngine;
using System.Collections.Generic;

public enum Pylon
{
    Central,
    FarRight,
    FarLeft
}

public enum WingRole
{
    Hull,
    RightWing,
    LeftWing,
    RudderWing,
    ElevatorWing
}

public enum ControlSurface
{
    LeftAileron,
    RightAileron,
    Rudder,
    Elevator
}

public enum FlapState
{
    Retracted,
    Takeoff,
    Landing
}

public enum LandingGearState
{
    Down,
    Up
}


public class Airplane
{
    // Dependencies
    private Engine engine;
    private readonly Gun gun;

    // Aircraft components
    private readonly Dictionary<WingRole, Wing> wings = new();
    private readonly Dictionary<ControlSurface, Transform> controlSurfaces = new();
    private readonly Dictionary<Pylon, Missile> missiles = new();

    // Physics
    private Rigidbody rigidbody;

    // Aircraft systems
    public FlapState Flaps { get; private set; }
        = FlapState.Retracted;

    public LandingGearState LandingGear { get; private set; }
        = LandingGearState.Down;

    public bool AirbrakeEnabled { get; private set; }

    private PIDController pitch_control_pid;
    private PIDController roll_control_pid;
    private PIDController yaw_control_pid;

    // Constructor
    public Airplane(Engine engine, Gun gun, Rigidbody rigidbody)
    {
        this.engine = engine;
        this.gun = gun;
        this.rigidbody = rigidbody;
    }

    // Component registration
    public void AddWing(WingRole role, Wing wing)
    {
        wings[role] = wing;
    }

    public void AddControlSurface(
        ControlSurface surface,
        Transform transform)
    {
        controlSurfaces[surface] = transform;
    }

    internal void AttachMissile(Pylon pylon, Missile missile)
    {
        missiles[pylon] = missile;
    }

    public bool RemoveMissile(Pylon pylon)
    {
        return missiles.Remove(pylon);
    }

    // Flight controls

    public void SetThrottle(float throttle){
        engine.innerThrottle = throttle;
    }

    // Aircraft systems
    public void SetFlaps(FlapState state)
    {
        Flaps = state;
    }

    public void SetLandingGear(LandingGearState state)
    {
        LandingGear = state;
    }

    public void SetAirbrake(bool enabled)
    {
        AirbrakeEnabled = enabled;
    }

    // Physics


[Header("AoA limits")]
[SerializeField] float aoaLimitDeg  = 26f;    // upper
[SerializeField] float aoaMinDeg    = -20f;   // lower (negative)
[SerializeField] float aoaMarginDeg = 2f;
[SerializeField] float elevatorSign = -1f;    // +1 if positive elevator INCREASES AoA

[Header("Pitch hold (no input)")]
[SerializeField] float holdDeadzone   = 0.02f; // |elevator input| below this = "no input"
[SerializeField] float holdMaxPitch   = 80f;   // hold disabled beyond this pitch (deg)

PIDController aoaLimiter      = new PIDController(0.095f, 0.05f, 0.005f);
PIDController aoaLowerLimiter = new PIDController(0.06f, 0.05f, 0.005f);
PIDController pitchHold       = new PIDController(0.05f, 0.01f, 0.01f);
PIDController aoaHold = new PIDController(0.05f, 0.02f, 0.01f);
[SerializeField] float holdAoaTarget = 0f;
bool  holdActive;
float holdTargetPitch;

public void ResetControllers()   // call on respawn
{
    aoaLimiter.Reset();
    aoaLowerLimiter.Reset();
    pitchHold.Reset();
    aoaHold.Reset();
    holdActive = false;
}

public void ComputeForces(FlightInput input)
{
    if (rigidbody == null) return;
    float dt = Time.fixedDeltaTime;

    Quaternion rot = rigidbody.rotation;

    // ---------- Measurements ----------
    // AoA, identical to UpdateHudData
    Vector3 localVel = Quaternion.Inverse(rot) * rigidbody.linearVelocity;
    float aoaDeg = localVel.x > 0.1f
        ? Mathf.Atan2(-localVel.y, localVel.x) * Mathf.Rad2Deg
        : 0f;

    // Pitch attitude (nose above horizon) and inverted check
    Vector3 fwd = rot * Vector3.right;
    float pitchDeg = Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg;
    bool inverted = (rot * Vector3.up).y < 0f;

    // ---------- Stage 0: pilot command / pitch hold ----------
    // Everything below works in the "AoA-increasing" direction.
    float manualCmd  = Mathf.Clamp(elevatorSign * (input.elevator + input.trim), -1f, 1f);
    bool pilotActive = Mathf.Abs(input.elevator) > holdDeadzone;
    bool holdAllowed = Mathf.Abs(pitchDeg) < holdMaxPitch;

    // 1 = wings roughly level (bank < ~45°), 0 = bank > ~72° or inverted
    float upY    = (rot * Vector3.up).y;
    float wPitch = Mathf.Clamp01((upY - 0.3f) / 0.4f);

    float pilotCmd;
    if (pilotActive || !holdAllowed)
    {
        holdActive = false;
        pitchHold.Calculate(pitchDeg, pitchDeg, dt);
        pitchHold.TrackOutput(manualCmd);
        aoaHold.Calculate(aoaDeg, aoaDeg, dt);
        aoaHold.TrackOutput(manualCmd);
        pilotCmd = manualCmd;
    }
    else
    {
        if (!holdActive) { holdActive = true; holdTargetPitch = pitchDeg; }
        if (wPitch < 0.5f) holdTargetPitch = pitchDeg;   // while banked, keep re-capturing

        float pitchOut = pitchHold.Calculate(pitchDeg, holdTargetPitch, dt);
        float aoaOut   = aoaHold.Calculate(aoaDeg, holdAoaTarget, dt);

        pilotCmd = wPitch * pitchOut + (1f - wPitch) * aoaOut;

        // The inactive PID follows the blended command so its integrator can't wind up
        if (wPitch < 0.05f) pitchHold.TrackOutput(pilotCmd);
        if (wPitch > 0.95f) aoaHold.TrackOutput(pilotCmd);
    }

    // ---------- Stage 1: upper AoA limit ----------
    float limitCmd = aoaLimiter.Calculate(aoaDeg, aoaLimitDeg - aoaMarginDeg, dt);
    float finalCmd;
    if (limitCmd >= pilotCmd)
    {
        finalCmd = pilotCmd;
        aoaLimiter.TrackOutput(pilotCmd);
    }
    else
    {
        finalCmd = limitCmd;
    }

    // ---------- Stage 2: lower AoA limit (skipped when inverted) ----------
    if (!inverted)
    {
        float lowerCmd = aoaLowerLimiter.Calculate(aoaDeg, aoaMinDeg + aoaMarginDeg, dt);
        if (lowerCmd <= finalCmd)
        {
            aoaLowerLimiter.TrackOutput(finalCmd);
        }
        else
        {
            finalCmd = lowerCmd;
        }
    }
    else
    {
        aoaLowerLimiter.Reset();
    }

    float elevator = elevatorSign * finalCmd;

    // ---------- Apply ----------
    SetThrottle(input.throttle);
    float propulsionForce = (float)engine.computePropulsionForce();
    rigidbody.AddForce(rot * Vector3.right * propulsionForce);

    wings[WingRole.LeftWing].ControlInput     =  input.aileron;
    wings[WingRole.RightWing].ControlInput    = -input.aileron;
    wings[WingRole.ElevatorWing].ControlInput =  elevator;
    wings[WingRole.RudderWing].ControlInput   =  input.rudder;

    foreach (Wing wing in wings.Values)
    {
        wing.Apply(rigidbody);
    }

    // Engine thrust
    // Wing lift
    // Aerodynamic drag
    // Additional forces from aircraft systems
}
    // Visual updates
    public void UpdateAnimations(FlightInput input)
    {
        AnimateControlSurface(ControlSurface.Rudder, input);
        AnimateControlSurface(ControlSurface.Elevator, input);
        AnimateControlSurface(ControlSurface.LeftAileron, input);
        AnimateControlSurface(ControlSurface.RightAileron, input);
    }


    private readonly Dictionary<ControlSurface, float> maxDeflection = new()
    {
        { ControlSurface.LeftAileron,  20f },
        { ControlSurface.RightAileron, 20f },
        { ControlSurface.Rudder,       25f },
        { ControlSurface.Elevator,     25f },
    };

    public void SetMaxDeflection(ControlSurface surface, float degrees)
    {
        maxDeflection[surface] = degrees;
    }

    public float GetMaxDeflection(ControlSurface surface)
    {
        return maxDeflection.TryGetValue(surface, out float deg) ? deg : 0f;
    }

    private readonly Dictionary<ControlSurface, Quaternion> restRotations = new();


    private void AnimateControlSurface(ControlSurface surface, FlightInput controlInput)
    {
        if (!controlSurfaces.TryGetValue(surface, out Transform t))
            return;

        float max = GetMaxDeflection(surface);
        float angle;

        Vector3 hingeAxis = Vector3.right;

        switch (surface)
        {
            case ControlSurface.LeftAileron:
                angle = controlInput.aileron * max;
                break;

            case ControlSurface.RightAileron:
                angle = -controlInput.aileron * max;   // opposite to the left one
                break;

            case ControlSurface.Rudder:
                angle = controlInput.rudder * max;
                hingeAxis = Vector3.up;
                break;

            case ControlSurface.Elevator:
                angle = controlInput.elevator * max;
                break;

            default:
                return;
        }

        Quaternion target = restRotations[surface] * Quaternion.AngleAxis(angle, hingeAxis);
        t.localRotation = Quaternion.RotateTowards(t.localRotation, target, 180f * Time.deltaTime);
    }
}