using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Objetivos")]
    [SerializeField] private Transform playerBody;

    [Header("Ajustes cámara")]
    [SerializeField] private float mouseSensitivity = 180f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;

    private float yaw;
    private float pitch;

    private void Start()
    {
        Vector3 startRotation = transform.eulerAngles;
        yaw = startRotation.y;
        pitch = startRotation.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        yaw += mouseX * mouseSensitivity * Time.deltaTime;
        pitch -= mouseY * mouseSensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}