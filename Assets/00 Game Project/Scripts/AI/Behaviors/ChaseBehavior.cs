using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace GameProjectFM.AI.Behaviors
{
    using Core;
    using Systems;

    public class NavMeshChaseBehavior : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private NavMeshMovementSystem navMovementSystem;
        [SerializeField] private DetectionSystem detectionSystem;
        [SerializeField] private ChaseSettings chaseSettings;

        // Chase state
        private bool isChasing;
        private Vector3 lastKnownPlayerPosition;
        private float chaseStartTime;
        private float lastPlayerSeenTime;
        private bool playerInFOV;
        private Coroutine searchCoroutine;

        // NavMesh specific
        private NavMeshAgent navMeshAgent;
        private float originalSpeed;
        private Vector3 searchStartPosition;

        // Speed transition
        private Coroutine speedTransitionCoroutine;
        private bool isTransitioningSpeed;

        // Events
        public System.Action OnChaseStarted;
        public System.Action OnChaseEnded;
        public System.Action OnPlayerCaught;

        // Public Properties
        public bool IsChasing => isChasing;
        public Vector3 LastKnownPlayerPosition => lastKnownPlayerPosition;
        public bool PlayerInFOV => playerInFOV;
        public float ChaseTimeRemaining => chaseSettings.maxChaseTime - (Time.time - chaseStartTime);
        public float PersistentChaseTimeRemaining => chaseSettings.persistentChaseTime - (Time.time - lastPlayerSeenTime);
        public float CurrentChaseSpeed => navMeshAgent != null ? navMeshAgent.speed : 0f;

        public void Initialize(NavMeshMovementSystem movement, DetectionSystem detection, ChaseSettings settings)
        {
            navMovementSystem = movement;
            detectionSystem = detection;
            chaseSettings = settings;

            // Get NavMeshAgent component
            navMeshAgent = GetComponent<NavMeshAgent>();
            if (navMeshAgent != null)
            {
                originalSpeed = navMeshAgent.speed;
            }

            // Subscribe to detection events
            if (detectionSystem != null)
            {
                detectionSystem.OnPlayerDetected += StartChase;
                detectionSystem.OnPlayerLost += HandlePlayerLost;
            }

            Debug.Log("✅ NavMeshChaseBehavior initialized");
        }

        void OnDestroy()
        {
            // Unsubscribe from events
            if (detectionSystem != null)
            {
                detectionSystem.OnPlayerDetected -= StartChase;
                detectionSystem.OnPlayerLost -= HandlePlayerLost;
            }
        }

        private void StartChase(Transform player)
        {
            if (isChasing) return;

            isChasing = true;
            chaseStartTime = Time.time;
            lastPlayerSeenTime = Time.time;
            playerInFOV = true;
            UpdateLastKnownPosition(player.position);

            // Apply chase speed based on settings
            ApplyChaseSpeed();

            if (chaseSettings.showChaseDebug)
            {
                Debug.Log($"🏃 Starting chase! Player detected at {lastKnownPlayerPosition}");
                Debug.Log($"💨 Chase speed: {CurrentChaseSpeed:F1} (Mode: {chaseSettings.chaseSpeedMode})");
            }

            OnChaseStarted?.Invoke();
        }

        private void ApplyChaseSpeed()
        {
            if (navMeshAgent == null) return;

            float targetSpeed = CalculateChaseSpeed();

            if (chaseSettings.smoothSpeedTransition && !isTransitioningSpeed)
            {
                // Smooth transition to chase speed
                if (speedTransitionCoroutine != null)
                {
                    StopCoroutine(speedTransitionCoroutine);
                }
                speedTransitionCoroutine = StartCoroutine(TransitionToSpeed(targetSpeed));
            }
            else
            {
                // Immediate speed change
                navMeshAgent.speed = targetSpeed;
            }
        }

        private void ResetToNormalSpeed()
        {
            if (navMeshAgent == null) return;

            if (chaseSettings.smoothSpeedTransition && !isTransitioningSpeed)
            {
                // Smooth transition back to normal speed
                if (speedTransitionCoroutine != null)
                {
                    StopCoroutine(speedTransitionCoroutine);
                }
                speedTransitionCoroutine = StartCoroutine(TransitionToSpeed(originalSpeed));
            }
            else
            {
                // Immediate speed reset
                navMeshAgent.speed = originalSpeed;
            }
        }

        private float CalculateChaseSpeed()
        {
            switch (chaseSettings.chaseSpeedMode)
            {
                case ChaseSpeedMode.Multiplier:
                    return originalSpeed * chaseSettings.chaseSpeedMultiplier;

                case ChaseSpeedMode.Absolute:
                    return chaseSettings.absoluteChaseSpeed;

                case ChaseSpeedMode.Normal:
                default:
                    return originalSpeed;
            }
        }

        private IEnumerator TransitionToSpeed(float targetSpeed)
        {
            isTransitioningSpeed = true;
            float startSpeed = navMeshAgent.speed;
            float elapsed = 0f;

            if (chaseSettings.showChaseDebug)
            {
                Debug.Log($"🔄 Transitioning speed from {startSpeed:F1} to {targetSpeed:F1} over {chaseSettings.speedTransitionTime:F1}s");
            }

            while (elapsed < chaseSettings.speedTransitionTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / chaseSettings.speedTransitionTime;

                // Smooth transition curve
                t = Mathf.SmoothStep(0f, 1f, t);

                navMeshAgent.speed = Mathf.Lerp(startSpeed, targetSpeed, t);
                yield return null;
            }

            navMeshAgent.speed = targetSpeed;
            isTransitioningSpeed = false;
            speedTransitionCoroutine = null;

            if (chaseSettings.showChaseDebug)
            {
                Debug.Log($"✅ Speed transition complete: {navMeshAgent.speed:F1}");
            }
        }

        private void HandlePlayerLost()
        {
            if (!isChasing) return;

            playerInFOV = false;

            if (chaseSettings.showChaseDebug)
            {
                Debug.Log($"🔍 Player lost from FOV, continuing chase for {chaseSettings.persistentChaseTime}s...");
            }
        }

        public void UpdateChase()
        {
            if (!isChasing) return;

            float timeSinceChaseStart = Time.time - chaseStartTime;
            float timeSinceLastSeen = Time.time - lastPlayerSeenTime;

            // Check for overall chase timeout
            if (timeSinceChaseStart > chaseSettings.maxChaseTime)
            {
                if (chaseSettings.showChaseDebug)
                {
                    Debug.Log($"⏰ Max chase time ({chaseSettings.maxChaseTime}s) reached, ending chase");
                }
                EndChase();
                return;
            }

            // Update player visibility and position
            if (detectionSystem.PlayerDetected)
            {
                if (!playerInFOV)
                {
                    playerInFOV = true;
                    if (chaseSettings.showChaseDebug)
                    {
                        Debug.Log("👁️ Player back in sight!");
                    }

                    // Stop any ongoing search
                    if (searchCoroutine != null)
                    {
                        StopCoroutine(searchCoroutine);
                        searchCoroutine = null;
                    }
                }
                lastPlayerSeenTime = Time.time;
                UpdateLastKnownPosition(detectionSystem.DetectedPlayer.position);
            }

            // Check for persistent chase timeout (time since last seen)
            if (!playerInFOV && timeSinceLastSeen > chaseSettings.persistentChaseTime)
            {
                if (chaseSettings.showChaseDebug)
                {
                    Debug.Log($"⏰ Persistent chase time ({chaseSettings.persistentChaseTime}s) reached, ending chase");
                }
                EndChase();
                return;
            }

            // Chase behavior based on player visibility
            if (playerInFOV && detectionSystem.PlayerDetected)
            {
                // Player is visible, chase directly to current position
                ChaseToPosition(detectionSystem.DetectedPlayer.position);
            }
            else
            {
                // Player not visible, move to last known position
                ChaseToLastKnownPosition();
            }

            // Check if player is caught
            if (IsPlayerCaught())
            {
                HandlePlayerCaptured();
            }
        }

        private bool IsPlayerCaught()
        {
            if (!detectionSystem.PlayerDetected) return false;

            float distance = detectionSystem.GetDistanceToPlayer();
            return distance <= chaseSettings.captureDistance;
        }

        private void UpdateLastKnownPosition(Vector3 playerWorldPosition)
        {
            lastKnownPlayerPosition = playerWorldPosition;

            if (chaseSettings.showChaseDebug)
            {
                Debug.Log($"📍 Updated last known player position: {lastKnownPlayerPosition}");
            }
        }

        private void ChaseToPosition(Vector3 targetPosition)
        {
            if (navMovementSystem == null) return;

            // Check if we can reach the target position
            if (navMovementSystem.CanMoveTo(targetPosition))
            {
                // Move directly to target position
                bool moveSuccess = navMovementSystem.MoveToPosition(targetPosition);

                if (chaseSettings.showChaseDebug && moveSuccess)
                {
                    Debug.Log($"🎯 Chasing to position: {targetPosition}");
                }
                else if (!moveSuccess)
                {
                    Debug.LogWarning($"⚠️ Failed to chase to position: {targetPosition}");
                }
            }
            else
            {
                // Try to get as close as possible
                Vector3 closestPosition = GetClosestReachablePosition(targetPosition);
                if (closestPosition != Vector3.zero)
                {
                    navMovementSystem.MoveToPosition(closestPosition);
                    if (chaseSettings.showChaseDebug)
                    {
                        Debug.Log($"🎯 Target unreachable, chasing to closest position: {closestPosition}");
                    }
                }
                else
                {
                    if (chaseSettings.showChaseDebug)
                    {
                        Debug.LogWarning($"⚠️ Cannot find path to target: {targetPosition}");
                    }
                }
            }
        }

        private void ChaseToLastKnownPosition()
        {
            if (lastKnownPlayerPosition == Vector3.zero) return;

            // Check if we're already close to last known position
            float distanceToLastKnown = Vector3.Distance(transform.position, lastKnownPlayerPosition);

            if (distanceToLastKnown <= 1.5f) // Close enough to last known position
            {
                // Start searching if not already searching
                if (searchCoroutine == null && !playerInFOV)
                {
                    searchCoroutine = StartCoroutine(SearchAtCurrentPosition());
                }
            }
            else
            {
                // Move to last known position
                ChaseToPosition(lastKnownPlayerPosition);

                if (chaseSettings.showChaseDebug)
                {
                    Debug.Log($"🔍 Moving to last known position: {lastKnownPlayerPosition} (distance: {distanceToLastKnown:F1})");
                }
            }
        }

        private Vector3 GetClosestReachablePosition(Vector3 targetPosition)
        {
            // Use NavMesh.SamplePosition to find closest valid position
            NavMeshHit hit;
            float searchRadius = 5f; // Start with 5 unit radius

            for (int i = 0; i < 3; i++) // Try expanding radius if needed
            {
                if (NavMesh.SamplePosition(targetPosition, out hit, searchRadius, NavMesh.AllAreas))
                {
                    // Check if we can actually path to this position
                    if (navMovementSystem.CanMoveTo(hit.position))
                    {
                        return hit.position;
                    }
                }
                searchRadius *= 2f; // Double the search radius
            }

            return Vector3.zero; // No reachable position found
        }

        private IEnumerator SearchAtCurrentPosition()
        {
            if (chaseSettings.showChaseDebug)
            {
                Debug.Log($"🔍 Reached last known position, searching for {chaseSettings.searchTime}s...");
            }

            searchStartPosition = transform.position;
            float searchTime = 0f;

            // Perform a simple search pattern (rotate to look around)
            while (searchTime < chaseSettings.searchTime && isChasing && !playerInFOV)
            {
                // Rotate slowly to search
                float rotationSpeed = 90f; // degrees per second
                transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);

                searchTime += Time.deltaTime;
                yield return null;
            }

            // If player was found during search, stop searching
            if (playerInFOV)
            {
                if (chaseSettings.showChaseDebug)
                {
                    Debug.Log("👁️ Player found during search!");
                }
                searchCoroutine = null;
                yield break;
            }

            // If still no player found, end chase
            if (!detectionSystem.PlayerDetected && !playerInFOV && isChasing)
            {
                if (chaseSettings.showChaseDebug)
                {
                    Debug.Log("🚫 Search unsuccessful, ending chase");
                }
                EndChase();
            }

            searchCoroutine = null;
        }

        private void HandlePlayerCaptured()
        {
            Debug.Log("🎯 Schoonmaker heeft Roomba te pakken en uitgezet");
            OnPlayerCaught?.Invoke();
            EndChase();
        }

        public void EndChase()
        {
            if (!isChasing) return;

            isChasing = false;
            playerInFOV = false;

            // Stop any ongoing search
            if (searchCoroutine != null)
            {
                StopCoroutine(searchCoroutine);
                searchCoroutine = null;
            }

            // Reset speed to original
            ResetToNormalSpeed();

            if (chaseSettings.showChaseDebug)
            {
                Debug.Log("🏁 Chase ended");
                Debug.Log($"💨 Speed reset to: {originalSpeed:F1}");
            }

            OnChaseEnded?.Invoke();
        }

        public void ForceEndChase()
        {
            StopAllCoroutines();
            searchCoroutine = null;
            speedTransitionCoroutine = null;
            isTransitioningSpeed = false;
            EndChase();

            if (chaseSettings.showChaseDebug)
            {
                Debug.Log("🛑 Chase force ended");
            }
        }

        public void PauseChase()
        {
            if (isChasing && navMovementSystem != null)
            {
                navMovementSystem.StopMovement();
            }
        }

        public void ResumeChase()
        {
            if (isChasing && lastKnownPlayerPosition != Vector3.zero)
            {
                ChaseToLastKnownPosition();
            }
        }

        // Public utility methods
        public float GetDistanceToLastKnownPosition()
        {
            if (lastKnownPlayerPosition == Vector3.zero) return float.MaxValue;
            return Vector3.Distance(transform.position, lastKnownPlayerPosition);
        }

        public bool IsNearLastKnownPosition(float threshold = 2f)
        {
            return GetDistanceToLastKnownPosition() <= threshold;
        }

        public Vector3 GetDirectionToLastKnownPosition()
        {
            if (lastKnownPlayerPosition == Vector3.zero) return Vector3.zero;
            return (lastKnownPlayerPosition - transform.position).normalized;
        }

        // Public speed control methods
        public void SetChaseSpeedMultiplier(float multiplier)
        {
            chaseSettings.chaseSpeedMultiplier = Mathf.Clamp(multiplier, 1f, 3f);
            if (isChasing && chaseSettings.chaseSpeedMode == ChaseSpeedMode.Multiplier)
            {
                ApplyChaseSpeed();
            }
        }

        public void SetAbsoluteChaseSpeed(float speed)
        {
            chaseSettings.absoluteChaseSpeed = Mathf.Clamp(speed, 1f, 10f);
            if (isChasing && chaseSettings.chaseSpeedMode == ChaseSpeedMode.Absolute)
            {
                ApplyChaseSpeed();
            }
        }

        public void SetChaseSpeedMode(ChaseSpeedMode mode)
        {
            chaseSettings.chaseSpeedMode = mode;
            if (isChasing)
            {
                ApplyChaseSpeed();
            }
        }

        // Visual Debug
        void OnDrawGizmos()
        {
            if (!isChasing || chaseSettings == null) return;

            // Draw path to last known position
            if (lastKnownPlayerPosition != Vector3.zero)
            {
                // Draw line to last known position
                Gizmos.color = chaseSettings.chasePathColor;
                Gizmos.DrawLine(transform.position, lastKnownPlayerPosition);

                // Draw last known position marker
                Gizmos.color = chaseSettings.lastKnownPositionColor;
                Gizmos.DrawWireSphere(lastKnownPlayerPosition, 0.5f);
                Gizmos.DrawSphere(lastKnownPlayerPosition, 0.2f);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(lastKnownPlayerPosition + Vector3.up, "LAST SEEN");
#endif
            }

            // Draw capture radius around AI
            if (detectionSystem != null && detectionSystem.PlayerDetected)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, chaseSettings.captureDistance);

                // Draw fill circle to show capture zone
                Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
                Gizmos.DrawSphere(transform.position, chaseSettings.captureDistance);
            }

            // Draw current chase state
            if (Application.isPlaying)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);

#if UNITY_EDITOR
                Vector3 statusPos = transform.position + Vector3.up * 2.5f;
                string status = $"CHASE: {(playerInFOV ? "VISUAL" : "TRACKING")}";
                UnityEditor.Handles.Label(statusPos, status);

                // Show speed info
                Vector3 speedPos = transform.position + Vector3.up * 3.5f;
                string speedInfo = $"Speed: {CurrentChaseSpeed:F1} ({chaseSettings.chaseSpeedMode})";
                UnityEditor.Handles.Label(speedPos, speedInfo);

                // Show timers
                Vector3 timerPos = transform.position + Vector3.up * 4f;
                string timers = $"Max: {ChaseTimeRemaining:F1}s | Persist: {PersistentChaseTimeRemaining:F1}s";
                UnityEditor.Handles.Label(timerPos, timers);
#endif
            }

            // Draw search area if searching
            if (searchCoroutine != null && searchStartPosition != Vector3.zero)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(searchStartPosition, 2f);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(searchStartPosition + Vector3.up * 1.5f, "SEARCHING");
#endif
            }

            // Draw NavMesh path if available
            if (navMeshAgent != null && navMeshAgent.hasPath)
            {
                Gizmos.color = Color.blue;
                Vector3[] pathCorners = navMeshAgent.path.corners;

                for (int i = 0; i < pathCorners.Length - 1; i++)
                {
                    Gizmos.DrawLine(pathCorners[i], pathCorners[i + 1]);
                }
            }
        }
    }
}
