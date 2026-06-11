using Fusion;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
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
    private Collider bodyCollider;
    private OnlinePlayerHealth health;
    private Vector3 inputDirection;
    private bool isGrounded;
    private float lastBombTime = -10f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        health = GetComponent<OnlinePlayerHealth>();
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    public override void Spawned()
    {
        rb.useGravity = false;
        rb.isKinematic = true;

        if (Object.HasInputAuthority)
        {
            OnlineSessionState.IsOnlineSession = true;
        }
    }

    private void Update()
    {
        if (!Object.HasInputAuthority || IsEliminated())
        {
            return;
        }

        UpdateTileHighlight();
    }

    public override void FixedUpdateNetwork()
    {
        if (IsEliminated())
        {
            inputDirection = Vector3.zero;
            return;
        }

        if (GetInput(out OnlinePlayerInput input))
        {
            inputDirection = new Vector3(input.Move.x, 0f, input.Move.y);
            if (inputDirection.sqrMagnitude > 1f)
            {
                inputDirection.Normalize();
            }

            if (input.PlaceBomb && Object.HasStateAuthority && Runner.SimulationTime - lastBombTime >= bombCooldown)
        {
            PlaceBomb();
            lastBombTime = Runner.SimulationTime;
        }
    }
    else
    {
        inputDirection = Vector3.zero;
    }

    isGrounded = IsGrounded();

    float control = isGrounded ? 1f : airControl;
    // Fusion mặc định sử dụng Runner.DeltaTime trong FixedUpdateNetwork
    Vector3 moveStep = new Vector3(inputDirection.x, 0f, inputDirection.z) * (moveSpeed * control * Runner.DeltaTime);

    if (!IsFinite(moveStep))
    {
        return;
    }

    PlayerMovementUtility.TryMove(transform, bodyCollider, moveStep);
}

    private void PlaceBomb()
    {
        if (IsEliminated())
        {
            return;
        }

        NetworkObject resolvedBombPrefab = bombPrefab;
        if (resolvedBombPrefab == null && OnlineLobbyManager.Instance != null)
        {
            resolvedBombPrefab = OnlineLobbyManager.Instance.bombPrefab;
        }

        if (resolvedBombPrefab == null || Runner == null || !Object.HasStateAuthority)
        {
            return;
        }

        GridUtility.TryWorldToCell(transform.position, out int cellX, out int cellZ);
        if (HasBombAtCell(cellX, cellZ))
        {
            return;
        }

        Vector3 spawnPos = GridUtility.GetCellCenter(cellX, cellZ);
        spawnPos.y = transform.position.y;

        Runner.Spawn(resolvedBombPrefab, spawnPos, Quaternion.identity, Object.InputAuthority);
    }

    private bool HasBombAtCell(int cellX, int cellZ)
    {
        Vector3 center = GridUtility.GetCellCenter(cellX, cellZ);
        float radius = GridUtility.GetCellSize() * 0.35f;
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].GetComponentInParent<OnlineBomb>() != null)
            {
                return true;
            }
        }

        return false;
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

        Vector3 cellCenter = GridUtility.GetNearestCellCenter(transform.position);
        tileHighlight.position = cellCenter + Vector3.up * tileHighlightYOffset;
    }

    private bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    private bool IsEliminated()
    {
        return health != null && health.IsEliminated;
    }
}
