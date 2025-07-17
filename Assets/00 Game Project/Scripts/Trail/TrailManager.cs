using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

public class PlayerTrailManager : MonoBehaviour
{
    [Header("Trail Settings")]
    public GameObject decalPrefab;
    public Material trailDecalMaterial;
    public bool enableTrail = true;
    public bool limitTrailLength = false;
    public int maxTrailLength = 50;

    [Header("Movement Blocking")]
    [Tooltip("Voorkom movement naar posities met trails")]
    public bool blockMovementOnTrails = true;
    [Tooltip("Speler kan zijn eigen starting positie verlaten")]
    public bool allowLeavingStartPosition = true;

    [Header("Rendering Layer Settings")]
    public uint decalRenderingLayerMask = 2; // Ground layer

    [Header("Trail Visual Settings")]
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

    // ✅ Trail position tracking
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

        public TrailDecal(GameObject obj, DecalProjector projector, Vector3Int pos)
        {
            decalObject = obj;
            decalProjector = projector;
            gridPosition = pos;
            spawnTime = Time.time;
        }
    }

    void Start()
    {
        if (gridReference == null)
            gridReference = FindFirstObjectByType<Grid>();

        if (playerController == null)
            playerController = FindFirstObjectByType<GridPlayerController>();

        // Onthoud start positie van speler
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
        if (enableTrail && playerController != null)
        {
            Vector3Int currentGridPos = playerController.currentGridPosition;

            // Voeg trail toe als speler naar nieuwe positie beweegt
            if (!trailPositions.Contains(currentGridPos))
            {
                AddTrailAtPosition(currentGridPos);
            }
        }

        // Update trail fading
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

    private void AddTrailAtPosition(Vector3Int gridPosition)
    {
        if (!enableTrail) return;

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

        TrailDecal newTrail = new TrailDecal(trailDecal, projector, gridPosition);

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

            // ✅ Verwijder uit blocked positions
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
                // ✅ Trail is te oud, verwijder uit blocked positions
                trailPositions.Remove(trail.gridPosition);
                Destroy(trail.decalObject);
            }
            else
            {
                // Update fade
                float fadeProgress = age / trailFadeDuration;
                Color currentColor = Color.Lerp(trailStartColor, trailEndColor, fadeProgress);

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

    // ✅ Public API for movement checking
    public bool IsPositionBlocked(Vector3Int gridPosition)
    {
        if (!blockMovementOnTrails) return false;

        // Allow leaving start position
        if (allowLeavingStartPosition && gridPosition == playerStartPosition)
            return false;

        return trailPositions.Contains(gridPosition);
    }

    public bool CanMoveToPosition(Vector3Int fromPosition, Vector3Int toPosition)
    {
        if (!blockMovementOnTrails) return true;

        // Allow leaving start position
        if (allowLeavingStartPosition && fromPosition == playerStartPosition)
            return true;

        return !trailPositions.Contains(toPosition);
    }

    public HashSet<Vector3Int> GetBlockedPositions()
    {
        return new HashSet<Vector3Int>(trailPositions);
    }

    public int GetTrailLength()
    {
        return activeTrails.Count;
    }

    public int GetBlockedPositionCount()
    {
        return trailPositions.Count;
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
    }

    public void SetTrailEnabled(bool enabled)
    {
        enableTrail = enabled;
    }

    public void SetMovementBlocking(bool enabled)
    {
        blockMovementOnTrails = enabled;
    }

    public void SetDecalRenderingLayers(uint renderingMask)
    {
        decalRenderingLayerMask = renderingMask;

        // Update bestaande decals
        foreach (TrailDecal trail in activeTrails)
        {
            if (trail.decalProjector != null)
            {
                trail.decalProjector.renderingLayerMask = renderingMask;
            }
        }
    }

    // Debug methods
    [ContextMenu("Log Trail Info")]
    public void LogTrailInfo()
    {
        Debug.Log($"Active Trails: {activeTrails.Count}");
        Debug.Log($"Blocked Positions: {trailPositions.Count}");
        Debug.Log($"Movement Blocking: {blockMovementOnTrails}");

        foreach (Vector3Int pos in trailPositions)
        {
            Debug.Log($"Blocked: {pos}");
        }
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !blockMovementOnTrails) return;

        // Toon blocked positions
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

        // Toon start position
        if (allowLeavingStartPosition)
        {
            Gizmos.color = Color.green;
            Vector3 startWorldPos = gridReference.CellToWorld(playerStartPosition);
            startWorldPos += gridReference.cellSize * 0.5f;
            startWorldPos.y += 0.2f;

            Gizmos.DrawWireCube(startWorldPos, gridReference.cellSize * 0.8f);
        }
    }
}
