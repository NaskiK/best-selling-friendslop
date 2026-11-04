using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNameTag : NetworkBehaviour
{
    [Header("References (автоматично се намират, ако са празни)")]
    [SerializeField] private TMP_Text nameText;      // NameText
    [SerializeField] private Transform tagRoot;      // NameCanvas
    [SerializeField] private Transform followTarget; // Head от PhysicsRig

    [Header("Settings")]
    [SerializeField] private float height = 0.5f;
    [SerializeField] private bool hideOwnTag = false;

    private Camera cam;

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

        if (followTarget == null)
        {
            var rb = GetComponentInChildren<Rigidbody>();
            if (rb != null) followTarget = rb.transform;
        }

        // Нулираме отместванията в канваса, за да решава само Height
        if (nameText != null)
        {
            var rt = nameText.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition3D = Vector3.zero;
        }
        if (tagRoot is RectTransform canvasRt)
            canvasRt.pivot = new Vector2(0.5f, 0.5f);
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

        tagRoot.position = target.position + Vector3.up * height;
        tagRoot.rotation = cam.transform.rotation;
    }
}