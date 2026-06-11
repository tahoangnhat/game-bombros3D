using System.Collections.Generic;

public static class MatchGridState
{
    private static readonly HashSet<long> DestroyedCells = new HashSet<long>();

    public static void Reset()
    {
        DestroyedCells.Clear();
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
}
