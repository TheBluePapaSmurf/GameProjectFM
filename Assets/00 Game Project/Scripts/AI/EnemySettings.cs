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
        public float detectionRange = 5f;
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
    }

    [System.Serializable]
    public class VisualSettings
    {
        [Header("Visual Feedback")]
        public bool showPatrolPath = true;
        public bool showDetectionRange = false;
        public bool showFieldOfView = true;
        public bool showFovInEditor = true;
        public bool showTrailDetection = true;
        public Color patrolPathColor = Color.yellow;
        public Color detectionColor = Color.red;
        public Color fovColor = new Color(1f, 0f, 0f, 0.3f);
        public Color fovBorderColor = Color.red;
        public Color fovEditorColor = new Color(1f, 0f, 0f, 0.1f);
    }
}
