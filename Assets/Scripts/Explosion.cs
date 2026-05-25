using UnityEngine;

public class Explosion : MonoBehaviour
{
    public int damage = 1;
    public float lifeTime = 0.35f;
    public float hitRadius = 0.45f;
    public LayerMask damageMask = ~0;

    void Start()
    {
        ApplyDamage();
        Destroy(gameObject, lifeTime);
    }

    void ApplyDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, hitRadius, damageMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            PlayerHealth playerHealth = hits[i].GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}
