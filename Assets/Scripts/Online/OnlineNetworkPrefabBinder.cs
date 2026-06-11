using Fusion;
using UnityEngine;

public static class OnlineNetworkPrefabBinder
{
    public static void AutoAssign(ref NetworkObject playerPrefab, ref NetworkObject bombPrefab, ref NetworkObject explosionPrefab)
    {
        if (playerPrefab == null)
        {
            playerPrefab = LoadFusionNetworkPrefab("Assets/Prefab/Player.prefab");
        }

        if (bombPrefab == null)
        {
            bombPrefab = LoadFusionNetworkPrefab("Assets/Prefab/Bomb.prefab");
        }

        if (explosionPrefab == null)
        {
            explosionPrefab = LoadFusionNetworkPrefab("Assets/Prefab/Explosion.prefab");
        }
    }

    public static void SetupPrefabLinks(NetworkObject playerPrefab, NetworkObject bombPrefab, NetworkObject explosionPrefab)
    {
        if (playerPrefab != null)
        {
            OnlinePlayerController playerController = playerPrefab.GetComponent<OnlinePlayerController>();
            if (playerController != null && playerController.bombPrefab == null && bombPrefab != null)
            {
                playerController.bombPrefab = bombPrefab;
            }
        }

        if (bombPrefab != null)
        {
            OnlineBomb bomb = bombPrefab.GetComponent<OnlineBomb>();
            if (bomb != null && bomb.explosionPrefab == null && explosionPrefab != null)
            {
                bomb.explosionPrefab = explosionPrefab;
            }
        }
    }

    private static NetworkObject LoadFusionNetworkPrefab(string assetPath)
    {
#if UNITY_EDITOR
        GameObject prefabRoot = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefabRoot == null)
        {
            return null;
        }

        NetworkObject[] networkObjects = prefabRoot.GetComponents<NetworkObject>();
        for (int i = 0; i < networkObjects.Length; i++)
        {
            NetworkObject candidate = networkObjects[i];
            if (candidate != null && candidate.GetType().Namespace == "Fusion")
            {
                return candidate;
            }
        }

        return prefabRoot.GetComponent<NetworkObject>();
#else
        return null;
#endif
    }
}
