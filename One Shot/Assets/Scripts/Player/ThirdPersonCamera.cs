using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private float targetHeight = 1.5f;

    [Header("Distance")]
    [SerializeField] private float distance = 7f;
    [SerializeField] private float minDistance = 3f;
    [SerializeField] private float maxDistance = 10f;

    [Header("Rotation")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.05f;

    private Transform target;

    private float yaw;
    private float pitch = 20f;

    private Vector3 currentVelocity;

    private void LateUpdate()
    {
        if (target == null)
        {
            FindLocalPlayer();

            if (target == null)
                return;
        }

        HandleMouseLook();
        UpdateCameraPosition();
    }

    private void FindLocalPlayer()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.LocalClient == null)
            return;

        if (NetworkManager.Singleton.LocalClient.PlayerObject == null)
            return;

        target =
            NetworkManager.Singleton.LocalClient.PlayerObject.transform;

        yaw = target.eulerAngles.y;

        // Now that the player exists, start controlling the camera.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void HandleMouseLook()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        Vector2 mouseDelta =
            mouse.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;

        pitch = Mathf.Clamp(
            pitch,
            minPitch,
            maxPitch
        );
    }

    private void UpdateCameraPosition()
    {
        Vector3 targetPosition =
            target.position +
            Vector3.up * targetHeight;

        Quaternion cameraRotation =
            Quaternion.Euler(pitch, yaw, 0f);

        Vector3 desiredPosition =
            targetPosition -
            cameraRotation * Vector3.forward * distance;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            positionSmoothTime
        );

        transform.rotation = cameraRotation;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            return;

        if (target != null)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}