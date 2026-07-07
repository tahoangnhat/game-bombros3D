using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 1;

    int currentHealth;
    public bool IsAlive => currentHealth > 0;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

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

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.enabled = false;
        }
    }

    public void IncreaseHealth()
    {
        maxHealth++;
        currentHealth++;
        Debug.Log($"[Buff] {name} health increased to {currentHealth}/{maxHealth}.");
    }
}
