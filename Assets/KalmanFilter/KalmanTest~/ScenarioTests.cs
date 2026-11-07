// B + C. Trajectory scenarios with Gaussian radar noise, metrics averaged over Monte Carlo runs.
// D. sigma_a sweep (console table only). Scenario results are also written to CSV (output/) for plots: python3 plot_results.py
using System;
using System.IO;
using UnityEngine;
using static Settings;

static class ScenarioTests
{
    const string OutDir = "output";
    const float Dt = 0.05f, Duration = 30f, Warmup = 3f, Lead = 1f;
    const int Runs = 50;

    // True trajectory: time t -> (position, velocity)
    delegate (Vector3 p, Vector3 v) Trajectory(double t);

    class Scenario
    {
        public string Name, Slug;             // Slug = name used in file names
        public Func<Random, Trajectory> Make; // Random is used only by the random trajectory
        public bool Gaps, Jitter;             // 5 missed steps in a row, dt jitter +-30%
        public float TurnStart, TurnEnd;      // for the recovery-after-maneuver report
        public bool CheckCalibration;         // calibration is checked only where the model is exact
    }

    class Result { public double Radar, Filt, Vel, Base, Lead, Cover, Nis; public bool Finite; public double[] ErrAt; }

    static readonly float[] SweepValues = { 1f, 2f, 5f, 10f, 20f, 50f, 100f, 200f };

    public static void Run()
    {
        const float V = 250f;
        var scenarios = new[]
        {
            new Scenario { Name = "Random maneuvers (exact model)", Slug = "random", Make = RandomAccel, CheckCalibration = true },
            new Scenario { Name = "Straight + gaps + dt jitter", Slug = "straight", Gaps = true, Jitter = true,
                           Make = _ => t => (new Vector3(0, 1000, 0) + new Vector3(V, 0, 0) * (float)t, new Vector3(V, 0, 0)) },
            new Scenario { Name = "Straight -> sharp 8g 90deg turn", Slug = "turn", TurnStart = 10f, TurnEnd = 10f + Turn90Duration(V, 80f),
                           Make = _ => t => Turn90(t, V, 80f, 10f) },
            new Scenario { Name = "Constant 3g turn", Slug = "circle", Make = _ => t => Circle(t, V, 30f, 1000f) },
            new Scenario { Name = "Snake + altitude change", Slug = "snake", Make = _ => t => Snake(t, V) },
        };

        Directory.CreateDirectory(OutDir);
        File.WriteAllText(Path.Combine(OutDir, "params.csv"),            // settings, for plot labels
            $"sigma_radar,sigma_a,warmup,lead,runs\n{SigmaRadar},{SigmaAccel},{Warmup},{Lead},{Runs}\n");
        Report(scenarios, SigmaRadar);
        Sweep(scenarios, SigmaRadar);
        foreach (var sc in scenarios)                                   // one step-by-step run per scenario, for plots
            using (var w = new StreamWriter(Path.Combine(OutDir, $"trace_{sc.Slug}.csv"))) RunScenario(sc, SigmaRadar, SigmaAccel, 1, w);
        Console.WriteLine($"\n  CSV written to {Path.GetFullPath(OutDir)}");
    }

    // B. Scenario table at sigma_a = SigmaAccel + checks
    static void Report(Scenario[] scenarios, float sigma)
    {
        Console.WriteLine($"\nB. Scenarios (radar sigma {sigma} m, sigma_a {SigmaAccel}, {Runs} runs of {Duration} s, metrics after {Warmup} s)");
        using var csv = new StreamWriter(Path.Combine(OutDir, "summary.csv"));
        csv.WriteLine("slug,scenario,rmse_radar,rmse_filter,rmse_vel,lead_baseline,lead_filter,inside_2sigma,nis");
        Console.WriteLine($"  {"",-34} {"pos RMSE: radar",16} {"filter",7} {"vel RMSE",9} {"lead 1s: last meas",21} {"filter",7} {"in 2s",6} {"y2/S",5}");
        foreach (var sc in scenarios)
        {
            var r = RunScenario(sc, sigma, SigmaAccel);
            Console.WriteLine($"  {sc.Name,-34} {r.Radar,16:F1} {r.Filt,7:F1} {r.Vel,9:F1} {r.Base,21:F1} {r.Lead,7:F1} {r.Cover * 100,5:F0}% {r.Nis,5:F2}");
            csv.WriteLine($"{sc.Slug},{sc.Name},{r.Radar:F3},{r.Filt:F3},{r.Vel:F3},{r.Base:F3},{r.Lead:F3},{r.Cover:F4},{r.Nis:F4}");
            if (sc.TurnEnd > 0) ReportRecovery(sc, r.ErrAt);
            T.Check("filter beats radar, no NaN", r.Finite && r.Filt < r.Radar);
            T.Check("lead prediction beats aiming at the last measurement", r.Lead < r.Base);
            if (sc.CheckCalibration)
                T.Check("calibration: inside 2 sigma ~ 95%, y^2/S ~ 1", r.Cover > 0.93 && r.Cover < 0.97 && r.Nis > 0.9 && r.Nis < 1.1);
        }
    }

    // D. sigma_a sweep: per scenario position error, lead error and share inside 2 sigma. '*' = row minimum.
    static void Sweep(Scenario[] scenarios, float sigma)
    {
        var res = new Result[scenarios.Length, SweepValues.Length];
        for (int i = 0; i < scenarios.Length; i++)
            for (int j = 0; j < SweepValues.Length; j++) res[i, j] = RunScenario(scenarios[i], sigma, SweepValues[j]);

        Console.WriteLine($"\nD. sigma_a sweep (radar sigma {sigma} m, {Runs} runs)");
        PrintTable("position RMSE, m", scenarios, res, r => r.Filt, true);
        PrintTable("1 s lead RMSE, m", scenarios, res, r => r.Lead, true);
        PrintTable("inside 2 sigma, % (expect ~95)", scenarios, res, r => r.Cover * 100, false);
    }

    static void PrintTable(string title, Scenario[] sc, Result[,] res, Func<Result, double> metric, bool markMin)
    {
        Console.Write($"  {title,-34}");
        foreach (var v in SweepValues) Console.Write($"{"s_a=" + v,10}");
        Console.WriteLine();
        for (int i = 0; i < sc.Length; i++)
        {
            int best = 0;
            for (int j = 1; j < SweepValues.Length; j++) if (metric(res[i, j]) < metric(res[i, best])) best = j;
            Console.Write($"  {sc[i].Name,-34}");
            for (int j = 0; j < SweepValues.Length; j++)
                Console.Write($"{metric(res[i, j]),9:F1}{(markMin && j == best ? "*" : " ")}");
            Console.WriteLine();
        }
    }

    // Runs a scenario `runs` times and averages the metrics.
    // trace != null: every step (truth, measurement, estimate, velocities, PositionStd) is written to CSV
    static Result RunScenario(Scenario sc, float sigma, float sigmaAccel, int runs = Runs, TextWriter trace = null)
    {
        trace?.WriteLine("t,true_x,true_y,true_z,meas_x,meas_y,meas_z,est_x,est_y,est_z," +
                         "true_vx,true_vy,true_vz,est_vx,est_vy,est_vz,std_x,std_y,std_z");
        double radarSq = 0, filtSq = 0, velSq = 0, leadBaseSq = 0, leadFiltSq = 0, nis = 0;
        long n = 0, inside = 0;
        bool finite = true;
        int steps = (int)(Duration / Dt) + 1;
        var errAt = new double[steps];   // mean error per step (for recovery after the turn)

        for (int run = 0; run < runs; run++)
        {
            var rng = new Random(run + 1);
            Trajectory truth = sc.Make(rng);
            var kf = new KalmanFilter3D(sigma, sigmaAccel, MaxSpeed);
            double t = 0, lastT = 0;

            for (int i = 0; i < steps && t < Duration; i++, t += sc.Jitter ? Dt * (0.7 + 0.6 * rng.NextDouble()) : Dt)
            {
                float dt = (float)(t - lastT);
                lastT = t;
                var (p, v) = truth(t);
                bool gap = sc.Gaps && i % 100 >= 50 && i % 100 < 55;          // 5 steps in a row without radar
                Vector3 z = default;
                if (gap) kf.PredictOnly(dt);
                else { z = p + new Vector3(T.Gauss(rng), T.Gauss(rng), T.Gauss(rng)) * sigma; kf.Update(z, dt); }
                trace?.WriteLine($"{t:F4},{V3(p)},{(gap ? ",," : V3(z))},{V3(kf.Position)},{V3(v)},{V3(kf.Velocity)}," +
                                 $"{kf.X.PositionStd:F4},{kf.Y.PositionStd:F4},{kf.Z.PositionStd:F4}");
                if (gap) continue;
                Vector3 e = kf.Position - p;
                errAt[i] += e.magnitude / runs;
                finite &= float.IsFinite(e.magnitude) && float.IsFinite(kf.Velocity.magnitude);
                if (t < Warmup) continue;

                Vector3 future = truth(t + Lead).p;
                radarSq += Sq((z - p).magnitude);
                filtSq += Sq(e.magnitude);
                velSq += Sq((kf.Velocity - v).magnitude);
                leadBaseSq += Sq((z - future).magnitude);                      // baseline: aim at the last measurement
                leadFiltSq += Sq((kf.PredictPosition(Lead) - future).magnitude);
                foreach (var a in new[] { kf.X, kf.Y, kf.Z })
                    nis += a.LastInnovation * a.LastInnovation / a.LastInnovationVar;   // NIS = y^2 / S, mean ~ 1 if consistent
                inside += (Math.Abs(e.x) < 2 * kf.X.PositionStd ? 1 : 0) + (Math.Abs(e.y) < 2 * kf.Y.PositionStd ? 1 : 0)
                        + (Math.Abs(e.z) < 2 * kf.Z.PositionStd ? 1 : 0);
                n++;
            }
        }

        return new Result
        {
            Radar = Math.Sqrt(radarSq / n), Filt = Math.Sqrt(filtSq / n), Vel = Math.Sqrt(velSq / n),
            Base = Math.Sqrt(leadBaseSq / n), Lead = Math.Sqrt(leadFiltSq / n),
            Cover = inside / (3.0 * n), Nis = nis / (3.0 * n), Finite = finite, ErrAt = errAt,
        };
    }

    // Lag and recovery: peak mean error during the maneuver, and how many seconds after it
    // the error returns to the straight-flight level (+20%)
    static void ReportRecovery(Scenario sc, double[] errAt)
    {
        int s = (int)(sc.TurnStart / Dt), e = (int)(sc.TurnEnd / Dt), w = (int)(Warmup / Dt);
        double before = 0; for (int i = w; i < s; i++) before += errAt[i] / (s - w);
        double peak = 0; for (int i = s; i < errAt.Length; i++) peak = Math.Max(peak, errAt[i]);
        int r = e; while (r < errAt.Length && errAt[r] > 1.2 * before) r++;
        string rec = r < errAt.Length ? $"{(r - e) * Dt:F2} s" : "not recovered";
        Console.WriteLine($"  {"",-34} error before maneuver {before:F1} m, peak {peak:F1} m, recovery {rec} after exit");
    }

    // ---------- trajectories (horizontal X-Z, altitude Y, as in Unity) ----------

    // Exactly the filter's model: random acceleration N(0, SigmaAccel^2), constant within each step.
    // Uses the chosen SigmaAccel, so in block B model = truth (calibration check),
    // and in sweep D the minimum should land exactly on it.
    static Trajectory RandomAccel(Random rng)
    {
        int m = (int)((Duration + Lead) / Dt) + 2;
        var P = new Vector3[m]; var Vel = new Vector3[m]; var A = new Vector3[m];
        P[0] = new Vector3(0, 1000, 0); Vel[0] = new Vector3(250, 0, 0);
        for (int k = 0; k < m; k++)
        {
            A[k] = new Vector3(T.Gauss(rng), T.Gauss(rng), T.Gauss(rng)) * SigmaAccel;
            if (k + 1 < m) { P[k + 1] = P[k] + Vel[k] * Dt + A[k] * (Dt * Dt / 2); Vel[k + 1] = Vel[k] + A[k] * Dt; }
        }
        return t =>
        {
            int k = Math.Min((int)Math.Round(t / Dt, 6), m - 1); float tau = (float)(t - k * Dt);
            return (P[k] + Vel[k] * tau + A[k] * (tau * tau / 2), Vel[k] + A[k] * tau);
        };
    }

    // Circular turn with centripetal acceleration acc: r = v^2 / acc, w = v / r
    static (Vector3, Vector3) Circle(double t, float speed, float acc, float height)
    {
        double r = speed * speed / acc, w = speed / r;
        return (new Vector3((float)(r * Math.Sin(w * t)), height, (float)(r * (1 - Math.Cos(w * t)))),
                new Vector3((float)(speed * Math.Cos(w * t)), 0, (float)(speed * Math.Sin(w * t))));
    }

    static float Turn90Duration(float speed, float acc) => (float)(Math.PI / 2 * speed / acc);

    // Straight along X until t0, 90deg turn with acceleration acc, then straight along Z
    static (Vector3, Vector3) Turn90(double t, float speed, float acc, float t0)
    {
        float dur = Turn90Duration(speed, acc), r = speed * speed / acc;
        Vector3 start = new Vector3(0, 1000, 0);
        if (t < t0) return (start + new Vector3(speed, 0, 0) * (float)t, new Vector3(speed, 0, 0));
        Vector3 turnStart = start + new Vector3(speed * t0, 0, 0);
        if (t < t0 + dur) { var (p, v) = Circle(t - t0, speed, acc, 0); return (turnStart + p, v); }
        Vector3 turnEnd = turnStart + new Vector3(r, 0, r);
        return (turnEnd + new Vector3(0, 0, speed) * (float)(t - t0 - dur), new Vector3(0, 0, speed));
    }

    // Forward along X, snake along Z (+-100 m, period 10 s, ~4g), altitude Y oscillates (+-150 m, period 15 s)
    static (Vector3, Vector3) Snake(double t, float speed)
    {
        double w1 = 2 * Math.PI / 10, w2 = 2 * Math.PI / 15;
        return (new Vector3(speed * (float)t, (float)(1000 + 150 * Math.Sin(w2 * t)), (float)(100 * Math.Sin(w1 * t))),
                new Vector3(speed, (float)(150 * w2 * Math.Cos(w2 * t)), (float)(100 * w1 * Math.Cos(w1 * t))));
    }

    static double Sq(double a) => a * a;
    static string V3(Vector3 a) => $"{a.x:F4},{a.y:F4},{a.z:F4}";
}
