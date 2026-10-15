using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class Gun
{
    Vector3 m_gunPoint;
    Vector3 m_gunDirection;

    int m_ammoQuantity;

    public Gun(Vector3 gunPoint, Vector3 gunDirection, int ammoQuantity)
    {
        m_gunPoint = gunPoint;
        m_gunDirection = gunDirection;
        m_ammoQuantity = ammoQuantity;
    }

    public void Fire() { }
}


abstract class HomingSystem
{
    private string m_homingMode = "";

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


class Missile
{
    private Rigidbody m_rigidBody;

    public Missile(Rigidbody rigidBody)
    {
        m_rigidBody = rigidBody;
    }

    // Called when the pilot tries to lock the missile onto the target
    public void TryLock(/*Target target*/) { }

    // Called after the lock was SUCCESSFULLY established; the missile is fired from its rails
    public void Attack() { }

    // Called on contact or if you are close enough to the target
    public void Detonate() { }
}