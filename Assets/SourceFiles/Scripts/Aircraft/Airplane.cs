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
    Tail
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

    // Aircraft systems
    public FlapState Flaps { get; private set; }
        = FlapState.Retracted;

    public LandingGearState LandingGear { get; private set; }
        = LandingGearState.Down;

    public bool AirbrakeEnabled { get; private set; }

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

        // Engine thrust
        // Wing lift
        // Aerodynamic drag
        // Additional forces from aircraft systems
    }

    public void ComputeTorques()
    {
        if (rigidbody == null)
            return;

        // Pitch torque
        // Roll torque
        // Yaw torque
    }

    // Visual updates
    public void UpdateAnimations()
    {
        AnimateControlSurface(ControlSurface.Rudder);
        AnimateControlSurface(ControlSurface.Elevator);
        AnimateControlSurface(ControlSurface.LeftAileron);
        AnimateControlSurface(ControlSurface.RightAileron);
    }

    private void AnimateControlSurface(ControlSurface surface)
    {
        if (!controlSurfaces.TryGetValue(surface, out Transform transform))
            return;

        // Apply the appropriate rotation based on controlInput.
    }
}