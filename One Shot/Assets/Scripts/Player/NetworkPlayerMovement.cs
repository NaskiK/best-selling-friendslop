using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -20f;

    private CharacterController characterController;

    private float verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // Only the player who owns this network object
        // is allowed to process keyboard input.
        if (!IsOwner)
            return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        Vector2 input = Vector2.zero;

        if (keyboard.wKey.isPressed)
            input.y += 1f;

        if (keyboard.sKey.isPressed)
            input.y -= 1f;

        if (keyboard.dKey.isPressed)
            input.x += 1f;

        if (keyboard.aKey.isPressed)
            input.x -= 1f;

        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 movement =
            new Vector3(input.x, 0f, input.y);

        bool sprinting =
            keyboard.leftShiftKey.isPressed;

        float currentSpeed =
            sprinting ? sprintSpeed : walkSpeed;

        movement *= currentSpeed;

        // Gravity
        if (characterController.isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        // Jump
        if (keyboard.spaceKey.wasPressedThisFrame &&
            characterController.isGrounded)
        {
            verticalVelocity =
                Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;

        movement.y = verticalVelocity;

        characterController.Move(
            movement * Time.deltaTime
        );
    }
}