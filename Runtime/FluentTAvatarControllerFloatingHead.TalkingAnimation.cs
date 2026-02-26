using System.Collections.Generic;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Talking Animation partial class
    /// Handles body animation swap during speech using the same swap-buffer pattern as Idle.
    /// When no talking animations are assigned, this is a no-op (backward compatible).
    /// </summary>
    public partial class FluentTAvatarControllerFloatingHead
    {
        private const string TALKING_OVERRIDE_0 = "talking_override_0";
        private const string TALKING_OVERRIDE_1 = "talking_override_1";
        private const string IS_TALKING_PARAM = "isTalking";

        // Talking swap state
        private int currentTalkingSlot;
        private int lastPlayedTalkingIndex = -1;
        private bool talkingSwapEnabled;

        // Current talking state
        private bool isTalkingState;

        #region Talking Animation Initialization

        /// <summary>
        /// Initialize talking animations with validation.
        /// Called from Start() after overrideController is created.
        /// </summary>
        private void InitializeTalkingAnimations()
        {
            if (overrideController == null)
                return;

            // No talking animations assigned — backward compatible, no-op
            if (talkingAnimations == null || talkingAnimations.Count == 0)
                return;

            // Remove null entries
            talkingAnimations.RemoveAll(e => e == null || e.clip == null);
            if (talkingAnimations.Count == 0)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] All talking animation entries have null clips. Talking animation disabled.");
                return;
            }

            // Validate weights
            bool allZeroWeight = true;
            for (int i = 0; i < talkingAnimations.Count; i++)
            {
                if (talkingAnimations[i].weight > 0f)
                {
                    allZeroWeight = false;
                    break;
                }
            }
            if (allZeroWeight)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] All talking animation weights are 0. Using equal probability.");
                for (int i = 0; i < talkingAnimations.Count; i++)
                    talkingAnimations[i].weight = 1f;
            }

            // Single clip: preventRepeat warning
            if (talkingAnimations.Count == 1 && talkingAnimations[0].preventRepeat)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Only 1 talking clip with preventRepeat enabled. preventRepeat will be ignored.");
            }

            // Initial override: slot 0
            int firstIndex = SelectNextTalkingClip(-1);
            overrideController[TALKING_OVERRIDE_0] = talkingAnimations[firstIndex].clip;
            lastPlayedTalkingIndex = firstIndex;
            currentTalkingSlot = 0;
            Debug.Log($"[FluentTAvatarControllerFloatingHead] Talking slot 0 initialized with {talkingAnimations[firstIndex].clip.name}");

            if (talkingAnimations.Count > 1)
            {
                // Multi-clip: load a different clip into slot 1, enable swap
                int secondIndex = SelectNextTalkingClip(firstIndex);
                overrideController[TALKING_OVERRIDE_1] = talkingAnimations[secondIndex].clip;
                Debug.Log($"[FluentTAvatarControllerFloatingHead] Talking slot 1 initialized with {talkingAnimations[secondIndex].clip.name}");
                talkingSwapEnabled = true;
            }
            else
            {
                // Single clip: override slot 1 with the same clip so ExitTime transition plays correctly
                overrideController[TALKING_OVERRIDE_1] = talkingAnimations[firstIndex].clip;
                Debug.Log($"[FluentTAvatarControllerFloatingHead] Single talking clip mode: both slots set to {talkingAnimations[firstIndex].clip.name}");
            }
        }

        #endregion

        #region Talking Swap Logic

        /// <summary>
        /// Swap the inactive talking slot's clip.
        /// Called by OnSwapSlotReady when the active slot's entry transition settles.
        /// The other slot is completely dormant at this point — safe to override.
        /// </summary>
        /// <param name="activeSlot">The slot that just became fully active (0 or 1)</param>
        private void SwapInactiveTalkingSlot(int activeSlot)
        {
            if (!talkingSwapEnabled || overrideController == null)
                return;

            // Recompute RMS for remaining audio portion before selecting next clip
            RefreshRemainingAudioRMS();

            currentTalkingSlot = activeSlot;
            int otherSlot = 1 - activeSlot;
            string otherSlotKey = otherSlot == 0 ? TALKING_OVERRIDE_0 : TALKING_OVERRIDE_1;

            int nextIndex = SelectNextTalkingClip(lastPlayedTalkingIndex);
            overrideController[otherSlotKey] = talkingAnimations[nextIndex].clip;
            lastPlayedTalkingIndex = nextIndex;
        }

        #endregion

        #region Talking Clip Selection

        /// <summary>
        /// Select next talking clip using weighted random, respecting preventRepeat.
        /// </summary>
        /// <param name="excludeIndex">Index to exclude if preventRepeat is set. -1 to exclude nothing.</param>
        /// <returns>Selected clip index</returns>
        private int SelectNextTalkingClip(int excludeIndex)
        {
            // Phase 2: energy matching — select clip by audio/motion similarity
            if (enableEnergyMatching && currentAudioRMSCurve != null)
            {
                // Lazy initialization: precompute caches if energy matching was enabled at runtime
                if (motionEnergyCaches == null)
                    InitializeEnergyMatching();

                if (motionEnergyCaches != null && motionEnergyCaches.Count > 1)
                    return SelectBestMatchingClip(currentAudioRMSCurve, excludeIndex);
            }

            // Phase 1 fallback: weighted random selection
            // Build candidate list with weights
            float totalWeight = 0f;
            List<int> candidates = new List<int>(talkingAnimations.Count);

            for (int i = 0; i < talkingAnimations.Count; i++)
            {
                // Skip if preventRepeat and this was the last played clip
                if (i == excludeIndex && talkingAnimations[i].preventRepeat && talkingAnimations.Count > 1)
                    continue;

                if (talkingAnimations[i].weight > 0f)
                {
                    candidates.Add(i);
                    totalWeight += talkingAnimations[i].weight;
                }
            }

            // Fallback: no candidates after filtering (all excluded)
            if (candidates.Count == 0)
            {
                for (int i = 0; i < talkingAnimations.Count; i++)
                {
                    if (talkingAnimations[i].weight > 0f)
                    {
                        candidates.Add(i);
                        totalWeight += talkingAnimations[i].weight;
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
                accumulated += talkingAnimations[candidates[i]].weight;
                if (randomValue <= accumulated)
                    return candidates[i];
            }

            // Fallback (floating point edge case)
            return candidates[candidates.Count - 1];
        }

        #endregion

        #region Talking State Control

        /// <summary>
        /// Set the talking state. Triggers Animator transition between Idle and Talking states.
        /// No-op if talking animations are not assigned.
        /// </summary>
        /// <param name="talking">True when speech starts, false when speech ends.</param>
        public void SetTalking(bool talking)
        {
            // No-op if no talking animations configured
            if (talkingAnimations == null || talkingAnimations.Count == 0)
                return;

            // Skip if already in the requested state
            if (isTalkingState == talking)
                return;

            isTalkingState = talking;

            if (animator != null)
            {
                animator.SetBool(IS_TALKING_PARAM, talking);
                Debug.Log($"[FluentTAvatarControllerFloatingHead] SetTalking({talking})");
            }
        }

        #endregion
    }
}
