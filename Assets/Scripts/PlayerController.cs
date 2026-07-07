using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float airControl = 0.5f;

    [Header("Bomb")]
    public GameObject bombPrefab;

    [Header("Tile Highlight")]
    public Transform tileHighlight;
    public float tileHighlightYOffset = 0.05f;

    [Header("Ground Check (optional)")]
    public Transform groundCheck; // optional: child transform positioned at player's feet
    public float groundDistance = 0.2f;
    public LayerMask groundMask = ~0; // default: everything

    Rigidbody rb;
    Collider bodyCollider;
    Vector3 inputDirection;
    bool isGrounded;
    private List<Bomb> activeBombs = new List<Bomb>();

    // Buff fields
    private float baseMoveSpeed;
    private int maxActiveBombs = 1;
    private int currentBombRange = 1;
    private float permanentSpeedMultiplier = 1f;

    void Awake()
    {
        if (OnlineSessionState.IsOnlineSession)
        {
            enabled = false;
            return;
        }

        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        baseMoveSpeed = moveSpeed;
    }

    void Start()
    {
        if (!OnlineSessionState.IsOnlineSession)
        {
            CameraFollow.FollowLocalPlayer(transform);
        }
    }

    void Update()
    {
        Vector2 moveInput = GetMoveInput();
        inputDirection = CameraFollow.GetCameraRelativeDirection(moveInput);
        if (inputDirection.sqrMagnitude > 1f) inputDirection.Normalize();

        // Place bomb with Space (no jump in this game)
        if (CheckPlaceBomb())
        {
            PlaceBomb();
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
        Vector3 moveStep = inputDirection * (moveSpeed * control * Time.fixedDeltaTime);
        moveStep.y = 0f;

        if (!IsFinite(moveStep))
        {
            return;
        }

        if (PlayerMovementUtility.TryMove(transform, bodyCollider, moveStep))
        {
            rb.position = transform.position;
        }
    }

    bool PlaceBomb()
    {
        if (bombPrefab == null) return false;

        // Clean up exploded bombs (null references)
        activeBombs.RemoveAll(bomb => bomb == null);
        if (activeBombs.Count >= maxActiveBombs)
        {
            return false;
        }

        GridUtility.TryWorldToCell(transform.position, out int cellX, out int cellZ);
        if (HasBombAtCell(cellX, cellZ))
        {
            return false;
        }

        Vector3 spawnPos = GridUtility.GetCellCenter(cellX, cellZ);
        spawnPos.y = transform.position.y;

        GameObject bombObject = Instantiate(bombPrefab, spawnPos, Quaternion.identity);
        Bomb bomb = bombObject.GetComponent<Bomb>();
        if (bomb != null)
        {
            bomb.SetOwnerCollider(GetComponent<Collider>());
            bomb.explosionRange = currentBombRange;
            activeBombs.Add(bomb);
            return true;
        }

        Destroy(bombObject);
        return false;
    }

    bool HasBombAtCell(int cellX, int cellZ)
    {
        for (int i = 0; i < activeBombs.Count; i++)
        {
            Bomb activeBomb = activeBombs[i];
            if (activeBomb == null)
            {
                continue;
            }

            GridUtility.TryWorldToCell(
                activeBomb.transform.position,
                out int activeCellX,
                out int activeCellZ);
            if (activeCellX == cellX && activeCellZ == cellZ)
            {
                return true;
            }
        }

        Bomb[] bombs = FindObjectsByType<Bomb>(FindObjectsInactive.Include);
        for (int i = 0; i < bombs.Length; i++)
        {
            Bomb bomb = bombs[i];
            if (bomb == null)
            {
                continue;
            }

            GridUtility.TryWorldToCell(bomb.transform.position, out int bombCellX, out int bombCellZ);
            if (bombCellX == cellX && bombCellZ == cellZ)
            {
                return true;
            }
        }

        return false;
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

    public int MaxActiveBombs => maxActiveBombs;
    public int CurrentBombRange => currentBombRange;
    public float SpeedMultiplier => permanentSpeedMultiplier;

    // Buff helper methods
    public void IncreaseBombRange()
    {
        currentBombRange++;
        Debug.Log($"[Buff] Local bomb range increased to {currentBombRange}");
    }

    public void IncreaseMaxActiveBombs()
    {
        maxActiveBombs++;
        Debug.Log($"[Buff] Local max active bombs increased to {maxActiveBombs}");
    }

    public void IncreasePermanentSpeed(float multiplier)
    {
        permanentSpeedMultiplier += Mathf.Max(0f, multiplier - 1f);
        moveSpeed = baseMoveSpeed * permanentSpeedMultiplier;
        Debug.Log($"[Buff] Local speed increased permanently to x{permanentSpeedMultiplier:0.##}");
    }
}
