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
        private const string EMOTION_DUMMY_0 = "emotion_dummy_0";
        private const string EMOTION_DUMMY_1 = "emotion_dummy_1";

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

            // Find matching motion mapping from shared emotionMotionMappings
            var motionMapping = GetServerMotionMapping(taggedMotion.tag);
            if (motionMapping == null || motionMapping.animationClip == null)
            {
                Debug.LogWarning($"[FluentTAvatarControllerFloatingHead] No motion mapping found for tag '{taggedMotion.tag}'");
                return;
            }

            // Play the animation immediately (triggered at exact timing by timeline marker)
            PlayServerMotion(motionMapping);
        }

        /// <summary>
        /// Play server motion animation using animator triggers and override controller
        /// </summary>
        private void PlayServerMotion(EmotionMotionMapping motionMapping)
        {
            if (animator == null || overrideController == null || motionMapping.animationClip == null)
                return;

            // Determine which slot to use (alternate between 0 and 1)
            string dummyClipName = currentEmotionSlot == 0 ? EMOTION_DUMMY_0 : EMOTION_DUMMY_1;
            string triggerName = currentEmotionSlot == 0 ? "emotion0" : "emotion1";

            // Override the dummy clip with the actual animation
            overrideController[dummyClipName] = motionMapping.animationClip;

            // Set layer weight (optional, for blending)
            int serverMotionLayerIndex = GetLayerIndex("Server Motion Tagging");
            if (serverMotionLayerIndex >= 0)
            {
                animator.SetLayerWeight(serverMotionLayerIndex, motionMapping.blendWeight);
            }

            // Trigger the animation state
            animator.SetTrigger(triggerName);

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Playing server motion: {motionMapping.emotionTag} on slot {currentEmotionSlot} with trigger {triggerName}");

            // Alternate slot for next call
            currentEmotionSlot = 1 - currentEmotionSlot;
        }

        /// <summary>
        /// Get layer index by name
        /// </summary>
        private int GetLayerIndex(string layerName)
        {
            for (int i = 0; i < animator.layerCount; i++)
            {
                if (animator.GetLayerName(i) == layerName)
                    return i;
            }
            return -1;
        }

        #endregion

        #region Server Motion Tagging Processing

        /// <summary>
        /// Process server-side motion tagging data (legacy - no longer used, replaced by OnServerMotionTag callback)
        /// </summary>
        public void ProcessServerMotionTagging(TalkMotionData data)
        {
            if (!enableServerMotionTagging || data?.taggedMotion == null)
                return;

            var taggedMotion = data.taggedMotion;

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Processing server motion tag - Tag: {taggedMotion.tag}, Word: {taggedMotion.word}, Word Index: {taggedMotion.word_index}/{taggedMotion.total_words}, Confidence: {taggedMotion.confidence}");

            // Find matching motion mapping from shared emotionMotionMappings
            var motionMapping = GetServerMotionMapping(taggedMotion.tag);
            if (motionMapping == null || motionMapping.animationClip == null)
            {
                Debug.LogWarning($"[FluentTAvatarControllerFloatingHead] No motion mapping found for tag '{taggedMotion.tag}'");
                return;
            }

            // Play the motion immediately (timing handled by callback)
            PlayServerMotion(motionMapping);
        }

        /// <summary>
        /// Get motion mapping by emotion tag from shared emotionMotionMappings
        /// </summary>
        private EmotionMotionMapping GetServerMotionMapping(string emotionTag)
        {
            return emotionMotionMappings.FirstOrDefault(m =>
                string.Equals(m.emotionTag, emotionTag, StringComparison.OrdinalIgnoreCase));
        }

        #endregion
    }
}
