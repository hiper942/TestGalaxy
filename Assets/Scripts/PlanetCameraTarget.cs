using UnityEngine;

public class PlanetCameraTarget : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private PlayerMovement motor;

    [Header("Mouse")]
    [SerializeField] private float mouseSensitivity = 180f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 75f;

    private float pitch;
    private float yaw;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = 0f;
    }

    private void LateUpdate()
    {
        Vector3 upAxis = motor.GetUpAxis();

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        float yInput = invertY ? -mouseY : mouseY;

        yaw += mouseX * mouseSensitivity * Time.deltaTime;
        pitch += yInput * mouseSensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion yawRotation = Quaternion.AngleAxis(yaw, upAxis);

        Vector3 rightAxis = yawRotation * Vector3.right;
        Quaternion pitchRotation = Quaternion.AngleAxis(pitch, rightAxis);

        transform.rotation = yawRotation * pitchRotation;

        transform.position = player.position;
    }
}