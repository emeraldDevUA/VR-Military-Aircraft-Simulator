using UnityEngine;

public class DebrisManager : MonoBehaviour
{
    [Header("Explosion Settings")]
    [Tooltip("Outward force applied to fragments when fractured.")]
    public float explosionForce = 350f;

    [Tooltip("Radius of the outward explosion blast.")]
    public float explosionRadius = 5f;

    [Tooltip("Slight upward lift so debris scatters cleanly.")]
    public float upwardsModifier = 0.5f;

    [Header("VR Performance Cleanup")]
    [Tooltip("Seconds before fractured pieces are destroyed to protect VR frame rate.")]
    public float debrisLifetime = 4f;

    /// <summary>
    /// Hook this method to OpenFracture's Prefracture -> Callback Options -> On Completed event.
    /// </summary>
    public void OnObjectFractured()
    {
        // OpenFracture names the generated parent container "<ObjectName>Fragments"
        GameObject fragmentContainer = GameObject.Find(gameObject.name + "Fragments");

        if (fragmentContainer == null) return;

        Rigidbody[] fragmentBodies = fragmentContainer.GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody rb in fragmentBodies)
        {
            if (rb != null)
            {
                rb.AddExplosionForce(
                    explosionForce,
                    transform.position,
                    explosionRadius,
                    upwardsModifier,
                    ForceMode.Impulse
                );
            }
        }

        // Hide remaining sibling parts (cockpit, canopy, rails) while keeping CameraPoint active
        if (transform.parent != null)
        {
            foreach (Transform sibling in transform.parent)
            {
                if (sibling != transform && sibling.gameObject != fragmentContainer && sibling.GetComponentInChildren<Camera>() == null)
                {
                    sibling.gameObject.SetActive(false);
                }
            }
        }

        // Automatically remove the fragment container after debrisLifetime seconds
        Destroy(fragmentContainer, debrisLifetime);
    }
}