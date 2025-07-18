using UnityEngine;
using System.Collections;

public class GridPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public Grid grid;

    [Header("Rotation Settings")]
    public bool enableRotation = true;
    public float rotationSpeed = 10f;
    public bool instantRotation = false;

    [Header("Movement Options")]
    public bool allowMovementQueue = true;
    public bool instantMovement = false;

    [Header("Trail Integration")]
    [Tooltip("Reference to trail manager for movement blocking")]
    public PlayerTrailManager trailManager; // ✅ Nu public!
    [Tooltip("Show feedback when move is blocked")]
    public bool showBlockedMoveFeedback = true;

    [Header("Current State")]
    public Vector3Int currentGridPosition;
    public Vector2Int lastMoveDirection;

    private bool isMoving = false;
    private bool isRotating = false;
    private Coroutine moveCoroutine;
    private Coroutine rotateCoroutine;
    private Vector2Int queuedDirection = Vector2Int.zero;

    void Start()
    {
        if (grid == null)
            grid = FindFirstObjectByType<Grid>();

        // Auto-find trail manager if not assigned
        if (trailManager == null)
            trailManager = GetComponent<PlayerTrailManager>();

        currentGridPosition = grid.WorldToCell(transform.position);
        SnapToGrid();

        // ✅ Debug info
        Debug.Log($"GridPlayerController initialized. Grid: {grid}, TrailManager: {trailManager}");
    }

    public void MoveToDirection(Vector2Int direction)
    {
        if (enableRotation && direction != Vector2Int.zero)
        {
            RotateToDirection(direction);
            lastMoveDirection = direction;
        }

        if (instantMovement)
        {
            InstantMoveToDirection(direction);
            return;
        }

        if (isMoving && allowMovementQueue)
        {
            queuedDirection = direction;
            return;
        }

        if (isMoving) return;

        Vector3Int targetPosition = currentGridPosition + new Vector3Int(direction.x, 0, direction.y);

        // Check if movement is allowed (trail blocking)
        if (!CanMoveTo(targetPosition))
        {
            if (showBlockedMoveFeedback)
            {
                Debug.Log($"Movement blocked by trail at position: {targetPosition}");
            }
            return;
        }

        moveCoroutine = StartCoroutine(MoveToPosition(targetPosition));
    }

    // Check if position is accessible
    private bool CanMoveTo(Vector3Int targetPosition)
    {
        // Als geen trail manager, altijd toestaan
        if (trailManager == null) return true;

        // Check of target position blocked is
        return trailManager.CanMoveToPosition(currentGridPosition, targetPosition);
    }

    private void RotateToDirection(Vector2Int direction)
    {
        if (!enableRotation || direction == Vector2Int.zero) return;

        float targetAngle = GetAngleFromDirection(direction);
        Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0);

        if (instantRotation)
        {
            transform.rotation = targetRotation;
        }
        else
        {
            if (rotateCoroutine != null)
                StopCoroutine(rotateCoroutine);

            rotateCoroutine = StartCoroutine(RotateTowards(targetRotation));
        }
    }

    private float GetAngleFromDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return 0f;
        if (direction == Vector2Int.right) return 90f;
        if (direction == Vector2Int.down) return 180f;
        if (direction == Vector2Int.left) return 270f;

        return Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
    }

    private IEnumerator RotateTowards(Quaternion targetRotation)
    {
        isRotating = true;
        Quaternion startRotation = transform.rotation;

        float elapsedTime = 0;
        float rotateTime = 1f / rotationSpeed;

        while (elapsedTime < rotateTime)
        {
            float t = elapsedTime / rotateTime;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRotation;
        isRotating = false;
    }

    private void InstantMoveToDirection(Vector2Int direction)
    {
        Vector3Int targetPosition = currentGridPosition + new Vector3Int(direction.x, 0, direction.y);

        // Check movement blocking voor instant movement
        if (!CanMoveTo(targetPosition))
        {
            if (showBlockedMoveFeedback)
            {
                Debug.Log($"Instant movement blocked by trail at position: {targetPosition}");
            }
            return;
        }

        currentGridPosition = targetPosition;

        Vector3 targetPos = grid.CellToWorld(currentGridPosition);
        targetPos += grid.cellSize * 0.5f;
        targetPos.y = transform.position.y;
        transform.position = targetPos;
    }

    private IEnumerator MoveToPosition(Vector3Int targetGridPos)
    {
        isMoving = true;

        Vector3 startPos = transform.position;
        Vector3 targetPos = grid.CellToWorld(targetGridPos);
        targetPos += grid.cellSize * 0.5f;
        targetPos.y = startPos.y;

        float elapsedTime = 0;
        float moveTime = 1f / moveSpeed;

        while (elapsedTime < moveTime)
        {
            float t = elapsedTime / moveTime;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        currentGridPosition = targetGridPos;
        isMoving = false;

        if (queuedDirection != Vector2Int.zero && allowMovementQueue)
        {
            Vector2Int nextDirection = queuedDirection;
            queuedDirection = Vector2Int.zero;
            MoveToDirection(nextDirection);
        }
    }

    private void SnapToGrid()
    {
        Vector3 worldPos = grid.CellToWorld(currentGridPosition);
        worldPos += grid.cellSize * 0.5f;
        worldPos.y = transform.position.y;
        transform.position = worldPos;
    }

    // Public methods
    public void SetRotationEnabled(bool enabled)
    {
        enableRotation = enabled;
    }

    public void SetInstantRotation(bool instant)
    {
        instantRotation = instant;
    }

    public Vector2Int GetLastMoveDirection()
    {
        return lastMoveDirection;
    }

    public bool IsCurrentlyMoving()
    {
        return isMoving;
    }

    public bool IsCurrentlyRotating()
    {
        return isRotating;
    }

    // ✅ Public getter for TrailManager
    public PlayerTrailManager GetTrailManager()
    {
        return trailManager;
    }

    // Trail integration methods
    public bool IsPositionBlocked(Vector3Int gridPosition)
    {
        if (trailManager == null) return false;
        return trailManager.IsPositionBlocked(gridPosition);
    }

    public void SetTrailManager(PlayerTrailManager manager)
    {
        trailManager = manager;
    }

    public Vector3Int WorldToGrid(Vector3 worldPosition)
    {
        return grid.WorldToCell(worldPosition);
    }

    public Vector3 GridToWorld(Vector3Int gridPosition)
    {
        return grid.CellToWorld(gridPosition);
    }

    void OnDrawGizmos()
    {
        // Toon bewegingsrichting
        if (Application.isPlaying && lastMoveDirection != Vector2Int.zero)
        {
            Gizmos.color = Color.green;
            Vector3 forward = transform.forward * 0.5f;
            Gizmos.DrawRay(transform.position, forward);
        }

        // Toon blocked directions
        if (Application.isPlaying && trailManager != null && trailManager.blockMovementOnTrails)
        {
            Gizmos.color = Color.red;
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

            foreach (Vector2Int dir in directions)
            {
                Vector3Int targetPos = currentGridPosition + new Vector3Int(dir.x, 0, dir.y);
                if (trailManager.IsPositionBlocked(targetPos))
                {
                    Vector3 worldPos = grid.CellToWorld(targetPos);
                    worldPos += grid.cellSize * 0.5f;
                    Vector3 direction = (worldPos - transform.position).normalized * 0.3f;

                    Gizmos.DrawLine(transform.position, transform.position + direction);
                    Gizmos.DrawWireSphere(transform.position + direction, 0.1f);
                }
            }
        }
    }
}
