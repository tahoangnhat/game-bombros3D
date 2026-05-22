using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "Game/LevelData", order = 1)]
public class LevelData : ScriptableObject
{
    [Header("Grid Settings")]
    public int width = 15;
    public int height = 13;
    
    [Header("Layout")]
    public CellType[] layout;  // Array lưu toàn bộ layout (width * height)
    
    public void Initialize()
    {
        if (layout == null || layout.Length != width * height)
        {
            layout = new CellType[width * height];
        }
    }
    
    // Lấy cell type tại vị trí (x, z)
    public CellType GetCellType(int x, int z)
    {
        if (x < 0 || x >= width || z < 0 || z >= height)
            return CellType.Empty;
        
        return layout[z * width + x];
    }
    
    // Đặt cell type tại vị trí (x, z)
    public void SetCellType(int x, int z, CellType type)
    {
        if (x < 0 || x >= width || z < 0 || z >= height)
            return;
        
        layout[z * width + x] = type;
    }
}
