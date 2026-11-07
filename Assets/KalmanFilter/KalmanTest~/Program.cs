// Filter tests. Run: dotnet run, then plots: python3 plot_results.py
//   A - correctness (CorrectnessTests.cs)
//   B - noisy scenarios and metrics, D - sigma_a sweep, CSV for plots (ScenarioTests.cs)
using System;

// Chosen filter settings, shared by all tests
static class Settings
{
    public const float SigmaRadar = 5f;    // radar noise std: used both to generate noise and in the filter
    public const float SigmaAccel = 50f;   // chosen by the sweep (block D)
    public const float MaxSpeed = 400f;    // initial velocity uncertainty
}

class Program
{
    static int Main()
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;   // '.' as decimal separator in CSV
        CorrectnessTests.Run();
        ScenarioTests.Run();
        Console.WriteLine(T.Failed == 0 ? "\nAll checks passed" : $"\nFailed checks: {T.Failed}");
        return T.Failed == 0 ? 0 : 1;
    }
}

// Shared helpers
static class T
{
    public static int Failed;

    public static void Check(string name, bool ok, string info = "")
    {
        Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {name}  {info}");
        if (!ok) Failed++;
    }

    // Gaussian noise N(0, 1), Box-Muller
    public static float Gauss(Random r) =>
        (float)(Math.Sqrt(-2 * Math.Log(1 - r.NextDouble())) * Math.Cos(2 * Math.PI * r.NextDouble()));
}
