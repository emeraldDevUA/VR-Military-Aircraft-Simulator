using UnityEngine;

// 3D tracker = three independent 1D filters (x, y, z).
// Valid because the axes are assumed uncorrelated and share the same noise.
public class KalmanFilter3D
{
    readonly Kalman1D fx, fy, fz;
    bool initialized;

    public KalmanFilter3D(float sigmaRadar, float sigmaAccel, float maxSpeed)
    {
        fx = new Kalman1D(sigmaRadar, sigmaAccel, maxSpeed);
        fy = new Kalman1D(sigmaRadar, sigmaAccel, maxSpeed);
        fz = new Kalman1D(sigmaRadar, sigmaAccel, maxSpeed);
    }

    // One step with a new measurement z, dt = time since the previous step.
    // Invalid measurement (NaN/Inf) -> treat as missed, predict only.
    // First valid measurement only initializes the state.
    public void Update(Vector3 z, float dt)
    {
        if (!IsFinite(z.x) || !IsFinite(z.y) || !IsFinite(z.z)) { PredictOnly(dt); return; }

        if (!initialized)
        {
            fx.Init(z.x); fy.Init(z.y); fz.Init(z.z);
            initialized = true;
            return;
        }

        fx.Predict(dt); fy.Predict(dt); fz.Predict(dt);
        fx.Correct(z.x); fy.Correct(z.y); fz.Correct(z.z);
    }

    // No measurement this frame: move the state forward, uncertainty grows
    public void PredictOnly(float dt)
    {
        if (!initialized) return;
        fx.Predict(dt); fy.Predict(dt); fz.Predict(dt);
    }

    public bool IsInitialized => initialized;

    static bool IsFinite(float a) => !float.IsNaN(a) && !float.IsInfinity(a);

    public Vector3 Position => new Vector3(fx.x, fy.x, fz.x);
    public Vector3 Velocity => new Vector3(fx.v, fy.v, fz.v);

    // Access to per-axis filters (diagnostics)
    public Kalman1D X => fx;
    public Kalman1D Y => fy;
    public Kalman1D Z => fz;

    // Where the target will be in t seconds: p + v t
    public Vector3 PredictPosition(float t) =>
        new Vector3(fx.PredictPosition(t), fy.PredictPosition(t), fz.PredictPosition(t));
}
