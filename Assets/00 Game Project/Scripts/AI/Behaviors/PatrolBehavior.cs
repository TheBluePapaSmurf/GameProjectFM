using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

namespace GameProjectFM.AI.Behaviors
{
    using Core;
    using Systems;

    public class NavMeshPatrolBehavior : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private NavMeshPatrolSettings patrolSettings;
        [SerializeField] private LookAroundSettings lookAroundSettings;
        [SerializeField] private NavMeshMovementSystem movementSystem;

        // Patrol state
        private int currentPatrolIndex = 0;
        private bool isWaiting;
        private bool isLookingAround;
        private bool isPatrolling;
        private bool pingPongDirection = true; // true = forward, false = backward
        private Coroutine currentAction;

        // NavMesh specific
        private Vector3 lastPatrolPosition;
        private Transform[] validPatrolPoints;

        // Events
        public System.Action OnPatrolPointReached;
        public System.Action OnPatrolCompleted;

        // Public Properties
        public bool IsPatrolling => isPatrolling && !isWaiting && !isLookingAround;
        public bool IsBusy => isWaiting || isLookingAround || (movementSystem != null && movementSystem.IsMoving);
        public Vector3 CurrentPatrolPoint => GetCurrentPatrolPoint();
        public int CurrentPatrolIndex => currentPatrolIndex;

        public void Initialize(NavMeshPatrolSettings patrol, LookAroundSettings lookAround, NavMeshMovementSystem movement)
        {
            patrolSettings = patrol;
            lookAroundSettings = lookAround;
            movementSystem = movement;

            ValidatePatrolPoints();
            Debug.Log("✅ NavMeshPatrolBehavior initialized");
        }

        private void ValidatePatrolPoints()
        {
            if (patrolSettings?.patrolPoints == null) return;

            List<Transform> validPoints = new List<Transform>();
            foreach (Transform point in patrolSettings.patrolPoints)
            {
                if (point != null)
                {
                    // Check if point is on NavMesh
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(point.position, out hit, 2f, NavMesh.AllAreas))
                    {
                        validPoints.Add(point);
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ Patrol point {point.name} is not on NavMesh, skipping");
                    }
                }
            }

            validPatrolPoints = validPoints.ToArray();
            Debug.Log($"✅ Validated {validPatrolPoints.Length} patrol points");
        }

        public void StartPatrol()
        {
            if (validPatrolPoints == null || validPatrolPoints.Length == 0)
            {
                Debug.LogWarning("⚠️ No valid patrol points defined!");
                return;
            }

            isPatrolling = true;
            currentPatrolIndex = GetNearestPatrolPointIndex();
            MoveToCurrentPatrolPoint();

            Debug.Log($"🚶 Started patrol from point {currentPatrolIndex}");
        }

        public void StopPatrol()
        {
            isPatrolling = false;

            if (currentAction != null)
            {
                StopCoroutine(currentAction);
                currentAction = null;
            }

            isWaiting = false;
            isLookingAround = false;

            if (movementSystem != null)
            {
                movementSystem.StopMovement();
            }

            Debug.Log("🛑 Patrol stopped");
        }

        public void ResumePatrol()
        {
            if (!IsBusy && validPatrolPoints != null && validPatrolPoints.Length > 0)
            {
                isPatrolling = true;
                MoveToCurrentPatrolPoint();
                Debug.Log("▶️ Patrol resumed");
            }
        }

        public void PausePatrol()
        {
            isPatrolling = false;
            if (movementSystem != null)
            {
                movementSystem.StopMovement();
            }
            Debug.Log("⏸️ Patrol paused");
        }

        public Vector3 GetNextPatrolPoint()
        {
            if (validPatrolPoints == null || validPatrolPoints.Length == 0)
                return transform.position;

            switch (patrolSettings.patrolType)
            {
                case PatrolType.Loop:
                    return GetNextLoopPoint();

                case PatrolType.PingPong:
                    return GetNextPingPongPoint();

                case PatrolType.Random:
                    return GetRandomPatrolPoint();

                default:
                    return validPatrolPoints[currentPatrolIndex].position;
            }
        }

        public Vector3 GetClosestPatrolPoint()
        {
            if (validPatrolPoints == null || validPatrolPoints.Length == 0)
                return transform.position;

            Transform closestPoint = validPatrolPoints[0];
            float minDistance = Vector3.Distance(transform.position, closestPoint.position);
            int closestIndex = 0;

            for (int i = 1; i < validPatrolPoints.Length; i++)
            {
                float distance = Vector3.Distance(transform.position, validPatrolPoints[i].position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPoint = validPatrolPoints[i];
                    closestIndex = i;
                }
            }

            currentPatrolIndex = closestIndex;
            return closestPoint.position;
        }

        private int GetNearestPatrolPointIndex()
        {
            if (validPatrolPoints == null || validPatrolPoints.Length == 0) return 0;

            int nearestIndex = 0;
            float minDistance = Vector3.Distance(transform.position, validPatrolPoints[0].position);

            for (int i = 1; i < validPatrolPoints.Length; i++)
            {
                float distance = Vector3.Distance(transform.position, validPatrolPoints[i].position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        private Vector3 GetCurrentPatrolPoint()
        {
            if (validPatrolPoints == null || validPatrolPoints.Length == 0 ||
                currentPatrolIndex >= validPatrolPoints.Length)
                return transform.position;

            return validPatrolPoints[currentPatrolIndex].position;
        }

        private void MoveToCurrentPatrolPoint()
        {
            if (!isPatrolling || validPatrolPoints == null || validPatrolPoints.Length == 0) return;

            Vector3 targetPoint = GetCurrentPatrolPoint();

            if (movementSystem != null && movementSystem.CanMoveTo(targetPoint))
            {
                lastPatrolPosition = targetPoint;
                bool moveSuccess = movementSystem.MoveToPositionWithCallback(targetPoint, OnReachedPatrolPoint);

                if (!moveSuccess)
                {
                    Debug.LogWarning($"⚠️ Failed to move to patrol point {currentPatrolIndex}");
                    // Try next patrol point
                    AdvancePatrolIndex();
                    StartCoroutine(DelayedMoveToNext());
                }
            }
            else
            {
                Debug.LogWarning($"⚠️ Cannot reach patrol point {currentPatrolIndex}, trying next");
                AdvancePatrolIndex();
                StartCoroutine(DelayedMoveToNext());
            }
        }

        private IEnumerator DelayedMoveToNext()
        {
            yield return new WaitForSeconds(0.5f);
            MoveToCurrentPatrolPoint();
        }

        private void OnReachedPatrolPoint()
        {
            if (!isPatrolling) return;

            Debug.Log($"🎯 Reached patrol point {currentPatrolIndex}");
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

            // Move to next patrol point if still patrolling
            if (isPatrolling)
            {
                AdvancePatrolIndex();
                OnPatrolCompleted?.Invoke();
                MoveToCurrentPatrolPoint();
            }
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

            Debug.Log($"👀 Looking around in {lookDirections.Count} directions");

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
                // Shuffle the directions
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

        private Vector3 GetNextLoopPoint()
        {
            int nextIndex = (currentPatrolIndex + 1) % validPatrolPoints.Length;
            return validPatrolPoints[nextIndex].position;
        }

        private Vector3 GetNextPingPongPoint()
        {
            if (pingPongDirection)
            {
                if (currentPatrolIndex >= validPatrolPoints.Length - 1)
                {
                    pingPongDirection = false;
                    return validPatrolPoints[currentPatrolIndex - 1].position;
                }
                else
                {
                    return validPatrolPoints[currentPatrolIndex + 1].position;
                }
            }
            else
            {
                if (currentPatrolIndex <= 0)
                {
                    pingPongDirection = true;
                    return validPatrolPoints[1].position;
                }
                else
                {
                    return validPatrolPoints[currentPatrolIndex - 1].position;
                }
            }
        }

        private Vector3 GetRandomPatrolPoint()
        {
            int randomIndex = Random.Range(0, validPatrolPoints.Length);
            // Avoid staying at the same point
            while (randomIndex == currentPatrolIndex && validPatrolPoints.Length > 1)
            {
                randomIndex = Random.Range(0, validPatrolPoints.Length);
            }
            return validPatrolPoints[randomIndex].position;
        }

        private Vector3 GetRandomNearbyPoint()
        {
            if (patrolSettings.useRandomPatrol)
            {
                Vector3 centerPoint = patrolSettings.patrolCenter != Vector3.zero ?
                    patrolSettings.patrolCenter : transform.position;

                return movementSystem.GetRandomPositionAround(centerPoint, patrolSettings.randomPatrolRadius);
            }

            return transform.position;
        }

        private void AdvancePatrolIndex()
        {
            if (validPatrolPoints == null || validPatrolPoints.Length == 0) return;

            switch (patrolSettings.patrolType)
            {
                case PatrolType.Loop:
                    currentPatrolIndex = (currentPatrolIndex + 1) % validPatrolPoints.Length;
                    break;

                case PatrolType.PingPong:
                    if (pingPongDirection)
                    {
                        currentPatrolIndex++;
                        if (currentPatrolIndex >= validPatrolPoints.Length - 1)
                        {
                            pingPongDirection = false;
                        }
                    }
                    else
                    {
                        currentPatrolIndex--;
                        if (currentPatrolIndex <= 0)
                        {
                            pingPongDirection = true;
                        }
                    }
                    break;

                case PatrolType.Random:
                    int newIndex = Random.Range(0, validPatrolPoints.Length);
                    while (newIndex == currentPatrolIndex && validPatrolPoints.Length > 1)
                    {
                        newIndex = Random.Range(0, validPatrolPoints.Length);
                    }
                    currentPatrolIndex = newIndex;
                    break;

                default:
                    currentPatrolIndex = (currentPatrolIndex + 1) % validPatrolPoints.Length;
                    break;
            }

            Debug.Log($"📍 Advanced to patrol point {currentPatrolIndex}");
        }

        public void ForceToNearestPatrolPoint()
        {
            if (validPatrolPoints == null || validPatrolPoints.Length == 0) return;

            currentPatrolIndex = GetNearestPatrolPointIndex();
            if (movementSystem != null)
            {
                Vector3 nearestPoint = GetCurrentPatrolPoint();
                movementSystem.TeleportToPosition(nearestPoint);
            }

            Debug.Log($"⚡ Forced to nearest patrol point {currentPatrolIndex}");
        }

        // Public interface for external control
        public void SetPatrolIndex(int index)
        {
            if (validPatrolPoints != null && index >= 0 && index < validPatrolPoints.Length)
            {
                currentPatrolIndex = index;
                Debug.Log($"📍 Set patrol index to {currentPatrolIndex}");
            }
        }

        public bool IsAtPatrolPoint(float threshold = 1f)
        {
            if (validPatrolPoints == null || currentPatrolIndex >= validPatrolPoints.Length) return false;

            Vector3 patrolPoint = validPatrolPoints[currentPatrolIndex].position;
            return Vector3.Distance(transform.position, patrolPoint) <= threshold;
        }

        void OnDrawGizmos()
        {
            if (validPatrolPoints == null || validPatrolPoints.Length <= 1) return;

            // Draw patrol path
            Gizmos.color = Color.yellow;

            for (int i = 0; i < validPatrolPoints.Length; i++)
            {
                if (validPatrolPoints[i] == null) continue;

                Vector3 worldPos = validPatrolPoints[i].position;

                // Draw patrol point sphere
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(worldPos, 0.5f);

                // Highlight current patrol point
                if (Application.isPlaying && i == currentPatrolIndex)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawSphere(worldPos, 0.3f);
                }

                // Draw path connections
                Gizmos.color = Color.blue;
                if (patrolSettings.patrolType == PatrolType.Loop)
                {
                    int nextIndex = (i + 1) % validPatrolPoints.Length;
                    if (validPatrolPoints[nextIndex] != null)
                    {
                        Vector3 nextWorldPos = validPatrolPoints[nextIndex].position;
                        Gizmos.DrawLine(worldPos, nextWorldPos);
                        DrawArrow(worldPos, nextWorldPos);
                    }
                }
                else if (patrolSettings.patrolType == PatrolType.PingPong && i < validPatrolPoints.Length - 1)
                {
                    if (validPatrolPoints[i + 1] != null)
                    {
                        Vector3 nextWorldPos = validPatrolPoints[i + 1].position;
                        Gizmos.DrawLine(worldPos, nextWorldPos);
                    }
                }

                // Draw point number labels
#if UNITY_EDITOR
                UnityEditor.Handles.Label(worldPos + Vector3.up * 0.8f, $"P{i}");
#endif
            }

            // Draw random patrol area if enabled
            if (patrolSettings.useRandomPatrol)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
                Vector3 center = patrolSettings.patrolCenter != Vector3.zero ?
                    patrolSettings.patrolCenter : transform.position;
                Gizmos.DrawSphere(center, patrolSettings.randomPatrolRadius);

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(center, patrolSettings.randomPatrolRadius);
            }
        }

        private void DrawArrow(Vector3 from, Vector3 to)
        {
            Vector3 direction = (to - from).normalized;
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
            Vector3 midPoint = Vector3.Lerp(from, to, 0.7f);

            float arrowSize = 0.3f;
            Vector3 arrowHead1 = midPoint - direction * arrowSize + right * arrowSize * 0.5f;
            Vector3 arrowHead2 = midPoint - direction * arrowSize - right * arrowSize * 0.5f;

            Gizmos.DrawLine(midPoint, arrowHead1);
            Gizmos.DrawLine(midPoint, arrowHead2);
        }
    }
}
