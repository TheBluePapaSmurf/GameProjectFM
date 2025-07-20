using UnityEngine;
using System.Collections;

namespace GameProjectFM.AI.Behaviors
{
    using Core;
    using Systems;

    public class ChaseBehavior : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MovementSystem movementSystem;
        [SerializeField] private DetectionSystem detectionSystem;
        [SerializeField] private ChaseSettings chaseSettings;

        // Chase state
        private bool isChasing;
        private Vector3Int lastKnownPlayerPosition;
        private float chaseStartTime;
        private float lastPlayerSeenTime;
        private bool playerInFOV;

        // Events
        public System.Action OnChaseStarted;
        public System.Action OnChaseEnded;
        public System.Action OnPlayerCaught;

        public bool IsChasing => isChasing;
        public Vector3Int LastKnownPlayerPosition => lastKnownPlayerPosition;

        public void Initialize(MovementSystem movement, DetectionSystem detection, ChaseSettings settings)
        {
            movementSystem = movement;
            detectionSystem = detection;
            chaseSettings = settings;

            // Subscribe to detection events
            detectionSystem.OnPlayerDetected += StartChase;
            detectionSystem.OnPlayerLost += HandlePlayerLost;
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

            if (chaseSettings.showChaseDebug)
            {
                Debug.Log("🏃 Starting chase! Player detected");
            }
            OnChaseStarted?.Invoke();
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

            // Update player visibility
            if (detectionSystem.PlayerDetected)
            {
                if (!playerInFOV)
                {
                    playerInFOV = true;
                    if (chaseSettings.showChaseDebug)
                    {
                        Debug.Log("👁️ Player back in sight!");
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

            // Chase behavior
            if (playerInFOV && detectionSystem.PlayerDetected)
            {
                // Player is visible, chase directly
                ChaseToPosition(lastKnownPlayerPosition);
            }
            else
            {
                // Player not visible, move to last known position
                if (chaseSettings.showChaseDebug && movementSystem.CurrentGridPosition != lastKnownPlayerPosition)
                {
                    Debug.Log($"🔍 Moving to last known position: {lastKnownPlayerPosition}");
                }
                ChaseToPosition(lastKnownPlayerPosition);
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
            if (movementSystem != null)
            {
                // Convert to grid position for consistency
                Grid grid = FindFirstObjectByType<Grid>();
                if (grid != null)
                {
                    lastKnownPlayerPosition = grid.WorldToCell(playerWorldPosition);
                }
            }
        }

        private void ChaseToPosition(Vector3Int targetPosition)
        {
            if (movementSystem.IsMoving) return;

            Vector3Int currentPos = movementSystem.CurrentGridPosition;
            if (currentPos == targetPosition)
            {
                // Reached last known position, search briefly
                if (!playerInFOV)
                {
                    StartCoroutine(SearchAtCurrentPosition());
                }
                return;
            }

            // Calculate direction to target
            Vector3Int direction = targetPosition - currentPos;

            // Move one step towards target (grid-based movement)
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
            {
                direction = new Vector3Int(direction.x > 0 ? 1 : -1, 0, 0);
            }
            else if (direction.z != 0)
            {
                direction = new Vector3Int(0, 0, direction.z > 0 ? 1 : -1);
            }

            Vector3Int nextPosition = currentPos + direction;
            movementSystem.MoveToPosition(nextPosition);
        }

        private IEnumerator SearchAtCurrentPosition()
        {
            if (chaseSettings.showChaseDebug)
            {
                Debug.Log($"🔍 Reached last known position, searching for {chaseSettings.searchTime}s...");
            }

            yield return new WaitForSeconds(chaseSettings.searchTime);

            // If still no player found and not in FOV, end chase
            if (!detectionSystem.PlayerDetected && !playerInFOV && isChasing)
            {
                if (chaseSettings.showChaseDebug)
                {
                    Debug.Log("🚫 Search unsuccessful, ending chase");
                }
                EndChase();
            }
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

            if (chaseSettings.showChaseDebug)
            {
                Debug.Log("🏁 Chase ended");
            }
            OnChaseEnded?.Invoke();
        }

        public void ForceEndChase()
        {
            StopAllCoroutines();
            EndChase();
        }

        // Visual Debug
        void OnDrawGizmos()
        {
            if (!isChasing || chaseSettings == null) return;

            // Draw path to last known position
            if (lastKnownPlayerPosition != Vector3Int.zero)
            {
                Grid grid = FindFirstObjectByType<Grid>();
                if (grid != null)
                {
                    Vector3 lastKnownWorldPos = grid.CellToWorld(lastKnownPlayerPosition);
                    lastKnownWorldPos += grid.cellSize * 0.5f;

                    // Draw line to last known position
                    Gizmos.color = chaseSettings.chasePathColor;
                    Gizmos.DrawLine(transform.position, lastKnownWorldPos);

                    // Draw last known position marker
                    Gizmos.color = chaseSettings.lastKnownPositionColor;
                    Gizmos.DrawWireSphere(lastKnownWorldPos, 0.5f);

#if UNITY_EDITOR
                    UnityEditor.Handles.Label(lastKnownWorldPos + Vector3.up, "LAST SEEN");
#endif
                }
            }

            // Draw capture radius around AI
            if (detectionSystem != null && detectionSystem.PlayerDetected)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, chaseSettings.captureDistance);
            }
        }
    }
}
