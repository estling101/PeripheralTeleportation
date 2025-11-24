using UnityEngine;

public class XRKeyboardMover : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f;

    [Header("Rotation Settings")]
    public float rotateSpeed = 90f;   // degrees per second

    void Update()
    {
        //
        // MOVEMENT (WASD)
        //
        float horizontal = Input.GetAxis("Horizontal");   // A/D
        float vertical = Input.GetAxis("Vertical");       // W/S

        Vector3 direction = new Vector3(horizontal, 0f, vertical);
        Vector3 movement = transform.TransformDirection(direction) * moveSpeed * Time.deltaTime;

        transform.position += movement;

        //
        // ROTATION (Arrow Keys)
        //
        float rotateInput = 0f;

        if (Input.GetKey(KeyCode.LeftArrow))
            rotateInput = -1f;
        if (Input.GetKey(KeyCode.RightArrow))
            rotateInput = 1f;

        if (rotateInput != 0f)
        {
            float rotationAmount = rotateInput * rotateSpeed * Time.deltaTime;
            transform.Rotate(0f, rotationAmount, 0f);
        }
    }
}
