using UnityEngine;

public class DestructibleWall : MonoBehaviour
{
    public int CellX { get; private set; }
    public int CellZ { get; private set; }

    public void Initialize(int cellX, int cellZ)
    {
        CellX = cellX;
        CellZ = cellZ;
    }

    public bool MatchesCell(int cellX, int cellZ)
    {
        return CellX == cellX && CellZ == cellZ;
    }

    public static void DestroyAtCell(int cellX, int cellZ)
    {
        DestructibleWall[] walls = Object.FindObjectsByType<DestructibleWall>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < walls.Length; i++)
        {
            DestructibleWall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            if (wall.MatchesCell(cellX, cellZ))
            {
                Object.Destroy(wall.gameObject);
                return;
            }

            GridUtility.TryWorldToCell(wall.transform.position, out int worldCellX, out int worldCellZ);
            if (worldCellX == cellX && worldCellZ == cellZ)
            {
                Object.Destroy(wall.gameObject);
                return;
            }
        }
    }
}
