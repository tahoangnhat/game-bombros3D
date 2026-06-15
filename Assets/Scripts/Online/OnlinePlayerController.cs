using Fusion;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
    private List<NetworkObject> activeBombs = new List<NetworkObject>();

    // Buff networked properties
    [Networked] public int CurrentBombRange { get; set; }
    [Networked] public int MaxActiveBombs { get; set; }
    [Networked] public float CurrentMoveSpeed { get; set; }

    private Coroutine speedBuffCoroutine;

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

        if (Object.HasStateAuthority)
        {
            CurrentBombRange = 1;
            MaxActiveBombs = 2;
            CurrentMoveSpeed = moveSpeed;
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

        if (inputDirection.sqrMagnitude > 0.01f)
        {
            transform.forward = inputDirection;
        }

        float control = isGrounded ? 1f : airControl;
        float currentSpeed = CurrentMoveSpeed > 0f ? CurrentMoveSpeed : moveSpeed;
        // Fusion mặc định sử dụng Runner.DeltaTime trong FixedUpdateNetwork
        Vector3 moveStep = new Vector3(inputDirection.x, 0f, inputDirection.z) * (currentSpeed * control * Runner.DeltaTime);

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

        // Clean up despawned / invalid bombs
        activeBombs.RemoveAll(bomb => bomb == null || !bomb.IsValid);
        if (activeBombs.Count >= MaxActiveBombs)
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

        NetworkObject spawnedBomb = Runner.Spawn(
            resolvedBombPrefab,
            spawnPos,
            Quaternion.identity,
            Object.InputAuthority,
            (NetworkRunner runner, NetworkObject obj) =>
            {
                OnlineBomb bomb = obj.GetComponent<OnlineBomb>();
                if (bomb != null)
                {
                    bomb.explosionRange = CurrentBombRange;
                }
            });

        if (spawnedBomb != null)
        {
            activeBombs.Add(spawnedBomb);
        }
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

    // Buff helper methods
    public void IncreaseBombRange()
    {
        if (!Object.HasStateAuthority) return;
        CurrentBombRange = Mathf.Min(2, CurrentBombRange + 1); // Max range 2 is 5x5
        Debug.Log($"[Buff] Online player range increased to {CurrentBombRange}");
    }

    public void IncreaseMaxActiveBombs()
    {
        if (!Object.HasStateAuthority) return;
        MaxActiveBombs = Mathf.Min(3, MaxActiveBombs + 1);
        Debug.Log($"[Buff] Online player max active bombs increased to {MaxActiveBombs}");
    }

    public void ApplySpeedBuff(float multiplier, float duration)
    {
        if (!Object.HasStateAuthority) return;

        if (speedBuffCoroutine != null)
        {
            StopCoroutine(speedBuffCoroutine);
        }
        speedBuffCoroutine = StartCoroutine(SpeedBuffRoutine(multiplier, duration));
    }

    private IEnumerator SpeedBuffRoutine(float multiplier, float duration)
    {
        CurrentMoveSpeed = moveSpeed * multiplier;
        yield return new WaitForSeconds(duration);
        CurrentMoveSpeed = moveSpeed;
        speedBuffCoroutine = null;
    }
}
