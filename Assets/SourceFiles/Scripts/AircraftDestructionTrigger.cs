using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AircraftDestructionTrigger : MonoBehaviour
{
    public GameObject intactAirframe;
    public GameObject fragmentContainer;
    public DebrisManager debrisManager;

    private bool fractured = false;
    private GameObject debrisRoot;
    private readonly List<Rigidbody> shardBodies = new List<Rigidbody>();
    private readonly List<Vector3> localOffsets = new List<Vector3>();
    private ParticleSystem sparkSystem;
    private AudioSource boomAudio;

    void Awake()
    {
        if (intactAirframe == null)
        {
            Transform found = transform.Find("F-2-airframe");
            if (found != null) intactAirframe = found.gameObject;
        }

        // Prevent solid child colliders from deflecting/bouncing the projectile before it triggers
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col.gameObject != gameObject)
                col.enabled = false;
        }

        // Cover tail fin, canopy, wings, and nose so any first touch triggers immediately
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 0.45f, 0f);
        box.size = new Vector3(7.8f, 1.35f, 12.0f);

        Material jetMat = null;
        if (intactAirframe != null)
        {
            var mr = intactAirframe.GetComponent<MeshRenderer>();
            if (mr != null) jetMat = mr.sharedMaterial;
        }

        debrisRoot = new GameObject("JetDebris_Pool");
        debrisRoot.SetActive(false);
        Mesh shardMesh = CreateJaggedShardMesh();

        for (int i = 0; i < 26; i++)
            PrebuildShard(shardMesh, jetMat, 0.32f, 1.0f, 0.04f);

        for (int i = 0; i < 28; i++)
            PrebuildShard(shardMesh, jetMat, 0.10f, 0.30f, 0.02f);

        PrebuildEffects();
    }

    void OnTriggerEnter(Collider other)
    {
        TriggerBoom(other);
    }

    void OnTriggerStay(Collider other)
    {
        TriggerBoom(other);
    }

    private void TriggerBoom(Collider other)
    {
        if (fractured) return;
        if (other == null || other.transform.IsChildOf(transform)) return;

        fractured = true;

        Vector3 center = intactAirframe != null ? intactAirframe.transform.position : transform.position;
        Vector3 hitPoint = other.ClosestPoint(center);

        other.gameObject.SetActive(false);
        Destroy(other.gameObject);

        foreach (Transform child in transform)
        {
            if (child.GetComponentInChildren<Camera>() == null)
                child.gameObject.SetActive(false);
        }

        debrisRoot.transform.position = center;
        debrisRoot.SetActive(true);

        for (int i = 0; i < shardBodies.Count; i++)
        {
            Rigidbody rb = shardBodies[i];
            Vector3 worldPos = center + localOffsets[i];
            rb.transform.position = worldPos;

            Vector3 dir = (worldPos - hitPoint).normalized;
            dir = (dir + Random.insideUnitSphere * 0.4f + Vector3.up * 0.28f).normalized;

            float speed = i < 26 ? Random.Range(11f, 20f) : Random.Range(18f, 30f);
            rb.linearVelocity = dir * speed;
            rb.angularVelocity = Random.insideUnitSphere * Random.Range(12f, 26f);
        }

        if (sparkSystem != null)
        {
            sparkSystem.transform.position = hitPoint;
            sparkSystem.Play();
        }
        if (boomAudio != null) boomAudio.Play();

        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null) StartCoroutine(ShakeCamera(cam.transform, 0.25f, 0.16f));

        Destroy(debrisRoot, 3.5f);
    }

    private IEnumerator ShakeCamera(Transform camTransform, float duration, float magnitude)
    {
        Vector3 originalPos = camTransform.localPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float damper = 1f - (elapsed / duration);
            camTransform.localPosition = originalPos + Random.insideUnitSphere * (magnitude * damper);
            elapsed += Time.deltaTime;
            yield return null;
        }
        camTransform.localPosition = originalPos;
    }

    private void PrebuildShard(Mesh mesh, Material mat, float sizeMin, float sizeMax, float thickness)
    {
        GameObject shard = new GameObject("Shard");
        shard.transform.SetParent(debrisRoot.transform);

        Vector3 offset = new Vector3(
            Random.Range(-2.2f, 2.2f),
            Random.Range(-0.2f, 0.35f),
            Random.Range(-2.8f, 2.8f)
        );
        localOffsets.Add(offset);
        shard.transform.localPosition = offset;
        shard.transform.localRotation = Random.rotation;
        shard.transform.localScale = new Vector3(
            Random.Range(sizeMin, sizeMax),
            thickness,
            Random.Range(sizeMin, sizeMax)
        );

        shard.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = shard.AddComponent<MeshRenderer>();
        if (mat != null) mr.sharedMaterial = mat;

        var rb = shard.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.linearDamping = 0.35f;
        rb.angularDamping = 0.2f;
        shardBodies.Add(rb);
    }

    private void PrebuildEffects()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
        Material particleMat = shader != null ? new Material(shader) : null;

        GameObject sparkObj = new GameObject("ShatterSparks");
        sparkObj.transform.SetParent(debrisRoot.transform);
        sparkSystem = sparkObj.AddComponent<ParticleSystem>();
        var sMain = sparkSystem.main;
        sMain.playOnAwake = false;
        sMain.duration = 0.15f;
        sMain.loop = false;
        sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.10f, 0.24f);
        sMain.startSpeed = new ParticleSystem.MinMaxCurve(18f, 38f);
        sMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        sMain.startColor = new Color(1f, 0.88f, 0.55f, 1f);
        sMain.gravityModifier = 0.7f;
        var sEm = sparkSystem.emission;
        sEm.rateOverTime = 0;
        sEm.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 32) });
        var sRender = sparkObj.GetComponent<ParticleSystemRenderer>();
        sRender.renderMode = ParticleSystemRenderMode.Stretch;
        sRender.velocityScale = 0.05f;
        if (particleMat != null) sRender.sharedMaterial = particleMat;

        boomAudio = debrisRoot.AddComponent<AudioSource>();
        boomAudio.playOnAwake = false;
        boomAudio.spatialBlend = 0f;
        boomAudio.volume = 0.28f;
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.45f);
        AudioClip clip = AudioClip.Create("BoomClip", samples, 1, sampleRate, false);
        float[] data = new float[samples];
        float lowPass = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 9.5f);
            float white = Random.Range(-1f, 1f);
            lowPass = Mathf.Lerp(lowPass, white, 0.06f);
            float sub = Mathf.Sin(2f * Mathf.PI * (55f * Mathf.Exp(-t * 5f)) * t);
            data[i] = Mathf.Clamp((lowPass * 0.9f + sub * 0.5f) * env, -1f, 1f);
        }
        clip.SetData(data, 0);
        boomAudio.clip = clip;
    }

    private Mesh CreateJaggedShardMesh()
    {
        Mesh m = new Mesh();
        Vector3[] verts = new Vector3[]
        {
            new Vector3(0f, 0.5f, 0.8f),
            new Vector3(0.7f, 0.5f, -0.5f),
            new Vector3(-0.6f, 0.5f, -0.3f),
            new Vector3(0f, -0.5f, 0.8f),
            new Vector3(0.7f, -0.5f, -0.5f),
            new Vector3(-0.6f, -0.5f, -0.3f)
        };
        int[] tris = new int[]
        {
            0, 1, 2,
            3, 5, 4,
            0, 3, 4, 0, 4, 1,
            1, 4, 5, 1, 5, 2,
            2, 5, 3, 2, 3, 0
        };
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }
}
