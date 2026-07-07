using UnityEngine;

public class Bomb : MonoBehaviour
{
    public float fuseTime = 2f;
    public GameObject explosionPrefab;
    public int explosionRange = 1;

    private Collider bombCollider;
    private Collider ownerCollider;
    private bool ownerCanPassThrough = true;
    private bool hasExploded;

    void Start()
    {
        bombCollider = GetComponent<Collider>();
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.useGravity = false;
            body.isKinematic = true;
        }

        if (ownerCollider != null && bombCollider != null)
        {
            Physics.IgnoreCollision(bombCollider, ownerCollider, true);
        }

        transform.position = GridUtility.GetNearestCellCenter(transform.position);
        Invoke(nameof(Explode), fuseTime);
    }

    void FixedUpdate()
    {
        if (!ownerCanPassThrough || ownerCollider == null || bombCollider == null)
        {
            return;
        }

        Vector3 direction;
        float distance;
        bool overlapping = Physics.ComputePenetration(
            bombCollider, bombCollider.transform.position, bombCollider.transform.rotation,
            ownerCollider, ownerCollider.transform.position, ownerCollider.transform.rotation,
            out direction, out distance);

        if (!overlapping)
        {
            Physics.IgnoreCollision(bombCollider, ownerCollider, false);
            ownerCanPassThrough = false;
            ownerCollider = null;
        }
    }

    public void SetOwnerCollider(Collider owner)
    {
        ownerCollider = owner;
        if (ownerCollider != null && bombCollider != null)
        {
            Physics.IgnoreCollision(bombCollider, ownerCollider, true);
        }
    }

    void Explode()
    {
        if (hasExploded)
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

        Destroy(gameObject);
    }

    void ProcessExplosionArm(int originX, int originZ, int stepX, int stepZ)
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

    bool ProcessExplosionCell(int cellX, int cellZ)
    {
        CellType cellType = MatchGridState.GetEffectiveCellType(cellX, cellZ);
        if (cellType == CellType.BorderWall || cellType == CellType.MiddleWall)
        {
            return false;
        }

        Vector3 worldPos = GridUtility.GetCellCenter(cellX, cellZ);
        SpawnExplosion(worldPos);
        DamagePlayersAtCell(cellX, cellZ);
        TriggerBombAtCell(cellX, cellZ);

        if (cellType == CellType.DestructibleWall)
        {
            MatchGridState.MarkDestroyed(cellX, cellZ);
            DestructibleWall.DestroyAtCell(cellX, cellZ);

            // Check consolidated buff spawning logic
            if (MatchGridState.TryDetermineBuffType(cellX, cellZ, out BuffItem.BuffType buffType))
            {
                Vector3 buffSpawnPos = GridUtility.GetCellCenter(cellX, cellZ);
                buffSpawnPos.y = 0.5f;
                BuffItem.CreateLocalBuff(buffSpawnPos, buffType);
            }

            return false;
        }

        return true;
    }

    void TriggerBombAtCell(int cellX, int cellZ)
    {
        Bomb[] bombs = FindObjectsByType<Bomb>(FindObjectsInactive.Exclude);
        for (int i = 0; i < bombs.Length; i++)
        {
            Bomb otherBomb = bombs[i];
            if (otherBomb == null || otherBomb == this || otherBomb.hasExploded)
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

    void DamagePlayersAtCell(int cellX, int cellZ)
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth health = players[i];
            if (health == null)
            {
                continue;
            }

            GridUtility.TryWorldToCell(health.transform.position, out int playerCellX, out int playerCellZ);
            if (playerCellX != cellX || playerCellZ != cellZ)
            {
                continue;
            }

            health.TakeDamage(1);
        }
    }

    void SpawnExplosion(Vector3 position)
    {
        if (explosionPrefab == null)
        {
            return;
        }

        Instantiate(explosionPrefab, position, Quaternion.identity);
    }
}
