using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public class AirfoilTests
{
    const double Tol = 1e-9;
    static readonly string[] RequiredKeys = { "alpha", "cl", "cd" };
    static readonly string[] AirfoilNames = { "NACA64A204", "NACA2412", "NACA0012" };

    static List<Dictionary<string, object>> LoadRows(string name)
        => CSV_Utils.Read($"Airfoils/{name}");

    static bool IsMissing(object v) =>
        v == null || v is DBNull || (v is string s && string.IsNullOrWhiteSpace(s));

    [TestCaseSource(nameof(AirfoilNames))]
    public void Data_HasRequiredKeys_NoNulls(string name)
    {
        var rows = LoadRows(name);
        Assert.IsNotEmpty(rows);
        for (int i = 0; i < rows.Count; i++)
            foreach (var key in RequiredKeys)
            {
                Assert.IsTrue(rows[i].ContainsKey(key), $"{name} row {i}: missing '{key}'");
                Assert.IsFalse(IsMissing(rows[i][key]), $"{name} row {i}: '{key}' null/empty");
            }
    }

    [TestCaseSource(nameof(AirfoilNames))]
    public void SampleByAlpha_AtTableRows_ReturnsExactValues(string name)
    {
        var rows = LoadRows(name);
        var foil = new Airfoil(rows);
        foreach (var r in rows)
        {
            var (cl, cd) = foil.SampleByAlpha(Convert.ToDouble(r["alpha"]));
            Assert.AreEqual(Convert.ToDouble(r["cl"]), cl, Tol);
            Assert.AreEqual(Convert.ToDouble(r["cd"]), cd, Tol);
        }
    }

    // ...convert the remaining tests the same way
}