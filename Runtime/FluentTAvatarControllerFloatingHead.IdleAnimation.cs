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
        private bool idleSwapEnabled;

        // Track which clip index is loaded in each slot (for eye control override)
        private int[] idleSlotClipIndex = new int[2] { -1, -1 };

        // Idle eye control override state — now uses suppression flags in main class

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
            idleSlotClipIndex[0] = firstIndex;
            lastPlayedIdleIndex = firstIndex;
            currentIdleSlot = 0;
            Debug.Log($"[FluentTAvatarControllerFloatingHead] Idle slot 0 initialized with {idleAnimations[firstIndex].clip.name}");

            // Apply eye control override for initial clip
            ApplyIdleOverrides(firstIndex);

            if (idleAnimations.Count > 1)
            {
                // Multi-clip: load a different clip into slot 1, enable swap
                int secondIndex = SelectNextIdleClip(firstIndex);
                overrideController[IDLE_OVERRIDE_1] = idleAnimations[secondIndex].clip;
                idleSlotClipIndex[1] = secondIndex;
                Debug.Log($"[FluentTAvatarControllerFloatingHead] Idle slot 1 initialized with {idleAnimations[secondIndex].clip.name}");
                idleSwapEnabled = true;
            }
            else
            {
                // Single clip: override slot 1 with the same clip so ExitTime transition plays correctly
                overrideController[IDLE_OVERRIDE_1] = idleAnimations[firstIndex].clip;
                idleSlotClipIndex[1] = firstIndex;
                Debug.Log($"[FluentTAvatarControllerFloatingHead] Single idle clip mode: both slots set to {idleAnimations[firstIndex].clip.name}");
            }
        }

        #endregion

        #region Idle Swap Logic

        /// <summary>
        /// Swap the inactive idle slot's clip.
        /// Called by OnSwapSlotReady when the active slot's entry transition settles.
        /// The other slot is completely dormant at this point — safe to override.
        /// </summary>
        /// <param name="activeSlot">The slot that just became fully active (0 or 1)</param>
        private void SwapInactiveIdleSlot(int activeSlot)
        {
            if (!idleSwapEnabled || overrideController == null)
                return;

            currentIdleSlot = activeSlot;

            // Apply eye control for the now-active clip
            int activeClipIndex = idleSlotClipIndex[activeSlot];
            if (activeClipIndex >= 0 && activeClipIndex < idleAnimations.Count)
            {
                ApplyIdleOverrides(activeClipIndex);
            }

            // Preload next clip into the inactive slot
            int otherSlot = 1 - activeSlot;
            string otherSlotKey = otherSlot == 0 ? IDLE_OVERRIDE_0 : IDLE_OVERRIDE_1;

            if (isTalkMotionIdleActive)
            {
                // During TalkMotion: keep TalkMotion idle in inactive slot (skip if already set)
                if (overrideController[otherSlotKey] != talkMotionIdleClip)
                {
                    overrideController[otherSlotKey] = talkMotionIdleClip;
                }
                idleSlotClipIndex[otherSlot] = -1;
            }
            else
            {
                int nextIndex = SelectNextIdleClip(lastPlayedIdleIndex);
                overrideController[otherSlotKey] = idleAnimations[nextIndex].clip;
                idleSlotClipIndex[otherSlot] = nextIndex;
                lastPlayedIdleIndex = nextIndex;
            }
        }

        #endregion

        #region Idle Eye Control

        private void ApplyIdleOverrides(int idleClipIndex)
        {
            var entry = idleAnimations[idleClipIndex];

            // Set idle override per clip
            _eyeControlOverrideByIdle = entry.overrideEyeControl;
            _eyeBlinkOverrideByIdle = entry.overrideEyeBlink;
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

        #region TalkMotion Idle Swap

        private const string IDLE_SWAP_TRIGGER = "idleSwap";
        private bool isTalkMotionIdleActive;

        /// <summary>
        /// Called by OnSentenceStarted. Loads talkMotionIdleClip into the inactive slot
        /// and fires idleSwap trigger for Animator transition.
        /// </summary>
        private Coroutine pendingIdleSwapCoroutine;

        private void OnSentenceStarted_IdleAnimation()
        {
            if (talkMotionIdleClip == null || overrideController == null || animator == null)
                return;

            if (isTalkMotionIdleActive)
                return;

            isTalkMotionIdleActive = true;

            // TalkMotion idle has no overrides — clear idle override
            _eyeControlOverrideByIdle = false;
            _eyeBlinkOverrideByIdle = false;

            ExecuteIdleSwap(talkMotionIdleClip, -1);
        }

        /// <summary>
        /// Called by OnSentenceEnded (isLastSentence). Loads a normal idle clip
        /// into the inactive slot and fires idleSwap trigger.
        /// </summary>
        private void OnSentenceEnded_IdleAnimation()
        {
            if (!isTalkMotionIdleActive || overrideController == null || animator == null)
                return;

            isTalkMotionIdleActive = false;

            int nextIndex = SelectNextIdleClip(lastPlayedIdleIndex);
            lastPlayedIdleIndex = nextIndex;

            ExecuteIdleSwap(idleAnimations[nextIndex].clip, nextIndex);
        }

        /// <summary>
        /// Wait for any in-progress transition to finish, then load clip into
        /// the inactive slot and fire idleSwap trigger.
        /// </summary>
        private void ExecuteIdleSwap(AnimationClip clip, int clipIndex)
        {
            if (pendingIdleSwapCoroutine != null)
            {
                StopCoroutine(pendingIdleSwapCoroutine);
            }
            pendingIdleSwapCoroutine = StartCoroutine(ExecuteIdleSwapCoroutine(clip, clipIndex));
        }

        private System.Collections.IEnumerator ExecuteIdleSwapCoroutine(AnimationClip clip, int clipIndex)
        {
            // Wait until any ongoing transition completes — both slots are in use during transition
            while (animator.IsInTransition(0))
            {
                yield return null;
            }

            // Now exactly one slot is active, the other is safe to modify
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            int activeSlot;
            if (stateInfo.IsName("Idle 0"))
                activeSlot = 0;
            else if (stateInfo.IsName("Idle 1"))
                activeSlot = 1;
            else
                activeSlot = currentIdleSlot;

            currentIdleSlot = activeSlot;

            int inactiveSlot = 1 - activeSlot;
            string inactiveSlotKey = inactiveSlot == 0 ? IDLE_OVERRIDE_0 : IDLE_OVERRIDE_1;

            overrideController[inactiveSlotKey] = clip;
            idleSlotClipIndex[inactiveSlot] = clipIndex;

            animator.SetTrigger(IDLE_SWAP_TRIGGER);
            pendingIdleSwapCoroutine = null;

            Debug.Log($"[FluentTAvatarControllerFloatingHead] IdleSwap: loaded '{clip.name}' into slot {inactiveSlot}, trigger fired");
        }

        #endregion
    }
}
