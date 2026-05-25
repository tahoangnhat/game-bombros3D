using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class OnlineExplosion : NetworkBehaviour
{
    public int damage = 1;
    public float lifeTime = 0.35f;
    public float hitRadius = 0.45f;
    public LayerMask damageMask = ~0;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            ApplyDamage();
            StartCoroutine(DespawnAfterDelay());
        }
    }

    private void ApplyDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, hitRadius, damageMask, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            OnlinePlayerHealth health = hit.GetComponentInParent<OnlinePlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(damage);
            }
        }
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(lifeTime);

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}
