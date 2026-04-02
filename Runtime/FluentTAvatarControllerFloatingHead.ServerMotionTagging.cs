using System;
using System.Collections.Generic;
using System.Linq;
using FluentT.APIClient.V3;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Server Motion Tagging partial class
    /// Handles server-side emotion tags and body animations
    /// This is a sample implementation showing how to use server-provided emotion tags
    /// </summary>
    public partial class FluentTAvatarControllerFloatingHead
    {
        private int currentEmotionSlot = 0; // Alternates between 0 and 1
        private const string GESTURE_OVERRIDE_0 = "gesture_override_0";
        private const string GESTURE_OVERRIDE_1 = "gesture_override_1";

        #region Server Motion Tagging Initialization

        private void InitializeServerMotionTagging()
        {
            if (!enableServerMotionTagging)
                return;

            if (animator == null)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Animator not found. Server motion tagging will not work.");
                return;
            }

            if (overrideController == null)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Override controller not found. Server motion tagging will not work.");
                return;
            }

            Debug.Log("[FluentTAvatarControllerFloatingHead] Server motion tagging initialized");
        }

        #endregion

        /// <summary>
        /// Reset emotion animation state to Idle immediately.
        /// Call this when speech ends to prevent long emotion animations from continuing.
        /// </summary>
        public void ResetEmotionState()
        {
            if (animator == null) return;
            RestoreEyeControlIfSuspended();
            animator.SetTrigger("emotionReset");
        }

        #region Server Motion Tagging Callbacks

        /// <summary>
        /// Called when motion tag marker is triggered from timeline.
        /// Handles both server-provided and client-detected emotion tags.
        /// </summary>
        public void OnServerMotionTag(FluentT.APIClient.V3.TaggedMotionContent taggedMotion, FluentT.APIClient.V3.TalkMotionData data)
        {
            if (taggedMotion == null)
                return;

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Motion tag triggered - Tag: {taggedMotion.tag}, Word: {taggedMotion.word}, Confidence: {taggedMotion.confidence}");

            // Find matching gesture mapping from shared gestureMappings
            var gestureMapping = GetServerMotionMapping(taggedMotion.tag);
            if (gestureMapping == null || gestureMapping.animationClips == null || gestureMapping.animationClips.Count == 0)
            {
                Debug.LogWarning($"[FluentTAvatarControllerFloatingHead] No gesture mapping found for tag '{taggedMotion.tag}'");
                return;
            }

            // Pick a random clip from the variants
            var clip = gestureMapping.animationClips[UnityEngine.Random.Range(0, gestureMapping.animationClips.Count)];
            if (clip == null)
                return;

            // Play the animation immediately (triggered at exact timing by timeline marker)
            PlayMotionClip(clip, gestureMapping.blendWeight, gestureMapping.emotionTag, gestureMapping.overrideEyeControl);
        }

        /// <summary>
        /// Play motion animation using animator triggers and override controller
        /// </summary>
        private void PlayMotionClip(AnimationClip clip, float blendWeight, string tag, bool overrideEyeControl = false)
        {
            if (animator == null || overrideController == null || clip == null)
                return;

            // Determine which slot to use (alternate between 0 and 1)
            string overrideClipName = currentEmotionSlot == 0 ? GESTURE_OVERRIDE_0 : GESTURE_OVERRIDE_1;
            string triggerName = currentEmotionSlot == 0 ? "emotion0" : "emotion1";

            // Override the placeholder clip with the actual animation
            overrideController[overrideClipName] = clip;

            // Layer weight is now managed by LayerWeightController StateMachineBehaviour
            // attached to each state in the Motion Tagging layer.

            // Clear any pending emotionReset trigger to prevent immediate return to Idle
            animator.ResetTrigger("emotionReset");

            // Trigger the animation state
            animator.SetTrigger(triggerName);

            // Handle eye control override per gesture
            if (overrideEyeControl)
            {
                SuspendEyeControl();
            }
            else
            {
                RestoreEyeControlIfSuspended();
            }

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Playing motion: {tag} ({clip.name}) on slot {currentEmotionSlot}");

            // Alternate slot for next call
            currentEmotionSlot = 1 - currentEmotionSlot;
        }

        #endregion

        #region Server Motion Tagging Processing

        /// <summary>
        /// Get gesture mapping by emotion tag from shared gestureMappings
        /// </summary>
        private GestureMapping GetServerMotionMapping(string emotionTag)
        {
            return gestureMappings.FirstOrDefault(m =>
                string.Equals(m.emotionTag, emotionTag, StringComparison.OrdinalIgnoreCase));
        }

        #endregion
    }
}
