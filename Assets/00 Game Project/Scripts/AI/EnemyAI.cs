using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace GameProjectFM.AI.Core
{
    using Systems;
    using Behaviors;
    using Visual;

    public class EnemyAI : MonoBehaviour
    {
        [Header("NavMesh Movement")]
        [SerializeField] private NavMeshMovementSettings navMeshMovementSettings = new NavMeshMovementSettings();

        [Header("Linear Path Patrol")]
        [SerializeField] private LinearPathBehaviour linearPathBehaviour;
        [SerializeField] private LookAroundSettings lookAroundSettings = new LookAroundSettings();

        [Header("Field of View")]
        [SerializeField] private FOVSettings fovSettings = new FOVSettings();

        [Header("Detection")]
        [SerializeField] private DetectionSettings detectionSettings = new DetectionSettings();

        [Header("Chase Settings")]
        [SerializeField] private ChaseSettings chaseSettings = new ChaseSettings();

        [Header("Trail Investigation")]
        [SerializeField] private TrailSettings trailSettings = new TrailSettings();

        [Header("FOV Cleanup")]
        [SerializeField] private FOVCleanupSettings fovCleanupSettings = new FOVCleanupSettings();

        [Header("Visual Settings")]
        [SerializeField] private VisualSettings visualSettings = new VisualSettings();

        [Header("External Dependencies")]
        [SerializeField] private PlayerTrailManager trailManager;

        // Core Systems (auto-created)
        private NavMeshMovementSystem navMovementSystem;
        private NavMeshAgent navMeshAgent;
        private FOVSystem fovSystem;
        private DetectionSystem detectionSystem;

        // Behavior Systems (auto-created)
        private NavMeshLinearPatrolBehavior linearPatrolBehavior;
        private NavMeshChaseBehavior chaseBehavior;
        private NavMeshTrailInvestigationBehavior trailBehavior;

        // Visual Components (auto-created)
        private FOVVisualizer fovVisualizer;

        // AI State
        private EnemyState currentState = EnemyState.Patrolling;
        private EnemyState previousState = EnemyState.Patrolling;

        // Stuck state protection
        private float stateTimer = 0f;
        private EnemyState lastState = EnemyState.Patrolling;

        // Navigation state
        private Vector3 lastKnownPlayerPosition;
        private float waitTimer = 0f;

        // Public Properties
        public EnemyState CurrentState => currentState;
        public bool IsMoving => navMovementSystem != null && navMovementSystem.IsMoving;
        public bool IsBusy => IsMoving || waitTimer > 0f;
        public Vector3 CurrentPosition => transform.position;
        public bool HasPath => navMeshAgent != null && navMeshAgent.hasPath;

        void Awake()
        {
            EnsureSettingsInitialized();
            InitializeNavMeshSystems();
            InitializeOtherSystems();
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
            if (navMeshMovementSettings == null) navMeshMovementSettings = new NavMeshMovementSettings();
            if (lookAroundSettings == null) lookAroundSettings = new LookAroundSettings();
            if (fovSettings == null) fovSettings = new FOVSettings();
            if (detectionSettings == null) detectionSettings = new DetectionSettings();
            if (chaseSettings == null) chaseSettings = new ChaseSettings();
            if (trailSettings == null) trailSettings = new TrailSettings();
            if (fovCleanupSettings == null) fovCleanupSettings = new FOVCleanupSettings();
            if (visualSettings == null) visualSettings = new VisualSettings();

            Debug.Log("✅ EnemyAI: All NavMesh settings initialized");
        }

        void Start()
        {
            InitializeAllSystems();
            StartInitialBehavior();
        }

        void Update()
        {
            HandleTimers();
            HandleAIUpdates();
            HandleStateTransitions();
            CheckForStuckState();
        }

        #region NavMesh System Initialization

        private void InitializeNavMeshSystems()
        {
            // Get or add NavMeshAgent
            navMeshAgent = GetComponent<NavMeshAgent>();
            if (navMeshAgent == null)
            {
                navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
            }

            // Get or add NavMeshMovementSystem
            navMovementSystem = GetComponent<NavMeshMovementSystem>();
            if (navMovementSystem == null)
            {
                navMovementSystem = gameObject.AddComponent<NavMeshMovementSystem>();
            }
        }

        private void InitializeOtherSystems()
        {
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
            // Initialize NavMesh Linear Patrol Behavior
            linearPatrolBehavior = GetComponent<NavMeshLinearPatrolBehavior>();
            if (linearPatrolBehavior == null)
            {
                linearPatrolBehavior = gameObject.AddComponent<NavMeshLinearPatrolBehavior>();
            }

            // Initialize NavMesh Chase Behavior
            chaseBehavior = GetComponent<NavMeshChaseBehavior>();
            if (chaseBehavior == null)
            {
                chaseBehavior = gameObject.AddComponent<NavMeshChaseBehavior>();
            }

            // Initialize NavMesh Trail Investigation Behavior
            trailBehavior = GetComponent<NavMeshTrailInvestigationBehavior>();
            if (trailBehavior == null)
            {
                trailBehavior = gameObject.AddComponent<NavMeshTrailInvestigationBehavior>();
            }

            Debug.Log("✅ EnemyAI: All NavMesh behaviors initialized");
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
            // Initialize NavMesh Movement System
            navMovementSystem.Initialize(navMeshMovementSettings);

            // Initialize FOV System first (needed for detection)
            fovSystem.Initialize(fovSettings, detectionSettings.player);

            // Initialize Detection System (depends on FOV)
            detectionSystem.Initialize(detectionSettings, fovSettings, fovSystem);

            // Initialize linear patrol behavior with LinearPathBehaviour
            if (linearPatrolBehavior != null && linearPathBehaviour != null)
            {
                linearPatrolBehavior.Initialize(linearPathBehaviour, lookAroundSettings, navMovementSystem);
            }
            else
            {
                Debug.LogWarning("⚠️ LinearPathBehaviour not assigned to EnemyAI! Patrol will not work.");
            }

            if (chaseBehavior != null)
            {
                chaseBehavior.Initialize(navMovementSystem, detectionSystem, chaseSettings);
            }

            if (trailBehavior != null)
            {
                trailBehavior.Initialize(trailSettings, fovCleanupSettings, navMovementSystem, fovSystem, trailManager, detectionSettings.player);
            }

            // Initialize visual components (optional)
            if (fovVisualizer != null)
            {
                fovVisualizer.Initialize(visualSettings, fovSystem);
            }

            Debug.Log("✅ EnemyAI: All NavMesh systems initialized");
        }

        private void ConnectEventHandlers()
        {
            // NavMesh Movement events
            if (navMovementSystem != null)
            {
                navMovementSystem.OnPositionChanged += HandlePositionChanged;
                navMovementSystem.OnMovementStateChanged += HandleMovementStateChanged;
                navMovementSystem.OnDestinationReached += HandleDestinationReached;
            }

            // Detection events
            if (detectionSystem != null)
            {
                detectionSystem.OnPlayerDetected += HandlePlayerDetected;
                detectionSystem.OnPlayerLost += HandlePlayerLost;
            }

            // Chase events
            if (chaseBehavior != null)
            {
                chaseBehavior.OnChaseStarted += HandleChaseStarted;
                chaseBehavior.OnChaseEnded += HandleChaseEnded;
                chaseBehavior.OnPlayerCaught += HandlePlayerCaught;
            }

            // Linear Patrol events
            if (linearPatrolBehavior != null)
            {
                linearPatrolBehavior.OnPatrolPointReached += HandlePatrolPointReached;
                linearPatrolBehavior.OnPatrolCompleted += HandlePatrolCompleted;
            }

            // Trail events
            if (trailBehavior != null)
            {
                trailBehavior.OnTrailDetected += HandleTrailDetected;
                trailBehavior.OnTrailInvestigationStarted += HandleTrailInvestigationStarted;
                trailBehavior.OnTrailCleaned += HandleTrailCleaned;
                trailBehavior.OnTrailInvestigationCompleted += HandleTrailInvestigationCompleted;
            }

            // FOV events
            if (fovSystem != null)
            {
                fovSystem.OnPlayerFOVChanged += HandlePlayerFOVChanged;
            }
        }

        #endregion

        #region AI Update Logic

        private void HandleTimers()
        {
            if (waitTimer > 0f)
            {
                waitTimer -= Time.deltaTime;
            }
        }

        private void HandleAIUpdates()
        {
            // Always check for trails first (except when investigating)
            if (currentState != EnemyState.InvestigatingTrail && trailSettings.investigateTrails)
            {
                if (trailBehavior != null)
                {
                    trailBehavior.CheckForTrails();
                }
            }

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
        }

        private void UpdatePatrolling()
        {
            if (waitTimer > 0f) return;

            // Check if LinearPathBehaviour is configured and patrol behavior is ready
            if (linearPathBehaviour != null && linearPathBehaviour.transform.childCount > 0)
            {
                // Make sure linear patrol is running
                if (!linearPatrolBehavior.IsPatrolling && !linearPatrolBehavior.IsBusy)
                {
                    linearPatrolBehavior.StartPatrol();
                }
            }
            else
            {
                Debug.LogWarning("⚠️ LinearPathBehaviour not configured or has no patrol points!");
            }
        }

        private void UpdateChasing()
        {
            if (chaseBehavior != null)
            {
                chaseBehavior.UpdateChase();
            }
            else if (detectionSettings.player != null)
            {
                // Fallback direct chase logic
                lastKnownPlayerPosition = detectionSettings.player.position;
                navMovementSystem.MoveToPosition(lastKnownPlayerPosition);

                // Check if player is caught
                if (Vector3.Distance(transform.position, detectionSettings.player.position) <= chaseSettings.captureDistance)
                {
                    HandlePlayerCaught();
                }
            }
        }

        private void UpdateMovingToTrail()
        {
            if (trailBehavior != null)
            {
                if (trailBehavior.HasTrailTarget)
                {
                    if (trailBehavior.IsAtTrailTarget())
                    {
                        ChangeState(EnemyState.InvestigatingTrail);
                        trailBehavior.StartInvestigationSequence();
                    }
                    else if (!navMovementSystem.IsMoving)
                    {
                        Vector3 trailWorldPos = trailBehavior.GetTrailWorldPosition();
                        navMovementSystem.MoveToPosition(trailWorldPos);
                        Debug.Log($"🚶 Moving towards trail at: {trailWorldPos}");
                    }
                }
                else
                {
                    // No current target, check for new trails
                    if (trailBehavior.HasMoreTrailsToInvestigate())
                    {
                        trailBehavior.MoveToNextTrail();
                    }
                    else
                    {
                        ChangeState(EnemyState.Returning);
                    }
                }
            }
        }

        private void UpdateInvestigatingTrail()
        {
            if (trailBehavior == null) return;

            // Check if investigation is still active
            if (!trailBehavior.IsInvestigating)
            {
                // Investigation completed, check for next actions
                if (trailBehavior.HasTrailTarget)
                {
                    ChangeState(EnemyState.MovingToTrail);
                }
                else if (trailBehavior.HasMoreTrailsToInvestigate())
                {
                    trailBehavior.MoveToNextTrail();
                    ChangeState(EnemyState.MovingToTrail);
                }
                else
                {
                    // No more trails, return to patrol or check for player
                    if (detectionSettings.chasePlayer && detectionSystem.PlayerDetected && !HasTrailsInFOV())
                    {
                        Debug.Log("🏃 Investigation complete, player still detected, starting chase");
                        ChangeState(EnemyState.Chasing);
                    }
                    else
                    {
                        Debug.Log("🚶 Investigation complete, returning to patrol");
                        ChangeState(EnemyState.Returning);
                    }
                }
            }
            else
            {
                // Safety check: if player is very close during investigation, interrupt
                if (detectionSettings.chasePlayer && detectionSystem.PlayerDetected)
                {
                    float playerDistance = detectionSystem.GetDistanceToPlayer();
                    if (playerDistance <= chaseSettings.captureDistance * 2f)
                    {
                        Debug.Log("⚠️ Player very close during investigation - interrupting to chase");
                        trailBehavior.StopInvestigation();
                        ChangeState(EnemyState.Chasing);
                    }
                }
            }
        }

        private void UpdateReturning()
        {
            if (!navMovementSystem.IsMoving)
            {
                // Return to closest patrol point from LinearPathBehaviour
                Transform closestPatrolPoint = GetNearestPatrolPoint();
                if (closestPatrolPoint != null)
                {
                    if (navMovementSystem.IsNearPosition(closestPatrolPoint.position, 2f))
                    {
                        ChangeState(EnemyState.Patrolling);
                        navMovementSystem.SetSpeed(navMeshMovementSettings.moveSpeed); // Reset speed
                    }
                    else
                    {
                        navMovementSystem.MoveToPosition(closestPatrolPoint.position);
                    }
                }
                else
                {
                    // No patrol points, just resume patrolling
                    ChangeState(EnemyState.Patrolling);
                }
            }
        }

        private Transform GetNearestPatrolPoint()
        {
            if (linearPathBehaviour == null) return null;

            return linearPathBehaviour.GetClosestPoint(transform.position);
        }

        private void CheckForStuckState()
        {
            // Safety check: if AI is stuck in same state for too long, reset
            if (currentState == lastState)
            {
                stateTimer += Time.deltaTime;

                if (stateTimer > 30f) // 30 seconds timeout
                {
                    Debug.LogWarning("⚠️ AI appears stuck, forcing reset to patrol state");

                    // Force end any active behaviors
                    if (trailBehavior != null && trailBehavior.IsInAlertState)
                    {
                        trailBehavior.ForceEndAlertState();
                    }

                    if (chaseBehavior != null && chaseBehavior.IsChasing)
                    {
                        chaseBehavior.ForceEndChase();
                    }

                    ChangeState(EnemyState.Patrolling);
                    stateTimer = 0f;
                }
            }
            else
            {
                stateTimer = 0f;
            }

            lastState = currentState;
        }

        #endregion

        #region State Management

        private void HandleStateTransitions()
        {
            // PRIORITY 1: Trail investigation (highest priority when player detected)
            if (trailBehavior != null && trailBehavior.HasTrailTarget && currentState == EnemyState.Patrolling)
            {
                ChangeState(EnemyState.MovingToTrail);
                return;
            }

            // PRIORITY 2: Chase player (only if no trails in FOV and chase enabled)
            if (detectionSettings.chasePlayer && detectionSystem.PlayerDetected)
            {
                if (!HasTrailsInFOV() && currentState != EnemyState.Chasing)
                {
                    ChangeState(EnemyState.Chasing);
                    return;
                }
                else if (HasTrailsInFOV() && (currentState == EnemyState.Chasing || currentState == EnemyState.Patrolling))
                {
                    Debug.Log("🔄 Stopping chase - trails detected in FOV");
                    if (chaseBehavior != null && chaseBehavior.IsChasing)
                    {
                        chaseBehavior.ForceEndChase();
                    }
                    return;
                }
            }

            // PRIORITY 3: Return to patrolling if no other activities
            if (currentState == EnemyState.Chasing && chaseBehavior != null && !chaseBehavior.IsChasing)
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
                    if (linearPatrolBehavior != null)
                    {
                        linearPatrolBehavior.StopPatrol();
                    }
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
                    if (linearPatrolBehavior != null)
                    {
                        linearPatrolBehavior.StartPatrol();
                    }
                    break;

                case EnemyState.Chasing:
                    // Chase behavior is event-driven
                    break;

                case EnemyState.MovingToTrail:
                    if (trailBehavior != null)
                    {
                        Debug.Log($"🎯 Moving to trail at {trailBehavior.CurrentTrailTarget}");
                    }
                    break;

                case EnemyState.InvestigatingTrail:
                    if (trailBehavior != null)
                    {
                        Debug.Log($"🔍 Starting trail investigation at {trailBehavior.CurrentTrailTarget}");
                    }
                    break;

                case EnemyState.Returning:
                    Debug.Log("🔙 Returning to patrol route");
                    break;
            }
        }

        #endregion

        #region Event Handlers

        private void HandlePositionChanged(Vector3 newPosition)
        {
            Debug.Log($"📍 Position changed to: {newPosition}");
        }

        private void HandleMovementStateChanged(bool isMoving)
        {
            // Movement state changes are handled in update loops
        }

        private void HandleDestinationReached()
        {
            if (currentState == EnemyState.Patrolling)
            {
                // Linear patrol behavior handles its own destination logic
                Debug.Log($"🚶 Destination reached during patrol");
            }
        }

        private void HandlePlayerDetected(Transform player)
        {
            Debug.Log("👁️ Player detected by AI!");

            // Trigger alert state in trail behavior
            if (trailBehavior != null)
            {
                trailBehavior.TriggerAlertState();
            }

            // PRIORITY CHECK: Only chase if NO trails are visible in FOV
            if (detectionSettings.chasePlayer && !HasTrailsInFOV())
            {
                Debug.Log("🏃 No trails in FOV - starting chase");
                ChangeState(EnemyState.Chasing);
            }
            else if (HasTrailsInFOV())
            {
                Debug.Log("🕵️ Trails detected in FOV - prioritizing trail cleanup over chase");
            }
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
            Debug.Log("🚶 Linear patrol point reached");
        }

        private void HandlePatrolCompleted()
        {
            Debug.Log("🔄 Linear patrol sequence completed, moving to next point");
        }

        private void HandleTrailDetected(Vector3 trailPosition)
        {
            Debug.Log($"🕵️ Trail detected at {trailPosition}");
        }

        private void HandleTrailInvestigationStarted(Vector3 trailPosition)
        {
            Debug.Log($"🔍 Trail investigation started at {trailPosition}");
        }

        private void HandleTrailCleaned(Vector3 trailPosition)
        {
            Debug.Log($"🧹 Trail cleaned at {trailPosition}");
        }

        private void HandleTrailInvestigationCompleted()
        {
            Debug.Log("✅ Trail investigation completed");

            // Check if there are more trails to investigate before returning to patrol
            if (trailBehavior != null && trailBehavior.HasMoreTrailsToInvestigate())
            {
                Debug.Log("🔍 More trails detected, moving to next trail");
                ChangeState(EnemyState.MovingToTrail);
            }
            else
            {
                Debug.Log("🚶 No more trails, resuming patrol");
                ChangeState(EnemyState.Patrolling);
            }
        }

        private void HandlePlayerFOVChanged(bool playerInFOV)
        {
            Debug.Log($"👁️ Player FOV status changed: {(playerInFOV ? "IN" : "OUT OF")} FOV");
        }

        private bool HasTrailsInFOV()
        {
            if (trailBehavior == null || trailManager == null) return false;

            var blockedPositions = trailManager.GetBlockedPositions();
            if (blockedPositions.Count == 0) return false;

            // Check if any trail is in FOV
            foreach (Vector3Int trailPos in blockedPositions)
            {
                // Convert grid position to world position (you may need to adjust this based on your trail system)
                Vector3 trailWorldPos = new Vector3(trailPos.x, trailPos.y, trailPos.z);

                if (fovSystem.IsPositionInFieldOfView(trailWorldPos))
                {
                    return true;
                }
            }

            return false;
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
            if (linearPatrolBehavior != null) linearPatrolBehavior.StopPatrol();
            if (chaseBehavior != null) chaseBehavior.ForceEndChase();
            if (trailBehavior != null) trailBehavior.StopInvestigation();
            if (navMovementSystem != null) navMovementSystem.StopMovement();
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
                trailBehavior.Initialize(trailSettings, fovCleanupSettings, navMovementSystem, fovSystem, trailManager, detectionSettings.player);
            }
        }

        public void SetLinearPath(LinearPathBehaviour newLinearPath)
        {
            linearPathBehaviour = newLinearPath;
            if (linearPatrolBehavior != null)
            {
                linearPatrolBehavior.SetLinearPath(newLinearPath);
            }
            Debug.Log($"🔄 LinearPathBehaviour updated: {(newLinearPath != null ? newLinearPath.name : "null")}");
        }

        #endregion

        #region Inspector Validation

        void OnValidate()
        {
            EnsureSettingsInitialized();

            // Validate settings ranges safely
            if (fovSettings != null)
            {
                if (fovSettings.fovAngle < 1f) fovSettings.fovAngle = 1f;
                if (fovSettings.fovAngle > 360f) fovSettings.fovAngle = 360f;
                if (fovSettings.fovRange < 0.1f) fovSettings.fovRange = 0.1f;
            }

            if (navMeshMovementSettings != null)
            {
                if (navMeshMovementSettings.moveSpeed < 0.1f) navMeshMovementSettings.moveSpeed = 0.1f;
                if (navMeshMovementSettings.rotationSpeed < 0.1f) navMeshMovementSettings.rotationSpeed = 0.1f;
            }

            // Validate detection settings
            if (detectionSettings != null && detectionSettings.player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    detectionSettings.player = playerObj.transform;
                    Debug.Log("🎯 Player automatically assigned to EnemyAI detection settings");
                }
                else
                {
                    Debug.LogWarning("⚠️ No Player found! Assign Player to Detection Settings manually.");
                }
            }

            // Validate LinearPathBehaviour
            if (linearPathBehaviour == null)
            {
                Debug.LogWarning("⚠️ LinearPathBehaviour not assigned! AI will not be able to patrol.");
            }
            else if (linearPathBehaviour.transform.childCount < 2)
            {
                Debug.LogWarning("⚠️ LinearPathBehaviour needs at least 2 child transforms for patrol points!");
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
        }

        #endregion

        #region Debug and Gizmos

        void OnDrawGizmos()
        {
            // LinearPathBehaviour will draw its own path, we just add AI state info

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
                Vector3 textPos = transform.position + Vector3.up * 2.5f;
                UnityEditor.Handles.Label(textPos, $"State: {currentState}");

                // Show current patrol target if available
                if (linearPatrolBehavior != null && linearPatrolBehavior.CurrentPatrolTransform != null)
                {
                    Vector3 targetTextPos = transform.position + Vector3.up * 3f;
                    UnityEditor.Handles.Label(targetTextPos, $"Target: {linearPatrolBehavior.CurrentPatrolTransform.name}");
                }
#endif
            }
        }

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
            if (Application.isPlaying && fovSystem != null && fovSystem.FOVPoints.Count > 1)
            {
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
                int resolution = Mathf.Max(3, fovSettings.fovResolution / 2);

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

        #endregion
    }
}
