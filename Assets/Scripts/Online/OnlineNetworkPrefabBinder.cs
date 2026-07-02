using Fusion;
using UnityEngine;

public static class OnlineNetworkPrefabBinder
{
    public static void AutoAssign(ref NetworkObject playerPrefab, ref NetworkObject bombPrefab, ref NetworkObject explosionPrefab, ref NetworkObject buffPrefab)
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

        if (buffPrefab == null)
        {
            buffPrefab = LoadFusionNetworkPrefab("Assets/Prefab/BuffItem.prefab");
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

    private static void CreateDefaultBuffPrefab()
    {
#if UNITY_EDITOR
        string path = "Assets/Prefab/BuffItem.prefab";
        if (System.IO.File.Exists(path)) return;

        Debug.Log("Generating default BuffItem prefab at Assets/Prefab/BuffItem.prefab...");
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "BuffItem";

        Collider col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        go.AddComponent<NetworkObject>();
        go.AddComponent<NetworkTransform>();
        go.AddComponent<BuffItem>();

        System.IO.Directory.CreateDirectory("Assets/Prefab");
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        Debug.Log("BuffItem prefab successfully created! Please run 'Tools > Fusion > Rebuild Prefab Table' if it doesn't show up in Fusion.");
#endif
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Game/Generate Buff Prefab")]
    public static void CreateDefaultBuffPrefabMenu()
    {
        CreateDefaultBuffPrefab();
    }
#endif
}
