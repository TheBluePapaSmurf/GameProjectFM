using UnityEngine;

namespace GameProjectFM.AI.Systems
{
    using Core;

    public class DetectionSystem : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private DetectionSettings detectionSettings;
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

        public void Initialize(DetectionSettings detection, FOVSystem fov)
        {
            detectionSettings = detection;
            fovSystem = fov;
        }

        private void CheckForPlayer()
        {
            if (detectionSettings.player == null) return;

            bool wasPlayerDetected = playerDetected;
            bool currentlyDetected = IsPlayerInRange() && (fovSystem.PlayerInFOV || !detectionSettings.chasePlayer);

            if (currentlyDetected != playerDetected)
            {
                playerDetected = currentlyDetected;

                if (playerDetected)
                {
                    lastDetectionTime = Time.time;
                    Debug.Log("👁️ Player detected!");
                    OnPlayerDetected?.Invoke(detectionSettings.player);
                }
                else
                {
                    Debug.Log("👤 Player lost!");
                    OnPlayerLost?.Invoke();
                }
            }
        }

        private bool IsPlayerInRange()
        {
            if (detectionSettings.player == null) return false;

            float distance = Vector3.Distance(transform.position, detectionSettings.player.position);
            return distance <= detectionSettings.detectionRange;
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

        void OnDrawGizmos()
        {
            if (!detectionSettings.chasePlayer) return;

            // Draw detection range
            Gizmos.color = playerDetected ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionSettings.detectionRange);

            // Draw line to player if detected
            if (playerDetected && detectionSettings.player != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, detectionSettings.player.position);
            }
        }
    }
}
