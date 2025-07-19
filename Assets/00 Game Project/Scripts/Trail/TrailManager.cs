using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

public class PlayerTrailManager : MonoBehaviour
{
    [Header("Trail Settings")]
    public GameObject decalPrefab;
    public Material trailDecalMaterial;
    [Tooltip("Deze Boolean functie bepaald of de speler mag starten met een Trail. Als je wilt dat de Trail pas mag starten zodra de speler poep" +
             "heeft opgepakt, dan zet je de enableTrail uit.")]
    public bool enableTrail = true;
    public bool limitTrailLength = false;
    public int maxTrailLength = 50;

    [Header("Temporary Trail System")]
    [Tooltip("Hoeveel tiles de tijdelijke trail nog actief is")]
    public int temporaryTrailTilesRemaining = 0;
    [Tooltip("Material voor tijdelijke trails (anders dan normale)")]
    public Material temporaryTrailMaterial;
    [Tooltip("Kleur voor tijdelijke trails")]
    public Color temporaryTrailColor = Color.green;

    [Header("Movement Blocking")]
    [Tooltip("Deze Boolean bepaald of de speler over zijn eigen stront mag lopen.")]
    public bool blockMovementOnTrails = true;
    public bool allowLeavingStartPosition = true;

    [Header("Rendering Layer Settings")]
    [Tooltip("Deze waarde bepaalt op welke Rendering Layer het object getoond wordt, 2 staat voor ground Rendering Layer.")]
    public uint decalRenderingLayerMask = 2; // Ground layer

    [Header("Trail Visual Settings")]
    [Tooltip("Deze Boolean bepaald of de Trail na loop van tijd wordt verwijderd, als de speler te sloom is.")]
    public bool fadeTrailOverTime = true;
    public float trailFadeDuration = 10f;
    public Color trailStartColor = Color.white;
    public Color trailEndColor = Color.clear;

    [Header("Trail Positioning")]
    public float trailYHeight = 0.5f;

    [Header("Trail Organization")]
    public string trailParentName = "Player Trails";
    public bool organizeInParent = true;

    [Header("Grid Settings")]
    public Grid gridReference;

    [Header("References")]
    public GridPlayerController playerController;

    // Trail tracking
    private HashSet<Vector3Int> trailPositions = new HashSet<Vector3Int>();
    private Queue<TrailDecal> activeTrails = new Queue<TrailDecal>();
    private Transform trailParent;
    private Vector3Int playerStartPosition;

    [System.Serializable]
    public class TrailDecal
    {
        public GameObject decalObject;
        public DecalProjector decalProjector;
        public Vector3Int gridPosition;
        public float spawnTime;
        public bool isTemporary;

        public TrailDecal(GameObject obj, DecalProjector projector, Vector3Int pos, bool temporary = false)
        {
            decalObject = obj;
            decalProjector = projector;
            gridPosition = pos;
            spawnTime = Time.time;
            isTemporary = temporary;
        }
    }

    void Start()
    {
        if (gridReference == null)
            gridReference = FindFirstObjectByType<Grid>();

        if (playerController == null)
            playerController = FindFirstObjectByType<GridPlayerController>();

        if (playerController != null)
        {
            playerStartPosition = playerController.currentGridPosition;
        }

        CreateTrailParent();

        if (decalPrefab == null)
            CreateDecalPrefab();
    }

    void Update()
    {
        if (playerController != null)
        {
            Vector3Int currentGridPos = playerController.currentGridPosition;

            // Check of we trail moeten toevoegen
            bool shouldAddTrail = (enableTrail || temporaryTrailTilesRemaining > 0)
                                && !trailPositions.Contains(currentGridPos);

            if (shouldAddTrail)
            {
                bool isTemporaryTrail = temporaryTrailTilesRemaining > 0;
                AddTrailAtPosition(currentGridPos, isTemporaryTrail);

                // Verlaag temporary trail counter
                if (temporaryTrailTilesRemaining > 0)
                {
                    temporaryTrailTilesRemaining--;
                    Debug.Log($"💩 Temporary trail: {temporaryTrailTilesRemaining} tiles remaining");
                }
            }
        }

        if (fadeTrailOverTime)
        {
            UpdateTrailFading();
        }
    }

    private void CreateTrailParent()
    {
        GameObject existingParent = GameObject.Find(trailParentName);

        if (existingParent != null)
        {
            trailParent = existingParent.transform;
        }
        else if (organizeInParent)
        {
            GameObject parentObj = new GameObject(trailParentName);
            trailParent = parentObj.transform;
        }
        else
        {
            trailParent = null;
        }
    }

    private void CreateDecalPrefab()
    {
        decalPrefab = new GameObject("Trail Decal");

        DecalProjector decalProjector = decalPrefab.AddComponent<DecalProjector>();

        decalProjector.size = new Vector3(1.8f, 1.8f, 0.5f);
        decalProjector.material = trailDecalMaterial;
        decalProjector.renderingLayerMask = decalRenderingLayerMask;

        decalPrefab.SetActive(false);
    }

    private void AddTrailAtPosition(Vector3Int gridPosition, bool isTemporary = false)
    {
        // Markeer positie als bezocht
        trailPositions.Add(gridPosition);

        // Spawn decal op grid positie
        Vector3 worldPosition = gridReference.CellToWorld(gridPosition);
        worldPosition += gridReference.cellSize * 0.5f;
        worldPosition.y = trailYHeight;

        GameObject trailDecal = Instantiate(decalPrefab, trailParent);
        trailDecal.transform.position = worldPosition;
        trailDecal.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        trailDecal.SetActive(true);

        DecalProjector projector = trailDecal.GetComponent<DecalProjector>();
        projector.renderingLayerMask = decalRenderingLayerMask;

        // ✅ Gebruik verschillende materials voor normale vs temporary trails
        if (isTemporary && temporaryTrailMaterial != null)
        {
            projector.material = temporaryTrailMaterial;
            trailDecal.name = "Temporary Trail Decal";
        }
        else if (isTemporary)
        {
            // Gebruik normale material maar met andere kleur
            Material tempMat = new Material(trailDecalMaterial);
            tempMat.SetColor("_Color", temporaryTrailColor);
            projector.material = tempMat;
            trailDecal.name = "Temporary Trail Decal";
        }
        else
        {
            projector.material = trailDecalMaterial;
            trailDecal.name = "Normal Trail Decal";
        }

        TrailDecal newTrail = new TrailDecal(trailDecal, projector, gridPosition, isTemporary);
        activeTrails.Enqueue(newTrail);

        if (limitTrailLength && activeTrails.Count > maxTrailLength)
        {
            RemoveOldestTrail();
        }
    }

    private void RemoveOldestTrail()
    {
        if (activeTrails.Count > 0)
        {
            TrailDecal oldestTrail = activeTrails.Dequeue();
            trailPositions.Remove(oldestTrail.gridPosition);

            if (oldestTrail.decalObject != null)
            {
                Destroy(oldestTrail.decalObject);
            }
        }
    }

    private void UpdateTrailFading()
    {
        if (trailDecalMaterial == null) return;

        Queue<TrailDecal> tempQueue = new Queue<TrailDecal>();

        while (activeTrails.Count > 0)
        {
            TrailDecal trail = activeTrails.Dequeue();

            if (trail.decalObject == null)
                continue;

            float age = Time.time - trail.spawnTime;

            if (age >= trailFadeDuration)
            {
                trailPositions.Remove(trail.gridPosition);
                Destroy(trail.decalObject);
            }
            else
            {
                float fadeProgress = age / trailFadeDuration;
                Color startColor = trail.isTemporary ? temporaryTrailColor : trailStartColor;
                Color endColor = trail.isTemporary ? Color.clear : trailEndColor;
                Color currentColor = Color.Lerp(startColor, endColor, fadeProgress);

                Material instanceMaterial = trail.decalProjector.material;
                if (instanceMaterial.HasProperty("_Color"))
                {
                    instanceMaterial.SetColor("_Color", currentColor);
                }

                tempQueue.Enqueue(trail);
            }
        }

        activeTrails = tempQueue;
    }

    // ✅ Public API voor poep powerup system
    public void ActivateTemporaryTrail(int tileCount)
    {
        temporaryTrailTilesRemaining += tileCount;
        Debug.Log($"💩 Temporary trail activated! {temporaryTrailTilesRemaining} tiles total");
    }

    public bool IsTemporaryTrailActive()
    {
        return temporaryTrailTilesRemaining > 0;
    }

    public int GetTemporaryTrailTilesRemaining()
    {
        return temporaryTrailTilesRemaining;
    }

    public bool RemoveTrailAt(Vector3Int gridPosition)
    {
        if (!trailPositions.Contains(gridPosition))
        {
            Debug.Log($"No trail found at position {gridPosition} to remove");
            return false;
        }

        // Remove from position set
        trailPositions.Remove(gridPosition);

        // Find and remove from active trails queue
        Queue<TrailDecal> tempQueue = new Queue<TrailDecal>();
        bool trailFound = false;

        while (activeTrails.Count > 0)
        {
            TrailDecal trail = activeTrails.Dequeue();

            if (trail.gridPosition == gridPosition)
            {
                // Destroy the trail object
                if (trail.decalObject != null)
                {
                    Destroy(trail.decalObject);
                }
                trailFound = true;
                Debug.Log($"🧹 Trail removed at {gridPosition}");
            }
            else
            {
                // Keep other trails
                tempQueue.Enqueue(trail);
            }
        }

        // Restore the queue without the removed trail
        activeTrails = tempQueue;

        return trailFound;
    }


    // Movement blocking API
    public bool IsPositionBlocked(Vector3Int gridPosition)
    {
        if (!blockMovementOnTrails) return false;

        if (allowLeavingStartPosition && gridPosition == playerStartPosition)
            return false;

        return trailPositions.Contains(gridPosition);
    }

    public bool CanMoveToPosition(Vector3Int fromPosition, Vector3Int toPosition)
    {
        if (!blockMovementOnTrails) return true;

        if (allowLeavingStartPosition && fromPosition == playerStartPosition)
            return true;

        return !trailPositions.Contains(toPosition);
    }

    public HashSet<Vector3Int> GetBlockedPositions()
    {
        return new HashSet<Vector3Int>(trailPositions);
    }

    // Trail management methods
    public void ClearTrail()
    {
        while (activeTrails.Count > 0)
        {
            TrailDecal trail = activeTrails.Dequeue();
            if (trail.decalObject != null)
                Destroy(trail.decalObject);
        }

        trailPositions.Clear();
        temporaryTrailTilesRemaining = 0;
    }

    public void SetTrailEnabled(bool enabled)
    {
        enableTrail = enabled;
    }

    public void SetMovementBlocking(bool enabled)
    {
        blockMovementOnTrails = enabled;
    }

    // Debug methods
    [ContextMenu("Log Trail Info")]
    public void LogTrailInfo()
    {
        Debug.Log($"Active Trails: {activeTrails.Count}");
        Debug.Log($"Blocked Positions: {trailPositions.Count}");
        Debug.Log($"Temporary Trail Tiles Remaining: {temporaryTrailTilesRemaining}");
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Toon blocked positions (normale trails)
        if (blockMovementOnTrails)
        {
            Gizmos.color = Color.red;
            foreach (Vector3Int gridPos in trailPositions)
            {
                if (gridReference != null)
                {
                    Vector3 worldPos = gridReference.CellToWorld(gridPos);
                    worldPos += gridReference.cellSize * 0.5f;
                    worldPos.y += 0.1f;

                    Gizmos.DrawWireCube(worldPos, gridReference.cellSize * 0.9f);
                }
            }
        }

        // Toon temporary trail indicator
        if (temporaryTrailTilesRemaining > 0)
        {
            Gizmos.color = Color.green;
            if (playerController != null && gridReference != null)
            {
                Vector3 playerPos = gridReference.CellToWorld(playerController.currentGridPosition);
                playerPos += gridReference.cellSize * 0.5f;
                playerPos.y += 0.3f;

                Gizmos.DrawWireSphere(playerPos, 0.5f);
            }
        }

        // Toon start position
        if (allowLeavingStartPosition)
        {
            Gizmos.color = Color.blue;
            Vector3 startWorldPos = gridReference.CellToWorld(playerStartPosition);
            startWorldPos += gridReference.cellSize * 0.5f;
            startWorldPos.y += 0.2f;

            Gizmos.DrawWireCube(startWorldPos, gridReference.cellSize * 0.8f);
        }
    }

    public void RefreshBlockedPositions()
    {
        // Force refresh van blocked positions door alle trails opnieuw te scannen
        HashSet<Vector3Int> newBlockedPositions = new HashSet<Vector3Int>();

        // Scan alle bestaande trail objecten in de scene
        GameObject[] trailObjects = GameObject.FindGameObjectsWithTag("Trail");
        foreach (GameObject obj in trailObjects)
        {
            Vector3Int gridPos = gridReference.WorldToCell(obj.transform.position);
            newBlockedPositions.Add(gridPos);
        }

        // Update de interne blocked positions
        trailPositions = newBlockedPositions;

        Debug.Log($"Refreshed blocked positions. Found {newBlockedPositions.Count} trails.");
    }

}
