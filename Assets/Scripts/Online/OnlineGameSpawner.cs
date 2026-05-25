using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class OnlineGameSpawner : MonoBehaviour
{
    [SerializeField] private Vector3 playerSpawnOffset = new Vector3(0f, 0.5f, 0f);

    private void Start()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        StartCoroutine(AssignSpawnsAfterMapBuild());
    }

    private IEnumerator AssignSpawnsAfterMapBuild()
    {
        yield return null;
        yield return null;

        RefreshSpawnPositions();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        StartCoroutine(RefreshSpawnPositionsNextFrame());
    }

    private IEnumerator RefreshSpawnPositionsNextFrame()
    {
        yield return null;
        RefreshSpawnPositions();
    }

    private void RefreshSpawnPositions()
    {
        ThemeManager themeManager = ThemeManager.Instance;
        if (themeManager == null || NetworkManager.Singleton == null)
        {
            return;
        }

        Vector3[] spawnPoints = new Vector3[]
        {
            themeManager.GetWorldPosition(1, 1) + playerSpawnOffset,
            themeManager.GetWorldPosition(themeManager.width - 2, 1) + playerSpawnOffset,
            themeManager.GetWorldPosition(1, themeManager.height - 2) + playerSpawnOffset,
            themeManager.GetWorldPosition(themeManager.width - 2, themeManager.height - 2) + playerSpawnOffset
        };

        int index = 0;
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (index >= spawnPoints.Length)
            {
                break;
            }

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
            {
                continue;
            }

            if (client.PlayerObject == null)
            {
                continue;
            }

            Transform playerTransform = client.PlayerObject.transform;
            playerTransform.position = spawnPoints[index];
            playerTransform.rotation = Quaternion.identity;

            OnlinePlayerController controller = client.PlayerObject.GetComponent<OnlinePlayerController>();
            if (controller != null)
            {
                controller.name = $"Player{index + 1}";
            }

            index++;
        }
    }
}
