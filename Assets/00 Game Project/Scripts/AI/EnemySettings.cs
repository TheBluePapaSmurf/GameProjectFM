using UnityEngine;
using UnityEngine.AI;

namespace GameProjectFM.AI.Core
{
    [System.Serializable]
    public class NavMeshMovementSettings
    {
        [Header("Movement Settings")]
        public float moveSpeed = 3.5f;
        public float rotationSpeed = 120f;
        public float acceleration = 8f;
        public float stoppingDistance = 0.1f;
        public bool autoBraking = true;

        [Header("Agent Properties")]
        public float agentRadius = 0.5f;
        public float agentHeight = 2f;
        public ObstacleAvoidanceType obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        public int avoidancePriority = 50;

        [Header("Pathfinding")]
        public int areaMask = -1;
        public bool autoRepath = true;
    }

    [System.Serializable]
    public class NavMeshPatrolSettings
    {
        [Header("Patrol Points")]
        public Transform[] patrolPoints = new Transform[0];
        public PatrolType patrolType = PatrolType.Loop;
        public float waitTimeAtPoint = 2f;
        public bool useRandomWaitTime = false;
        public Vector2 randomWaitRange = new Vector2(1f, 3f);

        [Header("Random Patrol")]
        public bool useRandomPatrol = false;
        public float randomPatrolRadius = 10f;
        public Vector3 patrolCenter = Vector3.zero;
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
        [Header("Field of View")]
        public bool showFieldOfView = true;
        public bool showFovInEditor = true;

        [Header("FOV Material")]
        [Tooltip("Custom material for FOV visualization. If null, uses default material with fovColor.")]
        public Material customFovMaterial;
        [Tooltip("Fallback color when no custom material is assigned")]
        public Color fovColor = new Color(1f, 0f, 0f, 0.3f);

        [Header("Editor Visualization")]
        public Color fovEditorColor = new Color(1f, 1f, 0f, 0.2f);
        public Color fovBorderColor = Color.yellow;

        [Header("Visual Feedback")]
        public bool showPatrolPath = true;
        public bool showTrailDetection = true;
        public bool showPlayerDetection = true;

        [Header("Colors")]
        public Color patrolPathColor = Color.yellow;
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

        [Header("Chase Speed Settings")]
        [Tooltip("How to handle speed during chase")]
        public ChaseSpeedMode chaseSpeedMode = ChaseSpeedMode.Multiplier;

        [Tooltip("Speed multiplier during chase (when using Multiplier mode)")]
        [Range(1f, 3f)]
        public float chaseSpeedMultiplier = 1.5f;

        [Tooltip("Absolute speed during chase (when using Absolute mode)")]
        [Range(1f, 10f)]
        public float absoluteChaseSpeed = 5f;

        [Tooltip("Enable smooth speed transitions")]
        public bool smoothSpeedTransition = true;

        [Tooltip("Speed transition time in seconds")]
        [Range(0.1f, 2f)]
        public float speedTransitionTime = 0.5f;

        [Header("Capture Settings")]
        [Tooltip("Distance within which player is considered caught")]
        public float captureDistance = 1.2f;

        [Tooltip("Show debug messages for chase behavior")]
        public bool showChaseDebug = true;

        [Header("Chase Visual")]
        public Color chasePathColor = Color.red;
        public Color lastKnownPositionColor = Color.orange;
    }

    public enum ChaseSpeedMode
    {
        [Tooltip("Multiply normal speed by chase speed multiplier")]
        Multiplier,
        [Tooltip("Use absolute chase speed value")]
        Absolute,
        [Tooltip("Keep normal movement speed during chase")]
        Normal
    }

}
