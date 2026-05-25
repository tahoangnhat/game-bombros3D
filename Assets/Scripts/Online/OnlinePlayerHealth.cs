using Unity.Netcode;
using UnityEngine;

public class OnlinePlayerHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 1;

    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;
        }
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || !IsSpawned)
        {
            return;
        }

        if (IsServer)
        {
            ApplyDamage(damage);
        }
        else
        {
            TakeDamageServerRpc(damage);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(int damage)
    {
        ApplyDamage(damage);
    }

    private void ApplyDamage(int damage)
    {
        if (CurrentHealth.Value <= 0)
        {
            return;
        }

        CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - damage);

        if (CurrentHealth.Value <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}
