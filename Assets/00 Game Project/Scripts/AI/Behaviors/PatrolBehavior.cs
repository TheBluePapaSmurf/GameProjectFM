using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace GameProjectFM.AI.Behaviors
{
    using Core;
    using Systems;

    public class PatrolBehavior : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private PatrolSettings patrolSettings;
        [SerializeField] private LookAroundSettings lookAroundSettings;
        [SerializeField] private MovementSystem movementSystem;

        // Patrol state
        private int currentPatrolIndex = 0;
        private bool isWaiting;
        private bool isLookingAround;
        private Coroutine currentAction;

        // Events
        public System.Action OnPatrolPointReached;
        public System.Action OnPatrolCompleted;

        public bool IsPatrolling => !isWaiting && !isLookingAround && !movementSystem.IsMoving;
        public bool IsBusy => isWaiting || isLookingAround || movementSystem.IsMoving;

        public void Initialize(PatrolSettings patrol, LookAroundSettings lookAround, MovementSystem movement)
        {
            patrolSettings = patrol;
            lookAroundSettings = lookAround;
            movementSystem = movement;
        }

        public void StartPatrol()
        {
            if (patrolSettings.patrolPoints.Count == 0)
            {
                Debug.LogWarning("No patrol points defined!");
                return;
            }

            currentPatrolIndex = 0;
            MoveToCurrentPatrolPoint();
        }

        public void StopPatrol()
        {
            if (currentAction != null)
            {
                StopCoroutine(currentAction);
                currentAction = null;
            }
            isWaiting = false;
            isLookingAround = false;
        }

        public void ResumePatrol()
        {
            if (!IsBusy && patrolSettings.patrolPoints.Count > 0)
            {
                MoveToCurrentPatrolPoint();
            }
        }

        public Vector3Int GetNextPatrolPoint()
        {
            if (patrolSettings.patrolPoints.Count == 0) return Vector3Int.zero;

            switch (patrolSettings.patrolType)
            {
                case PatrolType.Loop:
                    return GetNextLoopPoint();

                case PatrolType.PingPong:
                    return GetNextPingPongPoint();

                case PatrolType.Random:
                    return GetRandomPatrolPoint();

                case PatrolType.StayInArea:
                    return GetRandomNearbyPoint();

                default:
                    return patrolSettings.patrolPoints[currentPatrolIndex];
            }
        }

        public Vector3Int GetClosestPatrolPoint()
        {
            if (patrolSettings.patrolPoints.Count == 0) return Vector3Int.zero;

            Vector3Int closestPoint = patrolSettings.patrolPoints[0];
            float minDistance = Vector3Int.Distance(movementSystem.CurrentGridPosition, closestPoint);

            for (int i = 1; i < patrolSettings.patrolPoints.Count; i++)
            {
                float distance = Vector3Int.Distance(movementSystem.CurrentGridPosition, patrolSettings.patrolPoints[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPoint = patrolSettings.patrolPoints[i];
                    currentPatrolIndex = i;
                }
            }

            return closestPoint;
        }

        private void MoveToCurrentPatrolPoint()
        {
            if (patrolSettings.patrolPoints.Count == 0) return;

            Vector3Int targetPoint = GetNextPatrolPoint();

            if (targetPoint != movementSystem.CurrentGridPosition)
            {
                movementSystem.MoveToPosition(targetPoint);
            }
            else
            {
                StartPatrolPointSequence();
            }
        }

        private void StartPatrolPointSequence()
        {
            OnPatrolPointReached?.Invoke();

            if (currentAction != null)
            {
                StopCoroutine(currentAction);
            }

            currentAction = StartCoroutine(PatrolPointSequence());
        }

        private IEnumerator PatrolPointSequence()
        {
            // Wait at patrol point
            yield return StartCoroutine(WaitAtPatrolPoint());

            // Look around if enabled
            if (lookAroundSettings.enableLookAround)
            {
                yield return StartCoroutine(LookAroundSequence());
            }

            // Move to next patrol point
            AdvancePatrolIndex();
            OnPatrolCompleted?.Invoke();
            MoveToCurrentPatrolPoint();
        }

        private IEnumerator WaitAtPatrolPoint()
        {
            isWaiting = true;

            float waitTime = patrolSettings.useRandomWaitTime
                ? Random.Range(patrolSettings.randomWaitRange.x, patrolSettings.randomWaitRange.y)
                : patrolSettings.waitTimeAtPoint;

            Debug.Log($"🕰️ Waiting at patrol point for {waitTime:F1} seconds");
            yield return new WaitForSeconds(waitTime);

            isWaiting = false;
        }

        private IEnumerator LookAroundSequence()
        {
            isLookingAround = true;

            List<float> lookDirections = GenerateLookDirections();

            foreach (float direction in lookDirections)
            {
                yield return StartCoroutine(LookInDirection(direction));
                yield return new WaitForSeconds(lookAroundSettings.timeBetweenLooks);
            }

            isLookingAround = false;
        }

        private IEnumerator LookInDirection(float targetAngle)
        {
            Quaternion startRotation = transform.rotation;
            Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0);

            float rotationTime = 0f;
            float maxRotationTime = 0.5f;

            while (rotationTime < maxRotationTime)
            {
                rotationTime += Time.deltaTime;
                float t = rotationTime / maxRotationTime;
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }

            transform.rotation = targetRotation;
            yield return new WaitForSeconds(lookAroundSettings.lookAroundDuration / lookAroundSettings.lookDirections);
        }

        private List<float> GenerateLookDirections()
        {
            List<float> directions = new List<float>();
            float currentAngle = transform.eulerAngles.y;

            for (int i = 0; i < lookAroundSettings.lookDirections; i++)
            {
                float angle = currentAngle + (i * (lookAroundSettings.lookAngleRange / lookAroundSettings.lookDirections)) - (lookAroundSettings.lookAngleRange / 2f);
                directions.Add(angle);
            }

            if (lookAroundSettings.randomLookOrder)
            {
                for (int i = 0; i < directions.Count; i++)
                {
                    float temp = directions[i];
                    int randomIndex = Random.Range(i, directions.Count);
                    directions[i] = directions[randomIndex];
                    directions[randomIndex] = temp;
                }
            }

            return directions;
        }

        private Vector3Int GetNextLoopPoint()
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolSettings.patrolPoints.Count;
            return patrolSettings.patrolPoints[currentPatrolIndex];
        }

        private Vector3Int GetNextPingPongPoint()
        {
            // Implementation for ping-pong patrol
            // This would require additional state tracking for direction
            return GetNextLoopPoint(); // Simplified for now
        }

        private Vector3Int GetRandomPatrolPoint()
        {
            currentPatrolIndex = Random.Range(0, patrolSettings.patrolPoints.Count);
            return patrolSettings.patrolPoints[currentPatrolIndex];
        }

        private Vector3Int GetRandomNearbyPoint()
        {
            Vector3Int currentPos = movementSystem.CurrentGridPosition;
            return currentPos + new Vector3Int(
                Random.Range(-2, 3),
                0,
                Random.Range(-2, 3)
            );
        }

        private void AdvancePatrolIndex()
        {
            switch (patrolSettings.patrolType)
            {
                case PatrolType.Loop:
                    currentPatrolIndex = (currentPatrolIndex + 1) % patrolSettings.patrolPoints.Count;
                    break;

                case PatrolType.Random:
                    // Index is set in GetRandomPatrolPoint
                    break;

                default:
                    currentPatrolIndex = (currentPatrolIndex + 1) % patrolSettings.patrolPoints.Count;
                    break;
            }
        }

        void OnDrawGizmos()
        {
            if (patrolSettings.patrolPoints.Count <= 1) return;

            // Draw patrol path
            Gizmos.color = Color.yellow;

            for (int i = 0; i < patrolSettings.patrolPoints.Count; i++)
            {
                Vector3 worldPos = transform.position; // This would need the grid reference
                Gizmos.DrawWireSphere(worldPos, 0.3f);

                if (i < patrolSettings.patrolPoints.Count - 1)
                {
                    Vector3 nextWorldPos = transform.position; // This would need the grid reference
                    Gizmos.DrawLine(worldPos, nextWorldPos);
                }
                else if (patrolSettings.patrolType == PatrolType.Loop)
                {
                    Vector3 firstWorldPos = transform.position; // This would need the grid reference
                    Gizmos.DrawLine(worldPos, firstWorldPos);
                }
            }
        }
    }
}
