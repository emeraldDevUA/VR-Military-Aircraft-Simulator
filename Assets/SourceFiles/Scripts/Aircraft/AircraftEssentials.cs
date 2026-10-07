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


public struct Wing{

    public Wing(double area, double controlInput, double span, double flapRatio,
                Vector3 wingNormal, Vector3 displacement, Airfoil airfoil){
        m_area = area;
        m_controlInput = controlInput;
        m_wingNormal = wingNormal;
        m_displacement = displacement;
        m_airfoil = airfoil;
        m_flapRatio = flapRatio;
        m_aspectRatio = 1;
        m_efficiencyFactor = 1;
    }

    public double m_area{get; set;}
    // [1; -1]
    public double m_controlInput{get; set;}

    public Vector3 m_wingNormal{get; set;}

    public Vector3 m_displacement{get; set;}

    public Airfoil m_airfoil{get; set;}

    public double m_flapRatio{get; set;}

    public double m_aspectRatio{get; set;}

    public double m_efficiencyFactor{get; set;}

   public Vector3 ComputeForce(Rigidbody rb)
   {
       // Velocity at the wing, expressed in the wing's local frame
       Vector3 v = rb.transform.InverseTransformDirection(rb.GetPointVelocity(rb.transform.position));

       // Remove spanwise flow (local span axis = +X)
       Vector3 vPlane = Vector3.ProjectOnPlane(v, Vector3.right);
       float planeSpeed = vPlane.magnitude;
       if (planeSpeed < 1e-4f) return Vector3.zero;

       Vector3 flowDir = vPlane / planeSpeed;          // direction of motion
       Vector3 dragDir = -flowDir;
       Vector3 liftDir = Vector3.Cross(flowDir, Vector3.right);

       // m_wingNormal must be a unit vector in LOCAL space (e.g. Vector3.up)
       float aoa = Mathf.Asin(Mathf.Clamp(Vector3.Dot(dragDir, m_wingNormal), -1f, 1f)) * Mathf.Rad2Deg;

       Point coefficients = m_airfoil.SampleByAlpha((double)aoa);
       float liftCoefficient = (float)coefficients.X;
       float dragCoefficient = (float)coefficients.Y;

       if (m_flapRatio > 0f)
           liftCoefficient += (float)Mathf.Sqrt((float)m_flapRatio) * (float)m_airfoil.MaxCl * (float)m_controlInput;

       float inducedDragCoefficient= (float)((liftCoefficient * liftCoefficient) / (Mathf.PI * m_aspectRatio * m_efficiencyFactor));
       dragCoefficient += inducedDragCoefficient;

       float airDensity = 1.225f; // replace with an altitude-based lookup
       float dynamicPressure = 0.5f * (float) (planeSpeed * planeSpeed * airDensity * m_area);

       Vector3 localForce = (liftDir * liftCoefficient + dragDir * dragCoefficient) * dynamicPressure;
       return rb.transform.TransformDirection(localForce);   // back to world space
   }
}