using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace GameProjectFM.AI.Behaviors
{
    using Core;
    using Systems;

    public class TrailInvestigationBehavior : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private TrailSettings trailSettings;
        [SerializeField] private FOVCleanupSettings fovCleanupSettings;
        [SerializeField] private MovementSystem movementSystem;
        [SerializeField] private FOVSystem fovSystem;

        // Trail investigation state
        private Vector3Int currentTrailTarget;
        private bool hasTrailTarget;
        private bool isInvestigatingTrail;
        private Coroutine investigationCoroutine;

        // Trail tracking
        private List<Vector3Int> discoveredTrails = new List<Vector3Int>();
        private List<Vector3Int> currentlyInvestigating = new List<Vector3Int>();

        // External dependencies
        private PlayerTrailManager trailManager;
        private Grid grid;
        private Transform player;

        // Events
        public System.Action<Vector3Int> OnTrailDetected;
        public System.Action<Vector3Int> OnTrailInvestigationStarted;
        public System.Action<Vector3Int> OnTrailCleaned;
        public System.Action OnTrailInvestigationCompleted;

        public bool HasTrailTarget => hasTrailTarget;
        public bool IsInvestigating => isInvestigatingTrail;
        public Vector3Int CurrentTrailTarget => currentTrailTarget;

        public void Initialize(TrailSettings trail, FOVCleanupSettings fovCleanup, MovementSystem movement, FOVSystem fov, PlayerTrailManager trails, Grid gameGrid, Transform playerTransform)
        {
            trailSettings = trail;
            fovCleanupSettings = fovCleanup;
            movementSystem = movement;
            fovSystem = fov;
            trailManager = trails;
            grid = gameGrid;
            player = playerTransform;
        }

        public void CheckForTrails()
        {
            if (!trailSettings.investigateTrails) return;

            ValidateTrailTargets();

            HashSet<Vector3Int> blockedPositions = trailManager.GetBlockedPositions();
            if (blockedPositions.Count == 0) return;

            // FOV requirement check before trail detection
            if (fovCleanupSettings.requirePlayerInFOV && !fovCleanupSettings.requireTrailInFOV)
            {
                bool playerInFOV = fovSystem.IsPositionInFieldOfView(player.position);
                if (!playerInFOV)
                {
                    if (fovCleanupSettings.showFOVCleanupDebug)
                    {
                        Debug.Log("🚫 Player not in FOV - skipping trail detection entirely (Require Player in FOV = true)");
                    }
                    return;
                }
                else if (fovCleanupSettings.showFOVCleanupDebug)
                {
                    Debug.Log("✅ Player in FOV - proceeding with trail detection");
                }
            }

            // Filter trails on FOV
            List<TrailInfo> availableTrails = new List<TrailInfo>();

            foreach (Vector3Int trailPos in blockedPositions)
            {
                Vector3 trailWorldPos = grid.CellToWorld(trailPos);
                trailWorldPos += grid.cellSize * 0.5f;

                bool trailInFOV = fovSystem.IsPositionInFieldOfView(trailWorldPos);
                if (!trailInFOV) continue;

                float distance = Vector3Int.Distance(movementSystem.CurrentGridPosition, trailPos);
                bool isDiscovered = discoveredTrails.Contains(trailPos);
                bool canReconsiderTrail = (fovCleanupSettings.requireTrailInFOV || fovCleanupSettings.requirePlayerInFOV) && isDiscovered && IsCleanupAllowedByFOV();

                if ((!isDiscovered || canReconsiderTrail) && !currentlyInvestigating.Contains(trailPos))
                {
                    availableTrails.Add(new TrailInfo { position = trailPos, distance = distance });

                    if (canReconsiderTrail)
                    {
                        Debug.Log($"🔄 Reconsidering trail at {trailPos} because FOV requirements are now met");
                        discoveredTrails.Remove(trailPos);
                    }
                }
            }

            if (availableTrails.Count > 0)
            {
                if (trailSettings.alwaysClosestFirst)
                {
                    availableTrails.Sort((a, b) => a.distance.CompareTo(b.distance));
                }
                else if (trailSettings.prioritizeNewestTrails)
                {
                    availableTrails.Sort((a, b) => b.distance.CompareTo(a.distance));
                }

                Vector3Int targetTrail = availableTrails[0].position;
                Debug.Log($"🕵️ EnemyAI detected {availableTrails.Count} trails within FOV. Prioritizing closest at {targetTrail} (distance: {availableTrails[0].distance:F1})");

                StartTrailInvestigation(targetTrail);
                OnTrailDetected?.Invoke(targetTrail);
            }
        }

        public void StartTrailInvestigation(Vector3Int trailPosition)
        {
            if (hasTrailTarget)
            {
                Debug.Log($"Already investigating trail at {currentTrailTarget}, ignoring new trail at {trailPosition}");
                return;
            }

            discoveredTrails.Add(trailPosition);
            currentlyInvestigating.Add(trailPosition);

            currentTrailTarget = trailPosition;
            hasTrailTarget = true;

            Debug.Log($"🔍 Starting trail investigation towards {trailPosition}. Current position: {movementSystem.CurrentGridPosition}");
            OnTrailInvestigationStarted?.Invoke(trailPosition);
        }

        public bool IsAtTrailTarget()
        {
            return hasTrailTarget && movementSystem.CurrentGridPosition == currentTrailTarget;
        }

        public void StartInvestigationSequence()
        {
            if (investigationCoroutine != null)
            {
                StopCoroutine(investigationCoroutine);
            }

            investigationCoroutine = StartCoroutine(InvestigateTrailSequence());
        }

        public void StopInvestigation()
        {
            if (investigationCoroutine != null)
            {
                StopCoroutine(investigationCoroutine);
                investigationCoroutine = null;
            }

            CleanupTrailInvestigation();
        }

        private IEnumerator InvestigateTrailSequence()
        {
            isInvestigatingTrail = true;

            Debug.Log($"🔍 Starting trail investigation sequence at {currentTrailTarget}");

            yield return new WaitForSeconds(trailSettings.trailInvestigationTime);

            if (IsCleanupAllowedByFOV())
            {
                Debug.Log($"🧹 FOV requirements met. Starting cleanup of trail at {currentTrailTarget}");
                yield return StartCoroutine(CleanupTrailSequence());
            }
            else
            {
                Debug.Log($"🚫 FOV requirements not met. Skipping cleanup of trail at {currentTrailTarget}");
            }

            CleanupTrailInvestigation();
            OnTrailInvestigationCompleted?.Invoke();

            isInvestigatingTrail = false;
        }

        private IEnumerator CleanupTrailSequence()
        {
            Debug.Log($"🧹 Cleaning up trail at {currentTrailTarget}");

            yield return new WaitForSeconds(trailSettings.trailCleanupTime);

            trailManager.RemoveTrailAt(currentTrailTarget);
            OnTrailCleaned?.Invoke(currentTrailTarget);

            Debug.Log($"✅ Trail cleanup complete at {currentTrailTarget}");
        }

        private bool IsCleanupAllowedByFOV()
        {
            if (!fovCleanupSettings.requireTrailInFOV && !fovCleanupSettings.requirePlayerInFOV)
            {
                if (fovCleanupSettings.showFOVCleanupDebug)
                    Debug.Log("🟢 No FOV requirements - cleanup always allowed");
                return true;
            }

            if (fovCleanupSettings.requirePlayerInFOV && !fovCleanupSettings.requireTrailInFOV)
            {
                bool playerInFOV = fovSystem.IsPositionInFieldOfView(player.position);
                if (fovCleanupSettings.showFOVCleanupDebug)
                {
                    Debug.Log($"👤 ONLY Player FOV required:");
                    Debug.Log($"   - Player in FOV: {playerInFOV}");
                    Debug.Log($"   - Trail FOV status is COMPLETELY IGNORED");
                    Debug.Log($"   - Result: {(playerInFOV ? "✅ CLEANUP ALLOWED" : "🚫 CLEANUP BLOCKED")}");
                }
                return playerInFOV;
            }

            if (!fovCleanupSettings.requirePlayerInFOV && fovCleanupSettings.requireTrailInFOV)
            {
                bool trailInFOV = IsTrailInFieldOfView();
                if (fovCleanupSettings.showFOVCleanupDebug)
                {
                    Debug.Log($"🔍 ONLY Trail FOV required:");
                    Debug.Log($"   - Trail in FOV: {trailInFOV}");
                    Debug.Log($"   - Player FOV status is COMPLETELY IGNORED");
                    Debug.Log($"   - Result: {(trailInFOV ? "✅ CLEANUP ALLOWED" : "🚫 CLEANUP BLOCKED")}");
                }
                return trailInFOV;
            }

            if (fovCleanupSettings.requirePlayerInFOV && fovCleanupSettings.requireTrailInFOV)
            {
                bool playerInFOV = fovSystem.IsPositionInFieldOfView(player.position);
                bool trailInFOV = IsTrailInFieldOfView();
                bool bothRequired = playerInFOV && trailInFOV;

                if (fovCleanupSettings.showFOVCleanupDebug)
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

        private bool IsTrailInFieldOfView()
        {
            if (!hasTrailTarget) return false;

            Vector3 trailWorldPos = grid.CellToWorld(currentTrailTarget);
            trailWorldPos += grid.cellSize * 0.5f;

            return fovSystem.IsPositionInFieldOfView(trailWorldPos);
        }

        private void ValidateTrailTargets()
        {
            HashSet<Vector3Int> currentTrails = trailManager.GetBlockedPositions();

            discoveredTrails.RemoveAll(trail => !currentTrails.Contains(trail));
            currentlyInvestigating.RemoveAll(trail => !currentTrails.Contains(trail));

            if (hasTrailTarget && !currentTrails.Contains(currentTrailTarget))
            {
                Debug.Log($"Current trail target {currentTrailTarget} no longer exists. Clearing target.");
                CleanupTrailInvestigation();
            }
        }

        private void CleanupTrailInvestigation()
        {
            if (hasTrailTarget)
            {
                currentlyInvestigating.Remove(currentTrailTarget);
            }

            currentTrailTarget = Vector3Int.zero;
            hasTrailTarget = false;
            isInvestigatingTrail = false;

            if (investigationCoroutine != null)
            {
                StopCoroutine(investigationCoroutine);
                investigationCoroutine = null;
            }
        }

        public Vector3Int GetDirectionToTarget()
        {
            if (!hasTrailTarget) return Vector3Int.zero;

            Vector3Int direction = currentTrailTarget - movementSystem.CurrentGridPosition;

            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
            {
                return new Vector3Int(direction.x > 0 ? 1 : -1, 0, 0);
            }
            else if (direction.z != 0)
            {
                return new Vector3Int(0, 0, direction.z > 0 ? 1 : -1);
            }

            return Vector3Int.zero;
        }
    }
}
