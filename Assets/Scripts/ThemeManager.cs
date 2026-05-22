using UnityEngine;

[System.Serializable]
public class PrefabOption
{
    public GameObject prefab;
    public Vector3 offset;
}

[System.Serializable]
public class SeasonTheme
{
    public string seasonName;
    public LevelData levelData;
    
    [Header("Prefabs cho mùa này")]
    public PrefabOption[] floorPrefabs;
    public PrefabOption[] borderWallPrefabs;
    public PrefabOption[] middleWallPrefabs;
    public PrefabOption[] destructibleWallPrefabs;
}

public class ThemeManager : MonoBehaviour
{
    public static ThemeManager Instance { get; private set; }

    [Header("Grid Settings")]
    public int width = 15;
    public int height = 13;
    public float cellSize = 1f;
    public Vector3 gridOrigin = Vector3.zero;

    [Header("Position Offset")]
    public Vector3 floorOffset = new Vector3(0.0f, 0, 0.0f);
    public Vector3 borderWallOffset = new Vector3(0f, 0, 0f);
    public Vector3 middleWallOffset = new Vector3(0f, 0, 0f);
    public Vector3 destructibleOffset = new Vector3(0f, 0, 0f);

    [Header("Themes")]
    [SerializeField] private SeasonTheme[] themes = new SeasonTheme[4];
    
    private int currentThemeIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // Apply first theme by default
        if (themes.Length > 0 && themes[0].levelData != null)
        {
            SetTheme(0);
        }
    }

    public void SetTheme(int themeIndex)
    {
        if (themeIndex < 0 || themeIndex >= themes.Length)
        {
            Debug.LogError($"Invalid theme index: {themeIndex}");
            return;
        }

        currentThemeIndex = themeIndex;
        ApplyTheme(themes[themeIndex]);
    }

    public void SetThemeByName(string seasonName)
    {
        for (int i = 0; i < themes.Length; i++)
        {
            if (themes[i].seasonName == seasonName)
            {
                SetTheme(i);
                return;
            }
        }
        Debug.LogError($"Theme '{seasonName}' not found");
    }

    private void ApplyTheme(SeasonTheme theme)
    {
        if (theme.levelData == null)
        {
            Debug.LogError($"Theme '{theme.seasonName}' has no LevelData assigned");
            return;
        }

        // Clear level cũ
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        // Cập nhật grid size từ LevelData
        width = theme.levelData.width;
        height = theme.levelData.height;

        // Generate level mới
        GenerateLevel(theme);
        Debug.Log($"Applied theme: {theme.seasonName}");

        // Debug: log layout info to verify LevelData is used
        LogLayoutInfo(theme.levelData, 10);
    }

    // Debug helper: print counts and sample coordinates for each CellType
    private void LogLayoutInfo(LevelData levelData, int sampleLimit = 10)
    {
        if (levelData == null) return;
        int w = levelData.width;
        int h = levelData.height;
        int countEmpty = 0, countBorder = 0, countMiddle = 0, countDestruct = 0;
        System.Text.StringBuilder sbDestruct = new System.Text.StringBuilder();

        for (int z = 0; z < h; z++)
        {
            for (int x = 0; x < w; x++)
            {
                var t = levelData.GetCellType(x, z);
                switch (t)
                {
                    case CellType.Empty: countEmpty++; break;
                    case CellType.BorderWall: countBorder++; break;
                    case CellType.MiddleWall: countMiddle++; break;
                    case CellType.DestructibleWall:
                        countDestruct++;
                        if (sbDestruct.Length == 0) sbDestruct.Append("Destructibles: ");
                        if (sbDestruct.Length < sampleLimit * 10)
                        {
                            sbDestruct.Append($"({x},{z}) ");
                        }
                        break;
                }
            }
        }

        Debug.Log($"LevelData summary: {w}x{h} Empty:{countEmpty} Border:{countBorder} Middle:{countMiddle} Destructible:{countDestruct}");
        if (sbDestruct.Length > 0) Debug.Log(sbDestruct.ToString());
    }

    private void GenerateLevel(SeasonTheme theme)
{
    // Clear cũ
    foreach (Transform child in transform)
        Destroy(child.gameObject);

    width = theme.levelData.width;
    height = theme.levelData.height;

    for (int x = 0; x < width; x++)
    {
        for (int z = 0; z < height; z++)
        {
            Vector3 basePos = new Vector3(
                gridOrigin.x + x * cellSize,
                0,
                gridOrigin.z + z * cellSize
            );

            CellType cellType = theme.levelData.GetCellType(x, z);

            switch (cellType)
            {
                case CellType.BorderWall:
                    SpawnPrefab(theme.borderWallPrefabs, basePos, borderWallOffset);
                    break;

                case CellType.MiddleWall:
                    SpawnPrefab(theme.middleWallPrefabs, basePos, middleWallOffset);
                    break;

                case CellType.DestructibleWall:
                    // Spawn sàn trước
                    SpawnPrefab(theme.floorPrefabs, basePos, floorOffset);
                    // Spawn vật phá lên trên sàn
                    SpawnDestructible(theme.destructibleWallPrefabs, basePos, destructibleOffset);
                    break;

                case CellType.Empty:
                default:
                    SpawnPrefab(theme.floorPrefabs, basePos, floorOffset);
                    break;
            }
        }
    }

    Debug.Log($"Generated level: {theme.seasonName} - {width}x{height}");
}

    private void SpawnPrefab(PrefabOption[] prefabOptions, Vector3 basePos, Vector3 offset)
    {
        if (prefabOptions == null || prefabOptions.Length == 0) return;

        PrefabOption option = prefabOptions[Random.Range(0, prefabOptions.Length)];
        if (option.prefab == null) return;

        Vector3 appliedOffset = (option.offset != Vector3.zero) ? option.offset : offset;
        Vector3 pos = basePos + appliedOffset;
        GameObject go = Instantiate(option.prefab, pos, Quaternion.identity, transform);

        // Align object so its bounds center XZ matches cell center, and bottom sits on the floor
        AlignToGridAndFloor(go, new Vector3(basePos.x + appliedOffset.x, 0f, basePos.z + appliedOffset.z), basePos.y + appliedOffset.y);
    }

    private void SpawnDestructible(PrefabOption[] prefabOptions, Vector3 basePos, Vector3 offset)
    {
        if (prefabOptions == null || prefabOptions.Length == 0) return;

        PrefabOption option = prefabOptions[Random.Range(0, prefabOptions.Length)];
        if (option.prefab == null) return;

        Vector3 appliedOffset = (option.offset != Vector3.zero) ? option.offset : offset;
        Vector3 pos = basePos + appliedOffset;
        GameObject go = Instantiate(option.prefab, pos, Quaternion.identity, transform);

        // Random rotation
        float rot = Random.Range(0, 4) * 90f;
        go.transform.rotation = Quaternion.Euler(0, rot, 0);

        // Align to grid center and floor after rotation
        AlignToGridAndFloor(go, new Vector3(basePos.x + appliedOffset.x, 0f, basePos.z + appliedOffset.z), basePos.y + appliedOffset.y);
    }

    // Align object's renderer bounds so its center XZ matches targetCenterXZ and its lowest Y sits at targetFloorY
    private void AlignToGridAndFloor(GameObject obj, Vector3 targetCenterXZ, float targetFloorY)
    {
        if (obj == null) return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;

        Bounds total = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            total.Encapsulate(renderers[i].bounds);
        }

        // current center and minY
        Vector3 currentCenter = total.center;
        float minY = total.min.y;

        // compute deltas
        Vector3 delta = Vector3.zero;
        delta.x = targetCenterXZ.x - currentCenter.x;
        delta.z = targetCenterXZ.z - currentCenter.z;
        delta.y = targetFloorY - minY;

        if (Mathf.Abs(delta.x) > 0.00001f || Mathf.Abs(delta.y) > 0.00001f || Mathf.Abs(delta.z) > 0.00001f)
        {
            obj.transform.position += delta;
        }
    }

    public Vector3 GetWorldPosition(int x, int z)
    {
        return new Vector3(gridOrigin.x + x * cellSize, 0, gridOrigin.z + z * cellSize) + floorOffset;
    }

    public SeasonTheme GetCurrentTheme()
    {
        return themes[currentThemeIndex];
    }

    public int GetThemeCount()
    {
        return themes.Length;
    }
}
