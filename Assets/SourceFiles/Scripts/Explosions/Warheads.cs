using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;

[CreateAssetMenu(menuName = "Weapons/Warhead Data")]
public class WarheadData : ScriptableObject
{
    public GameObject intactPrefab;
    public GameObject fracturedPrefab;
    public float explosionForce = 500f;
    public float explosionRadius = 5f;
    public float upwardsModifier = 0.5f;
    public float fragmentLifetime = 5f;
}

public abstract class Warhead
{
    protected readonly WarheadData data;

    protected Warhead(WarheadData data) => this.data = data;

    public virtual GameObject Explode(
        Vector3 position,
        Quaternion rotation,
        Vector3 velocity,
        Vector3 angularVelocity = default)   // radians/sec, world space
    {
        var debris = Object.Instantiate(data.fracturedPrefab, position, rotation);

        foreach (var rb in debris.GetComponentsInChildren<Rigidbody>())
        {
            // Velocity of this piece's point on the rotating parent: v + ω × r
            Vector3 r = rb.worldCenterOfMass - position;
            Vector3 inherited = velocity + Vector3.Cross(angularVelocity, r);

            rb.linearVelocity = inherited;          // use rb.velocity on Unity 2022 and earlier
            rb.angularVelocity = angularVelocity;

            rb.AddExplosionForce(data.explosionForce, position,
                                 data.explosionRadius, data.upwardsModifier,
                                 ForceMode.VelocityChange);
        }

        Object.Destroy(debris, data.fragmentLifetime);
        OnExploded(debris);
        return debris;
    }

    protected virtual void OnExploded(GameObject debris) { }
}

public class NormalWarhead : Warhead{


    public HighExplosiveWarhead(WarheadData data) : base(data) { }

    protected override void OnExploded(GameObject debris)
    {
        // VFX, sound, damage...
    }
}