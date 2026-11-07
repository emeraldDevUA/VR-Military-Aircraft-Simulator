// Kalman filter for 1 axis: position and speed
// State s = [x, v], constant-velocity model, measurement z = x + noise.

public class Kalman1D
{
    public float x { get; private set; }
    public float v { get; private set; }
    float p00, p01, p11;   // covariance P = [[p00, p01], [p01, p11]] (symmetric)

    readonly float R;        // measurement noise variance (sigmaRadar^2)
    readonly float q;        // process noise: acceleration variance (sigmaAccel^2)
    readonly float maxSpeed;

    // Diagnostics (used by tests): uncertainty and last innovation
    public float PositionStd => (float)System.Math.Sqrt(p00);
    public float VelocityStd => (float)System.Math.Sqrt(p11);
    public float PosVelCov => p01;
    public float LastInnovation { get; private set; }      // y = z - x
    public float LastInnovationVar { get; private set; }   // S = p00 + R

    public Kalman1D(float sigmaRadar, float sigmaAccel, float maxSpeed)
    {
        if (!(sigmaRadar > 0f) || !(sigmaAccel >= 0f) || !(maxSpeed > 0f))
            throw new System.ArgumentException("bad params: sigmaRadar > 0, sigmaAccel >= 0, maxSpeed > 0");

        R = sigmaRadar * sigmaRadar;
        q = sigmaAccel * sigmaAccel;
        this.maxSpeed = maxSpeed;
    }

    // Start from the first measurement: position known up to radar noise,
    // velocity unknown -> std = maxSpeed.
    public void Init(float initialMeasurement)
    {
        x = initialMeasurement;  v = 0f;
        p00 = R;  p01 = 0f;  p11 = maxSpeed * maxSpeed;
    }

    // Prediction step: s = F s,  P = F P F^T + Q
    // F = [[1, dt], [0, 1]],  Q = q * [[dt^4/4, dt^3/2], [dt^3/2, dt^2]]
    public void Predict(float dt)
    {
        if (!(dt > 0f) || float.IsInfinity(dt)) return;   // ignore invalid time steps

        x += v * dt;

        float dt2 = dt * dt, dt3 = dt2 * dt, dt4 = dt2 * dt2;
        float n00 = p00 + 2f * dt * p01 + dt2 * p11 + q * dt4 / 4f;
        float n01 = p01 + dt * p11 + q * dt3 / 2f;
        float n11 = p11 + q * dt2;
        p00 = n00;  p01 = n01;  p11 = n11;
    }

    // Correction step with H = [1, 0]:
    // y = z - x,  S = p00 + R,  K = [p00, p01] / S
    // s += K y,   P = (I - K H) P
    public void Correct(float subsequentMeasurement)
    {
        float y = subsequentMeasurement - x;
        float s = p00 + R;
        LastInnovation = y;  LastInnovationVar = s;

        float k0 = p00 / s;
        float k1 = p01 / s;

        x += k0 * y;
        v += k1 * y;

        // use old p00, p01 on the right side (values before this update)
        float a = p00, b = p01;
        p00 -= k0 * a;  p01 -= k0 * b;  p11 -= k1 * b;
    }

    // Extrapolate position t seconds ahead (does not change the state)
    public float PredictPosition(float t) => x + v * t;
}
