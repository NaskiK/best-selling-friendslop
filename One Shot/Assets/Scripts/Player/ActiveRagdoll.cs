
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class ActiveRagdoll : NetworkBehaviour
{
    [Header("Rigs")]
    [SerializeField] private Transform animatedRoot;
    [SerializeField] private Transform physicsRoot;
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody hips;

    [Header("Joint strength")]
    [SerializeField] private float jointSpring = 1500f;
    [SerializeField] private float jointDamper = 60f;

    [Header("Balance")]
    [Range(0f, 1f)] public float stability = 1f;
    [SerializeField] private float uprightStrength = 10000f;
    [SerializeField] private float turnStrength = 61f;
    [SerializeField] private float angularDamping = 27f;
    [SerializeField] private float rideSpring = 250f;
    [SerializeField] private float rideDamper = 30f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float responsiveness = 10f;
    [SerializeField] private float jumpSpeed = 6f;

    [Header("Jump Animation")]
    [SerializeField] private float jumpPreparationTime = 0.25f;

    [Range(0f, 1f)]
    [SerializeField] private float jumpPreparationSpeedMultiplier = 0.25f;

    [Header("Spawn")]
    [SerializeField] private float spawnSpacing = 1.5f;

    public Rigidbody Hips => hips;

    private class Bone
    {
        public ConfigurableJoint joint;
        public Transform target;
        public Quaternion startLocalRotation;
    }

    private readonly List<Bone> bones = new();

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int JumpHash =
        Animator.StringToHash("Jump");

    private Vector3 hipsUpLocal;
    private Vector3 hipsForwardLocal;
    private Vector3 lastHipsPos;
    private float rideHeight;

    private Vector3 moveDir;
    private Vector3 desiredDirection;

    private bool sprint;
    private bool jumpRequested;
    private bool jumpPreparing;
    private float jumpPreparationTimer;

    [ContextMenu("Convert CharacterJoints to ConfigurableJoints")]
    private void ConvertJoints()
    {
        foreach (var old in physicsRoot.GetComponentsInChildren<CharacterJoint>())
        {
            GameObject go = old.gameObject;
            Rigidbody parentRb = old.connectedBody;

            DestroyImmediate(old);

            var joint = go.AddComponent<ConfigurableJoint>();
            joint.connectedBody = parentRb;

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            transform.position +=
                new Vector3(OwnerClientId * spawnSpacing, 0f, 0f);
        }

        var animatedByName = new Dictionary<string, Transform>();

        foreach (var bone in animatedRoot.GetComponentsInChildren<Transform>())
        {
            animatedByName[bone.name] = bone;
        }

        foreach (var joint in physicsRoot.GetComponentsInChildren<ConfigurableJoint>())
        {
            if (!animatedByName.TryGetValue(joint.name, out var target))
                continue;

            joint.slerpDrive = new JointDrive
            {
                positionSpring = jointSpring,
                positionDamper = jointDamper,
                maximumForce = float.MaxValue
            };

            bones.Add(new Bone
            {
                joint = joint,
                target = target,
                startLocalRotation = joint.transform.localRotation
            });
        }

        foreach (var renderer in animatedRoot.GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = false;
        }

        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        foreach (var rb in physicsRoot.GetComponentsInChildren<Rigidbody>())
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.solverIterations = 12;
            rb.solverVelocityIterations = 4;
        }

        hips.isKinematic = !IsOwner;

        hips.interpolation = IsOwner
            ? RigidbodyInterpolation.Interpolate
            : RigidbodyInterpolation.None;

        hipsUpLocal =
            hips.transform.InverseTransformDirection(Vector3.up);

        hipsForwardLocal =
            hips.transform.InverseTransformDirection(transform.forward);

        rideHeight = hips.position.y - transform.position.y;
        desiredDirection = transform.forward;
        lastHipsPos = hips.transform.position;
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        Vector3 pos = hips.transform.position;
        Vector3 delta = pos - lastHipsPos;
        delta.y = 0f;
        lastHipsPos = pos;

        float animSpeed = Time.deltaTime > 0f
            ? delta.magnitude / Time.deltaTime
            : 0f;

        if (IsOwner)
        {
            var keyboard = Keyboard.current;

            if (keyboard != null && Camera.main != null)
            {
                Vector2 input = new Vector2(
                    (keyboard.dKey.isPressed ? 1f : 0f) -
                    (keyboard.aKey.isPressed ? 1f : 0f),

                    (keyboard.wKey.isPressed ? 1f : 0f) -
                    (keyboard.sKey.isPressed ? 1f : 0f)
                );

                input = Vector2.ClampMagnitude(input, 1f);

                Transform cam = Camera.main.transform;

                Vector3 forward = cam.forward;
                forward.y = 0f;
                forward.Normalize();

                Vector3 right = cam.right;
                right.y = 0f;
                right.Normalize();

                moveDir = forward * input.y + right * input.x;

                sprint = keyboard.leftShiftKey.isPressed;

                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    jumpRequested = true;
                }

                if (moveDir.sqrMagnitude > 0.01f)
                {
                    desiredDirection = moveDir.normalized;
                }
            }
            else
            {
                moveDir = Vector3.zero;
            }

            float speedMultiplier = jumpPreparing
                ? jumpPreparationSpeedMultiplier
                : 1f;

            animSpeed = moveDir.magnitude *
                (sprint ? sprintSpeed : walkSpeed) *
                speedMultiplier;
        }

        if (animator.runtimeAnimatorController != null)
        {
            animator.SetFloat(
                SpeedHash,
                animSpeed,
                0.1f,
                Time.deltaTime
            );
        }
    }

    private void FixedUpdate()
    {
        if (!IsSpawned)
            return;

        foreach (var bone in bones)
        {
            bone.joint.targetRotation =
                Quaternion.Inverse(bone.target.localRotation) *
                bone.startLocalRotation;
        }

        if (!IsOwner)
            return;

        bool grounded = CheckGround(out RaycastHit hit);

        Vector3 up =
            hips.transform.TransformDirection(hipsUpLocal);

        Vector3 forward = Vector3.ProjectOnPlane(
            hips.transform.TransformDirection(hipsForwardLocal),
            Vector3.up
        );

        Vector3 torque =
            Vector3.Cross(up, Vector3.up) * uprightStrength;

        if (forward.sqrMagnitude > 0.001f)
        {
            float yaw = Vector3.SignedAngle(
                forward.normalized,
                desiredDirection,
                Vector3.up
            ) * Mathf.Deg2Rad;

            torque += Vector3.up * yaw * turnStrength;
        }

        torque -= hips.angularVelocity * angularDamping;

        hips.AddTorque(torque * stability, ForceMode.Acceleration);

        // Ride-height spring.
        Vector3 velocity = hips.linearVelocity;

        if (grounded && velocity.y <= 1f)
        {
            float error = rideHeight - hit.distance;

            float push = Mathf.Max(
                0f,
                error * rideSpring - velocity.y * rideDamper
            );

            hips.AddForce(
                Vector3.up * push * stability,
                ForceMode.Acceleration
            );
        }

        // Horizontal movement, slowed during jump preparation.
        Vector3 planar = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        float preparationMultiplier = jumpPreparing
            ? jumpPreparationSpeedMultiplier
            : 1f;

        Vector3 desiredVelocity =
            moveDir *
            (sprint ? sprintSpeed : walkSpeed) *
            preparationMultiplier;

        float control = grounded ? 1f : 0.2f;

        hips.AddForce(
            (desiredVelocity - planar) * responsiveness * control,
            ForceMode.Acceleration
        );

        // Begin jump preparation when Space is pressed.
        if (jumpRequested && grounded && !jumpPreparing)
        {
            jumpPreparing = true;
            jumpPreparationTimer = jumpPreparationTime;

            if (animator != null &&
                animator.runtimeAnimatorController != null)
            {
                animator.SetTrigger(JumpHash);
            }

            PlayJumpAnimationServerRpc();
        }

        jumpRequested = false;

        // Wait for the preparation animation before takeoff.
        if (jumpPreparing)
        {
            jumpPreparationTimer -= Time.fixedDeltaTime;

            if (jumpPreparationTimer <= 0f)
            {
                jumpPreparing = false;

                if (CheckGround(out _))
                {
                    Vector3 jumpVelocity = hips.linearVelocity;
                    jumpVelocity.y = jumpSpeed;
                    hips.linearVelocity = jumpVelocity;
                }
            }
        }
    }

    [ServerRpc]
    private void PlayJumpAnimationServerRpc()
    {
        PlayJumpAnimationClientRpc();
    }

    [ClientRpc]
    private void PlayJumpAnimationClientRpc()
    {
        if (IsOwner)
            return;

        if (animator == null ||
            animator.runtimeAnimatorController == null)
            return;

        animator.SetTrigger(JumpHash);
    }

    private bool CheckGround(out RaycastHit best)
    {
        best = default;

        bool found = false;
        float closest = float.MaxValue;

        RaycastHit[] hits = Physics.RaycastAll(
            hips.position,
            Vector3.down,
            rideHeight + 0.4f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (var hit in hits)
        {
            if (hit.transform.IsChildOf(transform))
                continue;

            if (hit.distance < closest)
            {
                closest = hit.distance;
                best = hit;
                found = true;
            }
        }

        return found;
    }
    
   [ClientRpc]
public void ApplySlapKnockbackClientRpc(
    Vector3 direction,
    float horizontalForce,
    float verticalForce)
{
    Debug.Log(
        $"[Slap Knockback] RPC received. IsOwner={IsOwner}, " +
        $"ClientId={NetworkManager.LocalClientId}, HipsNull={hips == null}",
        this);

    // Only the owning client simulates this player's physical ragdoll.
    if (!IsOwner || hips == null)
        return;

    Vector3 impulse =
        direction.normalized * horizontalForce +
        Vector3.up * verticalForce;

    Debug.Log(
        $"[Slap Knockback] Applying impulse {impulse}. " +
        $"Before velocity={hips.linearVelocity}",
        this);

    hips.AddForce(impulse, ForceMode.Impulse);
    Debug.Log(
    $"[Slap Knockback] After impulse: velocity={hips.linearVelocity}, " +
    $"kinematic={hips.isKinematic}",
    this);
}

}
