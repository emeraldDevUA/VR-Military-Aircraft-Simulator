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

    public Wing(double area, double activation, Vector3 wingNormal, Vector3 displacement){
        m_area = area;
        m_activation = activation;
        m_wingNormal = wingNormal;
        m_displacement = displacement;
    }

    public double m_area{get; set;}
    // [1; -1]
    public double m_activation{get; set;}

    public Vector3 m_wingNormal{get; set;}

    public Vector3 m_displacement{get; set;}

    public double computeForce(){
        return 0.0;
    }
}