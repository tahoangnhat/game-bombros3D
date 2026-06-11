using System.Collections;
using Fusion;
using UnityEngine;

public class OnlineExplosion : NetworkBehaviour
{
    public float lifeTime = 0.35f;

    [Networked] public int DestroyCellX { get; set; }
    [Networked] public int DestroyCellZ { get; set; }
    [Networked] public NetworkBool ShouldDestroyCell { get; set; }

    private bool destroyApplied;

    public void ConfigureCell(int cellX, int cellZ, bool destroyDestructible)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        DestroyCellX = cellX;
        DestroyCellZ = cellZ;
        ShouldDestroyCell = destroyDestructible;
    }

    public override void Spawned()
    {
        TryApplyDestroy();

        if (Object.HasStateAuthority)
        {
            StartCoroutine(DespawnAfterDelay());
        }
    }

    public override void Render()
    {
        TryApplyDestroy();
    }

    private void TryApplyDestroy()
    {
        if (destroyApplied || !ShouldDestroyCell)
        {
            return;
        }

        destroyApplied = true;
        MatchGridState.MarkDestroyed(DestroyCellX, DestroyCellZ);
        DestructibleWall.DestroyAtCell(DestroyCellX, DestroyCellZ);
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(lifeTime);

        if (Object != null && Object.IsValid)
        {
            Runner.Despawn(Object);
        }
    }
}
