using Fusion;
using System.Collections;
using UnityEngine;

public class OnlinePlayerHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 1;
    [SerializeField, Min(0f)] private float spectatorFollowDelay = 1f;

    [Networked]
    public int CurrentHealth { get; set; }

    [Networked]
    public int MaxHealth { get; set; }

    [Networked]
    public NetworkBool IsEliminated { get; set; }

    [Networked]
    public PlayerRef EliminatedBy { get; set; }

    public bool IsAlive => !IsEliminated && CurrentHealth > 0;

    private bool presentationApplied;
    private Renderer[] renderers;
    private Collider[] colliders;
    private Coroutine delayedFollowRoutine;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            MaxHealth = maxHealth;
            CurrentHealth = MaxHealth;
            IsEliminated = false;
            EliminatedBy = PlayerRef.None;
        }

        ApplyPresentation();
    }

    public override void Render()
    {
        ApplyPresentation();
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, PlayerRef.None);
    }

    public void TakeDamage(int damage, PlayerRef damageDealer)
    {
        if (damage <= 0 || Object == null || !Object.IsValid)
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            ApplyDamage(damage, damageDealer);
        }
    }

    private void ApplyDamage(int damage, PlayerRef damageDealer)
    {
        if (IsEliminated || CurrentHealth <= 0)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        Debug.Log($"[Health] Player {Object.InputAuthority.PlayerId} took {damage} damage. Health: {CurrentHealth}/{MaxHealth}");

        if (CurrentHealth <= 0)
        {
            Die(damageDealer);
        }
    }

    public void IncreaseHealth()
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority || IsEliminated)
        {
            return;
        }

        MaxHealth++;
        CurrentHealth++;
        Debug.Log($"[Buff] Player {Object.InputAuthority.PlayerId} health increased to {CurrentHealth}/{MaxHealth}.");
    }

    private void Die(PlayerRef damageDealer)
    {
        EliminatedBy = damageDealer;
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

        if (shouldHide && IsCurrentCameraTarget())
        {
            if (delayedFollowRoutine != null)
            {
                StopCoroutine(delayedFollowRoutine);
            }

            delayedFollowRoutine = StartCoroutine(FollowSurvivorAfterDelay());
        }

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

    private bool IsCurrentCameraTarget()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return false;
        }

        CameraFollow cameraFollow = mainCamera.GetComponent<CameraFollow>();
        return cameraFollow != null && cameraFollow.Target == transform;
    }

    private IEnumerator FollowSurvivorAfterDelay()
    {
        yield return new WaitForSecondsRealtime(spectatorFollowDelay);
        delayedFollowRoutine = null;

        // Do not override another camera transition that happened during the delay.
        if (IsCurrentCameraTarget())
        {
            FollowEliminator();
        }
    }

    private void FollowEliminator()
    {
        if (Runner == null)
        {
            return;
        }

        if (EliminatedBy != PlayerRef.None && EliminatedBy != Object.InputAuthority)
        {
            NetworkObject eliminatorObject = Runner.GetPlayerObject(EliminatedBy);
            if (eliminatorObject != null && eliminatorObject.IsValid)
            {
                OnlinePlayerHealth eliminatorHealth =
                    eliminatorObject.GetComponent<OnlinePlayerHealth>();
                if (eliminatorHealth != null && eliminatorHealth.IsAlive)
                {
                    CameraFollow.FollowLocalPlayer(eliminatorObject.transform);
                    return;
                }
            }
        }

        OnlinePlayerHealth[] players = FindObjectsByType<OnlinePlayerHealth>(FindObjectsInactive.Include);
        for (int i = 0; i < players.Length; i++)
        {
            OnlinePlayerHealth player = players[i];
            if (player != null && player != this && player.IsAlive)
            {
                CameraFollow.FollowLocalPlayer(player.transform);
                return;
            }
        }
    }
}
