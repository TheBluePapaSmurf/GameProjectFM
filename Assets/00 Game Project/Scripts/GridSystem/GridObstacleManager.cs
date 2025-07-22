using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GridObstacleManager : MonoBehaviour
{
    [Header("Obstacle Management")]
    [Tooltip("Automatically find all obstacles in scene on start")]
    public bool autoFindObstacles = true;

    [Header("Performance")]
    [Tooltip("Update obstacle positions when they move")]
    public bool trackMovingObstacles = true;

    private HashSet<Vector3Int> blockedPositions = new HashSet<Vector3Int>();
    private List<GridObstacle> registeredObstacles = new List<GridObstacle>();

    // Singleton pattern voor easy access
    public static GridObstacleManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (autoFindObstacles)
        {
            FindAndRegisterAllObstacles();
        }
    }

    public void RegisterObstacle(GridObstacle obstacle)
    {
        if (!registeredObstacles.Contains(obstacle))
        {
            registeredObstacles.Add(obstacle);
            UpdateBlockedPositions();
            Debug.Log($"Registered obstacle: {obstacle.name}");
        }
    }

    public void UnregisterObstacle(GridObstacle obstacle)
    {
        if (registeredObstacles.Remove(obstacle))
        {
            UpdateBlockedPositions();
            Debug.Log($"Unregistered obstacle: {obstacle.name}");
        }
    }

    private void FindAndRegisterAllObstacles()
    {
        GridObstacle[] obstacles = FindObjectsByType<GridObstacle>(FindObjectsSortMode.None);
        foreach (GridObstacle obstacle in obstacles)
        {
            RegisterObstacle(obstacle);
        }
    }

    private void UpdateBlockedPositions()
    {
        blockedPositions.Clear();

        foreach (GridObstacle obstacle in registeredObstacles)
        {
            if (obstacle != null)
            {
                Vector3Int[] obstaclePosisions = obstacle.GetBlockedWorldPositions();
                foreach (Vector3Int pos in obstaclePosisions)
                {
                    blockedPositions.Add(pos);
                }
            }
        }
    }

    public bool IsPositionBlocked(Vector3Int gridPosition)
    {
        return blockedPositions.Contains(gridPosition);
    }

    public bool CanMoveTo(Vector3Int fromPosition, Vector3Int toPosition)
    {
        return !IsPositionBlocked(toPosition);
    }

    public Vector3Int[] GetBlockedPositions()
    {
        return blockedPositions.ToArray();
    }

    public List<GridObstacle> GetRegisteredObstacles()
    {
        return new List<GridObstacle>(registeredObstacles);
    }

    void Update()
    {
        if (trackMovingObstacles)
        {
            // Check of obstacles bewogen zijn
            bool needsUpdate = false;
            foreach (GridObstacle obstacle in registeredObstacles)
            {
                if (obstacle != null && obstacle.transform.hasChanged)
                {
                    obstacle.transform.hasChanged = false;
                    needsUpdate = true;
                }
            }

            if (needsUpdate)
            {
                UpdateBlockedPositions();
            }
        }
    }
}
