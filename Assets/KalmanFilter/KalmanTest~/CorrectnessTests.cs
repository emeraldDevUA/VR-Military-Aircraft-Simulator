// A. Correctness checks: small deterministic cases, each checks one thing.
// Default settings from Settings; A1 and A3 use special values (see comments).
using System;
using UnityEngine;
using static Settings;

static class CorrectnessTests
{
    const float Dt = 0.05f;

    public static void Run()
    {
        Console.WriteLine("A. Correctness");
        Stationary();
        ConstantVelocityAndAxes();
        Start();
        BadInput();
        Gaps();
        LongRun();
    }

    // A1. Stationary target in noise: x -> 0, v -> 0, uncertainty decreases.
    //     sigma_a = 1: filter knows the target barely accelerates, so it can average for long
    static void Stationary()
    {
        var f = new Kalman1D(5f, 1f, MaxSpeed);
        var rng = new Random(1);
        f.Init(5f * T.Gauss(rng));
        float std0 = f.PositionStd;
        for (int i = 0; i < 400; i++) { f.Predict(Dt); f.Correct(5f * T.Gauss(rng)); }

        T.Check("A1 stationary target",
            Math.Abs(f.x) < 3 * f.PositionStd && Math.Abs(f.v) < 3 * f.VelocityStd && f.PositionStd < std0 / 2,
            $"x={f.x:F2} v={f.v:F2}, position std {std0:F1} -> {f.PositionStd:F2}");
    }

    // A2. Constant velocity without noise, motion along X only: Y and Z must not change
    static void ConstantVelocityAndAxes()
    {
        var kf = new KalmanFilter3D(SigmaRadar, SigmaAccel, MaxSpeed);
        Vector3 p0 = new Vector3(0, 1000, -300), vel = new Vector3(100, 0, 0);
        for (int i = 0; i <= 200; i++) kf.Update(p0 + vel * (i * Dt), Dt);

        float posErr = (kf.Position - (p0 + vel * (200 * Dt))).magnitude;
        T.Check("A2 constant velocity, no noise", posErr < 0.1f && Math.Abs(kf.Velocity.x - 100f) < 0.1f,
            $"position error {posErr:F4} m, v={kf.Velocity}");
        T.Check("A2 axes are independent", kf.Position.y == 1000f && kf.Position.z == -300f && kf.Velocity.y == 0f && kf.Velocity.z == 0f,
            $"Y,Z = {kf.Position.y}, {kf.Position.z}");
    }

    // A3. Start: first Update = the measurement itself, second gives v ~ (z1 - z0)/dt.
    //     sigma_radar = 1: the formula is exact only when noise << dt * maxSpeed
    static void Start()
    {
        var kf = new KalmanFilter3D(1f, SigmaAccel, MaxSpeed);
        Vector3 z0 = new Vector3(10, 20, 30), z1 = new Vector3(15, 18, 30);

        kf.Update(z0, 0.123f);   // dt of the first call has no effect
        T.Check("A3 first Update = z0", (kf.Position - z0).magnitude == 0f && kf.Velocity.magnitude == 0f);

        kf.Update(z1, Dt);
        Vector3 expected = (z1 - z0) * (1f / Dt);
        float relErr = (kf.Velocity - expected).magnitude / expected.magnitude;
        T.Check("A3 second Update: v ~ (z1-z0)/dt", relErr < 0.02f, $"v={kf.Velocity}, expected {expected}");
    }

    // A4. Bad input: constructor parameters and invalid dt / measurements
    static void BadInput()
    {
        bool Throws(Action a) { try { a(); return false; } catch (ArgumentException) { return true; } }
        T.Check("A4 bad parameters -> exception",
            Throws(() => new Kalman1D(0, 1, 1)) && Throws(() => new Kalman1D(-1, 1, 1)) && Throws(() => new Kalman1D(float.NaN, 1, 1)) &&
            Throws(() => new Kalman1D(1, -1, 1)) && Throws(() => new Kalman1D(1, 1, 0)) && !Throws(() => new Kalman1D(1, 0, 1)));

        var kf = new KalmanFilter3D(SigmaRadar, SigmaAccel, MaxSpeed);
        kf.Update(new Vector3(0, 0, 0), Dt);
        kf.Update(new Vector3(5, 0, 0), Dt);
        Vector3 p = kf.Position, v = kf.Velocity;
        float std = kf.X.PositionStd;

        foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) kf.PredictOnly(bad);
        T.Check("A4 PredictOnly with dt = 0, <0, NaN, Inf changes nothing",
            (kf.Position - p).magnitude == 0f && (kf.Velocity - v).magnitude == 0f && kf.X.PositionStd == std);

        kf.Update(new Vector3(6, 0, 0), 0f);
        kf.Update(new Vector3(6, 0, 0), -1f);
        T.Check("A4 Update with dt = 0 and dt < 0: correction only, no NaN",
            Finite(kf.Position) && Finite(kf.Velocity) && kf.X.PositionStd < std, $"position {kf.Position}");

        // invalid measurement = missed step: position moves by inertia, uncertainty grows
        var g = new KalmanFilter3D(SigmaRadar, SigmaAccel, MaxSpeed);
        g.Update(new Vector3(float.NaN, 0, 0), Dt);                       // ignored before initialization
        bool notInit = !g.IsInitialized;
        g.Update(new Vector3(0, 0, 0), Dt);
        g.Update(new Vector3(5, 0, 0), Dt);
        Vector3 expect = g.Position + g.Velocity * Dt;
        float std2 = g.X.PositionStd;
        g.Update(new Vector3(1, float.NaN, 1), Dt);
        g.Update(new Vector3(float.PositiveInfinity, 1, 1), 0f);         // dt = 0: nothing changes at all
        T.Check("A4 NaN/Inf in measurement -> treated as missed step",
            notInit && (g.Position - expect).magnitude < 1e-4f && g.X.PositionStd > std2 && Finite(g.Velocity), $"position {g.Position}");
    }

    // A5. 5 missed steps: position follows inertia, uncertainty grows, no jump when radar returns
    static void Gaps()
    {
        var kf = new KalmanFilter3D(SigmaRadar, SigmaAccel, MaxSpeed);
        Vector3 vel = new Vector3(200, 0, -50);
        Vector3 Truth(int i) => vel * (i * Dt);
        for (int i = 0; i <= 100; i++) kf.Update(Truth(i), Dt);   // warm-up without noise

        bool inertia = true, grows = true;
        float prevStd = kf.X.PositionStd;
        for (int i = 101; i <= 105; i++)
        {
            kf.PredictOnly(Dt);
            inertia &= (kf.Position - Truth(i)).magnitude < 0.1f;
            grows &= kf.X.PositionStd > prevStd;
            prevStd = kf.X.PositionStd;
        }
        T.Check("A5 gaps: motion by inertia", inertia);
        T.Check("A5 gaps: PositionStd grows", grows);

        kf.Update(Truth(106), Dt);
        float err = (kf.Position - Truth(106)).magnitude;
        T.Check("A5 after gap: no jump, no NaN", Finite(kf.Position) && err < 0.1f, $"error {err:F4} m");
    }

    // A6. Long run of 1e5 steps (dt jitter, gaps): P stays positive definite.
    //     P does not depend on measurements, only on the dt sequence, so a stationary target is enough.
    static void LongRun()
    {
        var f = new Kalman1D(SigmaRadar, SigmaAccel, MaxSpeed);
        var rng = new Random(2);
        f.Init(0f);
        bool ok = true;
        double worstCorr = 0;   // p01^2 / (p00 * p11), must be <= 1
        for (int i = 0; i < 100_000 && ok; i++)
        {
            f.Predict(0.01f + 0.09f * (float)rng.NextDouble());            // dt from 10 to 100 ms
            if (rng.NextDouble() > 0.05) f.Correct(SigmaRadar * T.Gauss(rng)); // 5% of steps missed

            // if p00 or p11 < 0, Std = NaN and the check below fails
            double p00 = (double)f.PositionStd * f.PositionStd, p11 = (double)f.VelocityStd * f.VelocityStd, p01 = f.PosVelCov;
            double corr = p01 * p01 / (p00 * p11);
            worstCorr = Math.Max(worstCorr, corr);
            ok = p00 > 0 && p11 > 0 && corr <= 1 && float.IsFinite(f.x) && float.IsFinite(f.v);
        }
        T.Check("A6 1e5 steps: p00 > 0, p11 > 0, p00*p11 >= p01^2, no NaN", ok, $"max p01^2/(p00*p11) = {worstCorr:F4}");
    }

    static bool Finite(Vector3 a) => float.IsFinite(a.x) && float.IsFinite(a.y) && float.IsFinite(a.z);
}
