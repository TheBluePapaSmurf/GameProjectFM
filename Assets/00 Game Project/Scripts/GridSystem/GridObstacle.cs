using UnityEngine;

[System.Serializable]
public class GridObstacle : MonoBehaviour
{
    [Header("Obstacle Settings")]
    [Tooltip("Positions this obstacle blocks (relative to transform position)")]
    public Vector3Int[] blockedPositions = { Vector3Int.zero };

    [Header("Visualization")]
    public bool showBlockedCells = true;
    public Color obstacleColor = Color.red;

    private Grid grid;
    private Vector3Int[] worldBlockedPositions;

    void Awake()
    {
        grid = FindFirstObjectByType<Grid>();
        CalculateWorldPositions();
    }

    void Start()
    {
        // Register dit obstacle bij het GridObstacleManager
        GridObstacleManager obstacleManager = FindFirstObjectByType<GridObstacleManager>();
        if (obstacleManager != null)
        {
            obstacleManager.RegisterObstacle(this);
        }
    }

    void OnDestroy()
    {
        // Unregister bij het GridObstacleManager
        GridObstacleManager obstacleManager = FindFirstObjectByType<GridObstacleManager>();
        if (obstacleManager != null)
        {
            obstacleManager.UnregisterObstacle(this);
        }
    }

    private void CalculateWorldPositions()
    {
        if (grid == null) return;

        Vector3Int obstacleGridPos = grid.WorldToCell(transform.position);
        worldBlockedPositions = new Vector3Int[blockedPositions.Length];

        for (int i = 0; i < blockedPositions.Length; i++)
        {
            worldBlockedPositions[i] = obstacleGridPos + blockedPositions[i];
        }
    }

    public Vector3Int[] GetBlockedWorldPositions()
    {
        if (worldBlockedPositions == null || worldBlockedPositions.Length != blockedPositions.Length)
        {
            CalculateWorldPositions();
        }
        return worldBlockedPositions;
    }

    public bool IsPositionBlocked(Vector3Int gridPosition)
    {
        Vector3Int[] blocked = GetBlockedWorldPositions();
        foreach (Vector3Int blockedPos in blocked)
        {
            if (blockedPos == gridPosition)
                return true;
        }
        return false;
    }

    void OnDrawGizmos()
    {
        if (!showBlockedCells) return;

        grid = grid ?? FindFirstObjectByType<Grid>();
        if (grid == null) return;

        Gizmos.color = obstacleColor;
        Vector3Int obstacleGridPos = grid.WorldToCell(transform.position);

        foreach (Vector3Int relativePos in blockedPositions)
        {
            Vector3Int worldGridPos = obstacleGridPos + relativePos;
            Vector3 worldPos = grid.CellToWorld(worldGridPos);
            worldPos += grid.cellSize * 0.5f;

            Gizmos.DrawWireCube(worldPos, grid.cellSize);
            Gizmos.color = new Color(obstacleColor.r, obstacleColor.g, obstacleColor.b, 0.3f);
            Gizmos.DrawCube(worldPos, grid.cellSize);
            Gizmos.color = obstacleColor;
        }
    }

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            CalculateWorldPositions();
        }
    }
}
