using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

namespace GameProjectFM.AI.Behaviors
{
    using Core;
    using Systems;

    public class NavMeshTrailInvestigationBehavior : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private TrailSettings trailSettings;
        [SerializeField] private FOVCleanupSettings fovCleanupSettings;
        [SerializeField] private NavMeshMovementSystem navMovementSystem;
        [SerializeField] private FOVSystem fovSystem;

        // Trail investigation state
        private Vector3 currentTrailTarget;
        private bool hasTrailTarget;
        private bool isInvestigatingTrail;
        private Coroutine investigationCoroutine;

        // Trail tracking - using world positions instead of grid
        private List<Vector3> discoveredTrails = new List<Vector3>();
        private List<Vector3> currentlyInvestigating = new List<Vector3>();

        // External dependencies
        private PlayerTrailManager trailManager;
        private Transform player;

        // Events
        public System.Action<Vector3> OnTrailDetected;
        public System.Action<Vector3> OnTrailInvestigationStarted;
        public System.Action<Vector3> OnTrailCleaned;
        public System.Action OnTrailInvestigationCompleted;

        // Alert State System
        private bool isInAlertState = false;
        private float alertStateEndTime = 0f;
        private List<Vector3> alertStateDetectedTrails = new List<Vector3>();

        // Public Properties
        public bool HasTrailTarget => hasTrailTarget;
        public bool IsInvestigating => isInvestigatingTrail;
        public Vector3 CurrentTrailTarget => currentTrailTarget;
        public bool IsInAlertState => isInAlertState && Time.time < alertStateEndTime;
        public bool IsBusy => IsInvestigating || HasTrailTarget;

        public void Initialize(TrailSettings trail, FOVCleanupSettings fovCleanup, NavMeshMovementSystem movement, FOVSystem fov, PlayerTrailManager trails, Transform playerTransform)
        {
            trailSettings = trail;
            fovCleanupSettings = fovCleanup;
            navMovementSystem = movement;
            fovSystem = fov;
            trailManager = trails;
            player = playerTransform;

            Debug.Log("✅ NavMeshTrailInvestigationBehavior initialized");
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
            List<Vector3> trailsInFOV = new List<Vector3>();

            foreach (Vector3Int trailGridPos in allTrails)
            {
                Vector3 trailWorldPos = GridToWorldPosition(trailGridPos);

                if (fovSystem.IsPositionInFieldOfView(trailWorldPos))
                {
                    trailsInFOV.Add(trailWorldPos);

                    // Add to discovered trails if not already there
                    if (!IsTrailDiscovered(trailWorldPos))
                    {
                        discoveredTrails.Add(trailWorldPos);
                    }

                    // Track alert state detected trails
                    if (!alertStateDetectedTrails.Contains(trailWorldPos))
                    {
                        alertStateDetectedTrails.Add(trailWorldPos);
                    }
                }
            }

            if (fovCleanupSettings.showAlertStateDebug)
            {
                Debug.Log($"🔍 Alert State: Detected {trailsInFOV.Count} trails in FOV to clean");
                foreach (Vector3 trail in trailsInFOV)
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

            foreach (Vector3Int trailGridPos in blockedPositions)
            {
                Vector3 trailWorldPos = GridToWorldPosition(trailGridPos);

                // In alert state, only check if trail is in FOV
                bool trailInFOV = fovSystem.IsPositionInFieldOfView(trailWorldPos);
                if (!trailInFOV) continue;

                float distance = Vector3.Distance(navMovementSystem.CurrentPosition, trailWorldPos);

                // Only add trails that are not currently being investigated
                if (!IsTrailBeingInvestigated(trailWorldPos))
                {
                    availableTrails.Add(new TrailInfo { worldPosition = trailWorldPos, distance = distance });

                    // Add to discovered trails
                    if (!IsTrailDiscovered(trailWorldPos))
                    {
                        discoveredTrails.Add(trailWorldPos);
                    }
                }
            }

            if (availableTrails.Count > 0)
            {
                // Always prioritize closest first in alert state
                availableTrails.Sort((a, b) => a.distance.CompareTo(b.distance));

                Vector3 targetTrail = availableTrails[0].worldPosition;

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
            List<TrailInfo> availableTrails = new List<TrailInfo>();

            foreach (Vector3Int trailGridPos in blockedPositions)
            {
                Vector3 trailWorldPos = GridToWorldPosition(trailGridPos);

                bool trailInFOV = fovSystem.IsPositionInFieldOfView(trailWorldPos);
                if (!trailInFOV) continue;

                float distance = Vector3.Distance(navMovementSystem.CurrentPosition, trailWorldPos);
                bool isDiscovered = IsTrailDiscovered(trailWorldPos);
                bool canReconsiderTrail = (fovCleanupSettings.requireTrailInFOV || fovCleanupSettings.requirePlayerInFOV) && isDiscovered && IsCleanupAllowedByFOV();

                if ((!isDiscovered || canReconsiderTrail) && !IsTrailBeingInvestigated(trailWorldPos))
                {
                    availableTrails.Add(new TrailInfo { worldPosition = trailWorldPos, distance = distance });

                    if (canReconsiderTrail)
                    {
                        Debug.Log($"🔄 Reconsidering trail at {trailWorldPos} because FOV requirements are now met");
                        RemoveDiscoveredTrail(trailWorldPos);
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

                Vector3 targetTrail = availableTrails[0].worldPosition;
                Debug.Log($"🕵️ EnemyAI detected {availableTrails.Count} trails within FOV. Prioritizing closest at {targetTrail} (distance: {availableTrails[0].distance:F1})");

                StartTrailInvestigation(targetTrail);
                OnTrailDetected?.Invoke(targetTrail);
            }
        }

        public void StartTrailInvestigation(Vector3 trailPosition)
        {
            if (hasTrailTarget)
            {
                Debug.Log($"Already investigating trail at {currentTrailTarget}, ignoring new trail at {trailPosition}");
                return;
            }

            if (!IsTrailDiscovered(trailPosition))
            {
                discoveredTrails.Add(trailPosition);
            }

            if (!IsTrailBeingInvestigated(trailPosition))
            {
                currentlyInvestigating.Add(trailPosition);
            }

            currentTrailTarget = trailPosition;
            hasTrailTarget = true;

            Debug.Log($"🔍 Starting trail investigation towards {trailPosition}. Current position: {navMovementSystem.CurrentPosition}");
            OnTrailInvestigationStarted?.Invoke(trailPosition);
        }

        public bool IsAtTrailTarget()
        {
            return hasTrailTarget && navMovementSystem.IsNearPosition(currentTrailTarget, 1f);
        }

        public Vector3 GetTrailWorldPosition()
        {
            return currentTrailTarget;
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

            Vector3Int gridPos = WorldToGridPosition(currentTrailTarget);
            trailManager.RemoveTrailAt(gridPos);
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

            // Normal state logic
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
            return fovSystem.IsPositionInFieldOfView(currentTrailTarget);
        }

        private void ValidateTrailTargets()
        {
            HashSet<Vector3Int> currentTrails = trailManager.GetBlockedPositions();
            List<Vector3> currentTrailsWorldPos = new List<Vector3>();

            foreach (Vector3Int gridPos in currentTrails)
            {
                currentTrailsWorldPos.Add(GridToWorldPosition(gridPos));
            }

            // Remove discovered trails that no longer exist
            discoveredTrails.RemoveAll(trail => !IsTrailStillExists(trail, currentTrailsWorldPos));
            currentlyInvestigating.RemoveAll(trail => !IsTrailStillExists(trail, currentTrailsWorldPos));

            if (hasTrailTarget && !IsTrailStillExists(currentTrailTarget, currentTrailsWorldPos))
            {
                Debug.Log($"Current trail target {currentTrailTarget} no longer exists. Clearing target.");
                CleanupTrailInvestigation();
            }
        }

        private bool IsTrailStillExists(Vector3 trailWorldPos, List<Vector3> currentTrails)
        {
            foreach (Vector3 currentTrail in currentTrails)
            {
                if (Vector3.Distance(trailWorldPos, currentTrail) < 0.5f) // Tolerance for floating point comparison
                {
                    return true;
                }
            }
            return false;
        }

        private void CleanupTrailInvestigation()
        {
            if (hasTrailTarget)
            {
                RemoveInvestigatingTrail(currentTrailTarget);
            }

            currentTrailTarget = Vector3.zero;
            hasTrailTarget = false;
            isInvestigatingTrail = false;

            if (investigationCoroutine != null)
            {
                StopCoroutine(investigationCoroutine);
                investigationCoroutine = null;
            }
        }

        public Vector3 GetDirectionToTarget()
        {
            if (!hasTrailTarget) return Vector3.zero;
            return (currentTrailTarget - navMovementSystem.CurrentPosition).normalized;
        }

        public bool HasMoreTrailsToInvestigate()
        {
            ValidateTrailTargets();

            HashSet<Vector3Int> currentTrails = trailManager.GetBlockedPositions();

            foreach (Vector3Int gridPos in currentTrails)
            {
                Vector3 trailWorldPos = GridToWorldPosition(gridPos);
                bool alreadyDiscovered = IsTrailDiscovered(trailWorldPos);
                bool currentlyInvestigating = IsTrailBeingInvestigated(trailWorldPos);
                bool isCurrentTarget = hasTrailTarget && Vector3.Distance(trailWorldPos, currentTrailTarget) < 0.5f;

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
                RemoveInvestigatingTrail(currentTrailTarget);
            }

            // Find the next available trail
            Vector3 nextTrail = FindNextTrailToInvestigate();

            if (nextTrail != Vector3.zero)
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

        private Vector3 FindNextTrailToInvestigate()
        {
            HashSet<Vector3Int> currentTrails = trailManager.GetBlockedPositions();
            Vector3 currentPosition = navMovementSystem.CurrentPosition;
            Vector3 closestTrail = Vector3.zero;
            float closestDistance = float.MaxValue;

            foreach (Vector3Int gridPos in currentTrails)
            {
                Vector3 trailWorldPos = GridToWorldPosition(gridPos);
                bool alreadyInvestigating = IsTrailBeingInvestigated(trailWorldPos);
                bool isCurrentTarget = hasTrailTarget && Vector3.Distance(trailWorldPos, currentTrailTarget) < 0.5f;

                if (alreadyInvestigating || isCurrentTarget) continue;

                float distance = Vector3.Distance(currentPosition, trailWorldPos);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTrail = trailWorldPos;
                }
            }

            return closestTrail;
        }

        public void SetTrailTarget(Vector3 trailPosition)
        {
            // Clear previous target
            if (hasTrailTarget)
            {
                RemoveInvestigatingTrail(currentTrailTarget);
            }

            // Set new target
            currentTrailTarget = trailPosition;
            hasTrailTarget = true;

            // Add to discovered and investigating lists
            if (!IsTrailDiscovered(trailPosition))
            {
                discoveredTrails.Add(trailPosition);
            }

            if (!IsTrailBeingInvestigated(trailPosition))
            {
                currentlyInvestigating.Add(trailPosition);
            }

            OnTrailDetected?.Invoke(trailPosition);
            Debug.Log($"🎯 New trail target set: {trailPosition}");
        }

        // Helper methods for trail tracking
        private bool IsTrailDiscovered(Vector3 trailWorldPos)
        {
            foreach (Vector3 discovered in discoveredTrails)
            {
                if (Vector3.Distance(discovered, trailWorldPos) < 0.5f)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsTrailBeingInvestigated(Vector3 trailWorldPos)
        {
            foreach (Vector3 investigating in currentlyInvestigating)
            {
                if (Vector3.Distance(investigating, trailWorldPos) < 0.5f)
                {
                    return true;
                }
            }
            return false;
        }

        private void RemoveDiscoveredTrail(Vector3 trailWorldPos)
        {
            for (int i = discoveredTrails.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(discoveredTrails[i], trailWorldPos) < 0.5f)
                {
                    discoveredTrails.RemoveAt(i);
                    break;
                }
            }
        }

        private void RemoveInvestigatingTrail(Vector3 trailWorldPos)
        {
            for (int i = currentlyInvestigating.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(currentlyInvestigating[i], trailWorldPos) < 0.5f)
                {
                    currentlyInvestigating.RemoveAt(i);
                    break;
                }
            }
        }

        // Grid conversion helpers - you may need to adjust these based on your grid setup
        private Vector3 GridToWorldPosition(Vector3Int gridPos)
        {
            // Assuming 1 unit grid size - adjust based on your grid setup
            return new Vector3(gridPos.x, gridPos.y, gridPos.z);
        }

        private Vector3Int WorldToGridPosition(Vector3 worldPos)
        {
            // Assuming 1 unit grid size - adjust based on your grid setup
            return new Vector3Int(
                Mathf.RoundToInt(worldPos.x),
                Mathf.RoundToInt(worldPos.y),
                Mathf.RoundToInt(worldPos.z)
            );
        }

        // Visualization
        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            // Draw current trail target
            if (hasTrailTarget)
            {
                Gizmos.color = trailSettings.trailTargetColor;
                Gizmos.DrawWireSphere(currentTrailTarget, 0.5f);
                Gizmos.DrawSphere(currentTrailTarget, 0.2f);

                // Draw path to target
                if (navMovementSystem != null)
                {
                    Gizmos.color = Color.orange;
                    Gizmos.DrawLine(navMovementSystem.CurrentPosition, currentTrailTarget);
                }

#if UNITY_EDITOR
                UnityEditor.Handles.Label(currentTrailTarget + Vector3.up, "TARGET TRAIL");
#endif
            }

            // Draw discovered trails
            Gizmos.color = Color.yellow;
            foreach (Vector3 trail in discoveredTrails)
            {
                Gizmos.DrawWireCube(trail, Vector3.one * 0.3f);
            }

            // Draw currently investigating trails
            Gizmos.color = Color.red;
            foreach (Vector3 trail in currentlyInvestigating)
            {
                Gizmos.DrawWireCube(trail, Vector3.one * 0.4f);
            }

            // Draw alert state indicator
            if (IsInAlertState)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, 2f);

#if UNITY_EDITOR
                float remainingTime = alertStateEndTime - Time.time;
                UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, $"ALERT: {remainingTime:F1}s");
#endif
            }
        }
    }

    // Helper class for trail sorting
    [System.Serializable]
    public class TrailInfo
    {
        public Vector3 worldPosition;
        public float distance;
    }
}
