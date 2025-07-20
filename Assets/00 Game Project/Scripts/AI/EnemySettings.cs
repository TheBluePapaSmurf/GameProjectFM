using UnityEngine;
using System.Collections.Generic;

namespace GameProjectFM.AI.Core
{
    [System.Serializable]
    public class GridMovementSettings
    {
        [Header("Grid Movement")]
        public Grid grid;
        public float moveSpeed = 3f;
        public bool enableRotation = true;
        public float rotationSpeed = 8f;

        [Header("Movement Curve")]
        public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public float bezierHeight = 0.5f;
        public bool useBezierMovement = true;
    }

    [System.Serializable]
    public class PatrolSettings
    {
        [Header("Patrol Settings")]
        public PatrolType patrolType = PatrolType.Loop;

        [SerializeField]
        public Vector3Int[] patrolPointsArray = new Vector3Int[4];

        [System.NonSerialized]
        private List<Vector3Int> _patrolPointsList;

        public List<Vector3Int> patrolPoints
        {
            get
            {
                if (_patrolPointsList == null)
                {
                    _patrolPointsList = new List<Vector3Int>();
                    if (patrolPointsArray != null)
                    {
                        for (int i = 0; i < patrolPointsArray.Length; i++)
                        {
                            if (patrolPointsArray[i] != Vector3Int.zero || i == 0)
                            {
                                _patrolPointsList.Add(patrolPointsArray[i]);
                            }
                        }
                    }
                }
                return _patrolPointsList;
            }
        }

        public float waitTimeAtPoint = 2f;
        public bool useRandomWaitTime = false;
        public Vector2 randomWaitRange = new Vector2(1f, 3f);

        public void RefreshPatrolPoints()
        {
            _patrolPointsList = null; // Force refresh
        }
    }


    [System.Serializable]
    public class LookAroundSettings
    {
        [Header("Look Around Behavior")]
        public bool enableLookAround = true;
        public float lookAroundDuration = 3f;
        public int lookDirections = 4;
        public float lookAngleRange = 90f;
        public bool randomLookOrder = true;
        public float timeBetweenLooks = 0.5f;
    }

    [System.Serializable]
    public class FOVSettings
    {
        [Header("Field of View")]
        public float fovAngle = 90f;
        public float fovRange = 4f;
        public int fovResolution = 20;
        public bool useFovForDetection = true;
        public LayerMask obstacleLayer = -1;
    }

    [System.Serializable]
    public class DetectionSettings
    {
        [Header("Detection")]
        public Transform player;
        public LayerMask playerLayer = -1;
        public bool chasePlayer = false;

        [Header("Detection Behavior")]
        [Tooltip("Player must be in FOV to be detected")]
        public bool requireFOVForDetection = true;

        [Tooltip("Additional checks for detection")]
        public bool requireLineOfSight = true;
    }

    [System.Serializable]
    public class TrailSettings
    {
        [Header("Trail Investigation")]
        public bool investigateTrails = true;
        public float trailCleanupTime = 3f;
        public float trailInvestigationTime = 2f;
        public bool prioritizeNewestTrails = false;
        public bool alwaysClosestFirst = true;
        public Color trailTargetColor = Color.orange;
    }

    [System.Serializable]
    public class FOVCleanupSettings
    {
        [Header("FOV Cleanup Settings")]
        public bool requireTrailInFOV = false;
        public bool requirePlayerInFOV = false;
        public bool showFOVCleanupDebug = true;
        public Color fovCleanupColor = Color.cyan;

        [Header("Alert State System")]
        [Tooltip("Once player is detected, AI enters alert state and cleans ALL trails in FOV")]
        public bool useAlertState = true;

        [Tooltip("How long the alert state lasts after last player detection")]
        public float alertStateDuration = 30f;

        [Tooltip("In alert state, automatically detect and clean all trails in FOV")]
        public bool alertStateAutoCleanFOVTrails = true;

        [Tooltip("Show debug messages for alert state")]
        public bool showAlertStateDebug = true;
    }

    [System.Serializable]
    public class VisualSettings
    {
        [Header("Visual Feedback")]
        public bool showPatrolPath = true;
        public bool showFieldOfView = true;
        public bool showFovInEditor = true;
        public bool showTrailDetection = true;
        public bool showPlayerDetection = true;

        [Header("Colors")]
        public Color patrolPathColor = Color.yellow;
        public Color fovColor = new Color(1f, 0f, 0f, 0.3f);
        public Color fovBorderColor = Color.red;
        public Color fovEditorColor = new Color(1f, 0f, 0f, 0.1f);
        public Color playerDetectedColor = Color.green;
        public Color playerInFOVColor = Color.yellow;
    }

    [System.Serializable]
    public class ChaseSettings
    {
        [Header("Chase Behavior")]
        [Tooltip("Maximum time to chase the player (in seconds)")]
        public float maxChaseTime = 15f;

        [Tooltip("Time to continue chasing after player leaves FOV (in seconds)")]
        public float persistentChaseTime = 8f;

        [Tooltip("Time to search at last known position")]
        public float searchTime = 3f;

        [Header("Capture Settings")]
        [Tooltip("Distance within which player is considered caught")]
        public float captureDistance = 1.2f;

        [Tooltip("Show debug messages for chase behavior")]
        public bool showChaseDebug = true;

        [Header("Chase Visual")]
        public Color chasePathColor = Color.red;
        public Color lastKnownPositionColor = Color.orange;
    }

}
