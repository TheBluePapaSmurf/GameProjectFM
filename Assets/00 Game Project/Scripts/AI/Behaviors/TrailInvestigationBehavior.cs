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

        // Alert State System
        // Alert State System
        private bool isInAlertState = false;
        private float alertStateEndTime = 0f;
        private List<Vector3Int> alertStateDetectedTrails = new List<Vector3Int>();

        // Public Properties
        public bool HasTrailTarget => hasTrailTarget;
        public bool IsInvestigating => isInvestigatingTrail;
        public Vector3Int CurrentTrailTarget => currentTrailTarget;
        public bool IsInAlertState => isInAlertState && Time.time < alertStateEndTime;  // ← DEZE PROPERTY ONTBRAK!
        public bool IsBusy => IsInvestigating || HasTrailTarget;


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

        public void TriggerAlertState()
        {
            if (!fovCleanupSettings.useAlertState) return;

            isInAlertState = true;
            alertStateEndTime = Time.time + fovCleanupSettings.alertStateDuration;

            if (fovCleanupSettings.showAlertStateDebug)
            {
                Debug.Log($"🚨 ALERT STATE TRIGGERED! Duration: {fovCleanupSettings.alertStateDuration}s");
            }

            // Immediately scan for ALL trails in FOV
            if (fovCleanupSettings.alertStateAutoCleanFOVTrails)
            {
                DetectAllTrailsInFOV();
            }
        }

        private void DetectAllTrailsInFOV()
        {
            HashSet<Vector3Int> allTrails = trailManager.GetBlockedPositions();
            List<Vector3Int> trailsInFOV = new List<Vector3Int>();

            foreach (Vector3Int trailPos in allTrails)
            {
                Vector3 trailWorldPos = grid.CellToWorld(trailPos);
                trailWorldPos += grid.cellSize * 0.5f;

                if (fovSystem.IsPositionInFieldOfView(trailWorldPos))
                {
                    trailsInFOV.Add(trailPos);

                    // Add to discovered trails if not already there
                    if (!discoveredTrails.Contains(trailPos))
                    {
                        discoveredTrails.Add(trailPos);
                    }

                    // Track alert state detected trails
                    if (!alertStateDetectedTrails.Contains(trailPos))
                    {
                        alertStateDetectedTrails.Add(trailPos);
                    }
                }
            }

            if (fovCleanupSettings.showAlertStateDebug)
            {
                Debug.Log($"🔍 Alert State: Detected {trailsInFOV.Count} trails in FOV to clean");
                foreach (Vector3Int trail in trailsInFOV)
                {
                    Debug.Log($"   - Trail at {trail}");
                }
            }
        }

        public void CheckAlertState()
        {
            if (!fovCleanupSettings.useAlertState) return;

            // Check if alert state has expired
            if (isInAlertState && Time.time >= alertStateEndTime)
            {
                EndAlertState();
                return;
            }

            // In alert state, detect trails but prevent infinite loop
            if (IsInAlertState && fovCleanupSettings.alertStateAutoCleanFOVTrails)
            {
                // Only detect trails every few frames to prevent spam
                if (Time.frameCount % 30 == 0) // Check every 30 frames (~0.5 seconds)
                {
                    DetectAllTrailsInFOV();
                }
            }
        }


        private void EndAlertState()
        {
            if (fovCleanupSettings.showAlertStateDebug)
            {
                Debug.Log($"⏰ Alert State ENDED after {fovCleanupSettings.alertStateDuration}s");
            }

            isInAlertState = false;
            alertStateDetectedTrails.Clear();
        }
        public void CheckForTrails()
        {
            if (!trailSettings.investigateTrails) return;

            // First check alert state
            CheckAlertState();

            ValidateTrailTargets();

            HashSet<Vector3Int> blockedPositions = trailManager.GetBlockedPositions();
            if (blockedPositions.Count == 0) return;

            // ALERT STATE LOGIC: If in alert state, bypass normal FOV requirements
            if (IsInAlertState)
            {
                if (fovCleanupSettings.showAlertStateDebug)
                {
                    Debug.Log("🚨 In Alert State - detecting ALL trails in FOV regardless of player position");
                }

                // In alert state, process all trails that are in FOV
                ProcessAlertStateTrails(blockedPositions);
                return;
            }

            // NORMAL STATE LOGIC: Standard FOV requirement checks
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

            // Process trails normally
            ProcessNormalStateTrails(blockedPositions);
        }

        private void ProcessAlertStateTrails(HashSet<Vector3Int> blockedPositions)
        {
            // Prevent processing if already investigating or moving to a trail
            if (hasTrailTarget || isInvestigatingTrail)
            {
                if (fovCleanupSettings.showAlertStateDebug)
                {
                    Debug.Log("🚨 Alert State: Already busy with trail, skipping detection");
                }
                return;
            }

            List<TrailInfo> availableTrails = new List<TrailInfo>();

            foreach (Vector3Int trailPos in blockedPositions)
            {
                Vector3 trailWorldPos = grid.CellToWorld(trailPos);
                trailWorldPos += grid.cellSize * 0.5f;

                // In alert state, only check if trail is in FOV
                bool trailInFOV = fovSystem.IsPositionInFieldOfView(trailWorldPos);
                if (!trailInFOV) continue;

                float distance = Vector3Int.Distance(movementSystem.CurrentGridPosition, trailPos);

                // Only add trails that are not currently being investigated
                if (!currentlyInvestigating.Contains(trailPos))
                {
                    availableTrails.Add(new TrailInfo { position = trailPos, distance = distance });

                    // Add to discovered trails
                    if (!discoveredTrails.Contains(trailPos))
                    {
                        discoveredTrails.Add(trailPos);
                    }
                }
            }

            if (availableTrails.Count > 0)
            {
                // Always prioritize closest first in alert state
                availableTrails.Sort((a, b) => a.distance.CompareTo(b.distance));

                Vector3Int targetTrail = availableTrails[0].position;

                if (fovCleanupSettings.showAlertStateDebug)
                {
                    Debug.Log($"🚨 Alert State: Targeting trail at {targetTrail} (found {availableTrails.Count} trails in FOV)");
                }

                StartTrailInvestigation(targetTrail);
                OnTrailDetected?.Invoke(targetTrail);
            }
            else if (fovCleanupSettings.showAlertStateDebug)
            {
                Debug.Log("🚨 Alert State: No available trails in FOV or all are being investigated");
            }
        }

        public void ForceEndAlertState()
        {
            if (fovCleanupSettings.showAlertStateDebug)
            {
                Debug.Log("🚨 Force ending alert state");
            }

            isInAlertState = false;
            alertStateEndTime = 0f;
            alertStateDetectedTrails.Clear();

            // Stop any current trail investigation if stuck
            if (isInvestigatingTrail)
            {
                StopInvestigation();
            }
        }


        private void ProcessNormalStateTrails(HashSet<Vector3Int> blockedPositions)
        {
            // This is the existing logic from the original CheckForTrails method
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

            // In alert state, ALWAYS allow cleanup if trail is in FOV
            bool shouldCleanup = false;

            if (IsInAlertState)
            {
                shouldCleanup = IsTrailInFieldOfView();
                if (fovCleanupSettings.showAlertStateDebug)
                {
                    Debug.Log($"🚨 Alert State: Trail cleanup = {shouldCleanup} (trail in FOV check only)");
                }
            }
            else
            {
                shouldCleanup = IsCleanupAllowedByFOV();
            }

            if (shouldCleanup)
            {
                Debug.Log($"🧹 Requirements met. Starting cleanup of trail at {currentTrailTarget}");
                yield return StartCoroutine(CleanupTrailSequence());
            }
            else
            {
                if (IsInAlertState)
                {
                    Debug.Log($"🚫 Alert State: Trail not in FOV. Skipping cleanup of trail at {currentTrailTarget}");
                }
                else
                {
                    Debug.Log($"🚫 FOV requirements not met. Skipping cleanup of trail at {currentTrailTarget}");
                }
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
            // In alert state, cleanup is ALWAYS allowed for trails in FOV
            if (IsInAlertState)
            {
                bool trailInFOV = IsTrailInFieldOfView();
                if (fovCleanupSettings.showAlertStateDebug)
                {
                    Debug.Log($"🚨 Alert State: Cleanup allowed = {trailInFOV} (only checking trail in FOV)");
                }
                return trailInFOV;
            }

            // Normal state logic (existing code)
            if (!fovCleanupSettings.requireTrailInFOV && !fovCleanupSettings.requirePlayerInFOV)
            {
                if (fovCleanupSettings.showFOVCleanupDebug)
                    Debug.Log("🟢 No FOV requirements - cleanup always allowed");
                return true;
            }

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

        public bool HasMoreTrailsToInvestigate()
        {
            // Validate current trails first
            ValidateTrailTargets();

            // Check if there are any discovered trails that haven't been investigated yet
            HashSet<Vector3Int> currentTrails = trailManager.GetBlockedPositions();

            foreach (Vector3Int trail in currentTrails)
            {
                bool alreadyDiscovered = discoveredTrails.Contains(trail);
                bool currentlyInvestigating = this.currentlyInvestigating.Contains(trail);
                bool isCurrentTarget = hasTrailTarget && trail == currentTrailTarget;

                // If it's a new trail or a discovered but not yet investigated trail
                if (!alreadyDiscovered || (!currentlyInvestigating && !isCurrentTarget))
                {
                    return true;
                }
            }

            return false;
        }

        public void MoveToNextTrail()
        {
            // Clear current target
            if (hasTrailTarget)
            {
                currentlyInvestigating.Remove(currentTrailTarget);
            }

            // Find the next available trail
            Vector3Int nextTrail = FindNextTrailToInvestigate();

            if (nextTrail != Vector3Int.zero)
            {
                SetTrailTarget(nextTrail);
                Debug.Log($"🎯 Moving to next trail: {nextTrail}");
            }
            else
            {
                CleanupTrailInvestigation();
                Debug.Log("🚫 No more trails to investigate");
            }
        }

        private Vector3Int FindNextTrailToInvestigate()
        {
            HashSet<Vector3Int> currentTrails = trailManager.GetBlockedPositions();
            Vector3Int currentPosition = movementSystem.CurrentGridPosition;
            Vector3Int closestTrail = Vector3Int.zero;
            float closestDistance = float.MaxValue;

            foreach (Vector3Int trail in currentTrails)
            {
                bool alreadyInvestigating = currentlyInvestigating.Contains(trail);
                bool isCurrentTarget = hasTrailTarget && trail == currentTrailTarget;

                // Skip trails that are already being investigated or are the current target
                if (alreadyInvestigating || isCurrentTarget) continue;

                // Calculate distance to this trail
                float distance = Vector3Int.Distance(currentPosition, trail);

                // Update closest trail
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTrail = trail;
                }
            }

            return closestTrail;
        }

        public void SetTrailTarget(Vector3Int trailPosition)
        {
            // Clear previous target
            if (hasTrailTarget)
            {
                currentlyInvestigating.Remove(currentTrailTarget);
            }

            // Set new target
            currentTrailTarget = trailPosition;
            hasTrailTarget = true;

            // Add to discovered and investigating lists
            if (!discoveredTrails.Contains(trailPosition))
            {
                discoveredTrails.Add(trailPosition);
            }

            if (!currentlyInvestigating.Contains(trailPosition))
            {
                currentlyInvestigating.Add(trailPosition);
            }

            OnTrailDetected?.Invoke(trailPosition);
            Debug.Log($"🎯 New trail target set: {trailPosition}");
        }

    }
}
