using Fusion;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class OnlinePlayerController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float airControl = 0.5f;
    [Tooltip("How quickly the player turns to face its movement direction, in degrees per second.")]
    public float turnSpeed = 720f;

    [Header("Bomb")]
    public NetworkObject bombPrefab;

    [Header("Tile Highlight")]
    public Transform tileHighlight;
    public float tileHighlightYOffset = 0.05f;

    [Header("Ground Check (optional)")]
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask groundMask = ~0;

    [Header("Player Visuals")]
    [Tooltip("Four materials used by player slots 1-4. The same animated model is shared by every slot.")]
    [SerializeField] private Material[] playerMaterials;

    private Rigidbody rb;
    private Collider bodyCollider;
    private OnlinePlayerHealth health;
    private Vector3 inputDirection;
    private bool isGrounded;
    private List<NetworkObject> activeBombs = new List<NetworkObject>();

    // Buff networked properties
    [Networked] public int CurrentBombRange { get; set; }
    [Networked] public int MaxActiveBombs { get; set; }
    [Networked] public float CurrentMoveSpeed { get; set; }
    [Networked] public float SpeedMultiplier { get; set; }
    [Networked] public int VisualIndex { get; set; }
    [Networked] public NetworkString<_32> Nickname { get; set; }

    private SkinnedMeshRenderer[] visualRenderers;
    private int appliedVisualIndex = -1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        health = GetComponent<OnlinePlayerHealth>();
        visualRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    public override void Spawned()
    {
        rb.useGravity = false;
        rb.isKinematic = true;

        if (Object.HasInputAuthority)
        {
            OnlineSessionState.IsOnlineSession = true;
            CameraFollow.FollowLocalPlayer(transform);
            
            string myName = "";
            if (OnlineLobbyManager.Instance != null)
            {
                myName = OnlineLobbyManager.Instance.BuildLocalDisplayName();
            }
            else
            {
                myName = SpringAuthSession.Username;
            }

            if (string.IsNullOrEmpty(myName))
            {
                myName = "Player " + Object.InputAuthority.PlayerId;
            }

            RPC_SetNickname(myName);
        }

        if (Object.HasStateAuthority)
        {
            CurrentBombRange = 1;
            MaxActiveBombs = 1;
            CurrentMoveSpeed = moveSpeed;
            SpeedMultiplier = 1f;
        }

        ApplyPlayerVisual();
    }

    public override void Render()
    {
        ApplyPlayerVisual();
    }

    private void Update()
    {
        if (Object == null || !Object.HasInputAuthority || IsEliminated())
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

            if (input.PlaceBomb && Object.HasStateAuthority)
            {
                PlaceBomb();
            }
        }
        else
        {
            inputDirection = Vector3.zero;
        }

        isGrounded = IsGrounded();

        float control = isGrounded ? 1f : airControl;
        float currentSpeed = CurrentMoveSpeed > 0f ? CurrentMoveSpeed : moveSpeed;
        // Input is converted to camera-relative world space by the local input reader.
        Vector3 moveDirection = inputDirection;
        moveDirection.y = 0f;
        Vector3 moveStep = moveDirection * (currentSpeed * control * Runner.DeltaTime);

        if (!IsFinite(moveStep))
        {
            return;
        }

        PlayerMovementUtility.TryMove(transform, bodyCollider, moveStep);
        FaceMovementDirection();
    }

    private void FaceMovementDirection()
    {
        if (inputDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(inputDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Runner.DeltaTime);
    }

    private bool PlaceBomb()
    {
        if (IsEliminated())
        {
            return false;
        }

        // Clean up despawned / invalid bombs
        activeBombs.RemoveAll(bomb => bomb == null || !bomb.IsValid);
        if (activeBombs.Count >= MaxActiveBombs)
        {
            return false;
        }

        NetworkObject resolvedBombPrefab = bombPrefab;
        if (resolvedBombPrefab == null && OnlineLobbyManager.Instance != null)
        {
            resolvedBombPrefab = OnlineLobbyManager.Instance.bombPrefab;
        }

        if (resolvedBombPrefab == null || Runner == null || !Object.HasStateAuthority)
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
            return true;
        }

        return false;
    }

    private bool HasBombAtCell(int cellX, int cellZ)
    {
        for (int i = 0; i < activeBombs.Count; i++)
        {
            NetworkObject activeBomb = activeBombs[i];
            if (activeBomb == null || !activeBomb.IsValid)
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

        OnlineBomb[] bombs = FindObjectsByType<OnlineBomb>(FindObjectsInactive.Include);
        for (int i = 0; i < bombs.Length; i++)
        {
            OnlineBomb bomb = bombs[i];
            if (bomb == null
                || bomb.Object == null
                || !bomb.Object.IsValid)
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

    public void SetVisualIndex(int index)
    {
        if (Object != null && Object.HasStateAuthority)
        {
            VisualIndex = Mathf.Clamp(index, 0, 3);
        }
    }

    private void ApplyPlayerVisual()
    {
        if (playerMaterials == null || playerMaterials.Length == 0)
        {
            return;
        }

        int materialIndex = Mathf.Abs(VisualIndex) % playerMaterials.Length;
        if (appliedVisualIndex == materialIndex)
        {
            return;
        }

        Material material = playerMaterials[materialIndex];
        if (material == null)
        {
            return;
        }

        if (visualRenderers == null || visualRenderers.Length == 0)
        {
            visualRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            SkinnedMeshRenderer visualRenderer = visualRenderers[i];
            if (visualRenderer != null && visualRenderer.sharedMesh != null)
            {
                visualRenderer.sharedMaterial = material;
            }
        }

        appliedVisualIndex = materialIndex;
    }

    // Buff helper methods
    public void IncreaseBombRange()
    {
        if (!Object.HasStateAuthority) return;
        CurrentBombRange++;
        Debug.Log($"[Buff] Online player range increased to {CurrentBombRange}");
    }

    public void IncreaseMaxActiveBombs()
    {
        if (!Object.HasStateAuthority) return;
        MaxActiveBombs++;
        Debug.Log($"[Buff] Online player max active bombs increased to {MaxActiveBombs}");
    }

    public void IncreasePermanentSpeed(float multiplier)
    {
        if (!Object.HasStateAuthority) return;

        SpeedMultiplier += Mathf.Max(0f, multiplier - 1f);
        CurrentMoveSpeed = moveSpeed * SpeedMultiplier;
        Debug.Log($"[Buff] Online speed increased permanently to x{SpeedMultiplier:0.##}");
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetNickname(string nickname)
    {
        Nickname = nickname;
    }
}
