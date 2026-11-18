using System;
using System.Collections.Generic;

public readonly struct Point
{
    public readonly double X;
    public readonly double Y;

    public Point(double x, double y)
    {
        X = x;
        Y = y;
    }

    public void Deconstruct(out double x, out double y) { x = X; y = Y; }
}

public class Airfoil
{
    private readonly List<Dictionary<string, object>> m_data;

    public double MaxCl { get; private set; }

    public Airfoil(List<Dictionary<string, object>> data)
    {
        if (data == null || data.Count == 0)
            throw new ArgumentException("Airfoil data must not be empty.", nameof(data));

        m_data = data;
        MaxCl = ComputeMaxCl();
    }

    private double ComputeMaxCl()
    {
        double max = double.NegativeInfinity;
        foreach (var row in m_data)
            max = Math.Max(max, Convert.ToDouble(row["cl"]));
        return max;
    }

    private static double Lerp(Dictionary<string, object> r0, Dictionary<string, object> r1, string key, double t)
    {
        double v0 = Convert.ToDouble(r0[key]);
        double v1 = Convert.ToDouble(r1[key]);
        return v0 + t * (v1 - v0);
    }

    /// <summary>
    /// alpha (degrees) -> Point(cl, cd). Clamps to the table's range.
    /// Assumes rows are sorted by ascending alpha.
    /// </summary>
    public Point SampleByAlpha(double alpha)
    {
        Dictionary<string, object> prev = null;

        foreach (var row in m_data)
        {
            double a1 = Convert.ToDouble(row["alpha"]);
            if (a1 >= alpha)
            {
                if (prev == null) // below the first row
                    return new Point(Convert.ToDouble(row["cl"]), Convert.ToDouble(row["cd"]));

                double a0 = Convert.ToDouble(prev["alpha"]);
                double t = Math.Abs(a1 - a0) < 1e-12 ? 0.0 : (alpha - a0) / (a1 - a0);
                return new Point(Lerp(prev, row, "cl", t), Lerp(prev, row, "cd", t));
            }
            prev = row;
        }

        // above the last row
        return new Point(Convert.ToDouble(prev["cl"]), Convert.ToDouble(prev["cd"]));
    }

    /// <summary>
    /// CL -> Point(alpha, cd). Returns the first bracket where CL crosses the target.
    /// </summary>
    public bool TrySampleByCl(double cL, out Point result)
    {
        result = default;
        Dictionary<string, object> prev = null;

        foreach (var row in m_data)
        {
            if (prev != null)
            {
                double cl0 = Convert.ToDouble(prev["cl"]);
                double cl1 = Convert.ToDouble(row["cl"]);

                if ((cl0 <= cL && cL <= cl1) || (cl1 <= cL && cL <= cl0))
                {
                    double t = Math.Abs(cl1 - cl0) < 1e-12 ? 0.0 : (cL - cl0) / (cl1 - cl0);
                    result = new Point(Lerp(prev, row, "alpha", t), Lerp(prev, row, "cd", t));
                    return true;
                }
            }
            prev = row;
        }

        return false;
    }
}