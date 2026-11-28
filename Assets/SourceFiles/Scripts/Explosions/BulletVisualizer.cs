using UnityEngine;

/// <summary>
/// Draws the bullets of a Gun as stretched, instanced tracer streaks
/// and plays muzzle flash / impact effects from the Gun's events.
/// </summary>
public class BulletVisualizer : MonoBehaviour
{
    [Header("Tracer")]
    [SerializeField] Material tracerMaterial;      // Unlit + emissive color, "Enable GPU Instancing" ON
    [SerializeField] float tracerLength = 6f;      // streak length in meters
    [SerializeField] float tracerWidth = 0.06f;
    [SerializeField] bool onlyTracers = true;      // draw only bullets flagged isTracer

    [Header("Effects")]
    [SerializeField] ParticleSystem muzzleFlash;   // child of the plane, near the gun point
    [SerializeField] ParticleSystem impactPrefab;  // small spark/dust burst

    Gun m_gun;
    Mesh m_mesh;
    readonly Matrix4x4[] m_batch = new Matrix4x4[1023];   // DrawMeshInstanced limit

    void Awake()
    {
        // Borrow Unity's cube mesh; scaled thin and long it becomes a streak
        GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        m_mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tmp);

        if (muzzleFlash != null)
        {
            var emission = muzzleFlash.emission;
            emission.enabled = false;   // Emit() still works with the module disabled
            muzzleFlash.Play();         // keep the system running so emitted particles simulate
        }

    }

    public void Bind(Gun gun)
    {
        Unbind();
        m_gun = gun;
        m_gun.OnFired += HandleFired;
        m_gun.OnHit += HandleHit;
    }

    void Unbind()
    {
        if (m_gun == null) return;
        m_gun.OnFired -= HandleFired;
        m_gun.OnHit -= HandleHit;
        m_gun = null;
    }

    void OnDestroy() => Unbind();

    void LateUpdate()
    {
        if (m_gun == null || tracerMaterial == null) return;

        // Bullets move in FixedUpdate; extrapolate so they look smooth at any frame rate
        float extra = Mathf.Clamp(Time.time - Time.fixedTime, 0f, Time.fixedDeltaTime);

        var bullets = m_gun.Bullets;
        int count = 0;

        for (int i = 0; i < bullets.Count; i++)
        {
            Bullet b = bullets[i];
            if (onlyTracers && !b.isTracer) continue;

            float speed = b.velocity.magnitude;
            if (speed < 0.01f) continue;

            Vector3 dir = b.velocity / speed;
            Vector3 head = b.position + b.velocity * extra;

            // Don't let the streak reach back behind the muzzle on a fresh bullet
            float length = Mathf.Min(tracerLength, speed * (b.age + extra));
            if (length < 0.05f) continue;

            // Cube is centered, so place its center half a length behind the head
            Vector3 center = head - dir * (length * 0.5f);

            m_batch[count++] = Matrix4x4.TRS(
                center,
                Quaternion.LookRotation(dir),
                new Vector3(tracerWidth, tracerWidth, length));

            if (count == m_batch.Length)
            {
                Graphics.DrawMeshInstanced(m_mesh, 0, tracerMaterial, m_batch, count);
                count = 0;
            }
        }

        if (count > 0)
            Graphics.DrawMeshInstanced(m_mesh, 0, tracerMaterial, m_batch, count);
    }

    void HandleFired(Vector3 position, Vector3 direction)
    {
        if (muzzleFlash == null) return;
        muzzleFlash.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        muzzleFlash.Emit(1);
    }

    void HandleHit(Bullet bullet, RaycastHit hit)
    {
        if (impactPrefab == null) return;
        ParticleSystem fx = Instantiate(impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
        Destroy(fx.gameObject, 2f);   // swap for a pool if you get many impacts per second
    }
}