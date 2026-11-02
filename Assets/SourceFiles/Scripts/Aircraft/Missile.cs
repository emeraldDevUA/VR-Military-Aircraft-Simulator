using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Missile
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