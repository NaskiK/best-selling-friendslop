using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNameTag : NetworkBehaviour
{
    [Header("References (автоматично се намират, ако са празни)")]
    [SerializeField] private TMP_Text nameText;      // NameText
    [SerializeField] private Transform tagRoot;      // NameCanvas
    [SerializeField] private Transform followTarget; // Hips или Head от PhysicsRig

    [Header("Settings")]
    [SerializeField] private float height = 1.2f;    // Hips ~1.2, Head ~0.5
    [SerializeField] private bool hideOwnTag = false;

    private Camera cam;

    // Owner пише името, всички го четат
    private readonly NetworkVariable<FixedString32Bytes> playerName = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        if (nameText == null)
            nameText = GetComponentInChildren<TMP_Text>(true);

        if (tagRoot == null && nameText != null)
        {
            var canvas = nameText.GetComponentInParent<Canvas>(true);
            if (canvas != null) tagRoot = canvas.transform;
        }

        // Ако няма зададена кост, взимаме първия Rigidbody от героя
        if (followTarget == null)
        {
            var rb = GetComponentInChildren<Rigidbody>();
            if (rb != null) followTarget = rb.transform;
        }
    }

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged += OnNameChanged;

        if (IsOwner && MultiplayerManager.Instance != null)
            playerName.Value = MultiplayerManager.Instance.PlayerName;

        UpdateText(playerName.Value);

        if (IsOwner && hideOwnTag && tagRoot != null)
            tagRoot.gameObject.SetActive(false);
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnNameChanged;
    }

    private void OnNameChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
    {
        UpdateText(newValue);
    }

    private void UpdateText(FixedString32Bytes value)
    {
        if (nameText != null)
            nameText.text = value.ToString();
    }

    private void LateUpdate()
    {
        if (tagRoot == null) return;

        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        Transform target = followTarget != null ? followTarget : transform;

        // Над героя в световни координати, без да се върти с костта
        tagRoot.position = target.position + Vector3.up * height;

        // Billboard: същата ротация като камерата, текстът не е огледален
        tagRoot.rotation = cam.transform.rotation;
    }
}