using UnityEngine;
using System.Collections.Generic;

namespace GameProjectFM.AI.Systems
{
    using Core;

    public class FOVSystem : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private FOVSettings fovSettings;
        [SerializeField] private Transform player;

        // FOV Data
        private List<Vector3> fovPoints = new List<Vector3>();
        private bool playerInFOV;

        // Events
        public System.Action<bool> OnPlayerFOVChanged;

        public bool PlayerInFOV => playerInFOV;
        public List<Vector3> FOVPoints => fovPoints;

        void Update()
        {
            if (fovSettings.useFovForDetection)
            {
                UpdateFOV();
                CheckPlayerFOV();
            }
        }

        public void Initialize(FOVSettings settings, Transform playerTransform)
        {
            fovSettings = settings;
            player = playerTransform;
        }

        public bool IsPositionInFieldOfView(Vector3 position)
        {
            if (!fovSettings.useFovForDetection) return true;

            Vector3 directionToPosition = (position - transform.position).normalized;
            Vector3 forward = transform.forward;
            float distance = Vector3.Distance(transform.position, position);
            float angle = Vector3.Angle(forward, directionToPosition);

            // Check angle
            if (angle > fovSettings.fovAngle / 2f) return false;

            // Check distance
            if (distance > fovSettings.fovRange) return false;

            // Check line of sight
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            Vector3 rayTarget = position + Vector3.up * 0.5f;
            Vector3 rayDirection = (rayTarget - rayOrigin).normalized;
            float rayDistance = Vector3.Distance(rayOrigin, rayTarget);

            return !Physics.Raycast(rayOrigin, rayDirection, rayDistance, fovSettings.obstacleLayer);
        }

        private void UpdateFOV()
        {
            fovPoints.Clear();

            float angleStep = fovSettings.fovAngle / fovSettings.fovResolution;
            float startAngle = -fovSettings.fovAngle / 2f;

            for (int i = 0; i <= fovSettings.fovResolution; i++)
            {
                float angle = startAngle + i * angleStep;
                Vector3 rayDirection = AngleToDirection(angle);

                Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;

                if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, fovSettings.fovRange, fovSettings.obstacleLayer))
                {
                    fovPoints.Add(hit.point);
                }
                else
                {
                    fovPoints.Add(rayOrigin + rayDirection * fovSettings.fovRange);
                }
            }
        }

        private void CheckPlayerFOV()
        {
            if (player == null) return;

            bool wasPlayerInFOV = playerInFOV;
            playerInFOV = IsPositionInFieldOfView(player.position);

            if (wasPlayerInFOV != playerInFOV)
            {
                OnPlayerFOVChanged?.Invoke(playerInFOV);
            }
        }

        private Vector3 AngleToDirection(float angleInDegrees)
        {
            float angleInRadians = angleInDegrees * Mathf.Deg2Rad;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            return forward * Mathf.Cos(angleInRadians) + right * Mathf.Sin(angleInRadians);
        }

        void OnDrawGizmos()
        {
            if (!fovSettings.useFovForDetection) return;

            // Draw FOV range
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, fovSettings.fovRange);

            // Draw FOV angle
            Vector3 forward = transform.forward;
            Vector3 leftBoundary = Quaternion.Euler(0, -fovSettings.fovAngle / 2f, 0) * forward;
            Vector3 rightBoundary = Quaternion.Euler(0, fovSettings.fovAngle / 2f, 0) * forward;

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, leftBoundary * fovSettings.fovRange);
            Gizmos.DrawRay(transform.position, rightBoundary * fovSettings.fovRange);

            // Draw FOV area
            if (fovPoints.Count > 0)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
                for (int i = 0; i < fovPoints.Count - 1; i++)
                {
                    Vector3[] triangle = { transform.position, fovPoints[i], fovPoints[i + 1] };
                    DrawTriangle(triangle);
                }
            }

            // Draw player status
            if (player != null)
            {
                Gizmos.color = playerInFOV ? Color.green : Color.red;
                Gizmos.DrawLine(transform.position, player.position);
            }
        }

        private void DrawTriangle(Vector3[] points)
        {
            Gizmos.DrawLine(points[0], points[1]);
            Gizmos.DrawLine(points[1], points[2]);
            Gizmos.DrawLine(points[2], points[0]);
        }
    }
}
