using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace GameProjectFM.AI.Systems
{
    using Core;

    public class NavMeshMovementSystem : MonoBehaviour
    {
        [Header("NavMesh Settings")]
        [SerializeField] private NavMeshMovementSettings movementSettings;

        private NavMeshAgent navMeshAgent;
        private bool isInitialized = false;

        // Events
        public System.Action<Vector3> OnPositionChanged;
        public System.Action<bool> OnMovementStateChanged;
        public System.Action OnDestinationReached;

        // Properties
        public bool IsMoving => navMeshAgent != null && navMeshAgent.velocity.magnitude > 0.1f;
        public Vector3 CurrentPosition => transform.position;
        public bool HasPath => navMeshAgent != null && navMeshAgent.hasPath;
        public float RemainingDistance => navMeshAgent != null ? navMeshAgent.remainingDistance : 0f;

        private Vector3 lastPosition;
        private bool wasMoving;

        void Awake()
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            if (navMeshAgent == null)
            {
                navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
            }
        }

        void Update()
        {
            if (!isInitialized) return;

            CheckMovementState();
            CheckPositionChange();
            CheckDestinationReached();
        }

        public void Initialize(NavMeshMovementSettings settings)
        {
            movementSettings = settings;
            SetupNavMeshAgent();
            lastPosition = transform.position;
            isInitialized = true;

            Debug.Log("✅ NavMeshMovementSystem initialized");
        }

        private void SetupNavMeshAgent()
        {
            if (navMeshAgent == null) return;

            navMeshAgent.speed = movementSettings.moveSpeed;
            navMeshAgent.angularSpeed = movementSettings.rotationSpeed;
            navMeshAgent.acceleration = movementSettings.acceleration;
            navMeshAgent.stoppingDistance = movementSettings.stoppingDistance;
            navMeshAgent.autoBraking = movementSettings.autoBraking;
            navMeshAgent.obstacleAvoidanceType = movementSettings.obstacleAvoidanceType;
            navMeshAgent.avoidancePriority = movementSettings.avoidancePriority;
            navMeshAgent.radius = movementSettings.agentRadius;
            navMeshAgent.height = movementSettings.agentHeight;
        }

        public bool CanMoveTo(Vector3 targetPosition)
        {
            if (navMeshAgent == null) return false;

            NavMeshPath testPath = new NavMeshPath();
            return navMeshAgent.CalculatePath(targetPosition, testPath) &&
                   testPath.status == NavMeshPathStatus.PathComplete;
        }

        public bool MoveToPosition(Vector3 targetPosition)
        {
            if (navMeshAgent == null || !navMeshAgent.isOnNavMesh)
            {
                Debug.LogWarning("NavMeshAgent is not on NavMesh!");
                return false;
            }

            if (navMeshAgent.SetDestination(targetPosition))
            {
                Debug.Log($"🎯 Moving to: {targetPosition}");
                return true;
            }

            return false;
        }

        public bool MoveToPositionWithCallback(Vector3 targetPosition, System.Action onComplete)
        {
            if (MoveToPosition(targetPosition))
            {
                StartCoroutine(WaitForDestination(onComplete));
                return true;
            }
            return false;
        }

        public void StopMovement()
        {
            if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.ResetPath();
                Debug.Log("🛑 Movement stopped");
            }
        }

        public void TeleportToPosition(Vector3 targetPosition)
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.Warp(targetPosition);
                lastPosition = targetPosition;
                OnPositionChanged?.Invoke(targetPosition);
                Debug.Log($"⚡ Teleported to: {targetPosition}");
            }
        }

        public Vector3 GetDirectionToTarget(Vector3 targetPosition)
        {
            return (targetPosition - transform.position).normalized;
        }

        public float GetDistanceToTarget(Vector3 targetPosition)
        {
            if (navMeshAgent != null && navMeshAgent.hasPath)
            {
                return navMeshAgent.remainingDistance;
            }
            return Vector3.Distance(transform.position, targetPosition);
        }

        public Vector3 GetRandomPositionAround(Vector3 center, float radius)
        {
            Vector3 randomDirection = Random.insideUnitSphere * radius;
            randomDirection += center;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return center;
        }

        public bool IsNearPosition(Vector3 position, float threshold = 1f)
        {
            return Vector3.Distance(transform.position, position) <= threshold;
        }

        private void CheckMovementState()
        {
            bool currentlyMoving = IsMoving;
            if (currentlyMoving != wasMoving)
            {
                wasMoving = currentlyMoving;
                OnMovementStateChanged?.Invoke(currentlyMoving);
            }
        }

        private void CheckPositionChange()
        {
            if (Vector3.Distance(transform.position, lastPosition) > 0.1f)
            {
                lastPosition = transform.position;
                OnPositionChanged?.Invoke(transform.position);
            }
        }

        private void CheckDestinationReached()
        {
            if (navMeshAgent != null && navMeshAgent.hasPath &&
                !navMeshAgent.pathPending && navMeshAgent.remainingDistance < 0.5f)
            {
                OnDestinationReached?.Invoke();
            }
        }

        private IEnumerator WaitForDestination(System.Action onComplete)
        {
            if (navMeshAgent == null) yield break;

            yield return new WaitUntil(() =>
                !navMeshAgent.pathPending &&
                navMeshAgent.remainingDistance < movementSettings.stoppingDistance + 0.1f);

            onComplete?.Invoke();
        }

        public void SetSpeed(float newSpeed)
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.speed = newSpeed;
            }
        }

        public void EnableAgent(bool enable)
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = enable;
            }
        }

        void OnDrawGizmos()
        {
            if (navMeshAgent != null && navMeshAgent.hasPath)
            {
                Gizmos.color = Color.red;
                Vector3[] pathCorners = navMeshAgent.path.corners;

                for (int i = 0; i < pathCorners.Length - 1; i++)
                {
                    Gizmos.DrawLine(pathCorners[i], pathCorners[i + 1]);
                }

                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(navMeshAgent.destination, 0.5f);
            }
        }
    }
}
