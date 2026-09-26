using UnityEngine;

public class PlayerControllerScript : MonoBehaviour
{
    [Header("References")]
    public Transform cameraPoint;
    public Camera playerCamera; // renamed from "camera" - that name shadows Component.camera and is deprecated
    public Rigidbody rigidbody;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {



        Vector3 transformVector = new Vector3(0, 0, 0);
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
}