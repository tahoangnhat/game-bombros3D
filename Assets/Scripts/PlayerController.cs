using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float airControl = 0.5f;

    [Header("Bomb")]
    public GameObject bombPrefab;
    public float bombCooldown = 0.5f;

    [Header("Tile Highlight")]
    public Transform tileHighlight;
    public float tileHighlightYOffset = 0.05f;

    [Header("Ground Check (optional)")]
    public Transform groundCheck; // optional: child transform positioned at player's feet
    public float groundDistance = 0.2f;
    public LayerMask groundMask = ~0; // default: everything

    Rigidbody rb;
    Vector3 inputDirection;
    bool isGrounded;
    float lastBombTime = -10f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    void Update()
    {
        Vector2 moveInput = GetMoveInput();
        inputDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        if (inputDirection.sqrMagnitude > 1f) inputDirection.Normalize();

        // Place bomb with Space (no jump in this game)
        if (CheckPlaceBomb() && Time.time - lastBombTime >= bombCooldown)
        {
            PlaceBomb();
            lastBombTime = Time.time;
        }

        UpdateTileHighlight();
    }

    Vector2 GetMoveInput()
    {
        // Gamepad (analog) first
        if (Gamepad.current != null)
        {
            return Gamepad.current.leftStick.ReadValue();
        }

        // Keyboard fallback (WASD / arrows)
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

    bool CheckPlaceBomb()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
        return false;
    }

    void FixedUpdate()
    {
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

    void PlaceBomb()
    {
        if (bombPrefab == null) return;

        Vector3 spawnPos = GetNearestGridCenter(transform.position);

        Instantiate(bombPrefab, spawnPos, Quaternion.identity);
    }

    void UpdateTileHighlight()
    {
        if (tileHighlight == null)
        {
            return;
        }

        Vector3 cellCenter = GetNearestGridCenter(transform.position);
        tileHighlight.position = cellCenter + Vector3.up * tileHighlightYOffset;
    }

    Vector3 GetNearestGridCenter(Vector3 worldPosition)
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

    bool IsGrounded()
    {
        Vector3 origin = groundCheck != null ? groundCheck.position : transform.position + Vector3.down * 0.5f;
        return Physics.CheckSphere(origin, groundDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}
