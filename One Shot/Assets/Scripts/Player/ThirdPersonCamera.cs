using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private float targetHeight = 0.5f;   // now measured from the hips, not the feet

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

    [Header("Offline test only")]
    [SerializeField] private Transform overrideTarget;    // drag the hips here in RagdollTest


    private Transform target;
    private float yaw;
    private float pitch = 20f;
    private Vector3 currentVelocity;

    private void LateUpdate()
    {
        // first lines of LateUpdate:
        if (target == null && overrideTarget != null)
        {
            target = overrideTarget;
            yaw = target.eulerAngles.y;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        HandleMouseLook();
        UpdateCameraPosition();
    }

    private void FindTarget()
    {

        // Offline test scene: use the assigned hips.
        if (overrideTarget != null)
        {
            SetTarget(overrideTarget);
            return;
        }

        // Real game: follow the local player's hips.
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.LocalClient == null) return;

        NetworkObject playerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
        if (playerObject == null) return;

        var ragdoll = playerObject.GetComponent<ActiveRagdoll>();
        if (ragdoll != null)
        {
            if (ragdoll.Hips == null) return;   // its Start() hasn't run yet, try again next frame
            SetTarget(ragdoll.CameraTarget);
        }
        else
        {
            SetTarget(playerObject.transform);
        }
    }

    private void SetTarget(Transform newTarget)
    {
        target = newTarget;
        yaw = target.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void HandleMouseLook()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mouseDelta = mouse.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void UpdateCameraPosition()
    {
        Vector3 targetPosition = target.position + Vector3.up * targetHeight;
        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = targetPosition - cameraRotation * Vector3.forward * distance;

        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPosition, ref currentVelocity, positionSmoothTime);
        transform.rotation = cameraRotation;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) return;

        if (target != null)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}