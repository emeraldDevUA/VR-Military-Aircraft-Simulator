using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MissileControlSurface
{
    CanardTop,
    CanardBottom,
    CanardLeft,
    CanardRight,

    TailFinTop,
    TailFinBottom,
    TailFinLeft,
    TailFinRight,

    RolleronLeft,
    RolleronRight
}


public class Missile
{
    // expose parameters
    private Rigidbody m_rigidBody;
    private readonly Dictionary<MissileControlSurface, Wing> wings = new();
    private readonly Dictionary<MissileControlSurface, Transform> controlSurfaces = new();

    private Engine engine;
    private List<float> controlInputs = new List<float>();

    private HomingSystem m_homingSystem;
    // warhead

    private Vector3 m_lockedTarget = new Vector3(0, 0, 0);


    
    public Missile(Rigidbody rigidBody, HomingSystem homingSystem)
    {
        m_rigidBody = rigidBody;
        m_homingSystem = homingSystem;
    }

    public void computeForces(MissileInput input){



    }
    
    // Called when the pilot tries to lock the missile onto the target
    public void TryLock(/*Target target*/) {
        m_homingSystem.TryLock();
    }

    // Called after the lock was SUCCESSFULLY established; the missile is fired from its rails
    public void Attack() {

    }

    float eps = 3.0f;
    // Called on contact or if you are close enough to the target
    public void Detonate() {
        if(Vector3.Distance(m_homingSystem.m_position, m_lockedTarget) <= eps){
            // m_warhead.detonate();
        }
    }

    public static float GetDeflection(MissileInput input, MissileControlSurface surface)
    {
        switch (surface)
        {
            case MissileControlSurface.TailFinTop:
            case MissileControlSurface.CanardTop:
                return Mathf.Clamp(input.yaw + input.roll, -1f, 1f);

            case MissileControlSurface.TailFinBottom:
            case MissileControlSurface.CanardBottom:
                return Mathf.Clamp(-input.yaw + input.roll, -1f, 1f);

            case MissileControlSurface.TailFinLeft:
            case MissileControlSurface.CanardLeft:
                return Mathf.Clamp(input.pitch + input.roll, -1f, 1f);

            case MissileControlSurface.TailFinRight:
            case MissileControlSurface.CanardRight:
                return Mathf.Clamp(-input.pitch + input.roll, -1f, 1f);

            default:
                return 0f; // rollerons are passive
        }
    }
    
}