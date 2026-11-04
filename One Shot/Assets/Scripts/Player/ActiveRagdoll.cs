
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
    [Range(0f, 1f)]
    public float stability = 1f;

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

    [Header("Slap Chaos")]
    [SerializeField] private float chaosDuration = 1.8f;
    [Range(0f, 1f)]
    [SerializeField] private float chaosJointStrength = 0.03f;
    [Range(0f, 1f)]
    [SerializeField] private float chaosControlStrength = 0.01f;

    [SerializeField] private float bodySpinImpulse = 25f;
    [SerializeField] private float limbSpinImpulse = 10f;
    [SerializeField] private float extraLaunchImpulse = 2f;
    [SerializeField] private float extraUpwardImpulse = 2f;

    public Rigidbody Hips => hips;

    private class Bone
    {
        public ConfigurableJoint joint;
        public Transform target;
        public Quaternion startLocalRotation;
        public JointDrive originalSlerpDrive;
    }

    private readonly List<Bone> bones = new();
    private Rigidbody[] physicsBodies;

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

    private float chaosTimer;

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
                startLocalRotation = joint.transform.localRotation,
                originalSlerpDrive = joint.slerpDrive
            });
        }

        physicsBodies =
            physicsRoot.GetComponentsInChildren<Rigidbody>();

        foreach (var renderer in animatedRoot.GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = false;
        }

        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        foreach (var rb in physicsBodies)
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

        if (chaosTimer > 0f)
        {
            chaosTimer -= Time.fixedDeltaTime;

            if (chaosTimer <= 0f)
            {
                chaosTimer = 0f;
                SetChaosJointStrength(1f);
            }
        }

        bool inChaos = chaosTimer > 0f;

        float controlStrength = inChaos
            ? chaosControlStrength
            : 1f;

        bool grounded = CheckGround(out RaycastHit hit);

        Vector3 up =
            hips.transform.TransformDirection(hipsUpLocal);

        Vector3 forward = Vector3.ProjectOnPlane(
            hips.transform.TransformDirection(hipsForwardLocal),
            Vector3.up
        );

        Vector3 torque =
            Vector3.Cross(up, Vector3.up) *
            uprightStrength * controlStrength;

        if (forward.sqrMagnitude > 0.001f)
        {
            float yaw = Vector3.SignedAngle(
                forward.normalized,
                desiredDirection,
                Vector3.up
            ) * Mathf.Deg2Rad;

            torque += Vector3.up *
                yaw * turnStrength * controlStrength;
        }

        torque -= hips.angularVelocity *
            angularDamping * controlStrength;

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
                Vector3.up * push * stability * controlStrength,
                ForceMode.Acceleration
            );
        }

        // Horizontal movement.
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
            (desiredVelocity - planar) *
            responsiveness * control * controlStrength,
            ForceMode.Acceleration
        );

        // Begin jump preparation.
        if (jumpRequested && grounded &&
            !jumpPreparing && !inChaos)
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

        // Jump after preparation.
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

    private void SetChaosJointStrength(float multiplier)
    {
        foreach (var bone in bones)
        {
            if (bone.joint == null)
                continue;

            JointDrive drive = bone.originalSlerpDrive;
            drive.positionSpring *= multiplier;
            drive.positionDamper *= multiplier;

            bone.joint.slerpDrive = drive;
        }
    }

    private void TriggerSlapChaos(Vector3 direction)
    {
        chaosTimer = chaosDuration;
        SetChaosJointStrength(chaosJointStrength);

        Vector3 launchDirection = direction.sqrMagnitude > 0.001f
            ? direction.normalized
            : transform.forward;

        // Extra launch for a more exaggerated slap.
        hips.AddForce(
            launchDirection * extraLaunchImpulse +
            Vector3.up * extraUpwardImpulse,
            ForceMode.Impulse
        );

        // Spin the entire body in a random direction.
        hips.AddTorque(
            Random.onUnitSphere * bodySpinImpulse,
            ForceMode.Impulse
        );

        // Flail the arms, legs and torso independently.
        if (physicsBodies == null)
            return;

        foreach (Rigidbody body in physicsBodies)
        {
            if (body == null || body == hips || body.isKinematic)
                continue;

            body.AddTorque(
                Random.onUnitSphere *
                Random.Range(limbSpinImpulse * 0.4f, limbSpinImpulse),
                ForceMode.Impulse
            );
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
        if (!IsOwner || hips == null)
            return;

        Vector3 impulse =
            direction.normalized * horizontalForce +
            Vector3.up * verticalForce;

        hips.AddForce(impulse, ForceMode.Impulse);

        TriggerSlapChaos(direction);
    }
}
