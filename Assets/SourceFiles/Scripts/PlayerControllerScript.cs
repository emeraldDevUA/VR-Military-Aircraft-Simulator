using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public struct Engine
{

    public Engine(double thrust, double throttle)
    {
        maxThrust = thrust;
        innerThrottle = throttle;
        enableAfterburner = false;
    }

    public double maxThrust { get; private set; }
    
    public double innerThrottle { get; set; }

    public bool enableAfterburner { get;  set; }

    public double computePropulsionForce()
    {
        double afterburner_multipler = enableAfterburner ? 1 : 0.5;

        return -innerThrottle * maxThrust * afterburner_multipler;
    }
}






public class PlayerControllerScript : MonoBehaviour
{
    [Header("References")]
    public Transform cameraPoint;
    public Camera playerCamera; // renamed from "camera" - that name shadows Component.camera and is deprecated
    public Rigidbody rigidbody;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;


    private Engine engine;


    public float mouseSensitivity = 3f;

    private float cameraPitch = 0f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        engine = new Engine(130000, 0.8);
        engine.enableAfterburner = true;
        
	List<Dictionary<string, object>> data = CSV_Utils.Read("Airfoils/NACA64A204");

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
    }

}
