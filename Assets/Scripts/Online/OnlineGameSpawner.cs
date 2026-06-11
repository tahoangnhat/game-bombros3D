using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class OnlineGameSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Prefabs")]
    public NetworkObject playerPrefab;

    [Header("Map Data Source")]
    public LevelData levelData;

    [Header("Spawn")]
    [SerializeField] private Vector3 playerSpawnOffset = new Vector3(0f, 0.5f, 0f);

    private readonly List<Vector3> spawnPositions = new List<Vector3>();
    private NetworkRunner registeredRunner;

    private void Start()
    {
        NetworkRunner runner = FindAnyObjectByType<NetworkRunner>();
        if (runner != null)
        {
            RegisterRunner(runner);
        }
    }

    private void OnDestroy()
    {
        if (registeredRunner != null)
        {
            registeredRunner.RemoveCallbacks(this);
            registeredRunner = null;
        }
    }

    public void SpawnActivePlayers(NetworkRunner runner)
    {
        if (runner == null || !runner.IsServer)
        {
            return;
        }

        RegisterRunner(runner);
        EnsurePrefabAssigned();
        EnsureSpawnPoints();

        if (playerPrefab == null)
        {
            Debug.LogError("[Spawner] Cannot spawn players: playerPrefab is not assigned.");
            return;
        }

        if (spawnPositions.Count == 0)
        {
            Debug.LogError("[Spawner] No PlayerSpawn cells were found for player spawning.");
            return;
        }

        List<PlayerRef> activePlayers = runner.ActivePlayers
            .OrderBy(player => player.PlayerId)
            .ToList();
        List<Vector3> randomizedSpawns = BuildRandomizedSpawnPositions();

        for (int index = 0; index < activePlayers.Count; index++)
        {
            Vector3 spawnPosition = randomizedSpawns[index % randomizedSpawns.Count];
            SpawnPlayer(runner, activePlayers[index], spawnPosition, index);
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || !runner.IsServer)
        {
            return;
        }

        RegisterRunner(runner);
        EnsurePrefabAssigned();
        EnsureSpawnPoints();

        SpawnPlayer(runner, player, GetRandomAvailableSpawnPosition(runner), -1);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[Spawner] Player {player.PlayerId} left.");
    }

    private void RegisterRunner(NetworkRunner runner)
    {
        if (registeredRunner == runner)
        {
            return;
        }

        if (registeredRunner != null)
        {
            registeredRunner.RemoveCallbacks(this);
        }

        registeredRunner = runner;
        registeredRunner.RemoveCallbacks(this);
        registeredRunner.AddCallbacks(this);
    }

    private void SpawnPlayer(NetworkRunner runner, PlayerRef player, Vector3 spawnPosition, int playerIndex)
    {
        if (runner == null || playerPrefab == null || spawnPositions.Count == 0)
        {
            return;
        }

        if (runner.GetPlayerObject(player) != null)
        {
            return;
        }

        NetworkObject playerObject = runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);
        if (playerObject == null)
        {
            return;
        }

        runner.SetPlayerObject(player, playerObject);

        OnlinePlayerController controller = playerObject.GetComponent<OnlinePlayerController>();
        if (controller != null)
        {
            controller.name = playerIndex >= 0 ? $"Player{playerIndex + 1}" : $"Player{player.PlayerId}";
        }

        Debug.Log($"[Spawner] Spawned player {player.PlayerId} at {spawnPosition}.");
    }

    private void EnsureSpawnPoints()
    {
        if (spawnPositions.Count == 0)
        {
            InitializeSpawnPoints();
        }
    }

    private void InitializeSpawnPoints()
    {
        ResolveLevelData();

        if (levelData == null)
        {
            Debug.LogError("[Spawner] LevelData is not assigned.");
            return;
        }

        spawnPositions.Clear();

        for (int z = 0; z < levelData.height; z++)
        {
            for (int x = 0; x < levelData.width; x++)
            {
                if (levelData.GetCellType(x, z) != CellType.PlayerSpawn)
                {
                    continue;
                }

                Vector3 worldPos = GridUtility.GetCellCenter(x, z);
                worldPos += playerSpawnOffset;
                spawnPositions.Add(worldPos);
            }
        }

        Debug.Log($"[Spawner] Found {spawnPositions.Count} PlayerSpawn cells.");
    }

    private List<Vector3> BuildRandomizedSpawnPositions()
    {
        List<Vector3> randomized = new List<Vector3>(spawnPositions);
        for (int i = 0; i < randomized.Count; i++)
        {
            int swapIndex = UnityEngine.Random.Range(i, randomized.Count);
            (randomized[i], randomized[swapIndex]) = (randomized[swapIndex], randomized[i]);
        }

        return randomized;
    }

    private Vector3 GetRandomAvailableSpawnPosition(NetworkRunner runner)
    {
        List<Vector3> candidates = BuildRandomizedSpawnPositions();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!IsSpawnOccupied(runner, candidates[i]))
            {
                return candidates[i];
            }
        }

        return candidates.Count > 0 ? candidates[0] : Vector3.zero;
    }

    private bool IsSpawnOccupied(NetworkRunner runner, Vector3 spawnPosition)
    {
        if (runner == null)
        {
            return false;
        }

        float sqrThreshold = GridUtility.GetCellSize() * GridUtility.GetCellSize() * 0.25f;
        foreach (PlayerRef activePlayer in runner.ActivePlayers)
        {
            NetworkObject playerObject = runner.GetPlayerObject(activePlayer);
            if (playerObject == null)
            {
                continue;
            }

            if ((playerObject.transform.position - spawnPosition).sqrMagnitude <= sqrThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveLevelData()
    {
        ThemeManager themeManager = ThemeManager.Instance;
        SeasonTheme theme = themeManager != null ? themeManager.GetCurrentTheme() : null;
        if (theme != null && theme.levelData != null)
        {
            levelData = theme.levelData;
        }
    }

    private void EnsurePrefabAssigned()
    {
        if (playerPrefab != null)
        {
            return;
        }

        OnlineLobbyManager lobbyManager = OnlineLobbyManager.Instance;
        if (lobbyManager != null)
        {
            playerPrefab = lobbyManager.playerPrefab;
        }
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner)
    {
        spawnPositions.Clear();
    }
}
