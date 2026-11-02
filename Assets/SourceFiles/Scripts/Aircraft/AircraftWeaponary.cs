using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

abstract class HomingSystem
{
    private string m_homingMode = "";

    public Vector3 m_position{ get; set;} = new Vector3(0,0,0);
    public HomingSystem()
    {
    }

    // Each implementation will have its own conditions for a lock to be established
    public abstract bool TryLock();

    // This method will be called by the missile to know the next location
    public abstract Vector3 GetUpdatedDirection();
}

// Heat-seeking homing system stub
class InfraredHomingSystem : HomingSystem
{
    public override bool TryLock() { return false; }
    public override Vector3 GetUpdatedDirection() { return Vector3.zero; }
}

// Radar-guided (semi-active) homing system stub
class SemiActiveHomingSystem : HomingSystem
{
    public override bool TryLock() { return false; }
    public override Vector3 GetUpdatedDirection() { return Vector3.zero; }
}

// Radar-guided (active) homing system stub
class ActiveHomingSystem : HomingSystem
{
    public override bool TryLock() { return false; }
    public override Vector3 GetUpdatedDirection() { return Vector3.zero; }
}
