using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace GameProjectFM.AI.Behaviors
{
    using Core;
    using System.Collections.Generic;
    using Systems;

    public class NavMeshLinearPatrolBehavior : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private LinearPathBehaviour linearPath;
        [SerializeField] private LookAroundSettings lookAroundSettings;
        [SerializeField] private NavMeshMovementSystem movementSystem;

        [Header("Patrol Settings")]
        [SerializeField] private float waitTimeAtPoint = 2f;
        [SerializeField] private bool useRandomWaitTime = false;
        [SerializeField] private Vector2 randomWaitRange = new Vector2(1f, 3f);

        // Patrol state
        private Transform currentPatrolPoint;
        private bool isWaiting;
        private bool isLookingAround;
        private bool isPatrolling;
        private Coroutine currentAction;

        // Events
        public System.Action OnPatrolPointReached;
        public System.Action OnPatrolCompleted;

        // Public Properties
        public bool IsPatrolling => isPatrolling && !isWaiting && !isLookingAround;
        public bool IsBusy => isWaiting || isLookingAround || (movementSystem != null && movementSystem.IsMoving);
        public Vector3 CurrentPatrolPoint => currentPatrolPoint != null ? currentPatrolPoint.position : Vector3.zero;
        public Transform CurrentPatrolTransform => currentPatrolPoint;

        public void Initialize(LinearPathBehaviour path, LookAroundSettings lookAround, NavMeshMovementSystem movement)
        {
            linearPath = path;
            lookAroundSettings = lookAround;
            movementSystem = movement;

            ValidateLinearPath();
            Debug.Log("✅ NavMeshLinearPatrolBehavior initialized with LinearPathBehaviour");
        }

        private void ValidateLinearPath()
        {
            if (linearPath == null)
            {
                Debug.LogError("❌ LinearPathBehaviour not assigned to NavMeshLinearPatrolBehavior!");
                return;
            }

            if (linearPath.transform.childCount < 2)
            {
                Debug.LogWarning("⚠️ LinearPathBehaviour needs at least 2 child transforms for patrol points!");
                return;
            }

            // Validate that all patrol points are on NavMesh
            int validPoints = 0;
            for (int i = 0; i < linearPath.transform.childCount; i++)
            {
                Transform point = linearPath.transform.GetChild(i);
                if (point != null)
                {
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(point.position, out hit, 2f, NavMesh.AllAreas))
                    {
                        validPoints++;
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ Patrol point {point.name} is not on NavMesh!");
                    }
                }
            }

            Debug.Log($"✅ Validated {validPoints}/{linearPath.transform.childCount} patrol points on NavMesh");
        }

        public void StartPatrol()
        {
            if (linearPath == null || linearPath.transform.childCount < 2)
            {
                Debug.LogWarning("⚠️ Cannot start patrol - LinearPathBehaviour not properly configured!");
                return;
            }

            isPatrolling = true;

            // Start at the closest point to current position
            currentPatrolPoint = linearPath.GetClosestPoint(transform.position);

            if (currentPatrolPoint != null)
            {
                MoveToCurrentPatrolPoint();
                Debug.Log($"🚶 Started linear patrol from closest point: {currentPatrolPoint.name}");
            }
            else
            {
                Debug.LogError("❌ Failed to find starting patrol point!");
            }
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

            Debug.Log("🛑 Linear patrol stopped");
        }

        public void ResumePatrol()
        {
            if (!IsBusy && linearPath != null && linearPath.transform.childCount >= 2)
            {
                isPatrolling = true;
                MoveToCurrentPatrolPoint();
                Debug.Log("▶️ Linear patrol resumed");
            }
        }

        public void PausePatrol()
        {
            isPatrolling = false;
            if (movementSystem != null)
            {
                movementSystem.StopMovement();
            }
            Debug.Log("⏸️ Linear patrol paused");
        }

        private void MoveToCurrentPatrolPoint()
        {
            if (!isPatrolling || currentPatrolPoint == null) return;

            Vector3 targetPosition = currentPatrolPoint.position;

            if (movementSystem != null && movementSystem.CanMoveTo(targetPosition))
            {
                bool moveSuccess = movementSystem.MoveToPositionWithCallback(targetPosition, OnReachedPatrolPoint);

                if (!moveSuccess)
                {
                    Debug.LogWarning($"⚠️ Failed to move to patrol point {currentPatrolPoint.name}");
                    // Try next patrol point
                    AdvanceToNextPatrolPoint();
                    StartCoroutine(DelayedMoveToNext());
                }
                else
                {
                    Debug.Log($"🎯 Moving to patrol point: {currentPatrolPoint.name}");
                }
            }
            else
            {
                Debug.LogWarning($"⚠️ Cannot reach patrol point {currentPatrolPoint.name}, trying next");
                AdvanceToNextPatrolPoint();
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

            Debug.Log($"🎯 Reached patrol point: {currentPatrolPoint.name}");
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
                AdvanceToNextPatrolPoint();
                OnPatrolCompleted?.Invoke();
                MoveToCurrentPatrolPoint();
            }
        }

        private IEnumerator WaitAtPatrolPoint()
        {
            isWaiting = true;

            float waitTime = useRandomWaitTime
                ? Random.Range(randomWaitRange.x, randomWaitRange.y)
                : waitTimeAtPoint;

            Debug.Log($"🕰️ Waiting at patrol point {currentPatrolPoint.name} for {waitTime:F1} seconds");
            yield return new WaitForSeconds(waitTime);

            isWaiting = false;
        }

        private IEnumerator LookAroundSequence()
        {
            isLookingAround = true;

            List<float> lookDirections = GenerateLookDirections();

            Debug.Log($"👀 Looking around in {lookDirections.Count} directions at {currentPatrolPoint.name}");

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

        private void AdvanceToNextPatrolPoint()
        {
            if (linearPath == null) return;

            Transform nextPoint = linearPath.GetNextPoint(currentPatrolPoint);
            if (nextPoint != null)
            {
                currentPatrolPoint = nextPoint;
                Debug.Log($"📍 Advanced to next patrol point: {currentPatrolPoint.name}");
            }
            else
            {
                Debug.LogWarning("⚠️ Failed to get next patrol point from LinearPathBehaviour");
            }
        }

        public Vector3 GetClosestPatrolPoint()
        {
            if (linearPath == null) return transform.position;

            Transform closestPoint = linearPath.GetClosestPoint(transform.position);
            if (closestPoint != null)
            {
                currentPatrolPoint = closestPoint;
                return closestPoint.position;
            }

            return transform.position;
        }

        public void ForceToNearestPatrolPoint()
        {
            if (linearPath == null) return;

            Transform nearestPoint = linearPath.GetClosestPoint(transform.position);
            if (nearestPoint != null)
            {
                currentPatrolPoint = nearestPoint;
                if (movementSystem != null)
                {
                    movementSystem.TeleportToPosition(nearestPoint.position);
                }
                Debug.Log($"⚡ Forced to nearest patrol point: {nearestPoint.name}");
            }
        }

        public bool IsAtPatrolPoint(float threshold = 1f)
        {
            if (currentPatrolPoint == null) return false;

            return Vector3.Distance(transform.position, currentPatrolPoint.position) <= threshold;
        }

        // Utility methods for external control
        public void SetLinearPath(LinearPathBehaviour newPath)
        {
            linearPath = newPath;
            ValidateLinearPath();
            Debug.Log($"🔄 LinearPathBehaviour updated: {(newPath != null ? newPath.name : "null")}");
        }

        public LinearPathBehaviour GetLinearPath()
        {
            return linearPath;
        }

        // Visualization
        void OnDrawGizmos()
        {
            // The LinearPathBehaviour will handle drawing the path
            // We just draw our current state

            if (!Application.isPlaying || currentPatrolPoint == null) return;

            // Draw current target
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentPatrolPoint.position, 0.8f);
            Gizmos.DrawSphere(currentPatrolPoint.position, 0.3f);

            // Draw line to current target
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, currentPatrolPoint.position);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(currentPatrolPoint.position + Vector3.up * 1.2f,
                $"TARGET: {currentPatrolPoint.name}");

            // Show current state
            Vector3 statusPos = transform.position + Vector3.up * 2f;
            string status = IsBusy ? "BUSY" : (isPatrolling ? "PATROLLING" : "IDLE");
            UnityEditor.Handles.Label(statusPos, status);
#endif
        }
    }
}
