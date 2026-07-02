using UnityEngine;

public static class GridUtility
{
    public static bool TryWorldToCell(Vector3 worldPosition, out int cellX, out int cellZ)
    {
        ThemeManager themeManager = ThemeManager.Instance;
        if (themeManager == null || themeManager.cellSize <= 0f)
        {
            cellX = Mathf.RoundToInt(worldPosition.x);
            cellZ = Mathf.RoundToInt(worldPosition.z);
            return false;
        }

        float originX = themeManager.gridOrigin.x + themeManager.floorOffset.x;
        float originZ = themeManager.gridOrigin.z + themeManager.floorOffset.z;

        cellX = Mathf.RoundToInt((worldPosition.x - originX) / themeManager.cellSize);
        cellZ = Mathf.RoundToInt((worldPosition.z - originZ) / themeManager.cellSize);
        return true;
    }

    public static Vector3 GetCellCenter(int cellX, int cellZ)
    {
        ThemeManager themeManager = ThemeManager.Instance;
        if (themeManager == null || themeManager.cellSize <= 0f)
        {
            return new Vector3(cellX, 0f, cellZ);
        }

        Vector3 center = themeManager.GetWorldPosition(cellX, cellZ);
        center.y = themeManager.GetWorldPosition(0, 0).y;
        return center;
    }

    public static Vector3 GetNearestCellCenter(Vector3 worldPosition)
    {
        TryWorldToCell(worldPosition, out int cellX, out int cellZ);
        return GetCellCenter(cellX, cellZ);
    }

    public static float GetCellSize()
    {
        ThemeManager themeManager = ThemeManager.Instance;
        return themeManager != null && themeManager.cellSize > 0f ? themeManager.cellSize : 1f;
    }

    public static bool IsInsideGrid(int cellX, int cellZ)
    {
        ThemeManager themeManager = ThemeManager.Instance;
        if (themeManager == null)
        {
            return true;
        }

        return cellX >= 0 && cellX < themeManager.width && cellZ >= 0 && cellZ < themeManager.height;
    }

    public static CellType GetCellType(int cellX, int cellZ)
    {
        if (!IsInsideGrid(cellX, cellZ))
        {
            return CellType.BorderWall;
        }

        ThemeManager themeManager = ThemeManager.Instance;
        if (themeManager == null)
        {
            return CellType.Empty;
        }

        SeasonTheme theme = themeManager.GetCurrentTheme();
        if (theme == null || theme.levelData == null)
        {
            return CellType.Empty;
        }

        return theme.levelData.GetCellType(cellX, cellZ);
    }

    public static bool IsCellBlockingForMovement(int cellX, int cellZ)
    {
        CellType cellType = MatchGridState.GetEffectiveCellType(cellX, cellZ);
        return cellType == CellType.BorderWall
            || cellType == CellType.MiddleWall
            || cellType == CellType.DestructibleWall;
    }
}
