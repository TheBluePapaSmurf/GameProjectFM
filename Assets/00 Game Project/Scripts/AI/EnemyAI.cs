using UnityEngine;
using System.Collections;

namespace GameProjectFM.AI.Core
{
    using Systems;
    using Behaviors;
    using Visual;

    public class EnemyAI : MonoBehaviour
    {
        [Header("Grid Movement")]
        [SerializeField] private GridMovementSettings gridMovementSettings = new GridMovementSettings();

        [Header("Patrol Settings")]
        [SerializeField] private PatrolSettings patrolSettings = new PatrolSettings();

        [Header("Look Around")]
        [SerializeField] private LookAroundSettings lookAroundSettings = new LookAroundSettings();

        [Header("Field of View")]
        [SerializeField] private FOVSettings fovSettings = new FOVSettings();

        [Header("Detection")]
        [SerializeField] private DetectionSettings detectionSettings = new DetectionSettings();

        [Header("Trail Investigation")]
        [SerializeField] private TrailSettings trailSettings = new TrailSettings();

        [Header("FOV Cleanup")]
        [SerializeField] private FOVCleanupSettings fovCleanupSettings = new FOVCleanupSettings();

        [Header("Visual Settings")]
        [SerializeField] private VisualSettings visualSettings = new VisualSettings();

        [Header("External Dependencies")]
        [SerializeField] private PlayerTrailManager trailManager;

        // Core Systems (auto-created)
        private MovementSystem movementSystem;
        private FOVSystem fovSystem;
        private DetectionSystem detectionSystem;

        // Behavior Systems (auto-created)
        private PatrolBehavior patrolBehavior;
        private ChaseBehavior chaseBehavior;
        private TrailInvestigationBehavior trailBehavior;

        // Visual Components (auto-created)
        private FOVVisualizer fovVisualizer;

        // AI State
        private EnemyState currentState = EnemyState.Patrolling;
        private EnemyState previousState = EnemyState.Patrolling;

        // Public Properties
        public EnemyState CurrentState => currentState;
        public bool IsMoving => movementSystem != null && movementSystem.IsMoving;
        public bool IsBusy => (patrolBehavior != null && patrolBehavior.IsBusy) ||
                              (chaseBehavior != null && chaseBehavior.IsChasing) ||
                              (trailBehavior != null && trailBehavior.IsInvestigating);
        public Vector3Int CurrentGridPosition => movementSystem != null ? movementSystem.CurrentGridPosition : Vector3Int.zero;

        void Awake()
        {
            EnsureSettingsInitialized();
            InitializeSystems();
            InitializeBehaviors();
            InitializeVisualComponents();
            ConnectEventHandlers();
        }

        public EnemyAI()
        {
            EnsureSettingsInitialized();
        }

        private void EnsureSettingsInitialized()
        {
            // Force initialization of all settings to prevent null reference exceptions
            if (gridMovementSettings == null) gridMovementSettings = new GridMovementSettings();
            if (patrolSettings == null) patrolSettings = new PatrolSettings();
            if (lookAroundSettings == null) lookAroundSettings = new LookAroundSettings();
            if (fovSettings == null) fovSettings = new FOVSettings();
            if (detectionSettings == null) detectionSettings = new DetectionSettings();
            if (trailSettings == null) trailSettings = new TrailSettings();
            if (fovCleanupSettings == null) fovCleanupSettings = new FOVCleanupSettings();
            if (visualSettings == null) visualSettings = new VisualSettings();
            
            Debug.Log("✅ EnemyAI: All settings initialized");
        }

        void Start()
        {
            InitializeAllSystems();
            StartInitialBehavior();
        }

        void Update()
        {
            HandleAIUpdates();
            HandleStateTransitions();
        }

        #region System Initialization

        private void InitializeSystems()
        {
            // Create or get movement system
            movementSystem = GetComponent<MovementSystem>();
            if (movementSystem == null)
            {
                movementSystem = gameObject.AddComponent<MovementSystem>();
            }

            // Create or get FOV system
            fovSystem = GetComponent<FOVSystem>();
            if (fovSystem == null)
            {
                fovSystem = gameObject.AddComponent<FOVSystem>();
            }

            // Create or get detection system
            detectionSystem = GetComponent<DetectionSystem>();
            if (detectionSystem == null)
            {
                detectionSystem = gameObject.AddComponent<DetectionSystem>();
            }
        }

        private void InitializeBehaviors()
        {
            // Create or get patrol behavior
            patrolBehavior = GetComponent<PatrolBehavior>();
            if (patrolBehavior == null)
            {
                patrolBehavior = gameObject.AddComponent<PatrolBehavior>();
            }

            // Create or get chase behavior
            chaseBehavior = GetComponent<ChaseBehavior>();
            if (chaseBehavior == null)
            {
                chaseBehavior = gameObject.AddComponent<ChaseBehavior>();
            }

            // Create or get trail investigation behavior
            trailBehavior = GetComponent<TrailInvestigationBehavior>();
            if (trailBehavior == null)
            {
                trailBehavior = gameObject.AddComponent<TrailInvestigationBehavior>();
            }
        }

        private void InitializeVisualComponents()
        {
            // Create or get FOV visualizer (optional)
            fovVisualizer = GetComponent<FOVVisualizer>();
            if (fovVisualizer == null)
            {
                fovVisualizer = gameObject.AddComponent<FOVVisualizer>();
            }
        }

        private void InitializeAllSystems()
        {
            // Initialize all systems with their settings
            movementSystem.Initialize(gridMovementSettings);
            fovSystem.Initialize(fovSettings, detectionSettings.player);
            detectionSystem.Initialize(detectionSettings, fovSystem);

            // Initialize behaviors
            patrolBehavior.Initialize(patrolSettings, lookAroundSettings, movementSystem);
            chaseBehavior.Initialize(movementSystem, detectionSystem);
            trailBehavior.Initialize(trailSettings, fovCleanupSettings, movementSystem, fovSystem, trailManager, gridMovementSettings.grid, detectionSettings.player);

            // Initialize visual components (optional)
            if (fovVisualizer != null)
            {
                fovVisualizer.Initialize(visualSettings, fovSystem);
            }

            Debug.Log("🤖 EnemyAI: All systems initialized successfully");
        }

        private void ConnectEventHandlers()
        {
            // Movement events
            movementSystem.OnPositionChanged += HandlePositionChanged;
            movementSystem.OnMovementStateChanged += HandleMovementStateChanged;

            // Detection events
            detectionSystem.OnPlayerDetected += HandlePlayerDetected;
            detectionSystem.OnPlayerLost += HandlePlayerLost;

            // Chase events
            chaseBehavior.OnChaseStarted += HandleChaseStarted;
            chaseBehavior.OnChaseEnded += HandleChaseEnded;
            chaseBehavior.OnPlayerCaught += HandlePlayerCaught;

            // Patrol events
            patrolBehavior.OnPatrolPointReached += HandlePatrolPointReached;
            patrolBehavior.OnPatrolCompleted += HandlePatrolCompleted;

            // Trail events
            trailBehavior.OnTrailDetected += HandleTrailDetected;
            trailBehavior.OnTrailInvestigationStarted += HandleTrailInvestigationStarted;
            trailBehavior.OnTrailCleaned += HandleTrailCleaned;
            trailBehavior.OnTrailInvestigationCompleted += HandleTrailInvestigationCompleted;

            // FOV events
            fovSystem.OnPlayerFOVChanged += HandlePlayerFOVChanged;
        }

        #endregion

        #region AI Update Logic

        private void HandleAIUpdates()
        {
            // Update behaviors based on current state
            switch (currentState)
            {
                case EnemyState.Patrolling:
                    UpdatePatrolling();
                    break;

                case EnemyState.Chasing:
                    UpdateChasing();
                    break;

                case EnemyState.MovingToTrail:
                    UpdateMovingToTrail();
                    break;

                case EnemyState.InvestigatingTrail:
                    UpdateInvestigatingTrail();
                    break;

                case EnemyState.Returning:
                    UpdateReturning();
                    break;
            }

            // Always check for trails when not chasing
            if (currentState != EnemyState.Chasing && trailSettings.investigateTrails)
            {
                trailBehavior.CheckForTrails();
            }
        }

        private void UpdatePatrolling()
        {
            if (!patrolBehavior.IsBusy)
            {
                patrolBehavior.ResumePatrol();
            }
        }

        private void UpdateChasing()
        {
            chaseBehavior.UpdateChase();
        }

        private void UpdateMovingToTrail()
        {
            if (trailBehavior.HasTrailTarget)
            {
                if (trailBehavior.IsAtTrailTarget())
                {
                    ChangeState(EnemyState.InvestigatingTrail);
                    trailBehavior.StartInvestigationSequence();
                }
                else if (!movementSystem.IsMoving)
                {
                    // Move towards trail
                    Vector3Int direction = trailBehavior.GetDirectionToTarget();
                    if (direction != Vector3Int.zero)
                    {
                        Vector3Int targetPos = movementSystem.CurrentGridPosition + direction;
                        movementSystem.MoveToPosition(targetPos);
                        Debug.Log($"🚶 Moving towards trail: {movementSystem.CurrentGridPosition} -> {targetPos} (target: {trailBehavior.CurrentTrailTarget})");
                    }
                    else
                    {
                        Debug.LogWarning($"No valid direction to trail target {trailBehavior.CurrentTrailTarget}");
                        ChangeState(EnemyState.Returning);
                    }
                }
            }
            else
            {
                ChangeState(EnemyState.Returning);
            }
        }

        private void UpdateInvestigatingTrail()
        {
            // Investigation is handled by coroutine in TrailInvestigationBehavior
            // State will change when investigation is complete
        }

        private void UpdateReturning()
        {
            if (!movementSystem.IsMoving)
            {
                // Return to closest patrol point
                Vector3Int closestPatrolPoint = patrolBehavior.GetClosestPatrolPoint();
                if (closestPatrolPoint != movementSystem.CurrentGridPosition)
                {
                    Vector3Int direction = movementSystem.GetDirectionToTarget(closestPatrolPoint);
                    if (direction != Vector3Int.zero)
                    {
                        Vector3Int targetPos = movementSystem.CurrentGridPosition + direction;
                        movementSystem.MoveToPosition(targetPos);
                    }
                }
                else
                {
                    // Reached patrol point, resume patrolling
                    ChangeState(EnemyState.Patrolling);
                }
            }
        }

        #endregion

        #region State Management

        private void HandleStateTransitions()
        {
            // Priority-based state transitions

            // Highest priority: Chasing
            if (detectionSettings.chasePlayer && detectionSystem.PlayerDetected && currentState != EnemyState.Chasing)
            {
                ChangeState(EnemyState.Chasing);
                return;
            }

            // Second priority: Trail investigation
            if (trailBehavior.HasTrailTarget && currentState == EnemyState.Patrolling)
            {
                ChangeState(EnemyState.MovingToTrail);
                return;
            }

            // Default: Return to patrolling if no other activities
            if (currentState == EnemyState.Chasing && !chaseBehavior.IsChasing)
            {
                ChangeState(EnemyState.Returning);
            }
        }

        private void ChangeState(EnemyState newState)
        {
            if (currentState == newState) return;

            EnemyState oldState = currentState;
            previousState = currentState;
            currentState = newState;

            Debug.Log($"🔄 State changed: {oldState} → {newState}");

            // Handle state exit logic
            OnStateExit(oldState);

            // Handle state enter logic
            OnStateEnter(newState);
        }

        private void OnStateExit(EnemyState exitingState)
        {
            switch (exitingState)
            {
                case EnemyState.Patrolling:
                    patrolBehavior.StopPatrol();
                    break;

                case EnemyState.Chasing:
                    // Chase behavior handles its own cleanup
                    break;

                case EnemyState.InvestigatingTrail:
                    // Investigation behavior handles its own cleanup
                    break;
            }
        }

        private void OnStateEnter(EnemyState enteringState)
        {
            switch (enteringState)
            {
                case EnemyState.Patrolling:
                    patrolBehavior.StartPatrol();
                    break;

                case EnemyState.Chasing:
                    // Chase behavior is event-driven
                    break;

                case EnemyState.MovingToTrail:
                    Debug.Log($"🎯 Moving to trail at {trailBehavior.CurrentTrailTarget}");
                    break;

                case EnemyState.InvestigatingTrail:
                    Debug.Log($"🔍 Starting trail investigation at {trailBehavior.CurrentTrailTarget}");
                    break;

                case EnemyState.Returning:
                    Debug.Log("🔙 Returning to patrol route");
                    break;
            }
        }

        #endregion

        #region Event Handlers

        private void HandlePositionChanged(Vector3Int newPosition)
        {
            Debug.Log($"📍 Position changed to: {newPosition}");
        }

        private void HandleMovementStateChanged(bool isMoving)
        {
            // Movement state changes are handled in update loops
        }

        private void HandlePlayerDetected(Transform player)
        {
            Debug.Log("👁️ Player detected by AI!");
        }

        private void HandlePlayerLost()
        {
            Debug.Log("👤 Player lost by AI!");
        }

        private void HandleChaseStarted()
        {
            ChangeState(EnemyState.Chasing);
        }

        private void HandleChaseEnded()
        {
            ChangeState(EnemyState.Returning);
        }

        private void HandlePlayerCaught()
        {
            Debug.Log("🎯 Player has been caught!");
            // Add game-specific logic here (e.g., game over, respawn, etc.)
        }

        private void HandlePatrolPointReached()
        {
            Debug.Log("🚶 Patrol point reached");
        }

        private void HandlePatrolCompleted()
        {
            Debug.Log("🔄 Patrol sequence completed, moving to next point");
        }

        private void HandleTrailDetected(Vector3Int trailPosition)
        {
            Debug.Log($"🕵️ Trail detected at {trailPosition}");
        }

        private void HandleTrailInvestigationStarted(Vector3Int trailPosition)
        {
            Debug.Log($"🔍 Trail investigation started at {trailPosition}");
        }

        private void HandleTrailCleaned(Vector3Int trailPosition)
        {
            Debug.Log($"🧹 Trail cleaned at {trailPosition}");
        }

        private void HandleTrailInvestigationCompleted()
        {
            Debug.Log("✅ Trail investigation completed, resuming patrol");
            ChangeState(EnemyState.Patrolling);
        }

        private void HandlePlayerFOVChanged(bool playerInFOV)
        {
            Debug.Log($"👁️ Player FOV status changed: {(playerInFOV ? "IN" : "OUT OF")} FOV");
        }

        #endregion

        #region Public Interface

        public void StartInitialBehavior()
        {
            ChangeState(EnemyState.Patrolling);
        }

        public void PauseAI()
        {
            enabled = false;
            patrolBehavior.StopPatrol();
            chaseBehavior.ForceEndChase();
            trailBehavior.StopInvestigation();
        }

        public void ResumeAI()
        {
            enabled = true;
            ChangeState(EnemyState.Patrolling);
        }

        public void SetTrailManager(PlayerTrailManager newTrailManager)
        {
            trailManager = newTrailManager;
            if (trailBehavior != null)
            {
                trailBehavior.Initialize(trailSettings, fovCleanupSettings, movementSystem, fovSystem, trailManager, gridMovementSettings.grid, detectionSettings.player);
            }
        }

        #endregion

        #region Inspector Validation

        void OnValidate()
        {
            // Safe validation that handles null settings
            EnsureSettingsInitialized();

            // Validate settings ranges safely
            if (fovSettings != null)
            {
                if (fovSettings.fovAngle < 1f) fovSettings.fovAngle = 1f;
                if (fovSettings.fovAngle > 360f) fovSettings.fovAngle = 360f;
                if (fovSettings.fovRange < 0.1f) fovSettings.fovRange = 0.1f;
            }

            if (gridMovementSettings != null)
            {
                if (gridMovementSettings.moveSpeed < 0.1f) gridMovementSettings.moveSpeed = 0.1f;
                if (gridMovementSettings.rotationSpeed < 0.1f) gridMovementSettings.rotationSpeed = 0.1f;
            }

            // Set default visual colors if not set
            if (visualSettings != null)
            {
                if (visualSettings.patrolPathColor == Color.clear)
                    visualSettings.patrolPathColor = Color.blue;

                if (visualSettings.fovEditorColor == Color.clear)
                    visualSettings.fovEditorColor = new Color(1f, 1f, 0f, 0.2f);

                if (visualSettings.fovBorderColor == Color.clear)
                    visualSettings.fovBorderColor = Color.yellow;
            }

            // Force refresh patrol points from array
            if (patrolSettings != null)
            {
                patrolSettings.RefreshPatrolPoints();
            }
        }

        #endregion

        #region Debug and Gizmos

        void OnDrawGizmos()
        {
            // Draw patrol points and path
            if (patrolSettings.patrolPoints.Count > 1)
            {
                Color pathColor = visualSettings.patrolPathColor != Color.clear ? visualSettings.patrolPathColor : Color.blue;
                Color pointColor = Color.yellow;

                for (int i = 0; i < patrolSettings.patrolPoints.Count; i++)
                {
                    Vector3Int gridPos = patrolSettings.patrolPoints[i];

                    // Skip zero positions (empty array elements)
                    if (gridPos == Vector3Int.zero && i > 0) continue;

                    Vector3 worldPos = gridMovementSettings.grid != null
                        ? gridMovementSettings.grid.CellToWorld(gridPos) + gridMovementSettings.grid.cellSize * 0.5f
                        : new Vector3(gridPos.x, gridPos.y, gridPos.z);

                    // Draw patrol point sphere
                    Gizmos.color = pointColor;
                    Gizmos.DrawWireSphere(worldPos, 0.5f);
                    Gizmos.DrawSphere(worldPos, 0.2f);

                    // Draw point number
#if UNITY_EDITOR
                    UnityEditor.Handles.Label(worldPos + Vector3.up * 0.8f, $"P{i}");
#endif

                    // Draw path connections
                    if (visualSettings.showPatrolPath)
                    {
                        Gizmos.color = pathColor;

                        if (i < patrolSettings.patrolPoints.Count - 1)
                        {
                            Vector3Int nextGridPos = patrolSettings.patrolPoints[i + 1];
                            if (nextGridPos != Vector3Int.zero || i + 1 == 0)
                            {
                                Vector3 nextWorldPos = gridMovementSettings.grid != null
                                    ? gridMovementSettings.grid.CellToWorld(nextGridPos) + gridMovementSettings.grid.cellSize * 0.5f
                                    : new Vector3(nextGridPos.x, nextGridPos.y, nextGridPos.z);

                                Gizmos.DrawLine(worldPos, nextWorldPos);
                                DrawArrow(worldPos, nextWorldPos);
                            }
                        }
                        else if (patrolSettings.patrolType == PatrolType.Loop && patrolSettings.patrolPoints.Count > 2)
                        {
                            Vector3Int firstGridPos = patrolSettings.patrolPoints[0];
                            Vector3 firstWorldPos = gridMovementSettings.grid != null
                                ? gridMovementSettings.grid.CellToWorld(firstGridPos) + gridMovementSettings.grid.cellSize * 0.5f
                                : new Vector3(firstGridPos.x, firstGridPos.y, firstGridPos.z);

                            Gizmos.DrawLine(worldPos, firstWorldPos);
                            DrawArrow(worldPos, firstWorldPos);
                        }
                    }
                }
            }

            // Draw FOV Visualization
            if (visualSettings.showFovInEditor && fovSystem != null && fovSettings.useFovForDetection)
            {
                DrawFOVGizmos();
            }

            // Draw current state and position in play mode
            if (Application.isPlaying)
            {
                Gizmos.color = Color.red;
                Vector3 currentPos = transform.position;
                Gizmos.DrawWireCube(currentPos, Vector3.one * 0.3f);

#if UNITY_EDITOR
                Vector3 textPos = transform.position + Vector3.up * 2f;
                UnityEditor.Handles.Label(textPos, $"State: {currentState}");
#endif
            }
        }

        // FOV Gizmo Drawing
        private void DrawFOVGizmos()
        {
            Vector3 forward = transform.forward;
            Vector3 position = transform.position;

            // Draw FOV range circle
            Gizmos.color = visualSettings.fovBorderColor;
            Gizmos.DrawWireSphere(position, fovSettings.fovRange);

            // Draw FOV angle boundaries
            Vector3 leftBoundary = Quaternion.Euler(0, -fovSettings.fovAngle / 2f, 0) * forward;
            Vector3 rightBoundary = Quaternion.Euler(0, fovSettings.fovAngle / 2f, 0) * forward;

            Gizmos.color = visualSettings.fovBorderColor;
            Gizmos.DrawRay(position, leftBoundary * fovSettings.fovRange);
            Gizmos.DrawRay(position, rightBoundary * fovSettings.fovRange);

            // Draw FOV area (filled)
            if (Application.isPlaying && fovSystem.FOVPoints.Count > 1)
            {
                // Use the calculated FOV points from the system
                Gizmos.color = visualSettings.fovEditorColor;
                for (int i = 0; i < fovSystem.FOVPoints.Count - 1; i++)
                {
                    Vector3[] triangle = { position, fovSystem.FOVPoints[i], fovSystem.FOVPoints[i + 1] };
                    DrawTriangleGizmo(triangle);
                }
            }
            else
            {
                // Draw static FOV area in editor
                Gizmos.color = visualSettings.fovEditorColor;
                int resolution = Mathf.Max(3, fovSettings.fovResolution / 2); // Lower resolution for editor

                float angleStep = fovSettings.fovAngle / resolution;
                float startAngle = -fovSettings.fovAngle / 2f;

                Vector3 previousPoint = position + AngleToDirection(startAngle) * fovSettings.fovRange;

                for (int i = 1; i <= resolution; i++)
                {
                    float angle = startAngle + i * angleStep;
                    Vector3 currentPoint = position + AngleToDirection(angle) * fovSettings.fovRange;

                    Vector3[] triangle = { position, previousPoint, currentPoint };
                    DrawTriangleGizmo(triangle);

                    previousPoint = currentPoint;
                }
            }

            // Draw player detection status
            if (Application.isPlaying && detectionSettings.player != null)
            {
                bool playerDetected = fovSystem.IsPositionInFieldOfView(detectionSettings.player.position);
                Gizmos.color = playerDetected ? Color.green : Color.red;
                Gizmos.DrawLine(position, detectionSettings.player.position);

#if UNITY_EDITOR
                Vector3 midPoint = Vector3.Lerp(position, detectionSettings.player.position, 0.5f);
                UnityEditor.Handles.Label(midPoint, playerDetected ? "DETECTED" : "HIDDEN");
#endif
            }
        }

        // Helper methods
        private Vector3 AngleToDirection(float angleInDegrees)
        {
            float angleInRadians = angleInDegrees * Mathf.Deg2Rad;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            return forward * Mathf.Cos(angleInRadians) + right * Mathf.Sin(angleInRadians);
        }

        private void DrawTriangleGizmo(Vector3[] points)
        {
            Gizmos.DrawLine(points[0], points[1]);
            Gizmos.DrawLine(points[1], points[2]);
            Gizmos.DrawLine(points[2], points[0]);
        }

        private void DrawArrow(Vector3 from, Vector3 to)
        {
            Vector3 direction = (to - from).normalized;
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
            Vector3 midPoint = Vector3.Lerp(from, to, 0.7f);

            float arrowSize = 0.3f;
            Vector3 arrowHead1 = midPoint - direction * arrowSize + right * arrowSize * 0.5f;
            Vector3 arrowHead2 = midPoint - direction * arrowSize - right * arrowSize * 0.5f;

            Gizmos.DrawLine(midPoint, arrowHead1);
            Gizmos.DrawLine(midPoint, arrowHead2);
        }

        #endregion  // ← DEZE REGEL TOEVOEGEN

    }
}