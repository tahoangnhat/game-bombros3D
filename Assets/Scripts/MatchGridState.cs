using System.Collections.Generic;
using UnityEngine;

public static class MatchGridState
{
    private static readonly HashSet<long> DestroyedCells = new HashSet<long>();

    // Buff counters
    public static int RangeBuffsSpawned { get; set; }
    public static int PlacementBuffsSpawned { get; set; }
    public static bool[] PlacementBuffSpawnedForSpawnPoint { get; set; } = new bool[4];

    public static void Reset()
    {
        DestroyedCells.Clear();
        RangeBuffsSpawned = 0;
        PlacementBuffsSpawned = 0;
        
        int spawnCount = 4;
        if (ThemeManager.Instance != null)
        {
            Vector3[] spawns = ThemeManager.Instance.GetPlayerSpawnPoints();
            if (spawns != null && spawns.Length > 0)
            {
                spawnCount = spawns.Length;
            }
        }

        if (PlacementBuffSpawnedForSpawnPoint == null || PlacementBuffSpawnedForSpawnPoint.Length != spawnCount)
        {
            PlacementBuffSpawnedForSpawnPoint = new bool[spawnCount];
        }
        else
        {
            for (int i = 0; i < PlacementBuffSpawnedForSpawnPoint.Length; i++)
            {
                PlacementBuffSpawnedForSpawnPoint[i] = false;
            }
        }
    }

    public static bool IsDestroyed(int cellX, int cellZ)
    {
        return DestroyedCells.Contains(ToKey(cellX, cellZ));
    }

    public static void MarkDestroyed(int cellX, int cellZ)
    {
        DestroyedCells.Add(ToKey(cellX, cellZ));
    }

    public static CellType GetEffectiveCellType(int cellX, int cellZ)
    {
        if (IsDestroyed(cellX, cellZ))
        {
            return CellType.Empty;
        }

        return GridUtility.GetCellType(cellX, cellZ);
    }

    private static long ToKey(int cellX, int cellZ)
    {
        return ((long)cellX << 32) | (uint)cellZ;
    }

    // Consolidated Buff determination logic
    public static bool TryDetermineBuffType(int cellX, int cellZ, out BuffItem.BuffType buffType)
    {
        buffType = BuffItem.BuffType.Health; // default fallback

        float roll = Random.value;

        // 1. Check Placement Buff (20% spawn rate, max 1 per player starting area, max 4 total)
        // Segment [0.0, 0.20]
        if (roll <= 0.20f)
        {
            int spawnIndex = GetNearestSpawnPointIndex(cellX, cellZ, out int dist);
            if (spawnIndex >= 0 && spawnIndex < PlacementBuffSpawnedForSpawnPoint.Length && dist <= 3)
            {
                if (PlacementBuffsSpawned < 4 && !PlacementBuffSpawnedForSpawnPoint[spawnIndex])
                {
                    PlacementBuffSpawnedForSpawnPoint[spawnIndex] = true;
                    PlacementBuffsSpawned++;
                    buffType = BuffItem.BuffType.Placement;
                    return true;
                }
            }
            return false;
        }

        // 2. Check Range Buff (25% spawn rate, max 10 total)
        // Segment (0.20, 0.45]
        if (roll > 0.20f && roll <= 0.45f)
        {
            if (RangeBuffsSpawned < 10)
            {
                RangeBuffsSpawned++;
                buffType = BuffItem.BuffType.Range;
                return true;
            }
            return false;
        }

        // 3. Check Speed Buff (20% spawn rate, unlimited)
        // Segment (0.45, 0.65]
        if (roll > 0.45f && roll <= 0.65f)
        {
            buffType = BuffItem.BuffType.Speed;
            return true;
        }

        // 4. Check Health Buff (10% spawn rate, unlimited)
        // Segment (0.65, 0.75]
        if (roll > 0.65f && roll <= 0.75f)
        {
            buffType = BuffItem.BuffType.Health;
            return true;
        }

        return false;
    }

    private static int GetNearestSpawnPointIndex(int cellX, int cellZ, out int distance)
    {
        distance = int.MaxValue;
        if (ThemeManager.Instance == null)
        {
            return -1;
        }

        Vector3[] spawns = ThemeManager.Instance.GetPlayerSpawnPoints();
        int nearestIndex = -1;

        for (int i = 0; i < spawns.Length; i++)
        {
            GridUtility.TryWorldToCell(spawns[i], out int sx, out int sz);
            int dist = System.Math.Abs(cellX - sx) + System.Math.Abs(cellZ - sz);
            if (dist < distance)
            {
                distance = dist;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }
}
