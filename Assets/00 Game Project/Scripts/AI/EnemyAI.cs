using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class EnemyAI : MonoBehaviour
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

    [Header("Patrol Settings")]
    public PatrolType patrolType = PatrolType.Loop;
    public List<Vector3Int> patrolPoints = new List<Vector3Int>();
    public float waitTimeAtPoint = 2f;
    public bool useRandomWaitTime = false;
    public Vector2 randomWaitRange = new Vector2(1f, 3f);

    [Header("Look Around Behavior")]
    public bool enableLookAround = true;
    public float lookAroundDuration = 3f;
    public int lookDirections = 4;
    public float lookAngleRange = 90f;
    public bool randomLookOrder = true;
    public float timeBetweenLooks = 0.5f;

    [Header("Field of View")]
    public float fovAngle = 90f;
    public float fovRange = 4f;
    public int fovResolution = 20;
    public bool useFovForDetection = true;
    public LayerMask obstacleLayer = -1;

    [Header("Detection")]
    public Transform player;
    public LayerMask playerLayer = -1;
    public bool chasePlayer = false;
    public float detectionRange = 5f;

    [Header("Trail Investigation")]
    public bool investigateTrails = true;
    public float trailCleanupTime = 3f;
    public float trailInvestigationTime = 2f;
    public bool prioritizeNewestTrails = false;
    public bool alwaysClosestFirst = true;
    public Color trailTargetColor = Color.orange;

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

    [Header("FOV Cleanup Settings")]
    public bool requireTrailInFOV = false;      // Trail moet binnen FOV zijn voor cleanup
    public bool requirePlayerInFOV = false;     // Player moet binnen FOV zijn voor cleanup
    public bool showFOVCleanupDebug = true;
    public Color fovCleanupColor = Color.cyan;

    [Header("Current State")]
    public Vector3Int currentGridPosition;
    public EnemyState currentState = EnemyState.Patrolling;

    public enum PatrolType
    {
        Loop,
        PingPong,
        Random,
        StayInArea
    }

    public enum EnemyState
    {
        Patrolling,
        Moving,
        Waiting,
        LookingAround,
        Chasing,
        Returning,
        InvestigatingTrail,
        MovingToTrail
    }

    private bool isMoving = false;
    private bool isWaiting = false;
    private bool isLookingAround = false;
    private bool isInvestigatingTrail = false;
    private int currentPatrolIndex = 0;
    private bool patrolForward = true;
    private Vector3Int startPosition;
    private Coroutine currentAction;
    private float originalRotationY;

    // Trail investigation - verbeterde versie
    private PlayerTrailManager trailManager;
    private Vector3Int currentTrailTarget;
    private List<Vector3Int> discoveredTrails = new List<Vector3Int>();
    private List<Vector3Int> currentlyInvestigating = new List<Vector3Int>();
    private bool hasTrailTarget = false;
    private float lastTrailCheckTime = 0f;
    private float trailCheckInterval = 1f;

    // Field of View data
    private Mesh fovMesh;
    private GameObject fovVisualizerObject;
    private MeshFilter fovMeshFilter;
    private MeshRenderer fovMeshRenderer;
    private Material fovMaterial;

    void Start()
    {
        InitializeEnemy();
        if (Application.isPlaying)
        {
            CreateFovVisualizer();
        }
        originalRotationY = transform.eulerAngles.y;
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        if (chasePlayer && player != null)
        {
            CheckPlayerDetection();
        }

        // Check voor trails maar niet elke frame en alleen als niet al bezig met trail
        if (investigateTrails && trailManager != null && Time.time - lastTrailCheckTime > trailCheckInterval)
        {
            if (currentState != EnemyState.MovingToTrail && currentState != EnemyState.InvestigatingTrail && currentState != EnemyState.Chasing)
            {
                CheckForTrails();
            }
            lastTrailCheckTime = Time.time;
        }

        // State handling
        bool canHandleState = !isMoving && !isWaiting && !isLookingAround && !isInvestigatingTrail;
        bool isTrailMovementState = currentState == EnemyState.MovingToTrail;

        if (canHandleState || (isTrailMovementState && !isMoving))
        {
            HandleCurrentState();
        }

        if (showFieldOfView)
        {
            UpdateFovVisualizer();
        }
    }

    void InitializeEnemy()
    {
        if (grid == null)
            grid = FindFirstObjectByType<Grid>();

        if (player == null)
        {
            GridPlayerController playerController = FindFirstObjectByType<GridPlayerController>();
            if (playerController != null)
                player = playerController.transform;
        }

        // Find trail manager
        trailManager = FindFirstObjectByType<PlayerTrailManager>();
        if (trailManager == null)
        {
            Debug.LogWarning("TrailManager not found! Trail investigation will be disabled.");
            investigateTrails = false;
        }

        currentGridPosition = grid.WorldToCell(transform.position);
        startPosition = currentGridPosition;
        SnapToGrid();

        if (patrolPoints.Count == 0)
        {
            CreateDefaultPatrol();
        }

        Debug.Log($"EnemyAI initialized at {currentGridPosition}");
    }

    void CheckForTrails()
    {
        // Valideer eerst onze trail targets
        ValidateTrailTargets();

        // Get trails van PlayerTrailManager
        HashSet<Vector3Int> blockedPositions = trailManager.GetBlockedPositions();

        if (blockedPositions.Count == 0) return;

        // NIEUWE LOGICA: Check eerst of we überhaupt trails mogen onderzoeken
        // Als alleen player FOV vereist is, check of player in FOV is VOORDAT we trails detecteren
        if (requirePlayerInFOV && !requireTrailInFOV)
        {
            bool playerInFOV = IsPlayerInFieldOfView();
            if (!playerInFOV)
            {
                if (showFOVCleanupDebug)
                {
                    Debug.Log("🚫 Player not in FOV - skipping trail detection entirely (Require Player in FOV = true)");
                }
                return; // Stop completely - geen trail detection
            }
            else
            {
                if (showFOVCleanupDebug)
                {
                    Debug.Log("✅ Player in FOV - proceeding with trail detection");
                }
            }
        }

        // Filter trails op FOV
        List<TrailInfo> availableTrails = new List<TrailInfo>();

        foreach (Vector3Int trailPos in blockedPositions)
        {
            // Check of trail binnen FOV is
            Vector3 trailWorldPos = grid.CellToWorld(trailPos);
            trailWorldPos += grid.cellSize * 0.5f;

            bool trailInFOV = IsPositionInFieldOfView(trailWorldPos);

            // Alleen trails binnen FOV kunnen gedetecteerd worden
            if (!trailInFOV) continue;

            float distance = Vector3Int.Distance(currentGridPosition, trailPos);

            // Check of trail al onderzocht is
            bool isDiscovered = discoveredTrails.Contains(trailPos);

            // Als FOV requirements actief zijn, overweeg trails die al onderzocht zijn opnieuw
            bool canReconsiderTrail = (requireTrailInFOV || requirePlayerInFOV) && isDiscovered && IsCleanupAllowedByFOV();

            // Check of trail niet al onderzocht of bezig met onderzoeken
            if ((!isDiscovered || canReconsiderTrail) &&
                !currentlyInvestigating.Contains(trailPos))
            {
                availableTrails.Add(new TrailInfo { position = trailPos, distance = distance });

                if (canReconsiderTrail)
                {
                    Debug.Log($"🔄 Reconsidering trail at {trailPos} because FOV requirements are now met");
                    // Remove uit discovered lijst zodat het opnieuw onderzocht kan worden
                    discoveredTrails.Remove(trailPos);
                }
            }
        }

        if (availableTrails.Count > 0)
        {
            // Sorteer op afstand (dichtstbijzijnde eerst)
            availableTrails.Sort((a, b) => a.distance.CompareTo(b.distance));

            Vector3Int targetTrail = availableTrails[0].position;

            Debug.Log($"🕵️ EnemyAI detected {availableTrails.Count} trails within FOV. Prioritizing closest at {targetTrail} (distance: {availableTrails[0].distance:F1})");
            StartTrailInvestigation(targetTrail);
        }
    }

    // Helper class voor trail informatie
    [System.Serializable]
    public class TrailInfo
    {
        public Vector3Int position;
        public float distance;
    }

    void StartTrailInvestigation(Vector3Int trailPosition)
    {
        // Check of we al een trail aan het onderzoeken zijn
        if (hasTrailTarget)
        {
            Debug.Log($"Already investigating trail at {currentTrailTarget}, ignoring new trail at {trailPosition}");
            return;
        }

        // Voeg trail toe aan onderzoek lijsten
        discoveredTrails.Add(trailPosition);
        currentlyInvestigating.Add(trailPosition);

        currentTrailTarget = trailPosition;
        hasTrailTarget = true;

        // Stop huidige actie
        if (currentAction != null)
        {
            StopCoroutine(currentAction);
            currentAction = null;
        }

        // Reset alle flags
        isWaiting = false;
        isLookingAround = false;
        isInvestigatingTrail = false;
        isMoving = false;

        // Set state
        currentState = EnemyState.MovingToTrail;

        Debug.Log($"🔍 Starting trail investigation towards {trailPosition}. Current position: {currentGridPosition}");

        // Force eerste movement stap
        HandleTrailMovement();
    }

    void HandleTrailMovement()
    {
        if (!hasTrailTarget)
        {
            Debug.Log("No trail target, returning to patrol");
            currentState = EnemyState.Returning;
            return;
        }

        // Check of we al bij de trail zijn
        if (currentGridPosition == currentTrailTarget)
        {
            Debug.Log($"🎯 Arrived at trail {currentTrailTarget}! Starting investigation...");
            currentAction = StartCoroutine(InvestigateTrailSequence());
            return;
        }

        // Beweeg naar trail
        Vector3Int direction = GetDirectionToTarget(currentTrailTarget);

        if (direction != Vector3Int.zero)
        {
            Vector3Int targetPos = currentGridPosition + direction;
            Debug.Log($"🚶 Moving towards trail: {currentGridPosition} -> {targetPos} (target: {currentTrailTarget})");
            MoveToPosition(targetPos);
        }
        else
        {
            Debug.LogWarning($"No valid direction to trail target {currentTrailTarget} from {currentGridPosition}");
            CleanupTrailInvestigation();
            currentState = EnemyState.Returning;
        }
    }

    IEnumerator InvestigateTrailSequence()
    {
        currentState = EnemyState.InvestigatingTrail;
        isInvestigatingTrail = true;

        Debug.Log($"🔍 Investigating trail at {currentGridPosition}");

        float startRotation = transform.eulerAngles.y;
        List<float> investigationAngles = new List<float>
        {
            startRotation,
            startRotation + 90f,
            startRotation + 180f,
            startRotation + 270f
        };

        foreach (float angle in investigationAngles)
        {
            yield return StartCoroutine(SmoothRotateToAngle(angle, rotationSpeed * 2f));
            yield return new WaitForSeconds(trailInvestigationTime / 4f);

            // Check voor speler tijdens investigation
            if (chasePlayer && player != null && IsPlayerInFieldOfView())
            {
                Debug.Log("🚨 Player spotted during trail investigation!");
                CleanupTrailInvestigation();
                currentState = EnemyState.Chasing;
                yield break;
            }
        }

        // Check of cleanup toegestaan is
        Vector3Int investigatedTrail = currentTrailTarget;

        if (IsCleanupAllowedByFOV())
        {
            Debug.Log($"✅ FOV requirements met - proceeding with cleanup of trail at {investigatedTrail}");

            // Directe cleanup
            bool trailRemoved = RemoveTrailDirectly(investigatedTrail);

            if (trailRemoved)
            {
                Debug.Log($"🧹 Trail at {investigatedTrail} removed after investigation");
            }
            else
            {
                Debug.LogWarning($"Failed to remove trail at {investigatedTrail}");
            }
        }
        else
        {
            Debug.Log($"🚫 FOV requirements not met - trail at {investigatedTrail} will NOT be cleaned up");

            // Mark trail als onderzocht maar niet opgeruimd
            if (currentlyInvestigating.Contains(investigatedTrail))
            {
                currentlyInvestigating.Remove(investigatedTrail);
            }
        }

        // Reset investigation state
        CleanupTrailInvestigation();

        Debug.Log("🧹 Trail investigation complete. Resuming patrol from current position.");
        currentState = EnemyState.Patrolling;  // ✅ DIRECT DOORGAAN MET PATROL

        yield return StartCoroutine(SmoothRotateToAngle(startRotation, rotationSpeed));
    }

    void CleanupTrailInvestigation()
    {
        // Remove van currently investigating lijst
        if (currentlyInvestigating.Contains(currentTrailTarget))
        {
            currentlyInvestigating.Remove(currentTrailTarget);
        }

        isInvestigatingTrail = false;
        hasTrailTarget = false;
        currentAction = null;

        Debug.Log($"🧽 Cleaned up investigation for trail at {currentTrailTarget}");
    }

    // NIEUWE METHODE: Direct trail removal zonder timer
    bool RemoveTrailDirectly(Vector3Int trailPosition)
    {
        bool trailRemoved = false;

        if (trailManager != null)
        {
            // Check of de trail nog bestaat voordat we proberen te verwijderen
            HashSet<Vector3Int> currentTrails = trailManager.GetBlockedPositions();
            if (currentTrails.Contains(trailPosition))
            {
                // Gebruik de PlayerTrailManager om de trail te verwijderen
                trailRemoved = trailManager.RemoveTrailAt(trailPosition);

                if (trailRemoved)
                {
                    Debug.Log($"🧹 Trail removed at {trailPosition} via TrailManager");
                }
                else
                {
                    Debug.LogWarning($"Failed to remove trail at {trailPosition} via TrailManager");
                    // Fallback: probeer handmatig GameObject cleanup
                    trailRemoved = RemoveTrailGameObject(trailPosition);
                }
            }
            else
            {
                Debug.Log($"Trail at {trailPosition} was already removed from TrailManager");
                trailRemoved = true; // Consider it "removed" if it's not in the manager anymore
            }
        }
        else
        {
            // Fallback als TrailManager niet beschikbaar is
            trailRemoved = RemoveTrailGameObject(trailPosition);
        }

        // Remove van onze lijsten na cleanup (altijd doen, ook als trail al weg was)
        if (discoveredTrails.Contains(trailPosition))
        {
            discoveredTrails.Remove(trailPosition);
        }
        if (currentlyInvestigating.Contains(trailPosition))
        {
            currentlyInvestigating.Remove(trailPosition);
        }

        return trailRemoved;
    }

    bool RemoveTrailGameObject(Vector3Int gridPosition)
    {
        Vector3 worldPosition = grid.CellToWorld(gridPosition);
        worldPosition += grid.cellSize * 0.5f;

        // Methode 1: Zoek via Trail tag
        GameObject[] trailObjects = GameObject.FindGameObjectsWithTag("Trail");
        foreach (GameObject obj in trailObjects)
        {
            float distance = Vector3.Distance(obj.transform.position, worldPosition);
            if (distance < 0.5f)
            {
                Debug.Log($"🗑️ Fallback: Removing trail object via tag: {obj.name}");
                Destroy(obj);
                return true;
            }
        }

        // Fallback methode: Zoek via Physics overlap
        Collider[] colliders = Physics.OverlapSphere(worldPosition, 0.5f);
        foreach (Collider col in colliders)
        {
            if (col.gameObject.name.Contains("Trail") || col.gameObject.name.Contains("Decal") || col.gameObject.name.Contains("Poop"))
            {
                Debug.Log($"🗑️ Fallback: Removing trail object via collider: {col.gameObject.name}");
                Destroy(col.gameObject);
                return true;
            }
        }

        Debug.LogWarning($"Fallback: Could not find trail GameObject at position {gridPosition}");
        return false;
    }

    // VOLLEDIGE FIX: Check FOV conditions op basis van settings
    public bool IsCleanupAllowedByFOV()
    {
        // Als beide settings uit staan, altijd cleanup toestaan
        if (!requireTrailInFOV && !requirePlayerInFOV)
        {
            if (showFOVCleanupDebug)
                Debug.Log("🟢 No FOV requirements - cleanup always allowed");
            return true;
        }

        // KRITIEKE FIX: Als alleen player FOV vereist is, negeer trail FOV compleet
        if (requirePlayerInFOV && !requireTrailInFOV)
        {
            bool playerInFOV = IsPlayerInFieldOfView();
            if (showFOVCleanupDebug)
            {
                Debug.Log($"👤 ONLY Player FOV required:");
                Debug.Log($"   - Player in FOV: {playerInFOV}");
                Debug.Log($"   - Trail FOV status is COMPLETELY IGNORED");
                Debug.Log($"   - Result: {(playerInFOV ? "✅ CLEANUP ALLOWED" : "🚫 CLEANUP BLOCKED")}");
            }
            return playerInFOV;
        }

        // Als alleen trail FOV vereist is
        if (!requirePlayerInFOV && requireTrailInFOV)
        {
            bool trailInFOV = IsTrailInFieldOfView();
            if (showFOVCleanupDebug)
            {
                Debug.Log($"🔍 ONLY Trail FOV required:");
                Debug.Log($"   - Trail in FOV: {trailInFOV}");
                Debug.Log($"   - Player FOV status is COMPLETELY IGNORED");
                Debug.Log($"   - Result: {(trailInFOV ? "✅ CLEANUP ALLOWED" : "🚫 CLEANUP BLOCKED")}");
            }
            return trailInFOV;
        }

        // Als beide vereist zijn
        if (requirePlayerInFOV && requireTrailInFOV)
        {
            bool playerInFOV = IsPlayerInFieldOfView();
            bool trailInFOV = IsTrailInFieldOfView();
            bool bothRequired = playerInFOV && trailInFOV;

            if (showFOVCleanupDebug)
            {
                Debug.Log($"📋 BOTH Player AND Trail FOV required:");
                Debug.Log($"   - Player in FOV: {playerInFOV}");
                Debug.Log($"   - Trail in FOV: {trailInFOV}");
                Debug.Log($"   - Result: {(bothRequired ? "✅ CLEANUP ALLOWED" : "🚫 CLEANUP BLOCKED")}");
            }
            return bothRequired;
        }

        return false;
    }



    // NIEUWE FUNCTIE: Check of er trails binnen FOV zijn
    bool IsTrailInFieldOfView()
    {
        if (trailManager == null)
        {
            if (showFOVCleanupDebug)
                Debug.Log("🚫 No trail manager found");
            return false;
        }

        // Check specifiek de current trail target als die er is
        if (hasTrailTarget)
        {
            Vector3 trailWorldPos = grid.CellToWorld(currentTrailTarget);
            trailWorldPos += grid.cellSize * 0.5f;
            bool targetTrailInFOV = IsPositionInFieldOfView(trailWorldPos);

            if (showFOVCleanupDebug)
            {
                Debug.Log($"🔍 Current trail target {currentTrailTarget} in FOV: {targetTrailInFOV}");
            }

            return targetTrailInFOV;
        }

        // Fallback: check alle trails
        HashSet<Vector3Int> blockedPositions = trailManager.GetBlockedPositions();

        foreach (Vector3Int trailPos in blockedPositions)
        {
            Vector3 trailWorldPos = grid.CellToWorld(trailPos);
            trailWorldPos += grid.cellSize * 0.5f;

            if (IsPositionInFieldOfView(trailWorldPos))
            {
                if (showFOVCleanupDebug)
                    Debug.Log($"🔍 Trail at {trailPos} is within FOV");
                return true;
            }
        }

        if (showFOVCleanupDebug)
            Debug.Log("🚫 No trails found within FOV");
        return false;
    }


    // HELPER FUNCTIE: Check of een specifieke positie binnen FOV is
    bool IsPositionInFieldOfView(Vector3 targetPosition)
    {
        Vector3 directionToTarget = (targetPosition - transform.position).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

        // Check range
        if (distanceToTarget > fovRange)
            return false;

        // Check angle
        float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);
        if (angleToTarget > fovAngle / 2f)
            return false;

        // Check line of sight (geen obstacles tussen enemy en target)
        if (Physics.Raycast(transform.position, directionToTarget, distanceToTarget, obstacleLayer))
            return false;

        return true;
    }

    void ValidateTrailTargets()
    {
        if (trailManager == null) return;

        HashSet<Vector3Int> blockedPositions = trailManager.GetBlockedPositions();

        // Remove trails die niet meer bestaan of buiten FOV zijn
        discoveredTrails.RemoveAll(trailPos =>
        {
            if (!blockedPositions.Contains(trailPos))
            {
                Debug.Log($"🧹 Removing discovered trail at {trailPos} - trail no longer exists");
                return true;
            }

            // GEWIJZIGD: Check FOV in plaats van detection range
            Vector3 trailWorldPos = grid.CellToWorld(trailPos);
            trailWorldPos += grid.cellSize * 0.5f;
            bool trailInFOV = IsPositionInFieldOfView(trailWorldPos);
            if (!trailInFOV)
            {
                Debug.Log($"👁️ Removing discovered trail at {trailPos} - trail out of FOV");
                return true;
            }

            return false;
        });

        // Remove investigating trails die niet meer bestaan of buiten FOV zijn
        currentlyInvestigating.RemoveAll(trailPos =>
        {
            if (!blockedPositions.Contains(trailPos))
            {
                Debug.Log($"🧹 Removing investigating trail at {trailPos} - trail no longer exists");
                return true;
            }

            Vector3 trailWorldPos = grid.CellToWorld(trailPos);
            trailWorldPos += grid.cellSize * 0.5f;
            bool trailInFOV = IsPositionInFieldOfView(trailWorldPos);
            if (!trailInFOV)
            {
                Debug.Log($"👁️ Removing investigating trail at {trailPos} - trail out of FOV");
                return true;
            }

            return false;
        });
    }

    void CreateFovVisualizer()
    {
        if (fovVisualizerObject != null)
        {
            DestroyImmediate(fovVisualizerObject);
        }

        fovVisualizerObject = new GameObject("FOV_Visualizer");
        fovVisualizerObject.transform.SetParent(transform);
        fovVisualizerObject.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        fovVisualizerObject.hideFlags = HideFlags.DontSave;

        fovMeshFilter = fovVisualizerObject.AddComponent<MeshFilter>();
        fovMeshRenderer = fovVisualizerObject.AddComponent<MeshRenderer>();

        CreateFovMaterial();

        fovMesh = new Mesh();
        fovMesh.name = "FOV_Mesh";
        fovMeshFilter.mesh = fovMesh;
    }

    void CreateFovMaterial()
    {
        Shader transparentShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (transparentShader == null)
        {
            transparentShader = Shader.Find("Unlit/Transparent");
        }
        if (transparentShader == null)
        {
            transparentShader = Shader.Find("Standard");
        }

        fovMaterial = new Material(transparentShader);
        fovMaterial.name = "FOV_Material";

        if (fovMaterial.HasProperty("_Surface"))
        {
            fovMaterial.SetFloat("_Surface", 1);
        }
        if (fovMaterial.HasProperty("_Blend"))
        {
            fovMaterial.SetFloat("_Blend", 0);
        }
        if (fovMaterial.HasProperty("_BaseColor"))
        {
            fovMaterial.SetColor("_BaseColor", fovColor);
        }
        else if (fovMaterial.HasProperty("_Color"))
        {
            fovMaterial.SetColor("_Color", fovColor);
        }

        if (fovMaterial.HasProperty("_Mode"))
        {
            fovMaterial.SetFloat("_Mode", 3);
            fovMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            fovMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            fovMaterial.SetInt("_ZWrite", 0);
            fovMaterial.DisableKeyword("_ALPHATEST_ON");
            fovMaterial.EnableKeyword("_ALPHABLEND_ON");
            fovMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        fovMaterial.renderQueue = 3000;
        fovMeshRenderer.material = fovMaterial;
        fovMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fovMeshRenderer.receiveShadows = false;
    }

    void UpdateFovVisualizer()
    {
        if (fovMesh == null || !Application.isPlaying) return;

        Vector3[] fovPoints = CalculateFovPoints();
        UpdateFovMesh(fovPoints);

        if (fovVisualizerObject != null)
        {
            fovVisualizerObject.SetActive(showFieldOfView);
        }

        if (fovMaterial != null)
        {
            if (fovMaterial.HasProperty("_BaseColor"))
            {
                fovMaterial.SetColor("_BaseColor", fovColor);
            }
            else if (fovMaterial.HasProperty("_Color"))
            {
                fovMaterial.SetColor("_Color", fovColor);
            }
        }
    }

    Vector3[] CalculateFovPoints()
    {
        int rayCount = Mathf.RoundToInt(fovAngle * fovResolution / 360f);
        if (rayCount < 3) rayCount = 3;

        float angle = transform.eulerAngles.y;
        float angleIncrease = fovAngle / rayCount;

        List<Vector3> points = new List<Vector3>();
        points.Add(transform.position);

        for (int i = 0; i <= rayCount; i++)
        {
            float currentAngle = angle - fovAngle / 2f + angleIncrease * i;
            Vector3 rayDirection = GetVectorFromAngle(currentAngle);

            float rayDistance = fovRange;
            Vector3 raycastOrigin = transform.position + Vector3.up * 0.5f;

            if (Application.isPlaying && Physics.Raycast(raycastOrigin, rayDirection, out RaycastHit hit, fovRange, obstacleLayer))
            {
                rayDistance = hit.distance;
            }

            Vector3 endPoint = raycastOrigin + rayDirection * rayDistance;
            endPoint.y = transform.position.y;
            points.Add(endPoint);
        }

        return points.ToArray();
    }

    void UpdateFovMesh(Vector3[] worldPoints)
    {
        if (fovMesh == null) return;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int i = 0; i < worldPoints.Length; i++)
        {
            vertices.Add(transform.InverseTransformPoint(worldPoints[i]));
        }

        for (int i = 1; i < vertices.Count - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(i + 1);
        }

        fovMesh.Clear();
        fovMesh.vertices = vertices.ToArray();
        fovMesh.triangles = triangles.ToArray();
        fovMesh.RecalculateNormals();
    }

    Vector3 GetVectorFromAngle(float angle)
    {
        float angleRad = angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(angleRad), 0, Mathf.Cos(angleRad));
    }

    bool IsPlayerInFieldOfView()
    {
        if (player == null || !useFovForDetection)
        {
            if (showFOVCleanupDebug && player == null)
                Debug.Log("🚫 Player is NULL - cannot check FOV");
            if (showFOVCleanupDebug && !useFovForDetection)
                Debug.Log("🚫 FOV detection is DISABLED");
            return false;
        }

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        Vector3 forward = transform.forward;
        float distance = Vector3.Distance(transform.position, player.position);
        float angle = Vector3.Angle(forward, directionToPlayer);

        if (showFOVCleanupDebug)
        {
            Debug.Log($"🎯 Player FOV details:");
            Debug.Log($"   Distance: {distance:F2} (max: {fovRange:F2})");
            Debug.Log($"   Angle: {angle:F2}° (max: {fovAngle / 2f:F2}°)");
        }

        // Check angle
        if (angle > fovAngle / 2f)
        {
            if (showFOVCleanupDebug)
                Debug.Log($"🚫 Player angle too wide: {angle:F2}° > {fovAngle / 2f:F2}°");
            return false;
        }

        // Check distance
        if (distance > fovRange)
        {
            if (showFOVCleanupDebug)
                Debug.Log($"🚫 Player too far: {distance:F2} > {fovRange:F2}");
            return false;
        }

        // Check line of sight
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 rayTarget = player.position + Vector3.up * 0.5f;
        Vector3 rayDirection = (rayTarget - rayOrigin).normalized;
        float rayDistance = Vector3.Distance(rayOrigin, rayTarget);

        if (Physics.Raycast(rayOrigin, rayDirection, rayDistance, obstacleLayer))
        {
            if (showFOVCleanupDebug)
                Debug.Log("🚫 Player blocked by obstacle");
            return false;
        }

        if (showFOVCleanupDebug)
            Debug.Log("✅ Player IS in FOV");
        return true;
    }


    void CreateDefaultPatrol()
    {
        patrolPoints.Clear();
        Vector3Int center = currentGridPosition;

        patrolPoints.Add(center);
        patrolPoints.Add(center + Vector3Int.right * 2);
        patrolPoints.Add(center + Vector3Int.right * 2 + Vector3Int.forward * 2);
        patrolPoints.Add(center + Vector3Int.forward * 2);
    }

    void HandleCurrentState()
    {
        switch (currentState)
        {
            case EnemyState.Patrolling:
                HandlePatrolMovement();
                break;

            case EnemyState.Chasing:
                HandleChasing();
                break;

            case EnemyState.Returning:
                HandleReturning();
                break;

            case EnemyState.MovingToTrail:
                HandleTrailMovement();
                break;

            // NIEUWE CASE: Direct doorgaan na trail investigation
            case EnemyState.InvestigatingTrail:
                // Do nothing - investigation coroutine handles this
                break;
        }
    }

    void HandlePatrolMovement()
    {
        if (patrolPoints.Count == 0) return;

        // NIEUWE CHECK: Als we net trail investigation hebben afgerond, 
        // vind het dichtstbijzijnde patrol point als nieuwe starting point
        if (currentPatrolIndex >= patrolPoints.Count || Vector3Int.Distance(currentGridPosition, patrolPoints[currentPatrolIndex]) > 3)
        {
            // Find closest patrol point en stel in als nieuwe target
            int closestIndex = 0;
            float minDistance = Vector3Int.Distance(currentGridPosition, patrolPoints[0]);

            for (int i = 1; i < patrolPoints.Count; i++)
            {
                float distance = Vector3Int.Distance(currentGridPosition, patrolPoints[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestIndex = i;
                }
            }

            currentPatrolIndex = closestIndex;
            Debug.Log($"🎯 Resuming patrol from closest point: index {closestIndex} at {patrolPoints[closestIndex]}");
        }

        Vector3Int targetPoint = GetNextPatrolPoint();
        if (targetPoint != currentGridPosition)
        {
            MoveToPosition(targetPoint);
        }
        else
        {
            StartPatrolPointSequence();
        }
    }

    void StartPatrolPointSequence()
    {
        currentAction = StartCoroutine(PatrolPointSequence());
    }

    IEnumerator PatrolPointSequence()
    {
        currentState = EnemyState.Waiting;
        isWaiting = true;

        float initialWait = useRandomWaitTime ?
            Random.Range(randomWaitRange.x * 0.3f, randomWaitRange.y * 0.3f) :
            waitTimeAtPoint * 0.3f;

        yield return new WaitForSeconds(initialWait);

        if (enableLookAround)
        {
            yield return StartCoroutine(LookAroundSequence());
        }

        float remainingWait = useRandomWaitTime ?
            Random.Range(randomWaitRange.x * 0.7f, randomWaitRange.y * 0.7f) :
            waitTimeAtPoint * 0.7f;

        yield return new WaitForSeconds(remainingWait);

        isWaiting = false;
        currentState = EnemyState.Patrolling;
    }

    IEnumerator LookAroundSequence()
    {
        currentState = EnemyState.LookingAround;
        isLookingAround = true;

        float startRotation = transform.eulerAngles.y;
        List<float> lookAngles = GenerateLookAngles(startRotation);

        foreach (float targetAngle in lookAngles)
        {
            yield return StartCoroutine(SmoothRotateToAngle(targetAngle, rotationSpeed * 1.5f));
            yield return new WaitForSeconds(timeBetweenLooks);

            if (chasePlayer && player != null && IsPlayerInFieldOfView())
            {
                Debug.Log("Player spotted while looking around!");
                isLookingAround = false;
                currentState = EnemyState.Chasing;
                yield break;
            }
        }

        yield return StartCoroutine(SmoothRotateToAngle(startRotation, rotationSpeed));
        isLookingAround = false;
    }

    List<float> GenerateLookAngles(float centerAngle)
    {
        List<float> angles = new List<float>();

        if (lookDirections <= 1)
        {
            return angles;
        }

        float angleStep = lookAngleRange / (lookDirections - 1);
        float startAngle = centerAngle - lookAngleRange / 2f;

        for (int i = 0; i < lookDirections; i++)
        {
            float angle = startAngle + angleStep * i;
            angles.Add(angle);
        }

        if (randomLookOrder)
        {
            for (int i = 0; i < angles.Count; i++)
            {
                float temp = angles[i];
                int randomIndex = Random.Range(i, angles.Count);
                angles[i] = angles[randomIndex];
                angles[randomIndex] = temp;
            }
        }

        return angles;
    }

    IEnumerator SmoothRotateToAngle(float targetAngle, float speed)
    {
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0);

        float elapsedTime = 0;
        float rotateTime = Quaternion.Angle(startRotation, targetRotation) / (speed * 360f);

        while (elapsedTime < rotateTime)
        {
            float t = elapsedTime / rotateTime;
            t = Mathf.SmoothStep(0f, 1f, t);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRotation;
    }

    Vector3Int GetNextPatrolPoint()
    {
        switch (patrolType)
        {
            case PatrolType.Loop:
                return GetLoopPatrolPoint();

            case PatrolType.PingPong:
                return GetPingPongPatrolPoint();

            case PatrolType.Random:
                return GetRandomPatrolPoint();

            case PatrolType.StayInArea:
                return GetRandomAreaPoint();

            default:
                return currentGridPosition;
        }
    }

    Vector3Int GetLoopPatrolPoint()
    {
        if (currentPatrolIndex >= patrolPoints.Count)
            currentPatrolIndex = 0;

        Vector3Int target = patrolPoints[currentPatrolIndex];

        if (target == currentGridPosition)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
            return patrolPoints[currentPatrolIndex];
        }

        return target;
    }

    Vector3Int GetPingPongPatrolPoint()
    {
        if (patrolPoints.Count <= 1) return currentGridPosition;

        Vector3Int target = patrolPoints[currentPatrolIndex];

        if (target == currentGridPosition)
        {
            if (patrolForward)
            {
                currentPatrolIndex++;
                if (currentPatrolIndex >= patrolPoints.Count)
                {
                    currentPatrolIndex = patrolPoints.Count - 2;
                    patrolForward = false;
                }
            }
            else
            {
                currentPatrolIndex--;
                if (currentPatrolIndex < 0)
                {
                    currentPatrolIndex = 1;
                    patrolForward = true;
                }
            }

            if (currentPatrolIndex >= 0 && currentPatrolIndex < patrolPoints.Count)
                return patrolPoints[currentPatrolIndex];
        }

        return target;
    }

    Vector3Int GetRandomPatrolPoint()
    {
        if (patrolPoints.Count == 0) return currentGridPosition;
        int randomIndex = Random.Range(0, patrolPoints.Count);
        return patrolPoints[randomIndex];
    }

    Vector3Int GetRandomAreaPoint()
    {
        if (patrolPoints.Count < 2) return currentGridPosition;

        Vector3Int min = patrolPoints[0];
        Vector3Int max = patrolPoints[1];

        int x = Random.Range(Mathf.Min(min.x, max.x), Mathf.Max(min.x, max.x) + 1);
        int z = Random.Range(Mathf.Min(min.z, max.z), Mathf.Max(min.z, max.z) + 1);

        return new Vector3Int(x, currentGridPosition.y, z);
    }

    void CheckPlayerDetection()
    {
        if (player == null) return;

        bool playerDetected = false;

        if (useFovForDetection)
        {
            playerDetected = IsPlayerInFieldOfView();
        }
        else
        {
            Vector3Int playerGridPos = grid.WorldToCell(player.position);
            float distance = Vector3Int.Distance(currentGridPosition, playerGridPos);
            playerDetected = distance <= detectionRange;
        }

        if (playerDetected && currentState != EnemyState.Chasing)
        {
            Debug.Log("Player detected! Starting chase.");
            currentState = EnemyState.Chasing;

            if (currentAction != null)
                StopCoroutine(currentAction);

            isWaiting = false;
            isLookingAround = false;
            isInvestigatingTrail = false;
            hasTrailTarget = false;
        }
        else if (!playerDetected && currentState == EnemyState.Chasing)
        {
            Debug.Log("Player lost. Returning to patrol.");
            currentState = EnemyState.Returning;
        }
    }

    void HandleChasing()
    {
        if (player == null)
        {
            currentState = EnemyState.Returning;
            return;
        }

        Vector3Int playerGridPos = grid.WorldToCell(player.position);
        Vector3Int direction = GetDirectionToTarget(playerGridPos);

        if (direction != Vector3Int.zero)
        {
            Vector3Int targetPos = currentGridPosition + direction;
            MoveToPosition(targetPos);
        }
    }

    void HandleReturning()
    {
        Vector3Int closestPatrolPoint = GetClosestPatrolPoint();

        if (closestPatrolPoint == currentGridPosition)
        {
            currentState = EnemyState.Patrolling;
        }
        else
        {
            Vector3Int direction = GetDirectionToTarget(closestPatrolPoint);
            if (direction != Vector3Int.zero)
            {
                Vector3Int targetPos = currentGridPosition + direction;
                MoveToPosition(targetPos);
            }
        }
    }

    Vector3Int GetClosestPatrolPoint()
    {
        if (patrolPoints.Count == 0) return startPosition;

        Vector3Int closest = patrolPoints[0];
        float minDistance = Vector3Int.Distance(currentGridPosition, closest);

        foreach (Vector3Int point in patrolPoints)
        {
            float distance = Vector3Int.Distance(currentGridPosition, point);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = point;
            }
        }

        return closest;
    }

    Vector3Int GetDirectionToTarget(Vector3Int target)
    {
        Vector3Int difference = target - currentGridPosition;

        if (Mathf.Abs(difference.x) > Mathf.Abs(difference.z))
        {
            return new Vector3Int(difference.x > 0 ? 1 : -1, 0, 0);
        }
        else if (difference.z != 0)
        {
            return new Vector3Int(0, 0, difference.z > 0 ? 1 : -1);
        }

        return Vector3Int.zero;
    }

    void MoveToPosition(Vector3Int targetGridPos)
    {
        if (isMoving) return;

        Vector3Int direction = targetGridPos - currentGridPosition;

        if (enableRotation && !isLookingAround)
        {
            RotateToDirection(direction);
        }

        currentAction = StartCoroutine(useBezierMovement ?
            BezierMoveCoroutine(targetGridPos) :
            LinearMoveCoroutine(targetGridPos));
    }

    void RotateToDirection(Vector3Int direction)
    {
        if (direction == Vector3Int.zero) return;

        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0);

        StartCoroutine(RotateCoroutine(targetRotation));
    }

    IEnumerator RotateCoroutine(Quaternion targetRotation)
    {
        Quaternion startRotation = transform.rotation;
        float elapsedTime = 0;
        float rotateTime = 1f / rotationSpeed;

        while (elapsedTime < rotateTime)
        {
            float t = elapsedTime / rotateTime;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRotation;
    }

    IEnumerator BezierMoveCoroutine(Vector3Int targetGridPos)
    {
        isMoving = true;
        currentState = EnemyState.Moving;

        Vector3 startPos = transform.position;
        Vector3 targetPos = grid.CellToWorld(targetGridPos);
        targetPos += grid.cellSize * 0.5f;
        targetPos.y = startPos.y;

        Vector3 midPoint = (startPos + targetPos) * 0.5f;
        midPoint.y += bezierHeight;

        float elapsedTime = 0;
        float moveTime = 1f / moveSpeed;

        while (elapsedTime < moveTime)
        {
            float t = elapsedTime / moveTime;
            float curveT = movementCurve.Evaluate(t);

            Vector3 currentPos = CalculateBezierPoint(startPos, midPoint, targetPos, curveT);
            currentPos.y = Mathf.Lerp(startPos.y, targetPos.y, curveT);

            transform.position = currentPos;
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        currentGridPosition = targetGridPos;
        isMoving = false;

        // Reset state na beweging
        if (currentState == EnemyState.Moving)
        {
            if (hasTrailTarget)
            {
                currentState = EnemyState.MovingToTrail;
            }
            else
            {
                currentState = EnemyState.Patrolling;
            }
        }
    }

    IEnumerator LinearMoveCoroutine(Vector3Int targetGridPos)
    {
        isMoving = true;
        currentState = EnemyState.Moving;

        Vector3 startPos = transform.position;
        Vector3 targetPos = grid.CellToWorld(targetGridPos);
        targetPos += grid.cellSize * 0.5f;
        targetPos.y = startPos.y;

        float elapsedTime = 0;
        float moveTime = 1f / moveSpeed;

        while (elapsedTime < moveTime)
        {
            float t = elapsedTime / moveTime;
            float curveT = movementCurve.Evaluate(t);
            transform.position = Vector3.Lerp(startPos, targetPos, curveT);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        currentGridPosition = targetGridPos;
        isMoving = false;

        // Reset state na beweging
        if (currentState == EnemyState.Moving)
        {
            if (hasTrailTarget)
            {
                currentState = EnemyState.MovingToTrail;
            }
            else
            {
                currentState = EnemyState.Patrolling;
            }
        }
    }

    Vector3 CalculateBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;

        Vector3 point = uu * p0;
        point += 2 * u * t * p1;
        point += tt * p2;

        return point;
    }

    void SnapToGrid()
    {
        Vector3 worldPos = grid.CellToWorld(currentGridPosition);
        worldPos += grid.cellSize * 0.5f;
        worldPos.y = transform.position.y;
        transform.position = worldPos;
    }

    // Public methods
    public void SetPatrolPoints(List<Vector3Int> newPoints)
    {
        patrolPoints = new List<Vector3Int>(newPoints);
        currentPatrolIndex = 0;
    }

    public void AddPatrolPoint(Vector3Int point)
    {
        patrolPoints.Add(point);
    }

    public void ClearPatrolPoints()
    {
        patrolPoints.Clear();
    }

    public void SetPatrolType(PatrolType newType)
    {
        patrolType = newType;
        currentPatrolIndex = 0;
        patrolForward = true;
    }

    public bool IsMoving()
    {
        return isMoving;
    }

    public bool IsWaiting()
    {
        return isWaiting;
    }

    public bool IsLookingAround()
    {
        return isLookingAround;
    }

    public bool IsInvestigatingTrail()
    {
        return isInvestigatingTrail;
    }

    void DrawFovGizmos()
    {
        Vector3 fovLeft = Quaternion.AngleAxis(-fovAngle / 2f, Vector3.up) * transform.forward * fovRange;
        Vector3 fovRight = Quaternion.AngleAxis(fovAngle / 2f, Vector3.up) * transform.forward * fovRange;

        Gizmos.color = fovBorderColor;
        Gizmos.DrawRay(transform.position, fovLeft);
        Gizmos.DrawRay(transform.position, fovRight);

        // Draw FOV arc
        Vector3 previousPoint = transform.position + fovLeft;
        for (int i = 1; i <= 20; i++)
        {
            float angle = Mathf.Lerp(-fovAngle / 2f, fovAngle / 2f, (float)i / 20f);
            Vector3 fovDirection = Quaternion.AngleAxis(angle, Vector3.up) * transform.forward * fovRange;
            Vector3 currentPoint = transform.position + fovDirection;
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }

        // Fill FOV area
        Gizmos.color = fovEditorColor;
        Mesh fovMeshGizmo = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        vertices.Add(Vector3.zero);

        for (int i = 0; i <= 20; i++)
        {
            float angle = Mathf.Lerp(-fovAngle / 2f, fovAngle / 2f, (float)i / 20f);
            Vector3 fovDirection = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * fovRange;
            vertices.Add(fovDirection);
        }

        for (int i = 1; i < vertices.Count - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(i + 1);
        }

        fovMeshGizmo.vertices = vertices.ToArray();
        fovMeshGizmo.triangles = triangles.ToArray();

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawMesh(fovMeshGizmo);
        Gizmos.matrix = Matrix4x4.identity;
    }

    void DrawBezierPreview(Vector3 start, Vector3 end)
    {
        Vector3 midPoint = (start + end) * 0.5f;
        midPoint.y += bezierHeight;

        int segments = 10;
        Vector3 previousPoint = start;

        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 currentPoint = CalculateBezierPoint(start, midPoint, end, t);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }

    void OnDestroy()
    {
        if (fovVisualizerObject != null)
        {
            DestroyImmediate(fovVisualizerObject);
        }

        if (fovMaterial != null)
        {
            DestroyImmediate(fovMaterial);
        }

        if (fovMesh != null)
        {
            DestroyImmediate(fovMesh);
        }
    }

    void OnValidate()
    {
        // Clamp values to reasonable ranges
        fovAngle = Mathf.Clamp(fovAngle, 1f, 360f);
        fovRange = Mathf.Max(fovRange, 0.1f);
        fovResolution = Mathf.Clamp(fovResolution, 1, 50);
        detectionRange = Mathf.Max(detectionRange, 0.1f);
        moveSpeed = Mathf.Max(moveSpeed, 0.1f);
        rotationSpeed = Mathf.Max(rotationSpeed, 0.1f);
        waitTimeAtPoint = Mathf.Max(waitTimeAtPoint, 0f);
        lookAroundDuration = Mathf.Max(lookAroundDuration, 0f);
        trailInvestigationTime = Mathf.Max(trailInvestigationTime, 0.1f);
        trailCleanupTime = Mathf.Max(trailCleanupTime, 0.1f);

        // Clamp look directions
        lookDirections = Mathf.Max(lookDirections, 1);
        lookAngleRange = Mathf.Clamp(lookAngleRange, 0f, 360f);
        timeBetweenLooks = Mathf.Max(timeBetweenLooks, 0f);

        // Clamp random wait range
        randomWaitRange.x = Mathf.Max(randomWaitRange.x, 0f);
        randomWaitRange.y = Mathf.Max(randomWaitRange.y, randomWaitRange.x);

        // Clamp bezier height
        bezierHeight = Mathf.Max(bezierHeight, 0f);

        // Update current grid position if grid is available
        if (grid != null && Application.isPlaying)
        {
            currentGridPosition = grid.WorldToCell(transform.position);
        }
    }

    // Public interface methods
    public void StopCurrentAction()
    {
        if (currentAction != null)
        {
            StopCoroutine(currentAction);
            currentAction = null;
        }

        isMoving = false;
        isWaiting = false;
        isLookingAround = false;
        isInvestigatingTrail = false;
        hasTrailTarget = false;
    }

    public void ResetToPatrol()
    {
        StopCurrentAction();
        currentState = EnemyState.Patrolling;
        currentPatrolIndex = 0;
        patrolForward = true;
    }

    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
    }

    public void SetChaseMode(bool enabled)
    {
        chasePlayer = enabled;
        if (!enabled && currentState == EnemyState.Chasing)
        {
            currentState = EnemyState.Returning;
        }
    }

    public void SetTrailInvestigation(bool enabled)
    {
        investigateTrails = enabled;
        if (!enabled)
        {
            StopCurrentAction();
            discoveredTrails.Clear();
            currentlyInvestigating.Clear();
            hasTrailTarget = false;
            if (currentState == EnemyState.MovingToTrail || currentState == EnemyState.InvestigatingTrail)
            {
                currentState = EnemyState.Returning;
            }
        }
    }

    public void TeleportToPosition(Vector3Int gridPosition)
    {
        StopCurrentAction();
        currentGridPosition = gridPosition;
        SnapToGrid();
        currentState = EnemyState.Patrolling;
    }

    public void SetFovSettings(float angle, float range)
    {
        fovAngle = Mathf.Clamp(angle, 1f, 360f);
        fovRange = Mathf.Max(range, 0.1f);
    }

    public void SetMovementSettings(float speed, float rotSpeed)
    {
        moveSpeed = Mathf.Max(speed, 0.1f);
        rotationSpeed = Mathf.Max(rotSpeed, 0.1f);
    }

    public void SetWaitSettings(float waitTime, bool useRandom, Vector2 randomRange)
    {
        waitTimeAtPoint = Mathf.Max(waitTime, 0f);
        useRandomWaitTime = useRandom;
        randomWaitRange = randomRange;
        randomWaitRange.x = Mathf.Max(randomWaitRange.x, 0f);
        randomWaitRange.y = Mathf.Max(randomWaitRange.y, randomWaitRange.x);
    }

    public void SetLookAroundSettings(bool enabled, float duration, int directions, float angleRange)
    {
        enableLookAround = enabled;
        lookAroundDuration = Mathf.Max(duration, 0f);
        lookDirections = Mathf.Max(directions, 1);
        lookAngleRange = Mathf.Clamp(angleRange, 0f, 360f);
    }

    // Debug and info methods
    public string GetCurrentStateInfo()
    {
        string info = $"State: {currentState}\n";
        info += $"Position: {currentGridPosition}\n";
        info += $"Moving: {isMoving}\n";
        info += $"Waiting: {isWaiting}\n";
        info += $"Looking Around: {isLookingAround}\n";
        info += $"Investigating Trail: {isInvestigatingTrail}\n";
        info += $"Has Trail Target: {hasTrailTarget}\n";

        if (hasTrailTarget)
        {
            info += $"Trail Target: {currentTrailTarget}\n";
        }

        info += $"Discovered Trails: {discoveredTrails.Count}\n";
        info += $"Currently Investigating: {currentlyInvestigating.Count}\n";
        info += $"Patrol Index: {currentPatrolIndex}/{patrolPoints.Count}\n";

        return info;
    }

    public List<Vector3Int> GetDiscoveredTrails()
    {
        return new List<Vector3Int>(discoveredTrails);
    }

    public List<Vector3Int> GetCurrentlyInvestigatingTrails()
    {
        return new List<Vector3Int>(currentlyInvestigating);
    }

    public Vector3Int GetCurrentTrailTarget()
    {
        return hasTrailTarget ? currentTrailTarget : Vector3Int.zero;
    }

    public bool HasValidTrailManager()
    {
        return trailManager != null;
    }

    public int GetTrailCount()
    {
        return trailManager != null ? trailManager.GetBlockedPositions().Count : 0;
    }

    // FOV Settings Methods
    public void SetRequireTrailInFOV(bool required)
    {
        requireTrailInFOV = required;
        Debug.Log($"Require Trail in FOV set to: {required}");
    }

    public void SetRequirePlayerInFOV(bool required)
    {
        requirePlayerInFOV = required;
        Debug.Log($"Require Player in FOV set to: {required}");
    }

    public void SetFOVCleanupDebug(bool enabled)
    {
        showFOVCleanupDebug = enabled;
    }

    public bool GetRequireTrailInFOV()
    {
        return requireTrailInFOV;
    }

    public bool GetRequirePlayerInFOV()
    {
        return requirePlayerInFOV;
    }

    // Performance monitoring
    public float GetLastTrailCheckTime()
    {
        return lastTrailCheckTime;
    }

    public float GetTrailCheckInterval()
    {
        return trailCheckInterval;
    }

    public void SetTrailCheckInterval(float interval)
    {
        trailCheckInterval = Mathf.Max(interval, 0.1f);
    }
}
