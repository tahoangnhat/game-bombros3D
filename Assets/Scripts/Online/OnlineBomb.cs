using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class OnlineBomb : NetworkBehaviour
{
    public float fuseTime = 2f;
    public NetworkObject explosionPrefab;
    public int explosionRange = 1;

    private Collider bombCollider;
    private Rigidbody bombRigidbody;
    private Collider ownerCollider;
    private bool ownerCanPassThrough = true;
    private bool hasExploded;

    public override void Spawned()
    {
        bombCollider = GetComponent<Collider>();
        bombRigidbody = GetComponent<Rigidbody>();

        if (bombRigidbody != null)
        {
            bombRigidbody.useGravity = false;
            bombRigidbody.isKinematic = true;
        }

        // Vector3 snapped = GridUtility.GetNearestCellCenter(transform.position);
        // snapped.y = transform.position.y;
        // transform.position = snapped;

        SetupOwnerPassThrough();

        if (Object.HasStateAuthority)
        {
            Invoke(nameof(Explode), fuseTime);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!ownerCanPassThrough || ownerCollider == null || bombCollider == null)
        {
            return;
        }

        if (!IsOverlapping(ownerCollider))
        {
            Physics.IgnoreCollision(bombCollider, ownerCollider, false);
            ownerCanPassThrough = false;
            ownerCollider = null;
        }
    }

    private void SetupOwnerPassThrough()
    {
        if (Runner == null || bombCollider == null)
        {
            return;
        }

        NetworkObject ownerObject = Runner.GetPlayerObject(Object.InputAuthority);
        if (ownerObject == null)
        {
            return;
        }

        ownerCollider = ownerObject.GetComponent<Collider>();
        if (ownerCollider != null)
        {
            Physics.IgnoreCollision(bombCollider, ownerCollider, true);
        }
    }

    private bool IsOverlapping(Collider other)
    {
        if (other == null || bombCollider == null)
        {
            return false;
        }

        Vector3 direction;
        float distance;
        return Physics.ComputePenetration(
            bombCollider,
            bombCollider.transform.position,
            bombCollider.transform.rotation,
            other,
            other.transform.position,
            other.transform.rotation,
            out direction,
            out distance);
    }

    private void Explode()
    {
        if (hasExploded || !Object.HasStateAuthority)
        {
            return;
        }

        hasExploded = true;
        CancelInvoke(nameof(Explode));

        GridUtility.TryWorldToCell(transform.position, out int bombCellX, out int bombCellZ);
        ProcessExplosionCell(bombCellX, bombCellZ);

        ProcessExplosionArm(bombCellX, bombCellZ, 1, 0);
        ProcessExplosionArm(bombCellX, bombCellZ, -1, 0);
        ProcessExplosionArm(bombCellX, bombCellZ, 0, 1);
        ProcessExplosionArm(bombCellX, bombCellZ, 0, -1);

        if (Object != null && Object.IsValid)
        {
            Runner.Despawn(Object);
        }
    }

    private void ProcessExplosionArm(int originX, int originZ, int stepX, int stepZ)
    {
        for (int i = 1; i <= explosionRange; i++)
        {
            int cellX = originX + stepX * i;
            int cellZ = originZ + stepZ * i;

            if (!GridUtility.IsInsideGrid(cellX, cellZ))
            {
                break;
            }

            if (!ProcessExplosionCell(cellX, cellZ))
            {
                break;
            }
        }
    }

    private bool ProcessExplosionCell(int cellX, int cellZ)
    {
        CellType cellType = MatchGridState.GetEffectiveCellType(cellX, cellZ);
        if (cellType == CellType.BorderWall || cellType == CellType.MiddleWall)
        {
            return false;
        }

        bool destroyDestructible = cellType == CellType.DestructibleWall;
        Vector3 worldPos = GridUtility.GetCellCenter(cellX, cellZ);
        SpawnExplosion(worldPos, cellX, cellZ, destroyDestructible);
        DamagePlayersAtCell(cellX, cellZ);
        TriggerBombAtCell(cellX, cellZ);

        if (destroyDestructible)
        {
            MatchGridState.MarkDestroyed(cellX, cellZ);

            if (Object.HasStateAuthority)
            {
                if (MatchGridState.TryDetermineBuffType(cellX, cellZ, out BuffItem.BuffType buffType))
                {
                    NetworkObject resolvedBuffPrefab = null;
                    if (OnlineLobbyManager.Instance != null)
                    {
                        resolvedBuffPrefab = OnlineLobbyManager.Instance.buffPrefab;
                    }

                    if (resolvedBuffPrefab != null)
                    {
                        Vector3 buffSpawnPos = GridUtility.GetCellCenter(cellX, cellZ);
                        buffSpawnPos.y = 0.5f;

                        Runner.Spawn(
                            resolvedBuffPrefab,
                            buffSpawnPos,
                            Quaternion.identity,
                            PlayerRef.None,
                            (NetworkRunner runner, NetworkObject obj) =>
                            {
                                BuffItem buff = obj.GetComponent<BuffItem>();
                                if (buff != null)
                                {
                                    buff.buffType = buffType;
                                }
                            });
                    }
                }
            }

            return false;
        }

        return true;
    }

    private void TriggerBombAtCell(int cellX, int cellZ)
    {
        OnlineBomb[] bombs = FindObjectsByType<OnlineBomb>(FindObjectsInactive.Exclude);
        for (int i = 0; i < bombs.Length; i++)
        {
            OnlineBomb otherBomb = bombs[i];
            if (otherBomb == null
                || otherBomb == this
                || otherBomb.hasExploded
                || otherBomb.Object == null
                || !otherBomb.Object.IsValid
                || !otherBomb.Object.HasStateAuthority)
            {
                continue;
            }

            GridUtility.TryWorldToCell(otherBomb.transform.position, out int bombCellX, out int bombCellZ);
            if (bombCellX == cellX && bombCellZ == cellZ)
            {
                otherBomb.Explode();
            }
        }
    }

    private void DamagePlayersAtCell(int cellX, int cellZ)
    {
        OnlinePlayerHealth[] players = FindObjectsByType<OnlinePlayerHealth>(FindObjectsInactive.Include);
        for (int i = 0; i < players.Length; i++)
        {
            OnlinePlayerHealth health = players[i];
            if (health == null || !health.IsAlive)
            {
                continue;
            }

            GridUtility.TryWorldToCell(health.transform.position, out int playerCellX, out int playerCellZ);
            if (playerCellX != cellX || playerCellZ != cellZ)
            {
                continue;
            }

            health.TakeDamage(1, Object.InputAuthority);
        }
    }

    private void SpawnExplosion(Vector3 position, int cellX, int cellZ, bool destroyDestructible)
    {
        NetworkObject resolvedExplosionPrefab = explosionPrefab;
        if (resolvedExplosionPrefab == null && OnlineLobbyManager.Instance != null)
        {
            resolvedExplosionPrefab = OnlineLobbyManager.Instance.explosionPrefab;
        }

        if (resolvedExplosionPrefab == null || Runner == null || !Object.HasStateAuthority)
        {
            return;
        }

        Runner.Spawn(
            resolvedExplosionPrefab,
            position,
            Quaternion.identity,
            PlayerRef.None,
            (NetworkRunner runner, NetworkObject networkObject) =>
            {
                OnlineExplosion explosion = networkObject.GetComponent<OnlineExplosion>();
                if (explosion != null)
                {
                    explosion.ConfigureCell(cellX, cellZ, destroyDestructible);
                }
            });
    }
}
