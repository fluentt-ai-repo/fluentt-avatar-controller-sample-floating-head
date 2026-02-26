using System.Collections.Generic;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Idle Animation partial class
    /// Handles multi-idle animation with weighted random selection and swap-buffer pattern
    /// </summary>
    public partial class FluentTAvatarControllerFloatingHead
    {
        private const string IDLE_OVERRIDE_0 = "idle_override_0";
        private const string IDLE_OVERRIDE_1 = "idle_override_1";

        // Idle swap state
        private int currentIdleSlot;
        private int lastPlayedIdleIndex = -1;
        private bool wasInIdleTransition;
        private bool idleSwapEnabled;

        #region Idle Animation Initialization

        /// <summary>
        /// Initialize idle animations with legacy migration and validation.
        /// Called from Start() after overrideController is created.
        /// </summary>
        private void InitializeIdleAnimations()
        {
            if (overrideController == null)
                return;

            // Legacy migration: move defaultIdleAnimationClip to idleAnimations list
            if (defaultIdleAnimationClip != null && idleAnimations.Count == 0)
            {
                idleAnimations.Add(new IdleAnimationEntry
                {
                    clip = defaultIdleAnimationClip,
                    weight = 1f,
                    preventRepeat = false
                });
                Debug.Log("[FluentTAvatarControllerFloatingHead] Migrated legacy defaultIdleAnimationClip to idleAnimations list");
            }

            // Fallback: no clips at all
            if (idleAnimations.Count == 0)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] No idle animations assigned. Using default override clip.");
                return;
            }

            // Remove null entries
            idleAnimations.RemoveAll(e => e == null || e.clip == null);
            if (idleAnimations.Count == 0)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] All idle animation entries have null clips. Using default override clip.");
                return;
            }

            // Validate weights
            bool allZeroWeight = true;
            for (int i = 0; i < idleAnimations.Count; i++)
            {
                if (idleAnimations[i].weight > 0f)
                {
                    allZeroWeight = false;
                    break;
                }
            }
            if (allZeroWeight)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] All idle animation weights are 0. Using equal probability.");
                for (int i = 0; i < idleAnimations.Count; i++)
                    idleAnimations[i].weight = 1f;
            }

            // Single clip: preventRepeat warning
            if (idleAnimations.Count == 1 && idleAnimations[0].preventRepeat)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Only 1 idle clip with preventRepeat enabled. preventRepeat will be ignored.");
            }

            // Initial override: slot 0
            int firstIndex = SelectNextIdleClip(-1);
            overrideController[IDLE_OVERRIDE_0] = idleAnimations[firstIndex].clip;
            lastPlayedIdleIndex = firstIndex;
            currentIdleSlot = 0;
            Debug.Log($"[FluentTAvatarControllerFloatingHead] Idle slot 0 initialized with {idleAnimations[firstIndex].clip.name}");

            if (idleAnimations.Count > 1)
            {
                // Multi-clip: load a different clip into slot 1, enable swap
                int secondIndex = SelectNextIdleClip(firstIndex);
                overrideController[IDLE_OVERRIDE_1] = idleAnimations[secondIndex].clip;
                Debug.Log($"[FluentTAvatarControllerFloatingHead] Idle slot 1 initialized with {idleAnimations[secondIndex].clip.name}");
                idleSwapEnabled = true;
            }
            else
            {
                // Single clip: override slot 1 with the same clip so ExitTime transition plays correctly
                overrideController[IDLE_OVERRIDE_1] = idleAnimations[firstIndex].clip;
                Debug.Log($"[FluentTAvatarControllerFloatingHead] Single idle clip mode: both slots set to {idleAnimations[firstIndex].clip.name}");
            }
        }

        #endregion

        #region Idle Swap Logic

        /// <summary>
        /// Check if idle transition completed and swap the inactive slot's clip.
        /// Called from Update().
        /// </summary>
        private void CheckIdleSwap()
        {
            if (!idleSwapEnabled || isTalkingState || animator == null)
                return;

            bool isInTransition = animator.IsInTransition(0);

            // Detect transition completion: was transitioning, now not
            if (wasInIdleTransition && !isInTransition)
            {
                // Transition just completed — the previously inactive slot is now active
                currentIdleSlot = 1 - currentIdleSlot;

                // Override the now-inactive slot with the next clip
                string inactiveSlotKey = currentIdleSlot == 0 ? IDLE_OVERRIDE_1 : IDLE_OVERRIDE_0;
                int nextIndex = SelectNextIdleClip(lastPlayedIdleIndex);
                overrideController[inactiveSlotKey] = idleAnimations[nextIndex].clip;
                lastPlayedIdleIndex = nextIndex;
            }

            wasInIdleTransition = isInTransition;
        }

        #endregion

        #region Idle Clip Selection

        /// <summary>
        /// Select next idle clip using weighted random, respecting preventRepeat.
        /// </summary>
        /// <param name="excludeIndex">Index to exclude if preventRepeat is set. -1 to exclude nothing.</param>
        /// <returns>Selected clip index</returns>
        private int SelectNextIdleClip(int excludeIndex)
        {
            // Build candidate list with weights
            float totalWeight = 0f;
            List<int> candidates = new List<int>(idleAnimations.Count);

            for (int i = 0; i < idleAnimations.Count; i++)
            {
                // Skip if preventRepeat and this was the last played clip
                if (i == excludeIndex && idleAnimations[i].preventRepeat && idleAnimations.Count > 1)
                    continue;

                if (idleAnimations[i].weight > 0f)
                {
                    candidates.Add(i);
                    totalWeight += idleAnimations[i].weight;
                }
            }

            // Fallback: no candidates after filtering (all excluded)
            if (candidates.Count == 0)
            {
                // Re-add all clips including excluded one
                for (int i = 0; i < idleAnimations.Count; i++)
                {
                    if (idleAnimations[i].weight > 0f)
                    {
                        candidates.Add(i);
                        totalWeight += idleAnimations[i].weight;
                    }
                }
            }

            // Should not happen but safety fallback
            if (candidates.Count == 0)
                return 0;

            // Single candidate
            if (candidates.Count == 1)
                return candidates[0];

            // Weighted random selection
            float randomValue = Random.Range(0f, totalWeight);
            float accumulated = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                accumulated += idleAnimations[candidates[i]].weight;
                if (randomValue <= accumulated)
                    return candidates[i];
            }

            // Fallback (floating point edge case)
            return candidates[candidates.Count - 1];
        }

        #endregion
    }
}
