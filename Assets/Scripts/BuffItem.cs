using UnityEngine;
using Fusion;

public class BuffItem : NetworkBehaviour
{
    public enum BuffType
    {
        Health,
        Range,
        Speed,
        Placement
    }

    [Networked]
    public BuffType buffType { get; set; }

    public float speedBuffMultiplier = 1.1f;
    public Vector3 visualScale = new Vector3(0.6f, 0.6f, 0.6f);

    public override void Spawned()
    {
        UpdateVisuals();
    }

    public override void Render()
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        transform.localScale = visualScale;
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            switch (buffType)
            {
                case BuffType.Health:
                    renderer.material.color = new Color(1f, 0.2f, 0.55f);
                    gameObject.name = "Health Buff";
                    break;
                case BuffType.Range:
                    renderer.material.color = Color.red;
                    gameObject.name = "Range Buff";
                    break;
                case BuffType.Speed:
                    renderer.material.color = Color.yellow;
                    gameObject.name = "Speed Buff";
                    break;
                case BuffType.Placement:
                    renderer.material.color = Color.green;
                    gameObject.name = "Placement Buff";
                    break;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        bool isOnline = OnlineSessionState.IsOnlineSession;

        if (isOnline)
        {
            // Only the server/host processes network pickups
            if (Object != null && !Object.HasStateAuthority)
            {
                return;
            }

            OnlinePlayerController onlinePlayer = other.GetComponentInParent<OnlinePlayerController>();
            if (onlinePlayer != null)
            {
                ApplyOnlineBuff(onlinePlayer);
                DespawnBuff();
            }
        }
        else
        {
            PlayerController localPlayer = other.GetComponentInParent<PlayerController>();
            if (localPlayer != null)
            {
                ApplyLocalBuff(localPlayer);
                Destroy(gameObject);
            }
        }
    }

    private void ApplyLocalBuff(PlayerController player)
    {
        switch (buffType)
        {
            case BuffType.Health:
                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    health.IncreaseHealth();
                }
                break;
            case BuffType.Range:
                player.IncreaseBombRange();
                break;
            case BuffType.Speed:
                player.IncreasePermanentSpeed(speedBuffMultiplier);
                break;
            case BuffType.Placement:
                player.IncreaseMaxActiveBombs();
                break;
        }
    }

    private void ApplyOnlineBuff(OnlinePlayerController player)
    {
        switch (buffType)
        {
            case BuffType.Health:
                OnlinePlayerHealth health = player.GetComponent<OnlinePlayerHealth>();
                if (health != null)
                {
                    health.IncreaseHealth();
                }
                break;
            case BuffType.Range:
                player.IncreaseBombRange();
                break;
            case BuffType.Speed:
                player.IncreasePermanentSpeed(speedBuffMultiplier);
                break;
            case BuffType.Placement:
                player.IncreaseMaxActiveBombs();
                break;
        }
    }

    private void DespawnBuff()
    {
        if (Object != null && Object.IsValid)
        {
            Runner.Despawn(Object);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Helper static method to instantiate a local buff item dynamically (colored cube)
    public static GameObject CreateLocalBuff(Vector3 position, BuffType type)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = position;

        Collider col = go.GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        BuffItem buff = go.AddComponent<BuffItem>();
        buff.buffType = type;
        buff.UpdateVisuals();

        return go;
    }
}
