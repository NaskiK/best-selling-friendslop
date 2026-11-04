using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private float targetHeight = 0.5f;   // above the hips

    [Header("Distance")]
    [SerializeField] private float distance = 4f;
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 8f;
    [SerializeField] private float zoomStep = 0.5f;
    [SerializeField] private float zoomSmoothing = 10f;

    [Header("First Person")]
    [SerializeField] private float firstPersonHeight = 0.8f; // Height offset for first person view (closer to head)
    private bool isFirstPerson = false;

    [Header("Rotation")]
    [SerializeField] private float mouseSensitivity = 0.25f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.03f;

    [Header("Collision")]
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float collisionPadding = 0.1f;

    private Transform target;
    private Transform playerRoot;

    private float yaw;
    private float pitch = 15f;
    private float currentDistance;
    private Vector3 currentVelocity;

    private void LateUpdate()
    {
        if (target == null)
        {
            FindLocalPlayer();

            if (target == null)
                return;
        }

        HandleViewToggle();
        HandleMouseLook();
        if (!isFirstPerson)
        {
            HandleZoom();
        }
        UpdateCameraPosition();
    }

    private void HandleViewToggle()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f5Key.wasPressedThisFrame)
        {
            isFirstPerson = !isFirstPerson;
        }
    }

    private void FindLocalPlayer()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.LocalClient == null)
            return;

        NetworkObject playerObject =
            NetworkManager.Singleton.LocalClient.PlayerObject;

        if (playerObject == null)
            return;

        playerRoot = playerObject.transform;

        // The root doesn't move with the active ragdoll, so follow the hips.
        var ragdoll = playerObject.GetComponent<ActiveRagdoll>();

        target = ragdoll != null
            ? ragdoll.Hips.transform
            : playerObject.transform;

        yaw = playerRoot.eulerAngles.y;
        currentDistance = distance;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void HandleMouseLook()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        Vector2 mouseDelta = mouse.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void HandleZoom()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        float scroll = mouse.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance = Mathf.Clamp(
                distance - Mathf.Sign(scroll) * zoomStep,
                minDistance,
                maxDistance
            );
        }
    }

    private void UpdateCameraPosition()
    {
        float activeHeight = isFirstPerson ? firstPersonHeight : targetHeight;
        Vector3 targetPosition = target.position + Vector3.up * activeHeight;

        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);

        if (isFirstPerson)
        {
            // In first person, snap directly to the target position without distance or collision checks
            currentDistance = 0f;
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref currentVelocity,
                positionSmoothTime
            );
            transform.rotation = cameraRotation;
            return;
        }

        Vector3 backward = cameraRotation * Vector3.back;

        // Find the nearest obstacle behind the player (ignoring our own bones).
        float wanted = distance;

        RaycastHit[] hits = Physics.SphereCastAll(
            targetPosition,
            collisionRadius,
            backward,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (var h in hits)
        {
            if (h.distance <= 0f)
                continue;   // already overlapping at the start, ignore

            if (playerRoot != null && h.transform.IsChildOf(playerRoot))
                continue;

            wanted = Mathf.Min(wanted, h.distance - collisionPadding);
        }

        wanted = Mathf.Max(wanted, 0.5f);

        // Snap in quickly when blocked, ease back out when clear.
        if (wanted < currentDistance)
            currentDistance = wanted;
        else
            currentDistance = Mathf.Lerp(
                currentDistance,
                wanted,
                1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime)
            );

        Vector3 desiredPosition =
            targetPosition + backward * currentDistance;

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