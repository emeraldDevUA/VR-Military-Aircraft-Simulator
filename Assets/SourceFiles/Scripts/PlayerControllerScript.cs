using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(InputSource))]
public class PlayerControllerScript : MonoBehaviour
{
    [Header("References")]
    public Transform cameraPoint;
    public Camera playerCamera;
    [FormerlySerializedAs("rigidbody")]
    public Rigidbody rb;                 // auto-filled from this GameObject if left empty


    [Header("Aircraft")]
    public float mass = 9500f;
    public Vector3 centerOfMass = new Vector3(-0.7f, 0f, 0f);       // local; X = forward
    public Vector3 inertiaTensor = new Vector3(35174.2f, 182376f, 152167f); // X = roll, Y = yaw, Z = pitch
    public float engineThrust = 75000f;
    public float elevatorIncidenceDeg = -7f;   // stabilizer mounting angle; negative = leading edge down
    public float initialTrim = 0f;       // elevator trim at start; negative = nose up (the -7 deg stabilizer incidence already trims level flight)
    public float initialThrottle = 0.2f; // 0..1; thrust is scaled by the throttle (Shift / Ctrl in keyboard mode)

    [Header("Rate damping (stability augmentation)")]
    public bool rateDamping = true;      // untick to compare with the bare airframe
    public float rollDamping  = 5e4f;    // N*m per rad/s about the forward axis
    public float pitchDamping = 3e5f;    // N*m per rad/s about the wing axis
    public float yawDamping   = 3e5f;    // N*m per rad/s about the vertical axis

    [Header("Start state")]
    public float startAltitude = 100f;
    public float startAoADeg = 4.5f;     // nose-up angle of attack at start (about trim for 120 m/s)
    public float startSpeed = 120f;      // m/s along the plane's forward axis (local +X)

    [Header("Debug")]
    public bool logState = true;
    public bool applyAero = true;        // untick to test the rigidbody without any aerodynamic force
    public bool drawDebugRays = true;
    public float debugRayScale = 0.0002f;

    private Engine engine;
    private List<Wing> wings;
    private InputSource source;

    [Header("Orbit Camera")]
    public float mouseSensitivity = 3f;
    public float orbitDistance = 15f;
    public float minOrbitDistance = 3f;
    public float maxOrbitDistance = 60f;
    public float zoomSensitivity = 5f;
    public float cameraSmoothTime = 10f;
    public bool invertOrbitY = false;

    private float cameraPitch;
    private float cameraYaw;
    private float targetOrbitDistance;
    private bool cameraFollow = true;

    private int step;
    private float lastThrust;

    // Wing indices
    const int LeftWing = 0, RightWing = 1, Elevator = 2, Rudder = 3;

    void Awake()
    {
        source = GetComponent<InputSource>();
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        // ---- Rigidbody setup (order matters: mass first, inertia last) ----
        if (source != null) source.input.trim = initialTrim;
        if (source != null) source.input.throttle = initialThrottle;

        rb.mass = mass;
        rb.centerOfMass = centerOfMass;
        rb.inertiaTensorRotation = Quaternion.identity;   // principal axes = body axes
        rb.inertiaTensor = inertiaTensor;
        rb.maxLinearVelocity = 500f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;   // smooth the visual between physics steps

        // The model assumes local +X is forward, so start level and facing +X
        Vector3 startPos = new Vector3(0f, startAltitude, 0f);
        Quaternion startRot = Quaternion.Euler(0f, 0f, startAoADeg);   // positive Z = nose up
        transform.SetPositionAndRotation(startPos, startRot);
        rb.position = startPos;                 // set the physics body itself, not just the transform
        rb.rotation = startRot;
        Physics.SyncTransforms();               // otherwise the body keeps its old pose until the first step,
                                                // and AddForceAtPosition computes torque from the wrong place
        rb.linearVelocity = Vector3.right * startSpeed;   // level flight path, nose pitched up by startAoADeg
        rb.angularVelocity = Vector3.zero;

        Debug.Log($"SETUP mass={rb.mass} com={rb.centerOfMass} inertia={rb.inertiaTensor} " +
                  $"inertiaRot={rb.inertiaTensorRotation.eulerAngles} lossyScale={transform.lossyScale} (must be 1,1,1) " +
                  $"linDamping={rb.linearDamping} angDamping={rb.angularDamping} maxLinVel={rb.maxLinearVelocity}");

        engine = new Engine(engineThrust, 1.0);
        engine.enableAfterburner = true;

        // ---- Airfoils ----
        Airfoil naca64a204 = new Airfoil(CSV_Utils.Read("Airfoils/NACA64A204"));
        Airfoil naca2412   = new Airfoil(CSV_Utils.Read("Airfoils/NACA2412"));
        Airfoil naca0012   = new Airfoil(CSV_Utils.Read("Airfoils/NACA0012"));

        // ---- Wings ----
        // Local frame: X = forward, Y = up, Z = LEFT (Unity is left-handed).
        // Wings sit 1 m behind the origin (0.3 m behind the COM at x = -0.7: stable),
        // tail surfaces 6.6 m behind.
        float wingOffset = -1.0f;
        float tailOffset = -6.6f;
        float inc = elevatorIncidenceDeg * Mathf.Deg2Rad;
        Vector3 elevatorNormal = new Vector3(-Mathf.Sin(inc), Mathf.Cos(inc), 0f);

        wings = new List<Wing>
        {
            new Wing(area: 6.96f, controlInput: 0f, span: 2.50f, flapRatio: 0.10f,
                     wingNormal: Vector3.up,   displacement: new Vector3(wingOffset, 0f, +2.35f), airfoil: naca2412),   // left
            new Wing(area: 6.96f, controlInput: 0f, span: 2.50f, flapRatio: 0.10f,
                     wingNormal: Vector3.up,   displacement: new Vector3(wingOffset, 0f, -2.35f), airfoil: naca2412),   // right
            new Wing(area: 6.54f, controlInput: 0f, span: 2.70f, flapRatio: 1.00f,
                     wingNormal: elevatorNormal, displacement: new Vector3(tailOffset, -0.1f, 0f),  airfoil: naca64a204), // elevator
            new Wing(area: 5.31f, controlInput: 0f, span: 3.10f, flapRatio: 0.15f,
                     wingNormal: Vector3.back, displacement: new Vector3(tailOffset, 0f, 0f),     airfoil: naca0012),   // rudder
        };


        if (playerCamera != null)
        {
            // Detach camera so aircraft rotation cannot affect it.
            playerCamera.transform.SetParent(null, true);

            Vector3 aircraftOrigin = transform.TransformPoint(Vector3.zero);
            Vector3 offset = playerCamera.transform.position - aircraftOrigin;

            if (offset.sqrMagnitude < 0.001f)
                offset = new Vector3(-10f, 5f, -10f);

            orbitDistance = Mathf.Clamp(
                offset.magnitude,
                minOrbitDistance,
                maxOrbitDistance
            );

            targetOrbitDistance = orbitDistance;

            Vector3 direction = offset.normalized;

            cameraYaw = Mathf.Atan2(direction.x, -direction.z) * Mathf.Rad2Deg;
            cameraPitch = Mathf.Asin(direction.y) * Mathf.Rad2Deg;
        }
    }

void Update()
{
    if (playerCamera == null) return;

    if (Input.GetKeyDown(KeyCode.C))
    {
        cameraFollow = !cameraFollow;
    }

    if (Input.GetMouseButton(0))
    {
        cameraYaw += Input.GetAxis("Mouse X") * mouseSensitivity;

        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        cameraPitch += invertOrbitY ? mouseY : -mouseY;

        cameraPitch = Mathf.Clamp(cameraPitch, -85f, 85f);
    }

    float scroll = Input.GetAxis("Mouse ScrollWheel");

    targetOrbitDistance = Mathf.Clamp(
        targetOrbitDistance - scroll * zoomSensitivity,
        minOrbitDistance,
        maxOrbitDistance
    );
}

void LateUpdate()
{
    if (playerCamera == null || !cameraFollow) return;

    Vector3 aircraftOrigin = transform.TransformPoint(Vector3.zero);

    Quaternion orbitRotation = Quaternion.Euler(
        cameraPitch,
        cameraYaw,
        0f
    );

    Vector3 orbitDirection = orbitRotation * Vector3.back;

    // Fixed distance from the aircraft origin.
    playerCamera.transform.position =
        aircraftOrigin + orbitDirection * orbitDistance;

    // Look at the aircraft origin.
    playerCamera.transform.rotation = Quaternion.LookRotation(
        aircraftOrigin - playerCamera.transform.position,
        Vector3.up
    );

    // Existing debug rays.
    if (!drawDebugRays || wings == null || rb == null) return;

    Debug.DrawRay(rb.transform.position, rb.transform.right * 10f, Color.white);

    foreach (var wing in wings)
        wing.DrawDebug(rb, debugRayScale);
}
    void FixedUpdate()
    {
        if (source == null || wings == null || rb == null) return;

        // Pitch rate BEFORE any aero force this step: shows whether something else spins the plane
        if (step < 6)
            Debug.Log($"step {step} start: q={transform.InverseTransformDirection(rb.angularVelocity).z:F3} " +
                      $"pos={transform.position} comWorld={rb.worldCenterOfMass} (expect x = pos.x - 0.7)");

        // Feed the latest input into the control surfaces every physics step
        FlightInput inp = source.input;
        wings[LeftWing].ControlInput  =  inp.aileron;
        wings[RightWing].ControlInput = -inp.aileron;
        wings[Elevator].ControlInput  =  inp.elevator + inp.trim;
        wings[Rudder].ControlInput    =  inp.rudder;

        // Thrust along the aircraft's forward axis (local +X)
        if (applyAero)
        {
            float thrust = Mathf.Abs((float)engine.computePropulsionForce()) * Mathf.Clamp01(source.input.throttle);   // forward only, scaled by throttle
            lastThrust = thrust;
            rb.AddForce(rb.rotation * Vector3.right * thrust);
            foreach (var wing in wings)
            {
                wing.Apply(rb);
            }

            if (rateDamping)
            {
                // local angular velocity: x = roll, y = yaw, z = pitch
                Vector3 wl = Quaternion.Inverse(rb.rotation) * rb.angularVelocity;
                Vector3 damp = new Vector3(-rollDamping * wl.x, -yawDamping * wl.y, -pitchDamping * wl.z);
                rb.AddTorque(rb.rotation * damp);
            }
        }

        step++;
        // every 5th physics step (0.1 s) for the first 3 s, then once per second
        if (logState && (step <= 150 ? step % 5 == 0 : step % 50 == 0)) LogState();
    }

    void LogState()
    {
        Vector3 vLocal = transform.InverseTransformDirection(rb.linearVelocity);
        Vector3 wLocal = transform.InverseTransformDirection(rb.angularVelocity);
        float pitchDeg = Mathf.Asin(Mathf.Clamp(transform.right.y, -1f, 1f)) * Mathf.Rad2Deg;   // nose above horizon
        float bankDeg = Mathf.Asin(Mathf.Clamp(transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;   // + = right bank

        float liftY = 0f;
        foreach (var w in wings) liftY += w.LastLift.y;
        float dragX = 0f;
        foreach (var w in wings) dragX += transform.InverseTransformDirection(w.LastDrag).x;

        // Pitch moment about the COM, local Z component, per surface [Nm]
        float[] mz = new float[wings.Count];
        for (int i = 0; i < wings.Count; i++)
        {
            Vector3 arm = transform.TransformPoint(wings[i].Displacement) - rb.worldCenterOfMass;
            Vector3 torque = Vector3.Cross(arm, wings[i].LastLift + wings[i].LastDrag);
            mz[i] = transform.InverseTransformDirection(torque).z;
        }

        Debug.Log($"t={Time.fixedTime:F2} pitch={pitchDeg:F1}deg p={wLocal.x:F2} q={wLocal.z:F2} r={wLocal.y:F2} bank={bankDeg:F0} |v|={rb.linearVelocity.magnitude:F1} vmax={rb.maxLinearVelocity:F0} vLocal={vLocal}\n" +
                  $"AoA wingL={wings[LeftWing].LastAoA:F1} wingR={wings[RightWing].LastAoA:F1} " +
                  $"elev={wings[Elevator].LastAoA:F1} rudder={wings[Rudder].LastAoA:F1}\n" +
                  $"Mz wings={mz[LeftWing] + mz[RightWing]:F0} elev={mz[Elevator]:F0}  " +
                  $"liftY={liftY:F0} weight={rb.mass * Physics.gravity.magnitude:F0}\n" +
                  $"in: ail={source.input.aileron:F2} ele={source.input.elevator:F2} rud={source.input.rudder:F2} trim={source.input.trim:F2} thr={source.input.throttle:F2}\n" +
                  $"thrust={lastThrust:F0} dragX={dragX:F0} CL wing={wings[LeftWing].LastCl:F2} " +
                  $"CD wing={wings[LeftWing].LastCd:F3} CD elev={wings[Elevator].LastCd:F3}");
    }
}