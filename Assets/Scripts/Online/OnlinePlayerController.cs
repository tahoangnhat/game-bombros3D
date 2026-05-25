using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(ClientNetworkTransform))]
public class OnlinePlayerController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float airControl = 0.5f;

    [Header("Bomb")]
    public NetworkObject bombPrefab;
    public float bombCooldown = 0.5f;

    [Header("Tile Highlight")]
    public Transform tileHighlight;
    public float tileHighlightYOffset = 0.05f;

    [Header("Ground Check (optional)")]
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask groundMask = ~0;

    private Rigidbody rb;
    private Vector3 inputDirection;
    private bool isGrounded;
    private float lastBombTime = -10f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            return;
        }

        OnlineSessionState.IsOnlineSession = true;
    }

    private void Update()
    {
        if (!IsOwner)
        {
            return;
        }

        Vector2 moveInput = GetMoveInput();
        inputDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        if (inputDirection.sqrMagnitude > 1f)
        {
            inputDirection.Normalize();
        }

        if (CheckPlaceBomb() && Time.time - lastBombTime >= bombCooldown)
        {
            PlaceBomb();
            lastBombTime = Time.time;
        }

        UpdateTileHighlight();
    }

    private Vector2 GetMoveInput()
    {
        if (Gamepad.current != null)
        {
            return Gamepad.current.leftStick.ReadValue();
        }

        if (Keyboard.current != null)
        {
            float h = 0f;
            float v = 0f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
            return new Vector2(h, v);
        }

        return Vector2.zero;
    }

    private bool CheckPlaceBomb()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
        {
            return;
        }

        isGrounded = IsGrounded();

        float control = isGrounded ? 1f : airControl;
        Vector3 moveStep = transform.TransformDirection(inputDirection) * (moveSpeed * control * Time.fixedDeltaTime);
        moveStep.y = 0f;

        if (!IsFinite(moveStep))
        {
            return;
        }

        rb.MovePosition(rb.position + moveStep);
    }

    private void PlaceBomb()
    {
        NetworkObject resolvedBombPrefab = bombPrefab;
        if (resolvedBombPrefab == null && OnlineLobbyManager.Instance != null)
        {
            resolvedBombPrefab = OnlineLobbyManager.Instance.bombPrefab;
        }

        if (resolvedBombPrefab == null)
        {
            return;
        }

        Vector3 pos = transform.position;
        Vector3 spawnPos = GetNearestGridCenter(pos);

        PlaceBombServerRpc(spawnPos);
    }

    [ServerRpc]
    private void PlaceBombServerRpc(Vector3 spawnPos, ServerRpcParams serverRpcParams = default)
    {
        NetworkObject resolvedBombPrefab = bombPrefab;
        if (resolvedBombPrefab == null && OnlineLobbyManager.Instance != null)
        {
            resolvedBombPrefab = OnlineLobbyManager.Instance.bombPrefab;
        }

        if (resolvedBombPrefab == null)
        {
            return;
        }

        NetworkObject bomb = Instantiate(resolvedBombPrefab, spawnPos, Quaternion.identity);
        bomb.Spawn();
    }

    private bool IsGrounded()
    {
        Vector3 origin = groundCheck != null ? groundCheck.position : transform.position + Vector3.down * 0.5f;
        return Physics.CheckSphere(origin, groundDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void UpdateTileHighlight()
    {
        if (tileHighlight == null)
        {
            return;
        }

        Vector3 cellCenter = GetNearestGridCenter(transform.position);
        tileHighlight.position = cellCenter + Vector3.up * tileHighlightYOffset;
    }

    private Vector3 GetNearestGridCenter(Vector3 worldPosition)
    {
        ThemeManager themeManager = ThemeManager.Instance;
        if (themeManager == null || themeManager.cellSize <= 0f)
        {
            return new Vector3(Mathf.Round(worldPosition.x), 0f, Mathf.Round(worldPosition.z));
        }

        float originX = themeManager.gridOrigin.x + themeManager.floorOffset.x;
        float originZ = themeManager.gridOrigin.z + themeManager.floorOffset.z;

        int cellX = Mathf.RoundToInt((worldPosition.x - originX) / themeManager.cellSize);
        int cellZ = Mathf.RoundToInt((worldPosition.z - originZ) / themeManager.cellSize);

        Vector3 center = themeManager.GetWorldPosition(cellX, cellZ);
        center.y = themeManager.GetWorldPosition(0, 0).y;
        return center;
    }

    private bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
