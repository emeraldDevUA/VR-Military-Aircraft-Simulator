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

    public void AttachMissile(Pylon pylon, Missile missile)
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
    public void ComputeForces(FlightInput input)
    {
        if (rigidbody == null)
            return;

        SetThrottle(input.throttle);
        float propulsionForce = (float)engine.computePropulsionForce();
        rigidbody.AddForce(rigidbody.rotation * Vector3.right * propulsionForce);

        wings[WingRole.LeftWing].ControlInput  =  input.aileron;
        wings[WingRole.RightWing].ControlInput = -input.aileron;
        wings[WingRole.ElevatorWing].ControlInput  =  input.elevator + input.trim;
        wings[WingRole.RudderWing].ControlInput    =  input.rudder;


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