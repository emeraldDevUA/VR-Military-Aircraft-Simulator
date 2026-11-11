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

public class PIDController
{
    public float Kp, Ki, Kd;
    public float OutputMin = -1f, OutputMax = 1f;

    private float integral;
    private float prevError;
    private bool first = true;

    public PIDController(float kp, float ki, float kd)
    {
        Kp = kp; Ki = ki; Kd = kd;
    }

    public float Calculate(float current, float target, float dt)
    {
        float error = target - current;
        float derivative = first ? 0f : (error - prevError) / dt;
        first = false;
        prevError = error;

        float unclamped = Kp * error + Ki * (integral + error * dt) + Kd * derivative;
        float output = Mathf.Clamp(unclamped, OutputMin, OutputMax);

        // Anti-windup: only integrate when not saturated
        if (Mathf.Approximately(unclamped, output))
            integral += error * dt;

        return output;
    }

    public void Reset() { integral = 0f; prevError = 0f; first = true; }
}

public class Airplane
{
    // Dependencies
    private readonly Engine engine;
    private readonly Gun gun;

    // Aircraft components
    private readonly Dictionary<WingRole, Wing> wings = new();
    private readonly Dictionary<ControlSurface, Transform> controlSurfaces = new();
    private readonly Dictionary<Pylon, Missile> missiles = new();

    // Physics
    private Rigidbody rigidbody;

    // Flight controls
    private Vector3 controlInput;
    private float m_throttle = 0f;

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
    public void SetControlInput(Vector3 input)
    {
        controlInput = Vector3.ClampMagnitude(input, 1f);
    }

    public Vector3 ControlInput => controlInput;

    public void SetThrottle(float throttle){
        m_throttle = throttle;
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
    public void ComputeForces()
    {
        if (rigidbody == null)
            return;

        float propulsionForce = (float)engine.computePropulsionForce();
        float thrust = propulsionForce * Mathf.Clamp01(m_throttle);

        rigidbody.AddForce(rigidbody.rotation * Vector3.right * thrust);

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
    public void UpdateAnimations()
    {
        AnimateControlSurface(ControlSurface.Rudder);
        AnimateControlSurface(ControlSurface.Elevator);
        AnimateControlSurface(ControlSurface.LeftAileron);
        AnimateControlSurface(ControlSurface.RightAileron);
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


    private void AnimateControlSurface(ControlSurface surface)
    {
        if (!controlSurfaces.TryGetValue(surface, out Transform t))
            return;

        float max = GetMaxDeflection(surface);
        float angle;

        Vector3 hingeAxis = Vector3.right;

        switch (surface)
        {
            case ControlSurface.LeftAileron:
                angle = controlInput.z * max;
                break;

            case ControlSurface.RightAileron:
                angle = -controlInput.z * max;   // opposite to the left one
                break;

            case ControlSurface.Rudder:
                angle = controlInput.y * max;
                hingeAxis = Vector3.up;
                break;

            case ControlSurface.Elevator:
                angle = controlInput.x * max;
                break;

            default:
                return;
        }

        Quaternion target = restRotations[surface] * Quaternion.AngleAxis(angle, hingeAxis);
        t.localRotation = Quaternion.RotateTowards(t.localRotation, target, 180f * Time.deltaTime);
    }
}