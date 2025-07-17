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
    public bool rotateWhileMoving = true;

    [Header("Movement Options")]
    public bool useGridBounds = false;
    public int gridWidth = 10;
    public int gridHeight = 10;

    [Header("Alternative Bounds")]
    public bool useCustomBounds = false;
    public int minX = -50;
    public int maxX = 50;
    public int minZ = -50;
    public int maxZ = 50;

    [Header("Continuous Movement")]
    public bool allowMovementQueue = true;
    public bool instantMovement = false;

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

        currentGridPosition = grid.WorldToCell(transform.position);
        SnapToGrid();
    }

    public void MoveToDirection(Vector2Int direction)
    {
        // Roteer eerst als dat nodig is
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

        if (IsValidGridPosition(targetPosition))
        {
            moveCoroutine = StartCoroutine(MoveToPosition(targetPosition));
        }
    }

    private void RotateToDirection(Vector2Int direction)
    {
        if (!enableRotation || direction == Vector2Int.zero) return;

        // Bereken doelrotatie gebaseerd op bewegingsrichting
        float targetAngle = GetAngleFromDirection(direction);
        Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0);

        if (instantRotation)
        {
            transform.rotation = targetRotation;
        }
        else
        {
            // Start smooth rotation coroutine
            if (rotateCoroutine != null)
                StopCoroutine(rotateCoroutine);

            rotateCoroutine = StartCoroutine(RotateTowards(targetRotation));
        }
    }

    private float GetAngleFromDirection(Vector2Int direction)
    {
        // Converteer grid direction naar world angle
        if (direction == Vector2Int.up) return 0f;        // Noord
        if (direction == Vector2Int.right) return 90f;    // Oost  
        if (direction == Vector2Int.down) return 180f;    // Zuid
        if (direction == Vector2Int.left) return 270f;    // West

        // Voor diagonale beweging (als je dat later wilt toevoegen)
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

        if (IsValidGridPosition(targetPosition))
        {
            currentGridPosition = targetPosition;
            Vector3 targetPos = grid.CellToWorld(currentGridPosition);
            targetPos += grid.cellSize * 0.5f;
            targetPos.y = transform.position.y;
            transform.position = targetPos;
        }
    }

    private bool IsValidGridPosition(Vector3Int gridPos)
    {
        if (!useGridBounds && !useCustomBounds)
        {
            return true;
        }

        if (useGridBounds)
        {
            return gridPos.x >= 0 && gridPos.x < gridWidth &&
                   gridPos.z >= 0 && gridPos.z < gridHeight;
        }

        if (useCustomBounds)
        {
            return gridPos.x >= minX && gridPos.x <= maxX &&
                   gridPos.z >= minZ && gridPos.z <= maxZ;
        }

        return true;
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

        // Process queued movement
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

    // Public methods voor externe toegang
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
        if (useCustomBounds)
        {
            Gizmos.color = Color.red;
            Vector3 center = new Vector3((minX + maxX) * 0.5f, 0, (minZ + maxZ) * 0.5f);
            Vector3 size = new Vector3(maxX - minX + 1, 0.1f, maxZ - minZ + 1);
            Gizmos.DrawWireCube(center, size);
        }

        // Toon bewegingsrichting
        if (Application.isPlaying && lastMoveDirection != Vector2Int.zero)
        {
            Gizmos.color = Color.green;
            Vector3 forward = transform.forward * 0.5f;
            Gizmos.DrawRay(transform.position, forward);
        }
    }
}
