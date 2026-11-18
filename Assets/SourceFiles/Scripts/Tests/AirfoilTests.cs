using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

public class AirfoilTests
{
    const double Tol = 1e-9;
    static readonly string[] RequiredKeys = { "alpha", "cl", "cd" };
    static readonly string[] AirfoilNames = { "NACA64A204", "NACA2412", "NACA0012" };

    // ----------------------------- helpers ---------------------------------

    static List<Dictionary<string, object>> LoadRows(string name)
        => CSV_Utils.Read($"Airfoils/{name}");

    // Treats null, DBNull and empty/whitespace strings as "missing"
    static bool IsMissing(object v) =>
        v == null || v is DBNull || (v is string s && string.IsNullOrWhiteSpace(s));

    // Invariant parsing so the tests don't depend on the machine's locale
    static double D(object v) => Convert.ToDouble(v, CultureInfo.InvariantCulture);

    static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    static Dictionary<string, object> Row(double alpha, double cl, double cd)
        => new Dictionary<string, object> { { "alpha", alpha }, { "cl", cl }, { "cd", cd } };

    // ======================= 1. Raw data integrity =========================
    // Note: Convert.ToDouble(null) returns 0, so the Airfoil class would silently
    // turn nulls into zeros. That's why nulls are checked on the raw data here.

    [TestCaseSource(nameof(AirfoilNames))]
    public void Data_HasRequiredKeys_NoNulls(string name)
    {
        var rows = LoadRows(name);
        Assert.IsNotEmpty(rows, $"{name}: no rows loaded");

        for (int i = 0; i < rows.Count; i++)
            foreach (var key in RequiredKeys)
            {
                Assert.IsTrue(rows[i].ContainsKey(key), $"{name} row {i}: missing '{key}'");
                Assert.IsFalse(IsMissing(rows[i][key]), $"{name} row {i}: '{key}' is null/empty");
            }
    }

    [TestCaseSource(nameof(AirfoilNames))]
    public void Data_AllValuesParseAndAreFinite(string name)
    {
        var rows = LoadRows(name);
        for (int i = 0; i < rows.Count; i++)
            foreach (var key in RequiredKeys)
            {
                object raw = rows[i][key];
                if (IsMissing(raw)) continue; // reported by the test above

                double v;
                try
                {
                    v = D(raw);
                }
                catch (FormatException)
                {
                    Assert.Fail($"{name} row {i}: '{key}' = \"{raw}\" is not a number");
                    return;
                }

                Assert.IsFalse(double.IsNaN(v), $"{name} row {i}: '{key}' is NaN");
                Assert.IsFalse(double.IsInfinity(v), $"{name} row {i}: '{key}' is Infinity");
            }
    }

    [TestCaseSource(nameof(AirfoilNames))]
    public void Data_AlphaStrictlyAscending(string name)
    {
        var alphas = LoadRows(name).Select(r => D(r["alpha"])).ToList();
        for (int i = 1; i < alphas.Count; i++)
            Assert.Greater(alphas[i], alphas[i - 1], $"{name}: alpha not ascending at row {i}");
    }

    [TestCaseSource(nameof(AirfoilNames))]
    public void Data_CdPositive(string name)
    {
        var rows = LoadRows(name);
        for (int i = 0; i < rows.Count; i++)
            Assert.Greater(D(rows[i]["cd"]), 0.0, $"{name} row {i}: non-positive cd");
    }

    // Unity doesn't force InvariantCulture. If CSV_Utils returns strings, the Airfoil
    // class (which uses Convert.ToDouble with the current culture) would misread
    // "0.55" as 55 on a German system. This fails if that is happening.
    [TestCaseSource(nameof(AirfoilNames))]
    public void Loading_IsCultureIndependent(string name)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            double invariantMax = new Airfoil(LoadRows(name)).MaxCl;

            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            double germanMax = new Airfoil(LoadRows(name)).MaxCl;

            Assert.AreEqual(invariantMax, germanMax, Tol,
                $"{name}: MaxCl depends on culture. Parse with InvariantCulture in CSV_Utils.");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ===================== 2. Constructor / MaxCl ==========================

    [Test]
    public void Constructor_Null_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Airfoil(null));
    }

    [Test]
    public void Constructor_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Airfoil(new List<Dictionary<string, object>>()));
    }

    [TestCaseSource(nameof(AirfoilNames))]
    public void MaxCl_IsFiniteAndMatchesData(string name)
    {
        var rows = LoadRows(name);
        var foil = new Airfoil(rows);

        Assert.IsTrue(IsFinite(foil.MaxCl), $"{name}: MaxCl = {foil.MaxCl}");
        Assert.AreEqual(rows.Max(r => D(r["cl"])), foil.MaxCl, Tol);
    }

    // ======================== 3. SampleByAlpha =============================

    [TestCaseSource(nameof(AirfoilNames))]
    public void SampleByAlpha_AtTableRows_ReturnsExactValues(string name)
    {
        var rows = LoadRows(name);
        var foil = new Airfoil(rows);

        foreach (var r in rows)
        {
            var (cl, cd) = foil.SampleByAlpha(D(r["alpha"]));
            Assert.AreEqual(D(r["cl"]), cl, Tol, $"{name}: cl at alpha {r["alpha"]}");
            Assert.AreEqual(D(r["cd"]), cd, Tol, $"{name}: cd at alpha {r["alpha"]}");
        }
    }

    [TestCaseSource(nameof(AirfoilNames))]
    public void SampleByAlpha_Midpoints_AreAverageOfNeighbours(string name)
    {
        var rows = LoadRows(name);
        var foil = new Airfoil(rows);

        for (int i = 1; i < rows.Count; i++)
        {
            double a0 = D(rows[i - 1]["alpha"]);
            double a1 = D(rows[i]["alpha"]);
            var (cl, cd) = foil.SampleByAlpha((a0 + a1) / 2);

            Assert.AreEqual((D(rows[i - 1]["cl"]) + D(rows[i]["cl"])) / 2, cl, Tol, $"{name}: cl between rows {i - 1} and {i}");
            Assert.AreEqual((D(rows[i - 1]["cd"]) + D(rows[i]["cd"])) / 2, cd, Tol, $"{name}: cd between rows {i - 1} and {i}");
        }
    }

    [TestCaseSource(nameof(AirfoilNames))]
    public void SampleByAlpha_QuarterPoint_IsLinear(string name)
    {
        var rows = LoadRows(name);
        var foil = new Airfoil(rows);

        double a0 = D(rows[0]["alpha"]), a1 = D(rows[1]["alpha"]);
        double cl0 = D(rows[0]["cl"]), cl1 = D(rows[1]["cl"]);
        double cd0 = D(rows[0]["cd"]), cd1 = D(rows[1]["cd"]);

        var p = foil.SampleByAlpha(a0 + 0.25 * (a1 - a0));

        Assert.AreEqual(cl0 + 0.25 * (cl1 - cl0), p.X, Tol);
        Assert.AreEqual(cd0 + 0.25 * (cd1 - cd0), p.Y, Tol);
    }

    [TestCaseSource(nameof(AirfoilNames))]
    public void SampleByAlpha_ClampsOutsideRange(string name)
    {
        var rows = LoadRows(name);
        var foil = new Airfoil(rows);
        var first = rows.First();
        var last = rows.Last();

        var lo = foil.SampleByAlpha(-1e6);
        var hi = foil.SampleByAlpha(1e6);

        Assert.AreEqual(D(first["cl"]), lo.X, Tol);
        Assert.AreEqual(D(first["cd"]), lo.Y, Tol);
        Assert.AreEqual(D(last["cl"]), hi.X, Tol);
        Assert.AreEqual(D(last["cd"]), hi.Y, Tol);
    }

    [TestCaseSource(nameof(AirfoilNames))]
    public void SampleByAlpha_FineSweep_AlwaysFiniteAndWithinTableBounds(string name)
    {
        var rows = LoadRows(name);
        var foil = new Airfoil(rows);

        double minCl = rows.Min(r => D(r["cl"])), maxCl = rows.Max(r => D(r["cl"]));
        double minCd = rows.Min(r => D(r["cd"])), maxCd = rows.Max(r => D(r["cd"]));

        // integer loop avoids floating-point drift: alpha from -20 to +25 in 0.05 steps
        for (int i = 0; i <= 900; i++)
        {
            double a = -20.0 + i * 0.05;
            var (cl, cd) = foil.SampleByAlpha(a);

            Assert.IsTrue(IsFinite(cl) && IsFinite(cd), $"{name}: non-finite at alpha={a}");
            Assert.That(cl, Is.InRange(minCl - Tol, maxCl + Tol), $"{name}: cl out of bounds at alpha={a}");
            Assert.That(cd, Is.InRange(minCd - Tol, maxCd + Tol), $"{name}: cd out of bounds at alpha={a}");
        }
    }

    [Test]
    public void SampleByAlpha_SingleRow_ReturnsThatRow()
    {
        var foil = new Airfoil(new List<Dictionary<string, object>> { Row(0.0, 0.3, 0.01) });

        var (cl, cd) = foil.SampleByAlpha(5.0);

        Assert.AreEqual(0.3, cl, Tol);
        Assert.AreEqual(0.01, cd, Tol);
    }

    // ======================== 4. TrySampleByCl =============================

    [TestCaseSource(nameof(AirfoilNames))]
    public void TrySampleByCl_AtTableRows_ReturnsTrueAndRoundTrips(string name)
    {
        var rows = LoadRows(name);
        var foil = new Airfoil(rows);

        foreach (var r in rows)
        {
            double cl = D(r["cl"]);
            Assert.IsTrue(foil.TrySampleByCl(cl, out var p), $"{name}: no bracket for cl={cl}");
            Assert.AreEqual(cl, foil.SampleByAlpha(p.X).X, 1e-6, $"{name}: round trip failed for cl={cl}");
        }
    }

    [TestCaseSource(nameof(AirfoilNames))]
    public void TrySampleByCl_RoundTripWithSampleByAlpha(string name)
    {
        var rows = LoadRows(name);
        var foil = new Airfoil(rows);

        for (int i = 1; i < rows.Count; i++)
        {
            double cl = (D(rows[i - 1]["cl"]) + D(rows[i]["cl"])) / 2;

            Assert.IsTrue(foil.TrySampleByCl(cl, out var p), $"{name}: no bracket for cl={cl}");
            Assert.IsTrue(IsFinite(p.X) && IsFinite(p.Y), $"{name}: non-finite result for cl={cl}");

            var back = foil.SampleByAlpha(p.X);
            Assert.AreEqual(cl, back.X, 1e-6, $"{name}: CL does not round-trip");
            Assert.AreEqual(p.Y, back.Y, 1e-6, $"{name}: CD does not match");
        }
    }

    [TestCaseSource(nameof(AirfoilNames))]
    public void TrySampleByCl_OutOfRange_ReturnsFalse(string name)
    {
        var rows = LoadRows(name);
        var foil = new Airfoil(rows);
        double minCl = rows.Min(r => D(r["cl"]));

        Assert.IsFalse(foil.TrySampleByCl(foil.MaxCl + 1.0, out var above));
        Assert.AreEqual(default(Point), above);

        Assert.IsFalse(foil.TrySampleByCl(minCl - 1.0, out var below));
        Assert.AreEqual(default(Point), below);
    }

    [TestCaseSource(nameof(AirfoilNames))]
    public void TrySampleByCl_NaN_ReturnsFalse(string name)
    {
        var foil = new Airfoil(LoadRows(name));
        Assert.IsFalse(foil.TrySampleByCl(double.NaN, out _));
    }

    // ============ 5. Bad input (documents CURRENT behaviour) ===============

    [Test]
    public void MaxCl_WithNaNInData_IsNaN_ConstructorDoesNotValidate()
    {
        var rows = new List<Dictionary<string, object>>
        {
            Row(0.0, 0.1, 0.01),
            Row(1.0, double.NaN, 0.01),
        };

        // Math.Max propagates NaN. Consider validating in the constructor and
        // replacing this test with Assert.Throws.
        Assert.IsTrue(double.IsNaN(new Airfoil(rows).MaxCl));
    }

    [Test]
    public void SampleByAlpha_NaN_ReturnsLastRow()
    {
        var foil = new Airfoil(new List<Dictionary<string, object>>
        {
            Row(0.0, 0.0, 0.01),
            Row(10.0, 1.0, 0.02),
        });

        // Every comparison with NaN is false, so the loop falls through to the
        // "above the last row" branch.
        var (cl, cd) = foil.SampleByAlpha(double.NaN);

        Assert.AreEqual(1.0, cl, Tol);
        Assert.AreEqual(0.02, cd, Tol);
    }

    [Test]
    public void MissingKey_ThrowsKeyNotFound()
    {
        var rows = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object> { { "alpha", 0.0 }, { "cl", 0.1 } } // no "cd"
        };

        var foil = new Airfoil(rows);

        Assert.Throws<KeyNotFoundException>(() => foil.SampleByAlpha(0.0));
    }
}
