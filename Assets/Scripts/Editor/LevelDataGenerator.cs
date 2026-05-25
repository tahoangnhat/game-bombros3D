using UnityEngine;
using UnityEditor;

public class LevelDataGenerator
{
    [MenuItem("Assets/Create Level Data/Spring Theme")]
    public static void CreateSpringLevel()
    {
        CreateLevelData("Spring", CreateSeasonalLayout());
    }

    [MenuItem("Assets/Create Level Data/Summer Theme")]
    public static void CreateSummerLevel()
    {
        CreateLevelData("Summer", CreateSeasonalLayout());
    }

    [MenuItem("Assets/Create Level Data/Autumn Theme")]
    public static void CreateAutumnLevel()
    {
        CreateLevelData("Autumn", CreateSeasonalLayout());
    }

    [MenuItem("Assets/Create Level Data/Winter Theme")]
    public static void CreateWinterLevel()
    {
        CreateLevelData("Winter", CreateSeasonalLayout());
    }

    private static void CreateLevelData(string themeName, CellType[] layout)
    {
        LevelData levelData = ScriptableObject.CreateInstance<LevelData>();
        levelData.width = 15;
        levelData.height = 13;
        levelData.layout = layout;

        string path = $"Assets/Resources/Levels/Level_{themeName}.asset";
        System.IO.Directory.CreateDirectory("Assets/Resources/Levels");
        
        AssetDatabase.CreateAsset(levelData, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"Created {themeName} level at {path}");
    }

    private static CellType[] CreateSeasonalLayout()
    {
        int width = 15;
        int height = 13;
        CellType[] layout = new CellType[width * height];

        // Initialize all to Empty
        for (int i = 0; i < layout.Length; i++)
            layout[i] = CellType.Empty;

        // Fill borders
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                if (x == 0 || x == width - 1 || z == 0 || z == height - 1)
                {
                    SetCell(layout, x, z, width, CellType.BorderWall);
                }
            }
        }

        // Fill middle walls (indestructible)
        for (int x = 1; x < width - 1; x += 2)
        {
            for (int z = 1; z < height - 1; z += 2)
            {
                SetCell(layout, x, z, width, CellType.MiddleWall);
            }
        }

        // Fill destructible walls (bình/cây) - skip player spawn areas
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < height - 1; z++)
            {
                CellType current = GetCell(layout, x, z, width);
                if (current == CellType.Empty)
                {
                    // Keep four corner spawn areas clear.
                    bool isSpawnCorner =
                        (x <= 2 && z <= 2) ||
                        (x >= width - 3 && z <= 2) ||
                        (x <= 2 && z >= height - 3) ||
                        (x >= width - 3 && z >= height - 3);
                    
                    if (!isSpawnCorner)
                    {
                        // Random placement: 60% chance
                        if (Random.value < 0.60f)
                        {
                            SetCell(layout, x, z, width, CellType.DestructibleWall);
                        }
                    }
                }
            }
        }

        return layout;
    }

    private static void SetCell(CellType[] layout, int x, int z, int width, CellType type)
    {
        if (x >= 0 && x < width && z >= 0 && z < layout.Length / width)
            layout[z * width + x] = type;
    }

    private static CellType GetCell(CellType[] layout, int x, int z, int width)
    {
        if (x >= 0 && x < width && z >= 0 && z < layout.Length / width)
            return layout[z * width + x];
        return CellType.Empty;
    }
}
