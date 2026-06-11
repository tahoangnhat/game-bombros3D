using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    private CellType selectedCellType = CellType.Empty;
    private LevelData levelData;
    private Vector2 scrollPosition;

    private void OnEnable()
    {
        levelData = (LevelData)target;
        levelData.Initialize();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(20);
        GUILayout.Label("Layout Editor", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // Tool selection
        GUILayout.Label("Select Cell Type:", EditorStyles.boldLabel);
        selectedCellType = (CellType)EditorGUILayout.EnumPopup("Cell Type", selectedCellType);

        GUILayout.Space(10);

        // Grid display
        GUILayout.Label($"Grid: {levelData.width}x{levelData.height}", EditorStyles.boldLabel);
        GUILayout.Label("Click cells to paint. Right-click to clear.", EditorStyles.helpBox);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        // Draw grid
        for (int z = levelData.height - 1; z >= 0; z--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < levelData.width; x++)
            {
                int index = z * levelData.width + x;
                CellType current = levelData.layout[index];

                string label = GetCellLabel(current);
                Color bgColor = GetCellColor(current);

                GUI.backgroundColor = bgColor;
                if (GUILayout.Button(label, GUILayout.Width(30), GUILayout.Height(30)))
                {
                    levelData.SetCellType(x, z, selectedCellType);
                    EditorUtility.SetDirty(levelData);
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        GUILayout.Space(10);

        // Actions
        if (GUILayout.Button("Clear All", GUILayout.Height(30)))
        {
            for (int i = 0; i < levelData.layout.Length; i++)
                levelData.layout[i] = CellType.Empty;
            EditorUtility.SetDirty(levelData);
        }

        if (GUILayout.Button("Fill Borders", GUILayout.Height(30)))
        {
            FillBorders();
            EditorUtility.SetDirty(levelData);
        }

        if (GUILayout.Button("Fill Middle Walls", GUILayout.Height(30)))
        {
            FillMiddleWalls();
            EditorUtility.SetDirty(levelData);
        }
    }

    private string GetCellLabel(CellType type)
{
    return type switch
    {
        CellType.Empty => ".",
        CellType.BorderWall => "B",
        CellType.MiddleWall => "M",
        CellType.DestructibleWall => "D",
        CellType.PlayerSpawn => "P",
        _ => "?"
    };
}

private Color GetCellColor(CellType type)
{
    return type switch
    {
        CellType.Empty => new Color(0.9f, 0.9f, 0.9f),
        CellType.BorderWall => new Color(0.8f, 0.6f, 0.4f),  // Brown
        CellType.MiddleWall => new Color(0.6f, 0.6f, 0.6f),  // Gray
        CellType.DestructibleWall => new Color(0.4f, 0.8f, 0.4f),  // Green
        CellType.PlayerSpawn => new Color(0.2f, 0.6f, 1f), // <-- Màu Xanh Dương nổi bật cho Spawn Point
        _ => Color.white
    };
}

    private void FillBorders()
    {
        for (int x = 0; x < levelData.width; x++)
        {
            for (int z = 0; z < levelData.height; z++)
            {
                if (x == 0 || x == levelData.width - 1 || z == 0 || z == levelData.height - 1)
                    levelData.SetCellType(x, z, CellType.BorderWall);
            }
        }
    }

    private void FillMiddleWalls()
    {
        for (int x = 0; x < levelData.width; x++)
        {
            for (int z = 0; z < levelData.height; z++)
            {
                if (x % 2 == 1 && z % 2 == 1 && x != 0 && x != levelData.width - 1 && z != 0 && z != levelData.height - 1)
                    levelData.SetCellType(x, z, CellType.MiddleWall);
            }
        }
    }
}
