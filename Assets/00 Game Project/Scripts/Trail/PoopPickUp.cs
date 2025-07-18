using UnityEngine;

public class PoopPickUp : MonoBehaviour
{
    [Header("Poop Settings")]
    [Tooltip("Aantal tiles dat de speler trail laat na het oppakken")]
    public int trailTileCount = 10;

    [Tooltip("Uniek ID voor deze poep (voor save/load)")]
    public string poopId;

    [Tooltip("Automatically generate ID on Start")]
    public bool autoGenerateId = true;

    [Header("Visual Settings")]
    [Tooltip("Material voor de poep")]
    public Material poopMaterial;

    [Tooltip("Scale animatie bij oppakken")]
    public bool animateOnPickup = true;
    public float pickupAnimationDuration = 0.5f;

    [Header("Audio")]
    [Tooltip("Geluid bij oppakken")]
    public AudioClip pickupSound;

    [Header("Grid Setup")]
    [Tooltip("Grid reference voor positie berekening")]
    public Grid gridReference;

    [Header("Debug")]
    [Tooltip("Show debug messages")]
    public bool enableDebug = true;

    private Vector3Int gridPosition;
    private bool isPickedUp = false;
    private AudioSource audioSource;

    void Start()
    {
        // Auto-generate ID if needed
        if (autoGenerateId && string.IsNullOrEmpty(poopId))
        {
            poopId = System.Guid.NewGuid().ToString();
        }

        // Find grid if not assigned
        if (gridReference == null)
            gridReference = FindFirstObjectByType<Grid>();

        // Calculate grid position
        if (gridReference != null)
            gridPosition = gridReference.WorldToCell(transform.position);

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && pickupSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Snap to grid
        SnapToGrid();

        if (enableDebug)
        {
            Debug.Log($"💩 PoopPickUp initialized at position: {transform.position}, Grid position: {gridPosition}");
        }
    }

    private void SnapToGrid()
    {
        if (gridReference == null) return;

        Vector3 worldPos = gridReference.CellToWorld(gridPosition);
        worldPos += gridReference.cellSize * 0.5f;
        worldPos.y = transform.position.y; // Behoud Y positie
        transform.position = worldPos;
    }

    void OnTriggerEnter(Collider other)
    {
        if (isPickedUp) return;

        if (enableDebug)
        {
            Debug.Log($"💩 Trigger detected with: {other.name}, Tag: {other.tag}");
        }

        // Check if het de Player is (via tag of component)
        GridPlayerController player = other.GetComponent<GridPlayerController>();

        // Alternative check via tag
        if (player == null && other.CompareTag("Player"))
        {
            player = other.GetComponent<GridPlayerController>();
        }

        // Alternative check via parent
        if (player == null)
        {
            player = other.GetComponentInParent<GridPlayerController>();
        }

        if (player != null)
        {
            if (enableDebug)
            {
                Debug.Log($"💩 Player detected! Activating powerup...");
            }
            ActivatePoopPowerup(player);
        }
        else if (enableDebug)
        {
            Debug.Log($"💩 Object '{other.name}' is not a player");
        }
    }

    private void ActivatePoopPowerup(GridPlayerController player)
    {
        if (isPickedUp) return;
        isPickedUp = true;

        if (enableDebug)
        {
            Debug.Log($"💩 Activating poop powerup for {trailTileCount} tiles");
        }

        // Activeer trail voor X tiles - meerdere methoden proberen
        PlayerTrailManager trailManager = null;

        // Method 1: Via public property
        if (trailManager == null)
            trailManager = player.trailManager;

        // Method 2: Via getter method
        if (trailManager == null)
            trailManager = player.GetTrailManager();

        // Method 3: Via component search
        if (trailManager == null)
            trailManager = player.GetComponent<PlayerTrailManager>();

        // Method 4: Via find in parent/children
        if (trailManager == null)
            trailManager = player.GetComponentInChildren<PlayerTrailManager>();

        if (trailManager != null)
        {
            trailManager.ActivateTemporaryTrail(trailTileCount);
            Debug.Log($"💩 Poep opgepakt! Trail geactiveerd voor {trailTileCount} tiles!");
        }
        else
        {
            Debug.LogError($"💩 PlayerTrailManager not found on player '{player.name}'!");
        }

        // Speel geluid
        if (audioSource != null && pickupSound != null)
        {
            audioSource.PlayOneShot(pickupSound);
        }

        // Animatie en vernietiging
        if (animateOnPickup)
        {
            StartCoroutine(PickupAnimation());
        }
        else
        {
            DestroyPoop();
        }
    }

    private System.Collections.IEnumerator PickupAnimation()
    {
        Vector3 originalScale = transform.localScale;
        float elapsedTime = 0f;

        // Scale down animatie
        while (elapsedTime < pickupAnimationDuration)
        {
            float t = elapsedTime / pickupAnimationDuration;
            float scale = Mathf.Lerp(1f, 0f, t);
            transform.localScale = originalScale * scale;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        DestroyPoop();
    }

    private void DestroyPoop()
    {
        if (enableDebug)
        {
            Debug.Log($"💩 Destroying poop: {name}");
        }
        Destroy(gameObject);
    }

    // Public methods
    public Vector3Int GetGridPosition()
    {
        return gridPosition;
    }

    public string GetPoopId()
    {
        return poopId;
    }

    public bool IsPickedUp()
    {
        return isPickedUp;
    }

    void OnDrawGizmos()
    {
        if (gridReference != null)
        {
            // Toon grid positie
            Gizmos.color = Color.brown;
            Vector3 worldPos = gridReference.CellToWorld(gridPosition);
            worldPos += gridReference.cellSize * 0.5f;
            Gizmos.DrawWireCube(worldPos, gridReference.cellSize * 0.8f);
        }

        // Toon pickup range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }

    void OnValidate()
    {
        // Minimum 1 tile
        if (trailTileCount < 1)
            trailTileCount = 1;
    }

    // Debug method
    [ContextMenu("Test Pickup")]
    public void TestPickup()
    {
        GridPlayerController player = FindFirstObjectByType<GridPlayerController>();
        if (player != null)
        {
            ActivatePoopPowerup(player);
        }
        else
        {
            Debug.LogError("💩 No GridPlayerController found in scene for testing!");
        }
    }
}
