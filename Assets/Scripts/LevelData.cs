using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "Game/LevelData", order = 1)]
public class LevelData : ScriptableObject
{
    [Header("Grid Settings")]
    public int width = 15;
    public int height = 13;
    
    [Header("Layout")]
    public CellType[] layout;  // Array lưu toàn bộ layout (width * height)
    
    private void OnEnable()
    {
        Initialize();
    }

    public void Initialize()
    {
        int expectedSize = width * height;
        if (expectedSize <= 0)
        {
            return;
        }

        if (layout == null)
        {
            layout = new CellType[expectedSize];
            return;
        }

        if (layout.Length == expectedSize)
        {
            return;
        }

        Debug.LogWarning(
            $"LevelData '{name}' layout size mismatch: {layout.Length} cells for {width}x{height} ({expectedSize} expected). Resizing layout.");

        CellType[] resized = new CellType[expectedSize];
        int copyLength = Mathf.Min(layout.Length, expectedSize);
        if (copyLength > 0)
        {
            System.Array.Copy(layout, resized, copyLength);
        }

        layout = resized;
    }

    // Lấy cell type tại vị trí (x, z)
    public CellType GetCellType(int x, int z)
    {
        if (x < 0 || x >= width || z < 0 || z >= height)
        {
            return CellType.Empty;
        }

        if (layout == null || layout.Length != width * height)
        {
            Initialize();
        }

        int index = z * width + x;
        if (layout == null || index < 0 || index >= layout.Length)
        {
            return CellType.Empty;
        }

        return layout[index];
    }
    
    // Đặt cell type tại vị trí (x, z)
    public void SetCellType(int x, int z, CellType type)
    {
        if (x < 0 || x >= width || z < 0 || z >= height)
        {
            return;
        }

        if (layout == null || layout.Length != width * height)
        {
            Initialize();
        }

        int index = z * width + x;
        if (layout == null || index < 0 || index >= layout.Length)
        {
            return;
        }

        layout[index] = type;
    }
}
