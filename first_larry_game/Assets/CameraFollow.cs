using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    public Transform target;       // assign the Player transform
    public Vector3 offset = new Vector3(0, 3f, -6f);
    public float followSpeed = 10f;
    public float rotationSpeed = 70f; // degrees per second for mouse orbit
    public float minPitch = -20f;
    public float maxPitch = 70f;

    private float yaw = 0f;
    private float pitch = 20f;

    void LateUpdate()
    {
        if (target == null) return;

        // --- Mouse look ---
        if (Mouse.current != null)
        {
            float mouseX = Mouse.current.delta.x.ReadValue();
            float mouseY = Mouse.current.delta.y.ReadValue();

            yaw += mouseX * rotationSpeed * Time.deltaTime;
            pitch -= mouseY * rotationSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // --- Compute rotation ---
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);

        // --- Desired position behind player ---
        Vector3 desiredPos = target.position + rotation * offset;

        // --- Smooth follow ---
        transform.position = Vector3.Lerp(transform.position, desiredPos, followSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 1.5f); // look slightly above the feet
    }
}
