using UnityEngine;

public class Bomb : MonoBehaviour
{
    public float fuseTime = 2f;
    public GameObject explosionPrefab;
    public int explosionRange = 3;
    public float tileSize = 1f;

    void Start()
    {
        Invoke(nameof(Explode), fuseTime);
    }

    void Explode()
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

        // Remove bomb after it detonates.
        Destroy(gameObject);
    }

    void SpawnExplosion(Vector3 position)
    {
        if (explosionPrefab == null) return;

        GameObject explosion = Instantiate(explosionPrefab, SnapToGrid(position), Quaternion.identity);
        explosion.transform.position = SnapToGrid(position);
    }

    Vector3 SnapToGrid(Vector3 position)
    {
        return new Vector3(Mathf.Round(position.x), Mathf.Round(position.y), Mathf.Round(position.z));
    }
}
