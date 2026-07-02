using Fusion;
using UnityEngine;

public class OnlinePlayerHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 1;

    [Networked]
    public int CurrentHealth { get; set; }

    [Networked]
    public NetworkBool IsEliminated { get; set; }

    [Networked]
    public NetworkBool HasShield { get; set; }

    public bool IsAlive => !IsEliminated && CurrentHealth > 0;

    private bool presentationApplied;
    private Renderer[] renderers;
    private Collider[] colliders;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CurrentHealth = maxHealth;
            IsEliminated = false;
        }

        ApplyPresentation();
    }

    public override void Render()
    {
        ApplyPresentation();
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || Object == null || !Object.IsValid)
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            ApplyDamage(damage);
        }
    }

    private void ApplyDamage(int damage)
    {
        if (IsEliminated || CurrentHealth <= 0)
        {
            return;
        }

        if (HasShield)
        {
            HasShield = false;
            Debug.Log($"[Health] Player {Object.InputAuthority.PlayerId} shield absorbed the damage. Shield broke!");
            return;
        }

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        Debug.Log($"[Health] Player {Object.InputAuthority.PlayerId} took {damage} damage. Health: {CurrentHealth}/{maxHealth}");

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        IsEliminated = true;
        ApplyPresentation();
    }

    private void ApplyPresentation()
    {
        bool shouldHide = IsEliminated;
        if (presentationApplied == shouldHide)
        {
            return;
        }

        presentationApplied = shouldHide;

        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        if (colliders == null || colliders.Length == 0)
        {
            colliders = GetComponentsInChildren<Collider>(true);
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = !shouldHide;
            }
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = !shouldHide;
            }
        }
    }
}
