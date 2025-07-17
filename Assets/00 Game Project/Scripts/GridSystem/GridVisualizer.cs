using UnityEngine;

public class GridVisualizer : MonoBehaviour
{
    [Header("Grid Visualization")]
    public bool showGrid = true;
    public int gridWidth = 50;  // Veel groter!
    public int gridHeight = 50; // Veel groter!
    public Color gridColor = Color.white;
    public float lineAlpha = 0.3f; // Lichter voor minder visuele ruis

    [Header("Grid Center")]
    public bool centerGrid = true;
    public Vector3Int gridCenter = Vector3Int.zero;

    private Grid grid;

    void Start()
    {
        grid = GetComponent<Grid>();
    }

    void OnDrawGizmos()
    {
        if (!showGrid) return;

        grid = grid ?? GetComponent<Grid>();
        if (grid == null) return;

        Color originalColor = Gizmos.color;
        Gizmos.color = new Color(gridColor.r, gridColor.g, gridColor.b, lineAlpha);

        int startX = centerGrid ? gridCenter.x - gridWidth / 2 : 0;
        int endX = centerGrid ? gridCenter.x + gridWidth / 2 : gridWidth;
        int startZ = centerGrid ? gridCenter.z - gridHeight / 2 : 0;
        int endZ = centerGrid ? gridCenter.z + gridHeight / 2 : gridHeight;

        // Horizontale lijnen
        for (int z = startZ; z <= endZ; z++)
        {
            Vector3 start = grid.CellToWorld(new Vector3Int(startX, 0, z));
            Vector3 end = grid.CellToWorld(new Vector3Int(endX, 0, z));
            Gizmos.DrawLine(start, end);
        }

        // Verticale lijnen
        for (int x = startX; x <= endX; x++)
        {
            Vector3 start = grid.CellToWorld(new Vector3Int(x, 0, startZ));
            Vector3 end = grid.CellToWorld(new Vector3Int(x, 0, endZ));
            Gizmos.DrawLine(start, end);
        }

        Gizmos.color = originalColor;
    }
}
