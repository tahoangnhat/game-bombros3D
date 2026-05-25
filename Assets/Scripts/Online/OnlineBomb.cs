using Unity.Netcode;
using UnityEngine;

public class OnlineBomb : NetworkBehaviour
{
    public float fuseTime = 2f;
    public NetworkObject explosionPrefab;
    public int explosionRange = 3;
    public float tileSize = 1f;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Invoke(nameof(Explode), fuseTime);
        }
    }

    private void Explode()
    {
        SpawnExplosion(transform.position);

        for (int i = 1; i <= explosionRange; i++)
        {
            Vector3 right = transform.position + Vector3.right * tileSize * i;
            Vector3 left = transform.position + Vector3.left * tileSize * i;
            Vector3 forward = transform.position + Vector3.forward * tileSize * i;
            Vector3 back = transform.position + Vector3.back * tileSize * i;

            SpawnExplosion(right);
            SpawnExplosion(left);
            SpawnExplosion(forward);
            SpawnExplosion(back);
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }

    private void SpawnExplosion(Vector3 position)
    {
        NetworkObject resolvedExplosionPrefab = explosionPrefab;
        if (resolvedExplosionPrefab == null && OnlineLobbyManager.Instance != null)
        {
            resolvedExplosionPrefab = OnlineLobbyManager.Instance.explosionPrefab;
        }

        if (resolvedExplosionPrefab == null)
        {
            return;
        }

        Vector3 snapped = SnapToGrid(position);
        NetworkObject explosion = Instantiate(resolvedExplosionPrefab, snapped, Quaternion.identity);
        explosion.Spawn();
    }

    private Vector3 SnapToGrid(Vector3 position)
    {
        return new Vector3(Mathf.Round(position.x), Mathf.Round(position.y), Mathf.Round(position.z));
    }
}
