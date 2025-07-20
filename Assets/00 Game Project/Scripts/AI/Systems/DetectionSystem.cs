using UnityEngine;

namespace GameProjectFM.AI.Systems
{
    using Core;

    public class DetectionSystem : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private DetectionSettings detectionSettings;
        [SerializeField] private FOVSettings fovSettings;
        [SerializeField] private FOVSystem fovSystem;

        // Detection state
        private bool playerDetected;
        private float lastDetectionTime;

        // Events
        public System.Action<Transform> OnPlayerDetected;
        public System.Action OnPlayerLost;

        public bool PlayerDetected => playerDetected;
        public Transform DetectedPlayer => detectionSettings.player;

        void Update()
        {
            CheckForPlayer();
        }

        public void Initialize(DetectionSettings detection, FOVSettings fov, FOVSystem fovSys)
        {
            detectionSettings = detection;
            fovSettings = fov;
            fovSystem = fovSys;
        }

        private void CheckForPlayer()
        {
            if (detectionSettings.player == null || fovSystem == null) return;

            bool wasPlayerDetected = playerDetected;
            bool currentlyDetected = IsPlayerDetectedByFOV();

            if (currentlyDetected != playerDetected)
            {
                playerDetected = currentlyDetected;

                if (playerDetected)
                {
                    lastDetectionTime = Time.time;
                    Debug.Log("👁️ Player detected by FOV!");
                    OnPlayerDetected?.Invoke(detectionSettings.player);
                }
                else
                {
                    Debug.Log("👤 Player lost from FOV!");
                    OnPlayerLost?.Invoke();
                }
            }
        }

        private bool IsPlayerDetectedByFOV()
        {
            if (detectionSettings.player == null || !fovSettings.useFovForDetection) return false;

            // Primary detection: Player must be in FOV
            bool inFOV = fovSystem.IsPositionInFieldOfView(detectionSettings.player.position);

            if (!inFOV) return false;

            // Additional check: Line of sight (if enabled)
            if (detectionSettings.requireLineOfSight)
            {
                return HasLineOfSightToPlayer();
            }

            return true;
        }

        private bool HasLineOfSightToPlayer()
        {
            Vector3 directionToPlayer = (detectionSettings.player.position - transform.position).normalized;
            float distanceToPlayer = Vector3.Distance(transform.position, detectionSettings.player.position);

            RaycastHit hit;
            if (Physics.Raycast(transform.position, directionToPlayer, out hit, distanceToPlayer, fovSettings.obstacleLayer))
            {
                // Check if we hit the player or an obstacle
                return hit.transform == detectionSettings.player;
            }

            return true; // No obstacles in the way
        }

        public float GetDistanceToPlayer()
        {
            if (detectionSettings.player == null) return float.MaxValue;
            return Vector3.Distance(transform.position, detectionSettings.player.position);
        }

        public Vector3 GetDirectionToPlayer()
        {
            if (detectionSettings.player == null) return Vector3.zero;
            return (detectionSettings.player.position - transform.position).normalized;
        }

        public bool IsPlayerInFOV()
        {
            if (detectionSettings.player == null || fovSystem == null) return false;
            return fovSystem.IsPositionInFieldOfView(detectionSettings.player.position);
        }

        void OnDrawGizmos()
        {
            if (!detectionSettings.chasePlayer || detectionSettings.player == null) return;

            // Draw line to player if in FOV
            if (playerDetected)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, detectionSettings.player.position);

#if UNITY_EDITOR
                Vector3 midPoint = Vector3.Lerp(transform.position, detectionSettings.player.position, 0.5f);
                UnityEditor.Handles.Label(midPoint + Vector3.up, "FOV DETECTED");
#endif
            }
            else if (fovSystem != null && fovSystem.IsPositionInFieldOfView(detectionSettings.player.position))
            {
                // Player in FOV but not detected (maybe line of sight blocked)
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, detectionSettings.player.position);

#if UNITY_EDITOR
                Vector3 midPoint = Vector3.Lerp(transform.position, detectionSettings.player.position, 0.5f);
                UnityEditor.Handles.Label(midPoint + Vector3.up, "IN FOV - NO LOS");
#endif
            }
        }
    }
}
