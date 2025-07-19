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
    public float detectionRange = 3f;
    public Transform player;
    public LayerMask playerLayer = -1;
    public bool chasePlayer = false;

    [Header("Trail Investigation")]
    public bool investigateTrails = true;
    public float trailDetectionRange = 6f;
    public float trailCleanupTime = 3f;
    public float trailInvestigationTime = 2f;
    public bool prioritizeNewestTrails = false; // Dit gaan we niet meer gebruiken
    public bool alwaysClosestFirst = true; // Nieuwe setting
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
    private List<Vector3Int> currentlyInvestigating = new List<Vector3Int>(); // Nieuwe lijst
    private bool hasTrailTarget = false;
    private float lastTrailCheckTime = 0f;
    private float trailCheckInterval = 1f; // Verhoogd naar 1 seconde

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

        // GEFIXT: Verbeterde state handling
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
        // Get trails van PlayerTrailManager
        HashSet<Vector3Int> blockedPositions = trailManager.GetBlockedPositions();

        if (blockedPositions.Count == 0) return;

        // Filter trails binnen detection range die nog niet onderzocht worden
        List<TrailInfo> availableTrails = new List<TrailInfo>();

        foreach (Vector3Int trailPos in blockedPositions)
        {
            float distance = Vector3Int.Distance(currentGridPosition, trailPos);

            // Check of trail binnen range is en niet al onderzocht of bezig met onderzoeken
            if (distance <= trailDetectionRange &&
                !discoveredTrails.Contains(trailPos) &&
                !currentlyInvestigating.Contains(trailPos))
            {
                availableTrails.Add(new TrailInfo { position = trailPos, distance = distance });
            }
        }

        if (availableTrails.Count > 0)
        {
            // Sorteer op afstand (dichtstbijzijnde eerst)
            availableTrails.Sort((a, b) => a.distance.CompareTo(b.distance));

            Vector3Int targetTrail = availableTrails[0].position;

            Debug.Log($"🕵️ EnemyAI detected {availableTrails.Count} trails. Prioritizing closest at {targetTrail} (distance: {availableTrails[0].distance:F1})");
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

        // GEFIXT: Reset alle flags om te zorgen dat movement kan beginnen
        isWaiting = false;
        isLookingAround = false;
        isInvestigatingTrail = false;
        isMoving = false; // TOEGEVOEGD: Zorg dat movement niet geblokkeerd wordt

        // Set state
        currentState = EnemyState.MovingToTrail;

        Debug.Log($"🔍 Starting trail investigation towards {trailPosition}. Current position: {currentGridPosition}");

        // TOEGEVOEGD: Force eerste movement stap
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

        // Check of de trail nog bestaat (misschien door andere AI of speler weggehaald)
        HashSet<Vector3Int> currentTrails = trailManager.GetBlockedPositions();
        if (!currentTrails.Contains(currentTrailTarget))
        {
            Debug.Log($"Trail at {currentTrailTarget} no longer exists, abandoning investigation");
            CleanupTrailInvestigation();
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
            // Als we geen richting kunnen vinden, stop de trail investigation
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

        // Start trail cleanup timer
        StartCoroutine(ScheduleTrailCleanup(currentTrailTarget));

        // Reset investigation state
        CleanupTrailInvestigation();

        Debug.Log("🧹 Trail investigation complete. Returning to patrol.");
        currentState = EnemyState.Returning;

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

    IEnumerator ScheduleTrailCleanup(Vector3Int trailPosition)
    {
        yield return new WaitForSeconds(trailCleanupTime);

        // Remove trail door het GameObject te zoeken en verwijderen
        bool trailRemoved = RemoveTrailGameObject(trailPosition);

        if (trailRemoved)
        {
            Debug.Log($"🧹 EnemyAI cleaned up trail at {trailPosition}");
        }
        else
        {
            Debug.Log($"Trail at {trailPosition} was already removed or not found");
        }

        // Remove van onze lijsten na cleanup
        if (discoveredTrails.Contains(trailPosition))
        {
            discoveredTrails.Remove(trailPosition);
        }
        if (currentlyInvestigating.Contains(trailPosition))
        {
            currentlyInvestigating.Remove(trailPosition);
        }
    }

    bool RemoveTrailGameObject(Vector3Int gridPosition)
    {
        Vector3 worldPosition = grid.CellToWorld(gridPosition);
        worldPosition += grid.cellSize * 0.5f;

        // Methode 1: Zoek via Trail tag (hoofdmethode)
        GameObject[] trailObjects = GameObject.FindGameObjectsWithTag("Trail");
        foreach (GameObject obj in trailObjects)
        {
            float distance = Vector3.Distance(obj.transform.position, worldPosition);
            if (distance < 0.5f) // 0.5 units tolerance
            {
                Debug.Log($"🗑️ Found and removing trail object via tag: {obj.name} at {obj.transform.position}");
                Destroy(obj);
                return true;
            }
        }

        // Fallback methode: Zoek via Physics overlap (voor het geval tag ontbreekt)
        Collider[] colliders = Physics.OverlapSphere(worldPosition, 0.5f);
        foreach (Collider col in colliders)
        {
            if (col.gameObject.name.Contains("Trail") || col.gameObject.name.Contains("Decal") || col.gameObject.name.Contains("Poop"))
            {
                Debug.Log($"🗑️ Found and removing trail object via collider: {col.gameObject.name}");
                Destroy(col.gameObject);
                return true;
            }
        }

        Debug.LogWarning($"Could not find trail GameObject at position {gridPosition} (world: {worldPosition})");
        return false;
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
        if (player == null || !useFovForDetection) return false;

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        Vector3 forward = transform.forward;

        float angle = Vector3.Angle(forward, directionToPlayer);
        if (angle > fovAngle / 2f) return false;

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > fovRange) return false;

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 rayTarget = player.position + Vector3.up * 0.5f;
        Vector3 rayDirection = (rayTarget - rayOrigin).normalized;
        float rayDistance = Vector3.Distance(rayOrigin, rayTarget);

        if (Physics.Raycast(rayOrigin, rayDirection, rayDistance, obstacleLayer))
        {
            return false;
        }

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
        }
    }

    void HandlePatrolMovement()
    {
        if (patrolPoints.Count == 0) return;

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

    void OnDrawGizmos()
    {
        if (grid == null) return;

        // Patrol path
        if (showPatrolPath && patrolPoints.Count > 1)
        {
            Gizmos.color = patrolPathColor;

            for (int i = 0; i < patrolPoints.Count; i++)
            {
                Vector3 worldPos = grid.CellToWorld(patrolPoints[i]);
                worldPos += grid.cellSize * 0.5f;

                Gizmos.DrawWireCube(worldPos, Vector3.one * 0.3f);

                if (patrolType == PatrolType.Loop)
                {
                    int nextIndex = (i + 1) % patrolPoints.Count;
                    Vector3 nextWorldPos = grid.CellToWorld(patrolPoints[nextIndex]);
                    nextWorldPos += grid.cellSize * 0.5f;
                    Gizmos.DrawLine(worldPos, nextWorldPos);

                    if (useBezierMovement)
                    {
                        DrawBezierPreview(worldPos, nextWorldPos);
                    }
                }
                else if (patrolType == PatrolType.PingPong && i < patrolPoints.Count - 1)
                {
                    Vector3 nextWorldPos = grid.CellToWorld(patrolPoints[i + 1]);
                    nextWorldPos += grid.cellSize * 0.5f;
                    Gizmos.DrawLine(worldPos, nextWorldPos);

                    if (useBezierMovement)
                    {
                        DrawBezierPreview(worldPos, nextWorldPos);
                    }
                }
            }
        }

        // Detection range
        if (showDetectionRange && !useFovForDetection)
        {
            Gizmos.color = detectionColor;
            Vector3 worldPos = transform.position;
            float worldDetectionRange = detectionRange * (grid ? grid.cellSize.x : 1f);
            Gizmos.DrawWireSphere(worldPos, worldDetectionRange);
        }

        // Trail detection range
        if (showTrailDetection && investigateTrails && Application.isPlaying && trailManager != null)
        {
            HashSet<Vector3Int> blockedPositions = trailManager.GetBlockedPositions();

            foreach (Vector3Int trailPos in blockedPositions)
            {
                float distance = Vector3Int.Distance(currentGridPosition, trailPos);
                if (distance <= trailDetectionRange)
                {
                    Vector3 trailWorldPos = grid.CellToWorld(trailPos);
                    trailWorldPos += grid.cellSize * 0.5f;

                    // Verschillende kleuren voor verschillende statussen
                    if (currentlyInvestigating.Contains(trailPos))
                    {
                        Gizmos.color = Color.red; // Wordt onderzocht
                        Gizmos.DrawWireCube(trailWorldPos, Vector3.one * 0.6f);
                    }
                    else if (discoveredTrails.Contains(trailPos))
                    {
                        Gizmos.color = Color.yellow; // Al ontdekt
                        Gizmos.DrawWireCube(trailWorldPos, Vector3.one * 0.4f);
                    }
                    else
                    {
                        Gizmos.color = Color.green; // Beschikbaar
                        Gizmos.DrawWireCube(trailWorldPos, Vector3.one * 0.3f);
                    }
                }
            }

            // Toon detection range
            Gizmos.color = trailTargetColor;
            Vector3 worldPos = transform.position;
            float worldTrailRange = trailDetectionRange * (grid ? grid.cellSize.x : 1f);
            Gizmos.DrawWireSphere(worldPos, worldTrailRange);
        }

        // Current trail target
        if (hasTrailTarget && Application.isPlaying)
        {
            Gizmos.color = trailTargetColor;
            Vector3 targetWorldPos = grid.CellToWorld(currentTrailTarget);
            targetWorldPos += grid.cellSize * 0.5f;
            Gizmos.DrawWireCube(targetWorldPos, Vector3.one * 0.4f);

            Gizmos.DrawLine(transform.position, targetWorldPos);
        }

        // FOV in editor
        if (showFieldOfView && showFovInEditor)
        {
            DrawFovGizmos();
        }

        // Current state
        if (Application.isPlaying)
        {
            Vector3 textPos = transform.position + Vector3.up * 2f;

#if UNITY_EDITOR
            string stateText = $"State: {currentState}";
            if (hasTrailTarget)
            {
                stateText += $"\nTrail: {currentTrailTarget}";
            }
            UnityEditor.Handles.Label(textPos, stateText);
#endif
        }
    }

    void DrawBezierPreview(Vector3 start, Vector3 end)
    {
        Vector3 midPoint = (start + end) * 0.5f;
        midPoint.y += bezierHeight;

        Gizmos.color = Color.cyan;
        Vector3 prevPoint = start;

        for (int i = 1; i <= 10; i++)
        {
            float t = i / 10f;
            Vector3 currentPoint = CalculateBezierPoint(start, midPoint, end, t);
            Gizmos.DrawLine(prevPoint, currentPoint);
            prevPoint = currentPoint;
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(midPoint, 0.1f);
    }

    void DrawFovGizmos()
    {
        Vector3 origin = transform.position;
        float angle = transform.eulerAngles.y;

        Gizmos.color = fovBorderColor;
        Vector3 viewAngleA = GetVectorFromAngle(angle - fovAngle / 2);
        Vector3 viewAngleB = GetVectorFromAngle(angle + fovAngle / 2);

        Gizmos.DrawLine(origin, origin + viewAngleA * fovRange);
        Gizmos.DrawLine(origin, origin + viewAngleB * fovRange);

        if (!Application.isPlaying)
        {
            Vector3[] fovPoints = CalculateFovPoints();

            if (fovPoints.Length > 2)
            {
#if UNITY_EDITOR
                UnityEditor.Handles.color = fovEditorColor;

                for (int i = 1; i < fovPoints.Length - 1; i++)
                {
                    Vector3[] triangle = { fovPoints[0], fovPoints[i], fovPoints[i + 1] };
                    UnityEditor.Handles.DrawAAConvexPolygon(triangle);
                }

                UnityEditor.Handles.color = fovBorderColor;
                for (int i = 1; i < fovPoints.Length; i++)
                {
                    UnityEditor.Handles.DrawLine(fovPoints[i - 1], fovPoints[i]);
                }
#endif
            }
        }

        if (Application.isPlaying && isLookingAround)
        {
            Gizmos.color = Color.green;
            Vector3 forward = transform.forward;
            Vector3 leftBound = Quaternion.Euler(0, -lookAngleRange / 2f, 0) * forward;
            Vector3 rightBound = Quaternion.Euler(0, lookAngleRange / 2f, 0) * forward;

            Gizmos.DrawLine(origin, origin + leftBound * fovRange * 0.7f);
            Gizmos.DrawLine(origin, origin + rightBound * fovRange * 0.7f);
        }
    }

    void OnDestroy()
    {
        if (fovVisualizerObject != null)
        {
            DestroyImmediate(fovVisualizerObject);
        }
    }

    void OnValidate()
    {
        if (Application.isPlaying && fovMaterial != null)
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
}
