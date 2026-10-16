using UnityEngine;

public class PlanetCameraController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform target;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Transform pitchPivot;

    [Header("Mouse")]
    [SerializeField] private float mouseSensitivity = 180f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 75f;

    [Header("Suavizado")]
    [SerializeField] private float gravityAlignSpeed = 12f;

    private float pitch;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (target == null || playerMovement == null || pitchPivot == null)
            return;

        Vector3 upAxis = playerMovement.GetUpAxis();

        // 1) seguir al jugador
        transform.position = target.position;

        // 2) alinear la cámara con la gravedad local
        Quaternion targetRotation =
            Quaternion.FromToRotation(transform.up, upAxis) * transform.rotation;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            gravityAlignSpeed * Time.deltaTime
        );

        // 3) yaw horizontal alrededor del "up" local de la cámara
        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(transform.up, mouseX * mouseSensitivity * Time.deltaTime, Space.World);

        // 4) pitch vertical en el pivot hijo
        float mouseY = Input.GetAxis("Mouse Y");
        float yInput = invertY ? -mouseY : mouseY;

        pitch += yInput * mouseSensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}