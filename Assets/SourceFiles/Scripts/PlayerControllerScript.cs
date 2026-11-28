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
    public Vector3 inertiaTensor = new Vector3(51068.2f, 142238f, 105310f); // X = roll, Y = yaw, Z = pitch
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
    public float startSpeed = 320f;      // m/s along the plane's forward axis (local +X)

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

    private Gun gun = new Gun(new Vector3(0.1f, 0.1f, -2.0f), new Vector3(0, 0, 1), 125);
    private Airplane plane;

    // Wing indices
    const int LeftWing = 0, RightWing = 1, Elevator = 2, Rudder = 3;


    [Header("Debug HUD")]
    public bool showHud = true;
    public KeyCode hudToggleKey = KeyCode.F3;

    private Vector3 hudWorldVel, hudLocalVel, hudLocalAngVelDeg;
    private Vector3 hudAccel, hudLastVel;
    private float hudSpeed, hudG, hudGSmooth, hudAoA, hudSideslip;

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
        float wingOffset = -0.6f;
        float tailOffset = -4.2f;

        float inc = elevatorIncidenceDeg * Mathf.Deg2Rad;
        Vector3 elevatorNormal = new Vector3(-Mathf.Sin(inc), Mathf.Cos(inc), 0f);

        plane = new Airplane(engine, gun, rb);

        plane.AddWing(WingRole.RightWing, new Wing(area: 6.96f, controlInput: 0f, span: 2.50f, flapRatio: 0.10f,
              wingNormal: Vector3.up,   displacement: new Vector3(wingOffset, 0f, -2.35f), airfoil: naca2412));

        plane.AddWing(WingRole.LeftWing, new Wing(area: 6.96f, controlInput: 0f, span: 2.50f, flapRatio: 0.10f,
              wingNormal: Vector3.up,   displacement: new Vector3(wingOffset, 0f, +2.35f), airfoil: naca2412));

        plane.AddWing(WingRole.ElevatorWing, new Wing(area: 6.54f, controlInput: 0f, span: 2.70f, flapRatio: 1.00f,
              wingNormal: elevatorNormal, displacement: new Vector3(tailOffset, -0.1f, 0f),  airfoil: naca64a204));

        plane.AddWing(WingRole.RudderWing,  new Wing(area: 5.31f, controlInput: 0f, span: 3.10f, flapRatio: 0.15f,
              wingNormal: Vector3.back, displacement: new Vector3(tailOffset, 0f, 0f), airfoil: naca0012));

        plane.AddWing(WingRole.Hull,  new Wing(area: 15.0f/3, controlInput: 0f, span:  4.8f/3f, flapRatio: 0f,
              wingNormal: Vector3.up, displacement: new Vector3(0.0f, 0f, -0.0f), airfoil: naca0012));


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

        hudLastVel = rb.linearVelocity;
    }

void Update()
{
    if (Input.GetKeyDown(hudToggleKey)) showHud = !showHud;

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
    Debug.Log($"InputSource mode = {source.mode}");
}


float aMin, aMax, rMin, rMax;

void LateUpdate()
{
    if (playerCamera == null || !cameraFollow) return;

    Vector3 aircraftOrigin = transform.TransformPoint(Vector3.zero);

    Quaternion orbitRotation = Quaternion.Euler(
        cameraPitch,
        cameraYaw,
        0f
    );

    Vector3 orbitDirection = orbitRotation * Vector3.forward;

    // Fixed distance from the aircraft origin.
    playerCamera.transform.position =
        aircraftOrigin + orbitDirection * orbitDistance;

    // Look at the aircraft origin.
    playerCamera.transform.rotation = Quaternion.LookRotation(
        aircraftOrigin - playerCamera.transform.position,
        Vector3.up
    );

    float a = Input.GetAxisRaw(source.aileronAxis);
    float r = Input.GetAxisRaw(source.rudderAxis);
    aMin = Mathf.Min(aMin, a); aMax = Mathf.Max(aMax, a);
    rMin = Mathf.Min(rMin, r); rMax = Mathf.Max(rMax, r);
    if (Time.frameCount % 30 == 0)
        Debug.Log($"ail raw {a:F2} [{aMin:F2}..{aMax:F2}]  rud raw {r:F2} [{rMin:F2}..{rMax:F2}]");
}
    void FixedUpdate()
    {
         FlightInput inp = source.input;

        if (applyAero)
        {
            float thrust = Mathf.Abs((float)engine.computePropulsionForce()) * Mathf.Clamp01(source.input.throttle);   // forward only, scaled by throttle
            lastThrust = thrust;

            plane.ComputeForces(inp);
            if (rateDamping)
            {
                // local angular velocity: x = roll, y = yaw, z = pitch
                Vector3 wl = Quaternion.Inverse(rb.rotation) * rb.angularVelocity;
                Vector3 damp = new Vector3(-rollDamping * wl.x, -yawDamping * wl.y, -pitchDamping * wl.z);
                rb.AddTorque(rb.rotation * damp);
            }
        }
         UpdateHudData();

    }

void UpdateHudData()
{
    float dt = Time.fixedDeltaTime;
    Quaternion inv = Quaternion.Inverse(rb.rotation);

    hudWorldVel = rb.linearVelocity;
    hudLocalVel = inv * hudWorldVel;
    hudSpeed = hudWorldVel.magnitude;

    // world rad/s -> local deg/s (x = roll, y = yaw, z = pitch)
    hudLocalAngVelDeg = inv * rb.angularVelocity * Mathf.Rad2Deg;

    // G-load as felt by the pilot (includes gravity), in body axes
    hudAccel = (hudWorldVel - hudLastVel) / dt;
    hudLastVel = hudWorldVel;
    Vector3 specific = inv * (hudAccel - Physics.gravity);
    hudG = specific.y / 9.81f;                         // positive = pushed into the seat (pulling up)
    hudGSmooth = Mathf.Lerp(hudGSmooth, hudG, 0.1f);

    // Local +X is forward. AoA positive = nose above the flight path.
    if (hudLocalVel.x > 0.1f)
    {
        hudAoA      = Mathf.Atan2(-hudLocalVel.y, hudLocalVel.x) * Mathf.Rad2Deg;
        hudSideslip = Mathf.Atan2(-hudLocalVel.z, hudLocalVel.x) * Mathf.Rad2Deg; // +Z is left, so positive = drifting right
    }
    else hudAoA = hudSideslip = 0f;
}

static string Fmt(Vector3 v) => $"({v.x,7:F1}, {v.y,7:F1}, {v.z,7:F1})";

void OnGUI()
{
    if (!showHud || rb == null) return;

    string text =
        $"Speed:      {hudSpeed,7:F1} m/s  ({hudSpeed * 3.6f:F0} km/h)\n" +
        $"Altitude:   {rb.position.y,7:F1} m\n" +
        $"Vel world:  {Fmt(hudWorldVel)}\n" +
        $"Vel local:  {Fmt(hudLocalVel)}\n" +
        $"AngVel local (deg/s)\n" +
        $"  roll {hudLocalAngVelDeg.x,7:F1}  yaw {hudLocalAngVelDeg.y,7:F1}  pitch {hudLocalAngVelDeg.z,7:F1}\n" +
        $"Accel world: {Fmt(hudAccel)}\n" +
        $"G-load:     {hudGSmooth,7:F2} g\n" +
        $"AoA / Slip: {hudAoA,6:F1}° / {hudSideslip,6:F1}°\n" +
        $"Throttle:   {source.input.throttle,5:F2}   Trim: {source.input.trim,5:F2}\n" +
        $"Thrust:     {lastThrust,8:F0} N\n" +
        $"Mass {rb.mass:F0} kg  LinDamp {rb.linearDamping:F2}  AngDamp {rb.angularDamping:F2}";

    GUI.Box(new Rect(10, 10, 400, 235), GUIContent.none);
    GUI.Label(new Rect(18, 14, 385, 230), text);
}

void OnDrawGizmos()
{
    if (!drawDebugRays || rb == null || !Application.isPlaying) return;

    Vector3 com = rb.worldCenterOfMass;

    Gizmos.color = Color.green;                       // velocity
    Gizmos.DrawRay(com, hudWorldVel * 0.1f);

    Gizmos.color = Color.red;                         // angular velocity axis
    Gizmos.DrawRay(com, rb.angularVelocity * 3f);

    Gizmos.color = Color.yellow;                      // center of mass
    Gizmos.DrawSphere(com, 0.1f);
}
}