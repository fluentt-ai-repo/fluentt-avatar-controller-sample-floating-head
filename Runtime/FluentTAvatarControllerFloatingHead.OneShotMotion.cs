using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// One-Shot Motion partial class.
    /// Provides on-demand single-play animation triggered by external systems (e.g. Flutter bridge).
    /// Reuses the existing gesture override slots so no Animator Controller modification is needed.
    /// </summary>
    public partial class FluentTAvatarControllerFloatingHead
    {
        private bool isOneShotMotionPlaying;
        private string currentOneShotMotionId;
        private Coroutine oneShotCoroutine;
        private bool isTalkMotionActive;

        // Group loop state
        private bool isOneShotGroupLooping;
        private string currentGroupId;
        private int lastGroupEntryIndex = -1;

        // Eye control override state
        private bool isEyeControlSuspendedByOneShot;
        private bool eyeControlValueBeforeOneShot;

        #region Public API

        /// <summary>
        /// Play a registered one-shot motion by its motionId.
        /// The motion plays once and automatically returns to idle upon completion.
        /// </summary>
        /// <param name="motionId">The unique identifier of the registered motion</param>
        /// <returns>true if the motion was found and playback started, false otherwise</returns>
        public bool PlayOneShotMotion(string motionId)
        {
            if (animator == null || overrideController == null)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Animator or override controller not initialized. Cannot play one-shot motion.");
                return false;
            }

            // TalkMotion has priority - reject one-shot during speech
            if (isTalkMotionActive)
            {
                Debug.LogWarning($"[FluentTAvatarControllerFloatingHead] TalkMotion is currently playing. One-shot motion '{motionId}' ignored.");
                return false;
            }

            var entry = oneShotMotions.FirstOrDefault(m => m.motionId == motionId);
            if (entry == null || entry.clip == null)
            {
                Debug.LogWarning($"[FluentTAvatarControllerFloatingHead] One-shot motion '{motionId}' not found or clip is null.");
                return false;
            }

            // Stop any currently playing one-shot
            if (isOneShotMotionPlaying)
            {
                StopOneShotMotionInternal();
            }

            // Play using the gesture override slot (Option A: reuse gesture slots)
            string overrideClipName = currentEmotionSlot == 0 ? GESTURE_OVERRIDE_0 : GESTURE_OVERRIDE_1;
            string triggerName = currentEmotionSlot == 0 ? "emotion0" : "emotion1";

            overrideController[overrideClipName] = entry.clip;
            animator.ResetTrigger("emotionReset");
            animator.SetTrigger(triggerName);

            currentEmotionSlot = 1 - currentEmotionSlot;

            isOneShotMotionPlaying = true;
            currentOneShotMotionId = motionId;

            // Suspend eye control / eye blink if this motion overrides them
            if (entry.overrideEyeControl)
                SuspendEyeControl();
            if (entry.overrideEyeBlink)
                SuspendEyeBlink();

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Playing one-shot motion: {motionId} ({entry.clip.name})");

            onOneShotMotionStarted?.Invoke(motionId);

            // Start coroutine to wait for clip completion and return to idle
            oneShotCoroutine = StartCoroutine(WaitForOneShotCompletion(entry.clip.length, motionId));

            return true;
        }

        /// <summary>
        /// Get the list of registered one-shot motion IDs.
        /// </summary>
        public List<string> GetOneShotMotionIds()
        {
            return oneShotMotions
                .Where(m => !string.IsNullOrEmpty(m.motionId))
                .Select(m => m.motionId)
                .ToList();
        }

        /// <summary>
        /// Check if a one-shot motion is currently playing.
        /// </summary>
        public bool IsOneShotMotionPlaying()
        {
            return isOneShotMotionPlaying;
        }

        /// <summary>
        /// Stop the currently playing one-shot motion immediately and return to idle.
        /// Also stops group looping if active.
        /// </summary>
        public void StopOneShotMotion()
        {
            isOneShotGroupLooping = false;
            currentGroupId = null;
            lastGroupEntryIndex = -1;

            if (!isOneShotMotionPlaying)
                return;

            StopOneShotMotionInternal();
            ResetEmotionState();
        }

        /// <summary>
        /// Play a registered one-shot motion group by its groupId.
        /// Randomly selects clips from the group (weighted) and loops until stopped or TalkMotion starts.
        /// </summary>
        /// <param name="groupId">The unique identifier of the registered group</param>
        /// <returns>true if the group was found and playback started, false otherwise</returns>
        public bool PlayOneShotMotionGroup(string groupId)
        {
            if (animator == null || overrideController == null)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Animator or override controller not initialized. Cannot play one-shot motion group.");
                return false;
            }

            if (isTalkMotionActive)
            {
                Debug.LogWarning($"[FluentTAvatarControllerFloatingHead] TalkMotion is currently playing. One-shot group '{groupId}' ignored.");
                return false;
            }

            var group = oneShotMotionGroups.FirstOrDefault(g => g.groupId == groupId);
            if (group == null || group.entries == null || group.entries.Count == 0)
            {
                Debug.LogWarning($"[FluentTAvatarControllerFloatingHead] One-shot motion group '{groupId}' not found or has no entries.");
                return false;
            }

            // Stop any current playback
            if (isOneShotMotionPlaying)
            {
                StopOneShotMotionInternal();
            }

            isOneShotGroupLooping = true;
            currentGroupId = groupId;
            lastGroupEntryIndex = -1;

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Starting one-shot group loop: {groupId}");
            PlayNextGroupEntry(group);
            return true;
        }

        /// <summary>
        /// Get the list of registered one-shot motion group IDs.
        /// </summary>
        public List<string> GetOneShotMotionGroupIds()
        {
            return oneShotMotionGroups
                .Where(g => !string.IsNullOrEmpty(g.groupId))
                .Select(g => g.groupId)
                .ToList();
        }

        /// <summary>
        /// Check if a one-shot motion group is currently looping.
        /// </summary>
        public bool IsOneShotGroupLooping()
        {
            return isOneShotGroupLooping;
        }

        #endregion

        #region Internal

        private void SuspendEyeControl()
        {
            if (!isEyeControlSuspendedByOneShot)
            {
                eyeControlValueBeforeOneShot = enableEyeControl;
                isEyeControlSuspendedByOneShot = true;
            }
            enableEyeControl = false;
        }

        private void RestoreEyeControlIfSuspended()
        {
            if (isEyeControlSuspendedByOneShot)
            {
                enableEyeControl = eyeControlValueBeforeOneShot;
                isEyeControlSuspendedByOneShot = false;
            }
        }

        private void StopOneShotMotionInternal()
        {
            if (oneShotCoroutine != null)
            {
                StopCoroutine(oneShotCoroutine);
                oneShotCoroutine = null;
            }

            RestoreEyeControlIfSuspended();
            RestoreEyeBlinkIfSuspended();

            string endedMotionId = currentOneShotMotionId;
            isOneShotMotionPlaying = false;
            currentOneShotMotionId = null;

            if (!string.IsNullOrEmpty(endedMotionId))
            {
                onOneShotMotionEnded?.Invoke(endedMotionId);
            }
        }

        private IEnumerator WaitForOneShotCompletion(float clipLength, string motionId)
        {
            yield return new WaitForSeconds(clipLength);

            // Only proceed if this is still the active one-shot (not replaced by another)
            if (!isOneShotMotionPlaying || currentOneShotMotionId != motionId)
                yield break;

            oneShotCoroutine = null;

            // If group looping, play next random entry instead of returning to idle
            if (isOneShotGroupLooping && !string.IsNullOrEmpty(currentGroupId))
            {
                isOneShotMotionPlaying = false;
                currentOneShotMotionId = null;
                onOneShotMotionEnded?.Invoke(motionId);

                var group = oneShotMotionGroups.FirstOrDefault(g => g.groupId == currentGroupId);
                if (group != null && group.entries.Count > 0)
                {
                    PlayNextGroupEntry(group);
                }
                else
                {
                    // Group became invalid, stop
                    RestoreEyeControlIfSuspended();
                    isOneShotGroupLooping = false;
                    currentGroupId = null;
                    ResetEmotionState();
                }
                yield break;
            }

            // Single play: return to idle
            RestoreEyeControlIfSuspended();
            ResetEmotionState();
            isOneShotMotionPlaying = false;
            currentOneShotMotionId = null;
            onOneShotMotionEnded?.Invoke(motionId);
            Debug.Log($"[FluentTAvatarControllerFloatingHead] One-shot motion '{motionId}' completed, returning to idle.");
        }

        private void PlayNextGroupEntry(OneShotMotionGroup group)
        {
            int index = SelectWeightedGroupEntry(group, lastGroupEntryIndex);
            lastGroupEntryIndex = index;
            var entry = group.entries[index];

            // Play using the gesture override slot
            string overrideClipName = currentEmotionSlot == 0 ? GESTURE_OVERRIDE_0 : GESTURE_OVERRIDE_1;
            string triggerName = currentEmotionSlot == 0 ? "emotion0" : "emotion1";

            overrideController[overrideClipName] = entry.clip;
            animator.ResetTrigger("emotionReset");
            animator.SetTrigger(triggerName);
            currentEmotionSlot = 1 - currentEmotionSlot;

            // Handle eye control / eye blink override per group entry
            if (entry.overrideEyeControl)
                SuspendEyeControl();
            else
                RestoreEyeControlIfSuspended();

            if (entry.overrideEyeBlink)
                SuspendEyeBlink();
            else
                RestoreEyeBlinkIfSuspended();

            string clipName = entry.clip != null ? entry.clip.name : "null";
            isOneShotMotionPlaying = true;
            currentOneShotMotionId = $"{group.groupId}[{index}]";

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Group '{group.groupId}' playing entry {index} ({clipName})");
            onOneShotMotionStarted?.Invoke(group.groupId);

            float clipLength = entry.clip != null ? entry.clip.length : 1f;
            oneShotCoroutine = StartCoroutine(WaitForOneShotCompletion(clipLength, currentOneShotMotionId));
        }

        private int SelectWeightedGroupEntry(OneShotMotionGroup group, int excludeIndex)
        {
            float totalWeight = 0f;
            var candidates = new List<int>();

            for (int i = 0; i < group.entries.Count; i++)
            {
                if (group.entries[i].clip == null) continue;
                // Avoid immediate repeat if more than one valid entry
                if (i == excludeIndex && group.entries.Count > 1) continue;

                candidates.Add(i);
                totalWeight += group.entries[i].weight;
            }

            if (candidates.Count == 0)
                return 0;

            float rand = Random.Range(0f, totalWeight);
            float accumulated = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                accumulated += group.entries[candidates[i]].weight;
                if (rand <= accumulated)
                    return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        /// <summary>
        /// Called by OnSentenceStarted to track TalkMotion state and interrupt one-shot if needed.
        /// </summary>
        private void OnSentenceStarted_OneShotMotion()
        {
            isTalkMotionActive = true;

            // Stop group looping
            isOneShotGroupLooping = false;
            currentGroupId = null;
            lastGroupEntryIndex = -1;

            if (isOneShotMotionPlaying)
            {
                Debug.Log($"[FluentTAvatarControllerFloatingHead] TalkMotion started, interrupting one-shot motion '{currentOneShotMotionId}'.");
                StopOneShotMotionInternal();
            }
        }

        /// <summary>
        /// Called by OnSentenceEnded to track TalkMotion state.
        /// </summary>
        private void OnSentenceEnded_OneShotMotion(bool isLastSentence)
        {
            if (isLastSentence)
            {
                isTalkMotionActive = false;
            }
        }

        #endregion
    }
}
