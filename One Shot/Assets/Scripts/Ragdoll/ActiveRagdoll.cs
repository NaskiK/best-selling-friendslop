using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ActiveRagdoll : MonoBehaviour
{
    [Header("Rigs")]
    [SerializeField] private Transform animatedRoot;   // AnimatedRig
    [SerializeField] private Transform physicsRoot;    // PhysicsRig
    [SerializeField] private Animator animator;        // the one on AnimatedRig

    [Header("Joint strength")]
    [SerializeField] private float jointSpring = 1500f;
    [SerializeField] private float jointDamper = 60f;

    [Header("Balance")]
    [Range(0f, 1f)] public float stability = 1f;       // the drunk meter will drive this
    [SerializeField] private float uprightStrength = 300f;
    [SerializeField] private float turnStrength = 150f;
    [SerializeField] private float angularDamping = 15f;
    [SerializeField] private float rideSpring = 120f;
    [SerializeField] private float rideDamper = 12f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float responsiveness = 10f;
    [SerializeField] private float jumpSpeed = 6f;

    public Transform CameraTarget => Hips.transform;
    public Rigidbody Hips { get; private set; }

    private class Bone
    {
        public ConfigurableJoint joint;
        public Transform target;
        public Quaternion startLocalRotation;
    }

    private readonly List<Bone> bones = new();
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private Vector3 hipsUpLocal, hipsForwardLocal;
    private float rideHeight;
    private Vector3 moveDir, desiredDirection;
    private bool sprint, jumpRequested;

    // Right-click the component header -> run this once in the editor.
    [ContextMenu("Convert CharacterJoints to ConfigurableJoints")]
    private void ConvertJoints()
    {
        foreach (var old in physicsRoot.GetComponentsInChildren<CharacterJoint>())
        {
            GameObject go = old.gameObject;
            Rigidbody parentRb = old.connectedBody;
            DestroyImmediate(old);

            var j = go.AddComponent<ConfigurableJoint>();
            j.connectedBody = parentRb;
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Free;
            j.rotationDriveMode = RotationDriveMode.Slerp;
            j.projectionMode = JointProjectionMode.PositionAndRotation;
        }
    }

    private void Start()
    {
        // First Rigidbody in the hierarchy is the hips.
        Hips = physicsRoot.GetComponentInChildren<Rigidbody>();

        // Map each physics joint to the same-named bone on the animated rig.
        var animatedByName = new Dictionary<string, Transform>();
        foreach (var t in animatedRoot.GetComponentsInChildren<Transform>())
            animatedByName[t.name] = t;

        foreach (var joint in physicsRoot.GetComponentsInChildren<ConfigurableJoint>())
        {
            if (!animatedByName.TryGetValue(joint.name, out var target)) continue;

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

        foreach (var r in animatedRoot.GetComponentsInChildren<Renderer>())
            r.enabled = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // hidden meshes would otherwise stop animating

        foreach (var rb in physicsRoot.GetComponentsInChildren<Rigidbody>())
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.solverIterations = 12;
            rb.solverVelocityIterations = 4;
        }

        // Bone axes are arbitrary, so remember which hips axis is "up" and "forward".
        hipsUpLocal = Hips.transform.InverseTransformDirection(Vector3.up);
        hipsForwardLocal = Hips.transform.InverseTransformDirection(transform.forward);
        rideHeight = Hips.position.y - transform.position.y;
        desiredDirection = transform.forward;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || Camera.main == null) return;

        Vector2 input = new Vector2(
            (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
            (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
        input = Vector2.ClampMagnitude(input, 1f);

        Transform cam = Camera.main.transform;
        Vector3 f = cam.forward; f.y = 0f; f.Normalize();
        Vector3 r = cam.right; r.y = 0f; r.Normalize();

        moveDir = f * input.y + r * input.x;
        sprint = kb.leftShiftKey.isPressed;
        if (kb.spaceKey.wasPressedThisFrame) jumpRequested = true;
        if (moveDir.sqrMagnitude > 0.01f) desiredDirection = moveDir.normalized;

        if (animator.runtimeAnimatorController != null)
        {
            Vector3 v = Hips.linearVelocity; v.y = 0f;
            animator.SetFloat(SpeedHash, v.magnitude, 0.1f, Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        // 1. Joints chase the animated pose.
        foreach (var b in bones)
            b.joint.targetRotation =
                Quaternion.Inverse(b.target.localRotation) * b.startLocalRotation;

        bool grounded = CheckGround(out RaycastHit hit);

        // 2. Stay upright and face the movement direction.
        Vector3 up = Hips.transform.TransformDirection(hipsUpLocal);
        Vector3 fwd = Vector3.ProjectOnPlane(
            Hips.transform.TransformDirection(hipsForwardLocal), Vector3.up);

        Vector3 torque = Vector3.Cross(up, Vector3.up) * uprightStrength;
        if (fwd.sqrMagnitude > 0.001f)
        {
            float yaw = Vector3.SignedAngle(fwd.normalized, desiredDirection, Vector3.up) * Mathf.Deg2Rad;
            torque += Vector3.up * yaw * turnStrength;
        }
        torque -= Hips.angularVelocity * angularDamping;
        Hips.AddTorque(torque * stability, ForceMode.Acceleration);

        // 3. Ride-height spring: keeps the hips off the ground so the legs don't carry everything.
        Vector3 vel = Hips.linearVelocity;
        if (grounded && vel.y <= 1f)
        {
            float err = rideHeight - hit.distance;
            float push = Mathf.Max(0f, err * rideSpring - vel.y * rideDamper);
            Hips.AddForce(Vector3.up * push * stability, ForceMode.Acceleration);
        }

        // 4. Walk.
        Vector3 planar = new Vector3(vel.x, 0f, vel.z);
        Vector3 desired = moveDir * (sprint ? sprintSpeed : walkSpeed);
        float control = grounded ? 1f : 0.2f;
        Hips.AddForce((desired - planar) * responsiveness * control, ForceMode.Acceleration);

        // 5. Jump.
        if (jumpRequested && grounded)
        {
            vel.y = jumpSpeed;
            Hips.linearVelocity = vel;
        }
        jumpRequested = false;
    }

    private bool CheckGround(out RaycastHit best)
    {
        best = default;
        bool found = false;
        float closest = float.MaxValue;

        var hits = Physics.RaycastAll(Hips.position, Vector3.down, rideHeight + 0.4f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(transform)) continue; // ignore our own bones
            if (h.distance < closest) { closest = h.distance; best = h; found = true; }
        }
        return found;
    }
}