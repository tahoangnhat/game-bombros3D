using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 1;

    int currentHealth;
    public bool hasShield;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || currentHealth <= 0) return;

        if (hasShield)
        {
            hasShield = false;
            Debug.Log($"[Health] {name} shield absorbed the damage. Shield broke!");
            return;
        }

        currentHealth -= damage;
        Debug.Log($"[Health] {name} took {damage} damage. Health: {Mathf.Max(0, currentHealth)}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log($"{name} died.");
        Destroy(gameObject);
    }
}
