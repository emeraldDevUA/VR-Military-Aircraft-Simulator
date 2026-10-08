using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class PlayerControllerScript : MonoBehaviour
{
    [Header("References")]
    public Transform cameraPoint;
    public Camera playerCamera;
    public Rigidbody rigidbody;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;

    private Engine engine;

    public float mouseSensitivity = 3f;

    private float cameraPitch = 0f;
    private List<Wing> wings;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        engine = new Engine(75000, 0.8);
        engine.enableAfterburner = true;
        
	    List<Dictionary<string, object>> data = CSV_Utils.Read("Airfoils/NACA64A204");
        Airfoil naca64a204 = new Airfoil(data);
        foreach (var row in data)
        {
            Debug.Log($"Alpha: {row["alpha"]}, CL: {row["cl"]}, CD: {row["cd"]}");
        }

        // Unity is X = right, Y = up, Z = forward.
        // The original C++ used X = forward, Z = right, so I swapped the components:
        // original (x, y, z) -> Unity (z, y, x)
        float wingOffset = -1.0f;
        float tailOffset = -6.6f;

       wings = new List<Wing>
       {
           new Wing(area: 6.96f, controlInput: 0f, span: 2.50f, flapRatio: 0.10f,
                    wingNormal: Vector3.up,    displacement: new Vector3(-2.7f,  0.0f, wingOffset), airfoil: naca64a204), // left wing
           new Wing(area: 6.96f, controlInput: 0f, span: 2.50f, flapRatio: 0.10f,
                    wingNormal: Vector3.up,    displacement: new Vector3(+2.7f,  0.0f, wingOffset), airfoil: naca64a204), // right wing
           new Wing(area: 6.54f, controlInput: 0f, span: 2.70f, flapRatio: 1.00f,
                    wingNormal: Vector3.up,    displacement: new Vector3( 0.0f, -0.1f, tailOffset), airfoil: naca64a204),   // elevator
//            new Wing(area: 5.31f, controlInput: 0f, span: 3.10f, flapRatio: 0.15f,
//                     wingNormal: Vector3.right, displacement: new Vector3( 0.0f,  0.0f, tailOffset), airfoil: naca64a204),   // rudder
       };

    }

    // Update is called once per frame
    void Update()
    {
        Vector3 transformVector = new Vector3(0, 0, 0);
        if (Input.GetMouseButton(0))
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // Camera rotates horizontally
            cameraPoint.Rotate(Vector3.up * mouseX, Space.World);

            // Camera rotates vertically
            cameraPitch -= mouseY;
            cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);

            cameraPoint.localRotation = Quaternion.Euler(
                cameraPitch,
                cameraPoint.localEulerAngles.y,
                0f
            );
        }
        // Raw input
        // Explicit WASD checks instead of the virtual "Horizontal"/"Vertical" axes
        float inputX = 0f;
        float inputZ = 0f;

      
        
        if (Input.GetKey(KeyCode.W)) transformVector.x -= 2f;
        if (Input.GetKey(KeyCode.S)) transformVector.x += 1f;
        if (Input.GetKey(KeyCode.D)) transformVector.z += 1f;
        if (Input.GetKey(KeyCode.A)) transformVector.z -= 1f;

  

        if (transformVector.sqrMagnitude > 0.001f)
        {
            // Move directly via the transform - no physics involved
            transform.position += transformVector * moveSpeed * Time.deltaTime;

          }
    }

    void FixedUpdate()
    {

        float thrust = (float)engine.computePropulsionForce();
        rigidbody.AddForce(transform.right * thrust);

        foreach (var wing in wings){
            wing.Apply(rigidbody);
            Debug.Log($"speed={rigidbody.linearVelocity.magnitude:F1} lift={wing.LastLift.magnitude:F0} drag={wing.LastDrag.magnitude:F0} aoa={wing.LastAoA:F1}");
            wing.DrawDebug(rigidbody);
            }


    }


}
