using UnityEngine;
using System.Collections;

namespace GameProjectFM.AI.Systems
{
    using Core;

    public class MovementSystem : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GridMovementSettings movementSettings;

        // Movement state
        private Vector3Int currentGridPosition;
        private bool isMoving;

        // Events
        public System.Action<Vector3Int> OnPositionChanged;
        public System.Action<bool> OnMovementStateChanged;

        public bool IsMoving => isMoving;
        public Vector3Int CurrentGridPosition => currentGridPosition;

        void Start()
        {
            if (movementSettings.grid != null)
            {
                currentGridPosition = movementSettings.grid.WorldToCell(transform.position);
                UpdateWorldPosition();
            }
        }

        public void Initialize(GridMovementSettings settings)
        {
            movementSettings = settings;
            if (movementSettings.grid != null)
            {
                currentGridPosition = movementSettings.grid.WorldToCell(transform.position);
                UpdateWorldPosition();
            }
        }

        public bool CanMoveTo(Vector3Int targetPosition)
        {
            if (movementSettings.grid == null) return false;

            // Basic bounds checking can be added here
            return !isMoving;
        }

        public void MoveToPosition(Vector3Int targetPosition)
        {
            if (isMoving || movementSettings.grid == null) return;

            if (CanMoveTo(targetPosition))
            {
                StartCoroutine(MovementCoroutine(targetPosition));
            }
        }

        public void TeleportToPosition(Vector3Int targetPosition)
        {
            if (movementSettings.grid == null) return;

            currentGridPosition = targetPosition;
            UpdateWorldPosition();
            OnPositionChanged?.Invoke(currentGridPosition);
        }

        private IEnumerator MovementCoroutine(Vector3Int targetPosition)
        {
            SetMoving(true);

            Vector3 startPos = transform.position;
            Vector3 endPos = movementSettings.grid.CellToWorld(targetPosition) + movementSettings.grid.cellSize * 0.5f;

            float journeyTime = Vector3.Distance(startPos, endPos) / movementSettings.moveSpeed;
            float elapsedTime = 0f;

            // Handle rotation
            if (movementSettings.enableRotation)
            {
                Vector3 direction = (endPos - startPos).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    StartCoroutine(RotateToDirection(targetRotation));
                }
            }

            // Movement with optional bezier curve
            while (elapsedTime < journeyTime)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / journeyTime;

                if (movementSettings.useBezierMovement)
                {
                    // Bezier curve movement
                    Vector3 midPoint = Vector3.Lerp(startPos, endPos, 0.5f) + Vector3.up * movementSettings.bezierHeight;
                    Vector3 currentPos = CalculateBezierPoint(t, startPos, midPoint, endPos);
                    transform.position = currentPos;
                }
                else
                {
                    // Linear movement with curve
                    float curveValue = movementSettings.movementCurve.Evaluate(t);
                    transform.position = Vector3.Lerp(startPos, endPos, curveValue);
                }

                yield return null;
            }

            // Ensure exact final position
            transform.position = endPos;
            currentGridPosition = targetPosition;

            SetMoving(false);
            OnPositionChanged?.Invoke(currentGridPosition);
        }

        private IEnumerator RotateToDirection(Quaternion targetRotation)
        {
            Quaternion startRotation = transform.rotation;
            float rotationTime = 0f;
            float maxRotationTime = 0.5f;

            while (rotationTime < maxRotationTime)
            {
                rotationTime += Time.deltaTime;
                float t = rotationTime / maxRotationTime;
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t * movementSettings.rotationSpeed);
                yield return null;
            }

            transform.rotation = targetRotation;
        }

        private Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;

            Vector3 point = uu * p0;
            point += 2 * u * t * p1;
            point += tt * p2;

            return point;
        }

        private void UpdateWorldPosition()
        {
            if (movementSettings.grid != null)
            {
                Vector3 worldPos = movementSettings.grid.CellToWorld(currentGridPosition);
                worldPos += movementSettings.grid.cellSize * 0.5f;
                transform.position = worldPos;
            }
        }

        private void SetMoving(bool moving)
        {
            isMoving = moving;
            OnMovementStateChanged?.Invoke(isMoving);
        }

        public Vector3Int GetDirectionToTarget(Vector3Int targetPosition)
        {
            Vector3Int direction = targetPosition - currentGridPosition;

            // Convert to unit direction (only move one step at a time)
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
            {
                return new Vector3Int(direction.x > 0 ? 1 : -1, 0, 0);
            }
            else if (direction.z != 0)
            {
                return new Vector3Int(0, 0, direction.z > 0 ? 1 : -1);
            }

            return Vector3Int.zero;
        }
    }
}
