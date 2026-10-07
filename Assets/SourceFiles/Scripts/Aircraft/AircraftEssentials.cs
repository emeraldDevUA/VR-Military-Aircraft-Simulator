using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public struct Engine
{
    public Engine(double thrust, double throttle)
    {
        maxThrust = thrust;
        innerThrottle = throttle;
        enableAfterburner = false;
    }

    public double maxThrust { get; private set; }

    public double innerThrottle { get; set; }

    public bool enableAfterburner { get;  set; }

    public double computePropulsionForce()
    {
        double afterburner_multiplier = enableAfterburner ? 2 : 1;

        return -innerThrottle * maxThrust * afterburner_multiplier;
    }
}


public class Wing
{
    private readonly Airfoil m_airfoil;
    private readonly Vector3 m_displacement;   // center of pressure, in the aircraft's local space
    private readonly Vector3 m_wingNormal;     // unit vector, local space
    private readonly Vector3 m_spanAxis;       // unit vector, local space
    private readonly float m_area;
    private readonly float m_chord;
    private readonly float m_wingspan;
    private readonly float m_aspectRatio;
    private readonly float m_flapRatio;

    public float EfficiencyFactor { get; set; } = 1f;

    private float m_controlInput;
    public float ControlInput                  // [-1, 1]
    {
        get => m_controlInput;
        set => m_controlInput = Mathf.Clamp(value, -1f, 1f);
    }

    public Wing(float area, float controlInput, float span, float flapRatio,
                Vector3 wingNormal, Vector3 displacement, Airfoil airfoil)
    {
        m_airfoil = airfoil;
        m_displacement = displacement;
        m_area = area;
        m_chord = area / span;
        m_wingspan = span;
        m_wingNormal = wingNormal.normalized;
        m_spanAxis = Vector3.Cross(m_wingNormal, Vector3.forward).normalized;
        m_aspectRatio = (span * span) / area;
        m_flapRatio = flapRatio;
        ControlInput = controlInput;
    }

    public Vector3 ComputeForce(Rigidbody rb)
    {
        Transform t = rb.transform;

        // Velocity at the wing's position, expressed in the aircraft's local frame
        Vector3 worldPoint = t.TransformPoint(m_displacement);
        Vector3 v = t.InverseTransformDirection(rb.GetPointVelocity(worldPoint));

        // Remove spanwise flow
        Vector3 vPlane = Vector3.ProjectOnPlane(v, m_spanAxis);
        float planeSpeed = vPlane.magnitude;
        if (planeSpeed < 1e-4f) return Vector3.zero;

        Vector3 flowDir = vPlane / planeSpeed;
        Vector3 dragDir = -flowDir;
        Vector3 liftDir = Vector3.Cross(flowDir, m_spanAxis);

        float aoa = Mathf.Asin(Mathf.Clamp(Vector3.Dot(dragDir, m_wingNormal), -1f, 1f)) * Mathf.Rad2Deg;

        Point coeffs = m_airfoil.SampleByAlpha(aoa);
        float liftCoeff = (float)coeffs.X;
        float dragCoeff = (float)coeffs.Y;

        if (m_flapRatio > 0f)
            liftCoeff += Mathf.Sqrt(m_flapRatio) * (float)m_airfoil.MaxCl * m_controlInput;

        dragCoeff += (liftCoeff * liftCoeff) / (Mathf.PI * m_aspectRatio * EfficiencyFactor);

        float airDensity = 1.225f; // replace with an altitude-based lookup
        float dynamicPressure = 0.5f * planeSpeed * planeSpeed * airDensity * m_area;

        Vector3 localForce = (liftDir * liftCoeff + dragDir * dragCoeff) * dynamicPressure;
        return t.TransformDirection(localForce);
    }

    // Convenience: apply at the wing's center of pressure (torque comes for free)
    public void Apply(Rigidbody rb)
    {
        rb.AddForceAtPosition(ComputeForce(rb), rb.transform.TransformPoint(m_displacement));
    }
}