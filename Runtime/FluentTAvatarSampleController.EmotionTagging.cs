using System;
using System.Collections.Generic;
using System.Linq;
using FluentT.Animation;
using FluentT.Talkmotion.Timeline;
using FluentT.Talkmotion.Timeline.Element;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Emotion Tagging control partial class
    /// Handles client-side emotion detection and animation
    /// This is a sample implementation showing how to add emotion-based animations
    /// </summary>
    public partial class FluentTAvatarSampleController
    {
        private TimelineTMAnimation timelineEmotionTagging;

        /// <summary>
        /// Detected emotion tag information
        /// </summary>
        private class DetectedEmotionTag
        {
            public string emotionTag;
            public string word;
            public int wordIndex;
            public int totalWords;
            public float startTime;
            public float duration;
            public float confidence;
        }

        #region Emotion Tagging Initialization

        private void InitializeEmotionTagging()
        {
            if (!enableClientEmotionTagging)
                return;

            // Get TMAnimationComponent from FluentTAvatar
            if (avatar == null || avatar.TMAnimationComponent == null)
            {
                Debug.LogWarning("[FluentTAvatarSampleController] FluentTAvatar or its TMAnimationComponent not found. Emotion tagging will not work.");
                return;
            }

            // Create timeline for emotion tagging using FluentTAvatar's TMAnimationComponent
            timelineEmotionTagging = new TimelineTMAnimation(avatar.TMAnimationComponent)
            {
                layer = 2, // Layer 2 for emotions
                timelineName = "EmotionTagging",
                updatePhase = TMAnimationLayer.UpdatePhase.LateUpdate
            };

            Debug.Log("[FluentTAvatarSampleController] Emotion tagging initialized");
        }

        #endregion

        #region Client-Side Emotion Tagging

        /// <summary>
        /// Process client-side emotion tagging for text
        /// </summary>
        private void ProcessClientEmotionTagging(string text, float audioDuration, float startTime)
        {
            if (!enableClientEmotionTagging || string.IsNullOrEmpty(text) || timelineEmotionTagging == null)
                return;

            var detectedTags = DetectEmotionTags(text, audioDuration);

            if (detectedTags.Count > 0)
            {
                // Start timeline if not already running
                if (!timelineEmotionTagging.IsRunning)
                {
                    timelineEmotionTagging.Play();
                }

                foreach (var tag in detectedTags)
                {
                    var motionMapping = GetEmotionMotionMapping(tag.emotionTag);
                    if (motionMapping != null && motionMapping.animationClip != null)
                    {
                        ApplyEmotionMotion(motionMapping, tag, startTime);
                    }
                }
            }
        }

        /// <summary>
        /// Detect emotion tags in text based on word mappings
        /// </summary>
        private List<DetectedEmotionTag> DetectEmotionTags(string text, float audioDuration)
        {
            var detectedTags = new List<DetectedEmotionTag>();
            var words = text.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            var candidateTags = new List<(WordEmotionMapping mapping, int wordIndex, string matchedWord)>();

            // Find all potential matches
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i].ToLower().Trim(".,!?:;\"'".ToCharArray());

                foreach (var mapping in wordEmotionMappings)
                {
                    if (string.IsNullOrEmpty(mapping.word) || string.IsNullOrEmpty(mapping.emotionTag))
                        continue;

                    bool isMatch = false;
                    if (mapping.partialMatch)
                    {
                        isMatch = word.Contains(mapping.word.ToLower());
                    }
                    else
                    {
                        isMatch = word.Equals(mapping.word.ToLower(), StringComparison.OrdinalIgnoreCase);
                    }

                    if (isMatch)
                    {
                        candidateTags.Add((mapping, i, words[i]));
                    }
                }
            }

            // Sort by priority and select up to max tags per sentence
            candidateTags.Sort((a, b) => b.mapping.priority.CompareTo(a.mapping.priority));

            var selectedTags = candidateTags.Take(maxEmotionTagsPerSentence).ToList();

            // Convert to DetectedEmotionTag with timing information
            foreach (var (mapping, wordIndex, matchedWord) in selectedTags)
            {
                var timing = EstimateWordTiming(wordIndex, words.Length, audioDuration);

                detectedTags.Add(new DetectedEmotionTag
                {
                    emotionTag = mapping.emotionTag,
                    word = matchedWord,
                    wordIndex = wordIndex,
                    totalWords = words.Length,
                    startTime = timing.startTime,
                    duration = timing.duration,
                    confidence = 1.0f // Client-side detection has 100% confidence
                });
            }

            return detectedTags;
        }

        /// <summary>
        /// Estimate timing for a word within the audio duration
        /// </summary>
        private (float startTime, float duration) EstimateWordTiming(int wordIndex, int totalWords, float audioDuration)
        {
            if (totalWords <= 0)
                return (0f, audioDuration);

            // Simple linear estimation - assumes words are evenly distributed
            float wordsPerSecond = totalWords / audioDuration;
            float wordDuration = audioDuration / totalWords;
            float startTime = wordIndex / (float)totalWords * audioDuration;

            // Add some randomness to make it more natural
            float variance = wordDuration * 0.2f; // 20% variance
            startTime += UnityEngine.Random.Range(-variance, variance);
            startTime = Mathf.Max(0f, startTime);

            return (startTime, wordDuration);
        }

        /// <summary>
        /// Get emotion motion mapping by emotion tag
        /// </summary>
        private EmotionMotionMapping GetEmotionMotionMapping(string emotionTag)
        {
            return emotionMotionMappings.FirstOrDefault(m =>
                string.Equals(m.emotionTag, emotionTag, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Apply emotion motion to timeline
        /// </summary>
        private void ApplyEmotionMotion(EmotionMotionMapping motionMapping, DetectedEmotionTag tag, float timelineStartTime)
        {
            if (timelineEmotionTagging == null)
                return;

            var animationClip = motionMapping.animationClip;
            if (animationClip == null)
                return;

            // Apply blend weight by scaling the animation clip
            if (motionMapping.blendWeight != 1.0f)
            {
                animationClip = CreateWeightedAnimationClip(animationClip, motionMapping.blendWeight);
            }

            var animationElement = new TMAnimationClipElement(animationClip);

            float duration = motionMapping.durationOverride > 0 ?
                motionMapping.durationOverride :
                animationElement.GetLength();

            var timeSlot = new TimeSlot(
                timelineEmotionTagging.CurrentTime + tag.startTime,
                duration,
                0.1f, // fade in
                0.1f  // fade out
            );

            timelineEmotionTagging.Reserve(timeSlot, animationElement);

            Debug.Log($"[FluentTAvatarSampleController] Applied emotion motion '{motionMapping.emotionTag}' for word '{tag.word}' at time {timelineStartTime + tag.startTime:F2}s, duration {duration:F2}s");
        }

        /// <summary>
        /// Create a weighted version of an animation clip for blending
        /// </summary>
        private TMAnimationClip CreateWeightedAnimationClip(TMAnimationClip originalClip, float weight)
        {
            if (weight == 1.0f)
                return originalClip;

            var weightedClip = new TMAnimationClip
            {
                name = $"{originalClip.name}_weighted_{weight:F2}",
                repeat = originalClip.repeat
            };

            // Copy and scale all curve data
            foreach (var curveData in originalClip.CurveDatas)
            {
                var weightedCurveData = new TMCurveData(curveData.relativePath);

                foreach (var blendCurve in curveData.BlendCurves)
                {
                    var weightedCurve = new TMAnimationCurve
                    {
                        key = blendCurve.key
                    };

                    foreach (var keyframe in blendCurve.KeyFrames)
                    {
                        weightedCurve.AddKeyFrame(new TMKeyframe
                        {
                            t = keyframe.t,
                            v = keyframe.v * weight
                        });
                    }

                    weightedCurveData.AddBlendCurve(weightedCurve);
                }

                weightedClip.AddCurveData(weightedCurveData);
            }

            return weightedClip;
        }

        private void UpdateEmotionTaggingTimeline(float deltaTime)
        {
            if (timelineEmotionTagging != null)
            {
                timelineEmotionTagging.Update(deltaTime);
            }
        }

        private void LateUpdateEmotionTaggingTimeline(float deltaTime)
        {
            if (timelineEmotionTagging != null)
            {
                timelineEmotionTagging.LateUpdate(deltaTime);
            }
        }

        #endregion
    }
}
