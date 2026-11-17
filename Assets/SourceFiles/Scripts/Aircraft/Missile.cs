using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Missile
{
    // expose parameters
    private Rigidbody m_rigidBody;
    private readonly Dictionary<WingRole, Wing> wings = new();
    private readonly Dictionary<ControlSurface, Transform> controlSurfaces = new();
    private Engine engine;
    private List<float> controlInputs = new List<float>();

    
    public List<float> getControlInputs() { return fetchParams(); }

    private List<float> fetchParams()
    {
        controlInputs.Clear();
        
        // add them from control surfaces
        
        return controlInputs;
    }
    
    public Missile(Rigidbody rigidBody)
    {
        m_rigidBody = rigidBody;
    }

    public void computeForces(/* LQR-corrected input */){ }
    
    // Called when the pilot tries to lock the missile onto the target
    public void TryLock(/*Target target*/) { }

    // Called after the lock was SUCCESSFULLY established; the missile is fired from its rails
    public void Attack() { }

    // Called on contact or if you are close enough to the target
    public void Detonate() { }
    
    
}