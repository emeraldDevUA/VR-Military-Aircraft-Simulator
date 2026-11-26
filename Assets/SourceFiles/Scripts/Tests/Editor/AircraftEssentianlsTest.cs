using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ---------------------------------------------------------------------------
// Engine
// ---------------------------------------------------------------------------
public class EngineTests
{
    const double Tol = 1e-9;

    // NOTE: the current convention is that thrust is NEGATIVE (-throttle * maxThrust).
    [TestCase(100000, 1.0, -100000)]
    [TestCase(100000, 0.5, -50000)]
    [TestCase(80000, 0.25, -20000)]
    [TestCase(100000, 0.0, 0)]
    public void PropulsionForce_IsMinusThrottleTimesMaxThrust(double maxThrust, double throttle, double expected)
    {
        var engine = new Engine(maxThrust, throttle);
        Assert.AreEqual(expected, engine.computePropulsionForce(), Tol);
    }

    [Test]
    public void NewEngine_AfterburnerIsOff()
    {
        Assert.IsFalse(new Engine(1000, 1).enableAfterburner);
    }

    [Test]
    public void Afterburner_DoublesForce()
    {
        var engine = new Engine(100000, 0.6);
        double normal = engine.computePropulsionForce();

        engine.enableAfterburner = true;

        Assert.AreEqual(2.0 * normal, engine.computePropulsionForce(), Tol);
    }

    [Test]
    public void ChangingThrottle_IsReflectedInForce()
    {
        var engine = new Engine(50000, 0.2);
        Assert.AreEqual(-10000, engine.computePropulsionForce(), Tol);

        engine.innerThrottle = 0.8;
        Assert.AreEqual(-40000, engine.computePropulsionForce(), Tol);
    }

    [Test]
    public void ForceMagnitude_IsMonotonicInThrottle()
    {
        var engine = new Engine(100000, 0.0);
        double previous = 0;

        for (double t = 0.1; t <= 1.0 + 1e-9; t += 0.1)
        {
            engine.innerThrottle = t;
            double magnitude = System.Math.Abs(engine.computePropulsionForce());
            Assert.Greater(magnitude, previous, $"throttle {t}");
            previous = magnitude;
        }
    }

    [Test]
    public void ForceIsFinite()
    {
        var engine = new Engine(100000, 1.0) { enableAfterburner = true };
        double f = engine.computePropulsionForce();
        Assert.IsFalse(double.IsNaN(f) || double.IsInfinity(f));
    }
}

// ---------------------------------------------------------------------------
// Wing
//
// Uses a synthetic, perfectly linear airfoil so that expected forces can be
// computed by hand:   cl = 0.1 * alpha[deg],   cd = 0.01,   table range +-20 deg
//
// Geometry: horizontal wing, normal = +Y, chord axis = +X (aircraft forward),
// so a wing flying along +X with its flow coming from slightly below has
// positive AoA. Local velocity for a given AoA is (cos a, -sin a, 0) * speed.
// ---------------------------------------------------------------------------
public class WingTests
{
    const float ClPerDeg = 0.1f;
    const float Cd0 = 0.01f;
    const float TableLimit = 20f;     // table covers -20..+20 deg
    const float Rho = 1.225f;         // hard-coded in Wing.ComputeForce
    const float Area = 10f;
    const float Span = 5f;            // aspect ratio = 25 / 10 = 2.5
    const float Speed = 50f;
    const float AoaTol = 0.01f;       // degrees

    GameObject m_go;
    Rigidbody m_rb;
    Airfoil m_airfoil;

    [SetUp]
    public void SetUp()
    {
        m_go = new GameObject("TestAircraft");
        m_rb = m_go.AddComponent<Rigidbody>();
        m_rb.useGravity = false;
        m_rb.centerOfMass = Vector3.zero;

        m_airfoil = MakeLinearAirfoil();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(m_go);
    }

    // ----------------------------- helpers ---------------------------------

    static Airfoil MakeLinearAirfoil()
    {
        var rows = new List<Dictionary<string, object>>();
        for (double a = -TableLimit; a <= TableLimit; a += 5.0)
        {
            rows.Add(new Dictionary<string, object>
            {
                { "alpha", a },
                { "cl", ClPerDeg * a },
                { "cd", (double)Cd0 }
            });
        }
        return new Airfoil(rows);
    }

    Wing MakeWing(float area = Area, float span = Span, float flap = 0f,
                  float control = 0f, Vector3? displacement = null)
    {
        return new Wing(area, control, span, flap, Vector3.up,
                        displacement ?? Vector3.zero, m_airfoil);
    }

    void SetVelocity(Vector3 worldVelocity)
    {
#if UNITY_6000_0_OR_NEWER
        m_rb.linearVelocity = worldVelocity;
#else
        m_rb.velocity = worldVelocity;
#endif
    }

    // Velocity (in the wing's local frame) that produces the given AoA
    static Vector3 LocalVelocityAtAoA(float speed, float aoaDeg)
    {
        float a = aoaDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f) * speed;
    }

    // Closed-form force (local frame) for the synthetic airfoil
    static Vector3 ExpectedLocalForce(float aoaDeg, float speed,
                                      float area = Area, float span = Span,
                                      float efficiency = 1f, float extraCl = 0f)
    {
        float a = aoaDeg * Mathf.Deg2Rad;
        float s = Mathf.Sin(a), c = Mathf.Cos(a);

        Vector3 liftDir = new Vector3(s, c, 0f);
        Vector3 dragDir = new Vector3(-c, s, 0f);

        // airfoil table clamps outside +-20 deg
        float cl = ClPerDeg * Mathf.Clamp(aoaDeg, -TableLimit, TableLimit) + extraCl;
        float aspectRatio = span * span / area;
        float cd = Cd0 + cl * cl / (Mathf.PI * aspectRatio * efficiency);
        float q = 0.5f * Rho * speed * speed * area;

        return (liftDir * cl + dragDir * cd) * q;
    }

    static void AssertVectorClose(Vector3 expected, Vector3 actual, float relTol = 1e-3f)
    {
        float tol = relTol * Mathf.Max(expected.magnitude, 1f);
        Assert.LessOrEqual((expected - actual).magnitude, tol,
            $"expected {expected}, got {actual}");
    }

    // ============================== AoA ====================================

    [TestCase(0f)]
    [TestCase(5f)]
    [TestCase(10f)]
    [TestCase(15f)]
    [TestCase(-5f)]
    [TestCase(-10f)]
    [TestCase(-15f)]
    public void AoA_MatchesFlightPathAngle(float aoa)
    {
        SetVelocity(LocalVelocityAtAoA(Speed, aoa));
        var wing = MakeWing();

        wing.ComputeForce(m_rb);

        Assert.AreEqual(aoa, wing.LastAoA, AoaTol);
    }

    [TestCase(10f)]
    [TestCase(100f)]
    public void AoA_IsIndependentOfSpeed(float speed)
    {
        SetVelocity(LocalVelocityAtAoA(speed, 7f));
        var wing = MakeWing();

        wing.ComputeForce(m_rb);

        Assert.AreEqual(7f, wing.LastAoA, AoaTol);
    }

    [Test]
    public void AoA_IsIndependentOfAircraftOrientation()
    {
        var rot = Quaternion.Euler(20f, 135f, -30f);
        m_go.transform.rotation = rot;
        SetVelocity(rot * LocalVelocityAtAoA(Speed, 8f));
        var wing = MakeWing();

        Vector3 force = wing.ComputeForce(m_rb);

        Assert.AreEqual(8f, wing.LastAoA, AoaTol);
        AssertVectorClose(rot * ExpectedLocalForce(8f, Speed), force);
    }

    [Test]
    public void AoA_PitchedAircraftFlyingLevel_ReportsPitchAngle()
    {
        // Nose pitched up 10 deg (rotation about +Z, X is forward),
        // velocity stays horizontal in the world -> AoA = +10.
        m_go.transform.rotation = Quaternion.Euler(0f, 0f, 10f);
        SetVelocity(new Vector3(Speed, 0f, 0f));
        var wing = MakeWing();

        wing.ComputeForce(m_rb);

        Assert.AreEqual(10f, wing.LastAoA, AoaTol);
    }

    // Wing behind the CG (x < 0) sees extra downward flow while pitching nose-up,
    // wing ahead of the CG sees the opposite.
    [TestCase(-4f)]
    [TestCase(4f)]
    public void AoA_IncludesVelocityFromAngularRate(float wingX)
    {
        const float pitchRate = 0.5f; // rad/s about +Z
        m_rb.angularVelocity = new Vector3(0f, 0f, pitchRate);
        SetVelocity(new Vector3(Speed, 0f, 0f));
        var wing = MakeWing(displacement: new Vector3(wingX, 0f, 0f));

        wing.ComputeForce(m_rb);

        float expected = Mathf.Atan2(-pitchRate * wingX, Speed) * Mathf.Rad2Deg;
        Assert.AreEqual(expected, wing.LastAoA, AoaTol);
    }

    // These two FAIL until m_spanAxis is computed AFTER m_wingNormal in the constructor.
    [Test]
    public void SpanwiseFlow_DoesNotChangeAoA()
    {
        SetVelocity(LocalVelocityAtAoA(Speed, 5f) + new Vector3(0f, 0f, 20f));
        var wing = MakeWing();

        wing.ComputeForce(m_rb);

        Assert.AreEqual(5f, wing.LastAoA, AoaTol);
    }

    [Test]
    public void SpanwiseFlow_DoesNotChangeForce()
    {
        Vector3 clean = LocalVelocityAtAoA(Speed, 5f);

        SetVelocity(clean);
        Vector3 reference = MakeWing().ComputeForce(m_rb);

        SetVelocity(clean + new Vector3(0f, 0f, 20f));
        Vector3 withSpanwise = MakeWing().ComputeForce(m_rb);

        AssertVectorClose(reference, withSpanwise);
    }

    [Test]
    public void AoA_BeyondTable_IsReportedUnclamped()
    {
        SetVelocity(LocalVelocityAtAoA(Speed, 30f));
        var wing = MakeWing();

        wing.ComputeForce(m_rb);

        Assert.AreEqual(30f, wing.LastAoA, AoaTol);
    }

    [Test]
    public void AoA_FlowStraightOntoWing_Is90_AndForceIsFinite()
    {
        SetVelocity(new Vector3(0f, -Speed, 0f)); // falling flat
        var wing = MakeWing();

        Vector3 force = wing.ComputeForce(m_rb);

        Assert.AreEqual(90f, wing.LastAoA, AoaTol);
        Assert.IsFalse(float.IsNaN(force.x) || float.IsNaN(force.y) || float.IsNaN(force.z));
    }

    // ============================= Forces ==================================

    [Test]
    public void Force_ZeroVelocity_ReturnsZero()
    {
        SetVelocity(Vector3.zero);
        Assert.AreEqual(Vector3.zero, MakeWing().ComputeForce(m_rb));
    }

    [Test]
    public void Force_LevelFlight_IsPureParasiteDrag()
    {
        SetVelocity(new Vector3(Speed, 0f, 0f));
        var wing = MakeWing();

        Vector3 force = wing.ComputeForce(m_rb);

        // q*S = 0.5 * 1.225 * 50^2 * 10 = 15312.5 ; cl = 0 -> no induced drag
        AssertVectorClose(new Vector3(-0.01f * 15312.5f, 0f, 0f), force);
    }

    [TestCase(-10f)]
    [TestCase(-5f)]
    [TestCase(2.5f)]
    [TestCase(5f)]
    [TestCase(10f)]
    [TestCase(15f)]
    public void Force_MatchesAnalyticValue(float aoa)
    {
        SetVelocity(LocalVelocityAtAoA(Speed, aoa));
        var wing = MakeWing();

        Vector3 force = wing.ComputeForce(m_rb);

        AssertVectorClose(ExpectedLocalForce(aoa, Speed), force);
    }

    [Test]
    public void Force_BeyondTable_UsesClampedCoefficients()
    {
        SetVelocity(LocalVelocityAtAoA(Speed, 30f));
        var wing = MakeWing();

        Vector3 force = wing.ComputeForce(m_rb);

        AssertVectorClose(ExpectedLocalForce(30f, Speed), force);
    }

    [Test]
    public void Lift_IsPerpendicularToVelocity()
    {
        Vector3 v = LocalVelocityAtAoA(Speed, 8f);
        SetVelocity(v);
        var wing = MakeWing();

        wing.ComputeForce(m_rb);

        Assert.AreEqual(0f, Vector3.Dot(wing.LastLift.normalized, v.normalized), 1e-3f);
    }

    [Test]
    public void Drag_IsOppositeToVelocity()
    {
        Vector3 v = LocalVelocityAtAoA(Speed, 8f);
        SetVelocity(v);
        var wing = MakeWing();

        wing.ComputeForce(m_rb);

        Assert.AreEqual(-1f, Vector3.Dot(wing.LastDrag.normalized, v.normalized), 1e-3f);
    }

    [Test]
    public void TotalForce_EqualsLiftPlusDrag_InWorldSpace()
    {
        var rot = Quaternion.Euler(15f, 60f, -20f);
        m_go.transform.rotation = rot;
        SetVelocity(rot * LocalVelocityAtAoA(Speed, 6f));
        var wing = MakeWing();

        Vector3 force = wing.ComputeForce(m_rb);

        AssertVectorClose(force, wing.LastLift + wing.LastDrag);
    }

    [Test]
    public void Lift_SignFollowsAoA()
    {
        SetVelocity(LocalVelocityAtAoA(Speed, 5f));
        var wing = MakeWing();
        wing.ComputeForce(m_rb);
        Assert.Greater(Vector3.Dot(wing.LastLift, Vector3.up), 0f, "positive AoA -> lift along +normal");

        SetVelocity(LocalVelocityAtAoA(Speed, -5f));
        wing.ComputeForce(m_rb);
        Assert.Less(Vector3.Dot(wing.LastLift, Vector3.up), 0f, "negative AoA -> lift along -normal");
    }

    [Test]
    public void Force_ScalesWithSpeedSquared()
    {
        var wing = MakeWing();

        SetVelocity(LocalVelocityAtAoA(50f, 5f));
        Vector3 f1 = wing.ComputeForce(m_rb);

        SetVelocity(LocalVelocityAtAoA(100f, 5f));
        Vector3 f2 = wing.ComputeForce(m_rb);

        AssertVectorClose(f1 * 4f, f2);
    }

    [Test]
    public void Lift_ScalesLinearlyWithArea()
    {
        SetVelocity(LocalVelocityAtAoA(Speed, 5f));

        var small = MakeWing(area: 10f);
        small.ComputeForce(m_rb);

        var big = MakeWing(area: 20f);
        big.ComputeForce(m_rb);

        Assert.AreEqual(2f, big.LastLift.magnitude / small.LastLift.magnitude, 1e-3f);
    }

    [Test]
    public void InducedDrag_IncreasesWithLowerEfficiency()
    {
        SetVelocity(LocalVelocityAtAoA(Speed, 10f));

        var wing = MakeWing();
        wing.EfficiencyFactor = 1f;
        wing.ComputeForce(m_rb);
        float dragFull = wing.LastDrag.magnitude;

        wing.EfficiencyFactor = 0.5f;
        Vector3 force = wing.ComputeForce(m_rb);
        float dragHalf = wing.LastDrag.magnitude;

        Assert.Greater(dragHalf, dragFull);
        AssertVectorClose(ExpectedLocalForce(10f, Speed, efficiency: 0.5f), force);
    }

    // ============================== Flaps ==================================

    [TestCase(1f)]
    [TestCase(-1f)]
    [TestCase(0.5f)]
    public void Flap_AddsLiftIncrementProportionalToControlInput(float control)
    {
        const float flapRatio = 0.25f;
        SetVelocity(new Vector3(Speed, 0f, 0f)); // AoA 0
        var wing = MakeWing(flap: flapRatio, control: control);

        Vector3 force = wing.ComputeForce(m_rb);

        float extraCl = Mathf.Sqrt(flapRatio) * (ClPerDeg * TableLimit) * control; // MaxCl = 2.0
        AssertVectorClose(ExpectedLocalForce(0f, Speed, extraCl: extraCl), force);
    }

    [Test]
    public void Flap_ZeroFlapRatio_IgnoresControlInput()
    {
        SetVelocity(LocalVelocityAtAoA(Speed, 3f));

        Vector3 neutral = MakeWing(flap: 0f, control: 0f).ComputeForce(m_rb);
        Vector3 deflected = MakeWing(flap: 0f, control: 1f).ComputeForce(m_rb);

        AssertVectorClose(neutral, deflected);
    }

    [Test]
    public void ControlInput_IsClampedToUnitRange()
    {
        var wing = MakeWing(control: 3f);
        Assert.AreEqual(1f, wing.ControlInput);

        wing.ControlInput = -7f;
        Assert.AreEqual(-1f, wing.ControlInput);

        wing.ControlInput = 0.4f;
        Assert.AreEqual(0.4f, wing.ControlInput, 1e-6f);
    }
}
