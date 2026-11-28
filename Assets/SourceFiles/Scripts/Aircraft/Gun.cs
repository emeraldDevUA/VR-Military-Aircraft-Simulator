using System;
using System.Collections.Generic;
using UnityEngine;

public struct Bullet
{
    public Vector3 position;
    public Vector3 velocity;
    public float age;
    public bool isTracer;
}

public class Gun
{
    // --- Your original data ---
    Vector3 m_gunPoint;       // muzzle position (local to mount if one is given)
    Vector3 m_gunDirection;   // firing direction (local to mount if one is given)
    int m_ammoQuantity;

    // --- Tuning ---
    public float MuzzleVelocity = 800f;   // m/s
    public float RoundsPerSecond = 20f;
    public float SpreadDegrees = 0.3f;
    public float BulletLifetime = 3f;     // seconds
    public int TracerEvery = 4;           // 0 = no tracers
    public LayerMask HitMask = ~0;        // exclude your own plane's layer!

    // --- Events ---
    public event Action<Bullet, RaycastHit> OnHit;
    public event Action OnEmpty;
    public event Action<Vector3, Vector3> OnFired;   // world position, world direction (muzzle flash, sound)

    // --- State ---
    readonly Transform m_mount;                     // optional: the plane's transform
    readonly List<Bullet> m_bullets = new List<Bullet>(256);
    float m_cooldown;
    int m_shotCounter;

    public int Ammo => m_ammoQuantity;
    public IReadOnlyList<Bullet> Bullets => m_bullets;   // read this to draw tracers

    public Gun(Vector3 gunPoint, Vector3 gunDirection, int ammoQuantity, Transform mount = null)
    {
        m_gunPoint = gunPoint;
        m_gunDirection = gunDirection.normalized;
        m_ammoQuantity = ammoQuantity;
        m_mount = mount;
    }

    /// <summary>Fires one round if the gun is ready and has ammo. Returns true if a bullet was spawned.</summary>
    public bool Fire(Vector3 inheritedVelocity)
    {
        if (m_cooldown > 0f) return false;

        if (m_ammoQuantity <= 0)
        {
            OnEmpty?.Invoke();
            return false;
        }

        Vector3 worldPoint = m_mount ? m_mount.TransformPoint(m_gunPoint) : m_gunPoint;
        Vector3 worldDir = m_mount ? m_mount.TransformDirection(m_gunDirection) : m_gunDirection;

        // Random cone spread around the firing direction
        Vector2 r = UnityEngine.Random.insideUnitCircle * SpreadDegrees;
        worldDir = Quaternion.LookRotation(worldDir) * Quaternion.Euler(r.y, r.x, 0f) * Vector3.forward;

        m_bullets.Add(new Bullet
        {
            position = worldPoint,
            velocity = inheritedVelocity + worldDir * MuzzleVelocity,
            age = 0f,
            isTracer = TracerEvery > 0 && m_shotCounter % TracerEvery == 0
        });

        m_shotCounter++;
        m_ammoQuantity--;
        m_cooldown += 1f / RoundsPerSecond;
        OnFired?.Invoke(worldPoint, worldDir);
        return true;
    }

    /// <summary>Call once per FixedUpdate. Handles firing while the trigger is held, then moves bullets.</summary>
    public void Tick(float dt, bool triggerHeld, Vector3 inheritedVelocity)
    {
        m_cooldown -= dt;
        if (m_cooldown < 0f && !triggerHeld) m_cooldown = 0f;   // don't bank cooldown while idle

        // While loop so high fire rates can spawn several rounds per physics step
        while (triggerHeld && m_cooldown <= 0f)
        {
            if (!Fire(inheritedVelocity)) break;
        }

        StepBullets(dt);
    }

    public void AddAmmo(int amount) => m_ammoQuantity += amount;

    void StepBullets(float dt)
    {
        for (int i = m_bullets.Count - 1; i >= 0; i--)
        {
            Bullet b = m_bullets[i];
            b.age += dt;
            b.velocity += Physics.gravity * dt;

            Vector3 delta = b.velocity * dt;
            float dist = delta.magnitude;

            // Sweep from old position to new position so fast bullets can't tunnel
            if (dist > 0f &&
                Physics.Raycast(b.position, delta / dist, out RaycastHit hit, dist,
                                HitMask, QueryTriggerInteraction.Ignore))
            {
                OnHit?.Invoke(b, hit);
                RemoveAtSwap(i);
                continue;
            }

            b.position += delta;

            if (b.age >= BulletLifetime)
            {
                RemoveAtSwap(i);
                continue;
            }

            m_bullets[i] = b;   // Bullet is a struct, so write the copy back
        }
    }

    // O(1) removal; order doesn't matter for bullets
    void RemoveAtSwap(int i)
    {
        int last = m_bullets.Count - 1;
        m_bullets[i] = m_bullets[last];
        m_bullets.RemoveAt(last);
    }
}