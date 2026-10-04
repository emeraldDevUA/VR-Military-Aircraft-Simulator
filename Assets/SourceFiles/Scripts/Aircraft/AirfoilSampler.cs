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
}

public class Airfoil
{
    private readonly List<Dictionary<string, object>> m_data;

    public Airfoil(List<Dictionary<string, object>> data)
    {
        m_data = data;
    }

    /// <summary>
    /// Finds the first bracket in the polar where CL crosses the target
    /// and linearly interpolates alpha and CD. Returns (alpha, cd).
    /// </summary>
    public bool TrySample(double cL, out Point result)
    {
        result = default;
        Dictionary<string, object> prev = null;

        foreach (var row in m_data)
        {
            if (prev != null)
            {
                double cl0 = Convert.ToDouble(prev["cl"]);
                double cl1 = Convert.ToDouble(row["cl"]);

                bool bracketed = (cl0 <= cL && cL <= cl1) || (cl1 <= cL && cL <= cl0);
                if (bracketed)
                {
                    double a0 = Convert.ToDouble(prev["alpha"]);
                    double a1 = Convert.ToDouble(row["alpha"]);
                    double cd0 = Convert.ToDouble(prev["cd"]);
                    double cd1 = Convert.ToDouble(row["cd"]);

                    // Avoid divide-by-zero on a flat segment
                    double t = Math.Abs(cl1 - cl0) < 1e-12 ? 0.0 : (cL - cl0) / (cl1 - cl0);

                    result = new Point(a0 + t * (a1 - a0), cd0 + t * (cd1 - cd0));
                    return true;
                }
            }
            prev = row;
        }

        return false; // cL is outside the range of the data
    }
}