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

    [MenuItem("Assets/Create Level Data/Regenerate All Season Levels")]
    public static void RegenerateAllSeasonLevels()
    {
        string[] themeNames = { "Spring", "Summer", "Autumn", "Winter" };
        CellType[] layout = CreateSeasonalLayout();

        foreach (string themeName in themeNames)
        {
            string path = $"Assets/Levels/Level_{themeName}.asset";
            LevelData levelData = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (levelData == null)
            {
                CreateLevelData(themeName, layout);
                continue;
            }

            levelData.width = 15;
            levelData.height = 13;
            levelData.layout = (CellType[])layout.Clone();
            EditorUtility.SetDirty(levelData);
            Debug.Log($"Regenerated {themeName} level at {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreateLevelData(string themeName, CellType[] layout)
    {
        LevelData levelData = ScriptableObject.CreateInstance<LevelData>();
        levelData.width = 15;
        levelData.height = 13;
        levelData.layout = layout;

        string path = $"Assets/Levels/Level_{themeName}.asset";
        System.IO.Directory.CreateDirectory("Assets/Levels");
        
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
                    // Keep four corner spawn areas clear (3x3 playable zone ending at grid index 3).
                    const int spawnCornerIndex = 3;
                    bool isSpawnCorner =
                        (x <= spawnCornerIndex && z <= spawnCornerIndex) ||
                        (x >= width - 1 - spawnCornerIndex && z <= spawnCornerIndex) ||
                        (x <= spawnCornerIndex && z >= height - 1 - spawnCornerIndex) ||
                        (x >= width - 1 - spawnCornerIndex && z >= height - 1 - spawnCornerIndex);
                    
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
