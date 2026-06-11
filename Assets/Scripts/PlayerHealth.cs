using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 1;

    int currentHealth;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || currentHealth <= 0) return;

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
