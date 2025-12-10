using System;
using System.Collections.Generic;
using System.Linq;
using FluentT.APIClient.V3;
using FluentT.Talkmotion.Timeline;
using FluentT.Talkmotion.Timeline.Element;
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
        private TimelineAnimatorClip timelineServerMotionTagging;

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

            // Create timeline for server motion tagging with custom dummy clips
            timelineServerMotionTagging = new TimelineAnimatorClip(animator, overrideController,
                "DummyServerMotion 0",
                "DummyServerMotion 1",
                "DummyServerMotion 2");
            timelineServerMotionTagging.SetEnableOverlap(true);

            Debug.Log("[FluentTAvatarControllerFloatingHead] Server motion tagging initialized");
        }

        #endregion

        #region Server Motion Tagging Callbacks

        /// <summary>
        /// Called when server motion tag marker is triggered from timeline
        /// This is called at exact timing based on word position
        /// </summary>
        public void OnServerMotionTag(FluentT.APIClient.V3.TaggedMotionContent taggedMotion, FluentT.APIClient.V3.TalkMotionData data)
        {
            if (!enableServerMotionTagging || taggedMotion == null)
                return;

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Server motion tag triggered - Tag: {taggedMotion.tag}, Word: {taggedMotion.word}, Confidence: {taggedMotion.confidence}");

            // Find matching motion mapping
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
        /// Play server motion animation
        /// </summary>
        private void PlayServerMotion(ServerMotionTagMapping motionMapping)
        {
            if (animator == null || motionMapping.animationClip == null)
                return;

            // Set layer weight
            if (animator.layerCount >= 3) // Layer 2 = Server Motion Layer
            {
                animator.SetLayerWeight(2, motionMapping.blendWeight);
            }

            // Play animation with crossfade
            float fadeDuration = 0.2f; // 0.2 second crossfade
            animator.CrossFadeInFixedTime(motionMapping.animationClip.name, fadeDuration, 2); // Layer 2

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Playing server motion: {motionMapping.emotionTag}");
        }

        #endregion

        #region Server Motion Tagging Processing

        /// <summary>
        /// Process server-side motion tagging data (legacy - no longer used, replaced by OnServerMotionTag callback)
        /// </summary>
        public void ProcessServerMotionTagging(TalkMotionData data)
        {
            if (!enableServerMotionTagging || data?.taggedMotion == null || timelineServerMotionTagging == null)
                return;

            var taggedMotion = data.taggedMotion;

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Processing server motion tag - Tag: {taggedMotion.tag}, Word: {taggedMotion.word}, Word Index: {taggedMotion.word_index}/{taggedMotion.total_words}, Confidence: {taggedMotion.confidence}");

            // Find matching motion mapping
            var motionMapping = GetServerMotionMapping(taggedMotion.tag);
            if (motionMapping == null || motionMapping.animationClip == null)
            {
                Debug.LogWarning($"[FluentTAvatarControllerFloatingHead] No motion mapping found for tag '{taggedMotion.tag}'");
                return;
            }

            // Start timeline if not already running
            if (!timelineServerMotionTagging.IsRunning)
            {
                timelineServerMotionTagging.Play();
            }

            // Apply the motion with timing estimation
            ApplyServerMotion(motionMapping, taggedMotion, data.audioClip?.length ?? 0f);
        }

        /// <summary>
        /// Get server motion mapping by emotion tag
        /// </summary>
        private ServerMotionTagMapping GetServerMotionMapping(string emotionTag)
        {
            return serverMotionTagMappings.FirstOrDefault(m =>
                string.Equals(m.emotionTag, emotionTag, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Apply server motion to timeline
        /// </summary>
        private void ApplyServerMotion(ServerMotionTagMapping motionMapping, TaggedMotionContent taggedMotion, float audioDuration)
        {
            if (timelineServerMotionTagging == null || motionMapping.animationClip == null)
                return;

            var animationClip = motionMapping.animationClip;

            // Estimate timing based on word position in sentence
            float startTime = EstimateServerMotionTiming(taggedMotion.word_index, taggedMotion.total_words, audioDuration);

            // Determine duration
            float duration = motionMapping.durationOverride > 0f ?
                motionMapping.durationOverride :
                animationClip.length;

            // Create animator clip element
            var animatorElement = new AnimatorClipElement(animationClip);

            // Create time slot with fade in/out
            var timeSlot = new TimeSlot(
                timelineServerMotionTagging.CurrentTime + startTime,
                duration,
                0.2f, // fade in
                0.2f  // fade out
            );

            // Set layer weight if different from 1.0
            if (motionMapping.blendWeight != 1.0f)
            {
                // Note: Layer weight is set on the Animator, not per clip
                // You may need to handle this differently based on your requirements
                animator.SetLayerWeight(2, motionMapping.blendWeight);
            }

            timelineServerMotionTagging.Reserve(timeSlot, animatorElement);

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Applied server motion '{motionMapping.emotionTag}' for word '{taggedMotion.word}' at time {startTime:F2}s, duration {duration:F2}s, confidence {taggedMotion.confidence:F2}");
        }

        /// <summary>
        /// Estimate timing for server motion tag based on word position
        /// </summary>
        private float EstimateServerMotionTiming(int wordIndex, int totalWords, float audioDuration)
        {
            if (totalWords <= 0 || audioDuration <= 0f)
                return 0f;

            // Linear estimation based on word position
            float normalizedPosition = (float)wordIndex / totalWords;
            float estimatedTime = normalizedPosition * audioDuration;

            return Mathf.Max(0f, estimatedTime);
        }

        private void UpdateServerMotionTaggingTimeline(float deltaTime)
        {
            if (timelineServerMotionTagging != null)
            {
                timelineServerMotionTagging.Update(deltaTime);
            }
        }

        private void LateUpdateServerMotionTaggingTimeline(float deltaTime)
        {
            if (timelineServerMotionTagging != null)
            {
                timelineServerMotionTagging.LateUpdate(deltaTime);
            }
        }

        #endregion
    }
}
