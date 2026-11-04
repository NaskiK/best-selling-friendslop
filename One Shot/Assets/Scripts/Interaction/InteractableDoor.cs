using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(MeshRenderer))]
public class InteractableDoor : NetworkBehaviour
{
    public enum Mode { Swing, Smash }

    [Header("Mode")]
    [SerializeField] private Mode mode = Mode.Swing;

    [Header("Interaction")]
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private string openText = "Press E to open";
    [SerializeField] private string closeText = "Press E to close";
    [SerializeField] private string smashText = "Press E to smash";

    [Header("Swing")]
    [SerializeField] private float openAngle = 100f;      // always positive; the side is chosen automatically
    [SerializeField] private float swingSpeed = 180f;
    [SerializeField] private bool invertDirection = false; // tick this if it swings TOWARD you

    [Header("Smash")]
    [SerializeField] private int cols = 3;
    [SerializeField] private int rows = 5;
    [SerializeField] private float explosionForce = 3f;
    [SerializeField] private float debrisLifetime = 8f;

    private readonly NetworkVariable<bool> isActivated = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<int> openDirection = new NetworkVariable<int>(1);

    private BoxCollider doorCollider;
    private MeshRenderer meshRenderer;
    private Quaternion closedRot;
    private bool promptShown;

    private void Awake()
    {
        doorCollider = GetComponent<BoxCollider>();
        meshRenderer = GetComponent<MeshRenderer>();
        closedRot = transform.localRotation;
    }

    private Quaternion GetOpenRot()
    {
        return closedRot * Quaternion.Euler(0f, Mathf.Abs(openAngle) * openDirection.Value, 0f);
    }

    public override void OnNetworkSpawn()
    {
        isActivated.OnValueChanged += OnStateChanged;

        if (isActivated.Value)
        {
            if (mode == Mode.Smash) HideDoor();
            else transform.localRotation = GetOpenRot();
        }
    }

    public override void OnNetworkDespawn()
    {
        isActivated.OnValueChanged -= OnStateChanged;
        HidePrompt();
    }

    private void OnStateChanged(bool oldValue, bool newValue)
    {
        if (mode == Mode.Smash && newValue)
        {
            SpawnDebris();
            HideDoor();
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (mode == Mode.Swing)
        {
            Quaternion target = isActivated.Value ? GetOpenRot() : closedRot;
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation, target, swingSpeed * Time.deltaTime);
        }

        HandlePrompt();
    }

    private void HandlePrompt()
    {
        bool broken = mode == Mode.Smash && isActivated.Value;
        bool near = false;
        Vector3 playerPos = default;

        if (!broken && TryGetLocalPlayerPosition(out playerPos))
        {
            Vector3 closest = doorCollider.ClosestPoint(playerPos);
            near = (closest - playerPos).sqrMagnitude <= interactRange * interactRange;
        }

        if (near)
        {
            string text = mode == Mode.Smash ? smashText
                        : (isActivated.Value ? closeText : openText);

            if (InteractionPrompt.Instance != null)
                InteractionPrompt.Instance.Show(this, text);
            promptShown = true;

            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame)
                InteractRpc(playerPos);
        }
        else if (promptShown)
        {
            HidePrompt();
        }
    }

    private void HidePrompt()
    {
        if (!promptShown) return;
        promptShown = false;
        if (InteractionPrompt.Instance != null)
            InteractionPrompt.Instance.Hide(this);
    }

    // The player's root doesn't move with the active ragdoll, so measure from the hips.
    private bool TryGetLocalPlayerPosition(out Vector3 pos)
    {
        pos = default;
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null)
            return false;

        NetworkObject player = nm.LocalClient.PlayerObject;
        var ragdoll = player.GetComponent<ActiveRagdoll>();
        pos = ragdoll != null ? ragdoll.Hips.transform.position : player.transform.position;
        return true;
    }

    // Positive = player is on the door's +Z side, negative = on the -Z side.
    // Uses the closed pose, so it's correct even if the door is mid-swing.
    private float SideOfPlayer(Vector3 playerWorldPos)
    {
        Transform parent = transform.parent;
        Vector3 p = parent != null ? parent.InverseTransformPoint(playerWorldPos) : playerWorldPos;
        Vector3 forwardInParent = closedRot * Vector3.forward;
        return Vector3.Dot(p - transform.localPosition, forwardInParent);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void InteractRpc(Vector3 playerPos)
    {
        if (mode == Mode.Smash)
        {
            if (!isActivated.Value) isActivated.Value = true;
        }
        else
        {
            if (!isActivated.Value)
            {
                // Opening: swing away from whoever pressed E.
                int dir = SideOfPlayer(playerPos) > 0f ? -1 : 1;
                if (invertDirection) dir = -dir;
                openDirection.Value = dir;
            }

            isActivated.Value = !isActivated.Value;
        }
    }

    private void HideDoor()
    {
        meshRenderer.enabled = false;
        doorCollider.enabled = false;
        HidePrompt();
    }

    private void SpawnDebris()
    {
        Vector3 size = doorCollider.size;
        Vector3 center = doorCollider.center;
        Vector3 worldCenter = transform.TransformPoint(center);

        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                Vector3 local = center + new Vector3(
                    -size.x * 0.5f + (x + 0.5f) * size.x / cols,
                    -size.y * 0.5f + (y + 0.5f) * size.y / rows,
                    0f);

                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.transform.SetPositionAndRotation(
                    transform.TransformPoint(local), transform.rotation);
                piece.transform.localScale = Vector3.Scale(
                    new Vector3(size.x / cols, size.y / rows, size.z) * 0.95f,
                    transform.lossyScale);
                piece.GetComponent<Renderer>().sharedMaterial = meshRenderer.sharedMaterial;

                var rb = piece.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                rb.AddExplosionForce(explosionForce, worldCenter, 5f, 0.3f, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 0.5f, ForceMode.Impulse);

                Destroy(piece, debrisLifetime);
            }
        }
    }
}