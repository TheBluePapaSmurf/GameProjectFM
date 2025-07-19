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

        // Chase state
        private bool isChasing;
        private Vector3Int lastKnownPlayerPosition;
        private float chaseStartTime;

        // Chase settings
        [Header("Chase Settings")]
        [SerializeField] private float maxChaseTime = 10f;
        [SerializeField] private float searchTime = 5f;

        // Events
        public System.Action OnChaseStarted;
        public System.Action OnChaseEnded;
        public System.Action OnPlayerCaught;

        public bool IsChasing => isChasing;
        public Vector3Int LastKnownPlayerPosition => lastKnownPlayerPosition;

        public void Initialize(MovementSystem movement, DetectionSystem detection)
        {
            movementSystem = movement;
            detectionSystem = detection;

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
            UpdateLastKnownPosition(player.position);

            Debug.Log("🏃 Starting chase!");
            OnChaseStarted?.Invoke();
        }

        private void HandlePlayerLost()
        {
            if (!isChasing) return;

            Debug.Log("🔍 Player lost during chase, searching last known position...");
            StartCoroutine(SearchAtLastKnownPosition());
        }

        public void UpdateChase()
        {
            if (!isChasing) return;

            // Check for timeout
            if (Time.time - chaseStartTime > maxChaseTime)
            {
                Debug.Log("⏰ Chase timeout, ending chase");
                EndChase();
                return;
            }

            // Update chase behavior
            if (detectionSystem.PlayerDetected)
            {
                // Player is visible, chase directly
                UpdateLastKnownPosition(detectionSystem.DetectedPlayer.position);
                ChaseToPosition(lastKnownPlayerPosition);
            }
            else
            {
                // Player not visible, search last known position
                SearchAtLastKnownPosition();
            }

            // Check if player is caught
            if (IsPlayerCaught())
            {
                Debug.Log("🎯 Player caught!");
                OnPlayerCaught?.Invoke();
                EndChase();
            }
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
            if (currentPos == targetPosition) return;

            // Calculate direction to target
            Vector3Int direction = targetPosition - currentPos;

            // Move one step towards target
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

        private IEnumerator SearchAtLastKnownPosition()
        {
            Debug.Log($"🔍 Searching at last known position: {lastKnownPlayerPosition}");

            // Move to last known position
            while (movementSystem.CurrentGridPosition != lastKnownPlayerPosition && isChasing)
            {
                ChaseToPosition(lastKnownPlayerPosition);
                yield return new WaitForSeconds(0.1f);
            }

            // Search for a bit
            yield return new WaitForSeconds(searchTime);

            // If still no player found, end chase
            if (!detectionSystem.PlayerDetected && isChasing)
            {
                Debug.Log("🚫 Search unsuccessful, ending chase");
                EndChase();
            }
        }

        private bool IsPlayerCaught()
        {
            if (!detectionSystem.PlayerDetected) return false;

            float distance = detectionSystem.GetDistanceToPlayer();
            return distance < 1.5f; // Catch distance
        }

        public void EndChase()
        {
            if (!isChasing) return;

            isChasing = false;
            Debug.Log("🏁 Chase ended");
            OnChaseEnded?.Invoke();
        }

        public void ForceEndChase()
        {
            StopAllCoroutines();
            EndChase();
        }
    }
}
