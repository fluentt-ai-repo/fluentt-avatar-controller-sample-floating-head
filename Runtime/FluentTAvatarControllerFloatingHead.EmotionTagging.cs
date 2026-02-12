using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FluentT.Animation;
using FluentT.Talkmotion.Timeline;
using FluentT.Talkmotion.Timeline.Element;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Emotion Tagging control partial class
    /// Handles client-side emotion detection and animation using regex-based pattern matching,
    /// weighted probability selection, and motion duration constraints.
    /// </summary>
    public partial class FluentTAvatarControllerFloatingHead
    {
        private TimelineTMAnimation timelineEmotionTagging;

        /// <summary>
        /// Compiled emotion pattern group - one regex per emotion tag
        /// </summary>
        private class CompiledEmotionPattern
        {
            public string emotionTag;
            public Regex compiledRegex;
            public float totalWeight;
        }

        /// <summary>
        /// Candidate emotion match found during text analysis
        /// </summary>
        private class EmotionCandidate
        {
            public string emotionTag;
            public int matchCharIndex;
            public float weight;
        }

        private List<CompiledEmotionPattern> compiledPatterns;
        private Coroutine emotionProcessingCoroutine;

        #region Emotion Tagging Initialization

        private void InitializeEmotionTagging()
        {
            if (!enableClientEmotionTagging)
                return;

            // Get TMAnimationComponent from FluentTAvatar
            if (avatar == null || avatar.TMAnimationComponent == null)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] FluentTAvatar or its TMAnimationComponent not found. Emotion tagging will not work.");
                return;
            }

            // Create timeline for emotion tagging using FluentTAvatar's TMAnimationComponent
            timelineEmotionTagging = new TimelineTMAnimation(avatar.TMAnimationComponent)
            {
                layer = 2, // Layer 2 for emotions
                timelineName = "EmotionTagging",
                updatePhase = TMAnimationLayer.UpdatePhase.LateUpdate
            };

            // Set blend mode from FluentTAvatar's face blend mode setting
            timelineEmotionTagging.SetBlendMode(avatar.faceBlendMode);

            // Compile regex patterns from dataset
            CompileEmotionPatterns();

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Emotion tagging initialized with blend mode: {avatar.faceBlendMode}, {compiledPatterns?.Count ?? 0} pattern groups compiled");
        }

        /// <summary>
        /// Compile emotion keyword entries into per-tag regex patterns.
        /// Groups entries by emotionTag, combines patterns into a single regex per tag.
        /// </summary>
        private void CompileEmotionPatterns()
        {
            compiledPatterns = new List<CompiledEmotionPattern>();

            if (emotionKeywordDataset == null || emotionKeywordDataset.Entries.Count == 0)
                return;

            // Group entries by emotionTag
            var groups = emotionKeywordDataset.Entries
                .Where(e => !string.IsNullOrEmpty(e.pattern) && !string.IsNullOrEmpty(e.emotionTag))
                .GroupBy(e => e.emotionTag, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var patterns = new List<string>();
                float totalWeight = 0f;

                foreach (var entry in group)
                {
                    // Validate each pattern individually
                    try
                    {
                        // Test compile to check validity
                        _ = new Regex(entry.pattern);
                        patterns.Add($"(?:{entry.pattern})");
                        totalWeight += entry.weight;
                    }
                    catch (ArgumentException ex)
                    {
                        Debug.LogWarning($"[FluentTAvatarControllerFloatingHead] Invalid regex pattern '{entry.pattern}' for emotion '{entry.emotionTag}': {ex.Message}");
                    }
                }

                if (patterns.Count == 0)
                    continue;

                // Combine all patterns for this emotion tag into one regex
                string combinedPattern = string.Join("|", patterns);

                try
                {
                    var compiled = new CompiledEmotionPattern
                    {
                        emotionTag = group.Key,
                        compiledRegex = new Regex(combinedPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase),
                        totalWeight = totalWeight
                    };
                    compiledPatterns.Add(compiled);
                }
                catch (ArgumentException ex)
                {
                    Debug.LogWarning($"[FluentTAvatarControllerFloatingHead] Failed to compile combined regex for emotion '{group.Key}': {ex.Message}");
                }
            }
        }

        #endregion

        #region Client-Side Emotion Tagging

        /// <summary>
        /// Start async emotion tagging processing via coroutine
        /// </summary>
        private void StartEmotionTaggingProcessing(string text, float audioDuration, float sentenceTimelineStart)
        {
            if (!enableClientEmotionTagging || string.IsNullOrEmpty(text) || timelineEmotionTagging == null)
                return;

            if (compiledPatterns == null || compiledPatterns.Count == 0)
                return;

            // Cancel any ongoing processing
            if (emotionProcessingCoroutine != null)
            {
                StopCoroutine(emotionProcessingCoroutine);
            }

            emotionProcessingCoroutine = StartCoroutine(
                ProcessEmotionTaggingAsync(text, audioDuration, sentenceTimelineStart));
        }

        /// <summary>
        /// Coroutine-based emotion tagging processing.
        /// Starts timeline immediately, then processes regex matches and reserves animations.
        /// </summary>
        private IEnumerator ProcessEmotionTaggingAsync(string text, float audioDuration, float sentenceTimelineStart)
        {
            // Start timeline if not already running
            if (!timelineEmotionTagging.IsRunning)
            {
                timelineEmotionTagging.Play();
            }

            // Collect all candidates from all compiled patterns
            var candidates = new List<EmotionCandidate>();

            foreach (var pattern in compiledPatterns)
            {
                var matches = pattern.compiledRegex.Matches(text);
                foreach (Match match in matches)
                {
                    candidates.Add(new EmotionCandidate
                    {
                        emotionTag = pattern.emotionTag,
                        matchCharIndex = match.Index,
                        weight = pattern.totalWeight
                    });
                }
            }

            if (candidates.Count == 0)
                yield break;

            // Weighted probability selection
            var selectedCandidates = SelectCandidatesByWeight(candidates, maxEmotionTagsPerSentence);

            // Sort selected candidates by character position (chronological order)
            selectedCandidates.Sort((a, b) => a.matchCharIndex.CompareTo(b.matchCharIndex));

            // Apply motion duration constraints and reserve animations
            float nextAvailableTime = 0f;
            int totalChars = text.Length;

            foreach (var candidate in selectedCandidates)
            {
                var motionMapping = GetEmotionMotionMapping(candidate.emotionTag);
                if (motionMapping == null || motionMapping.animationClip == null)
                    continue;

                // Estimate timing based on character position ratio
                float estimatedTime = totalChars > 0
                    ? (candidate.matchCharIndex / (float)totalChars) * audioDuration
                    : 0f;

                // Duration constraint: skip if overlapping with previous motion
                if (estimatedTime < nextAvailableTime)
                    continue;

                // Get motion duration
                var animationClip = motionMapping.animationClip;
                if (motionMapping.blendWeight != 1.0f)
                {
                    animationClip = CreateWeightedAnimationClip(animationClip, motionMapping.blendWeight);
                }

                var animationElement = new TMAnimationClipElement(animationClip);
                float duration = motionMapping.durationOverride > 0
                    ? motionMapping.durationOverride
                    : animationElement.GetLength();

                // Reserve animation on timeline
                var timeSlot = new TimeSlot(
                    timelineEmotionTagging.CurrentTime + estimatedTime,
                    duration,
                    0.1f, // fade in
                    0.1f  // fade out
                );

                timelineEmotionTagging.Reserve(timeSlot, animationElement);

                // Update next available time
                nextAvailableTime = estimatedTime + duration;

                Debug.Log($"[FluentTAvatarControllerFloatingHead] Emotion '{motionMapping.emotionTag}' at char {candidate.matchCharIndex} → time {estimatedTime:F2}s, duration {duration:F2}s");

                // Yield between reservations to spread work across frames
                yield return null;
            }

            emotionProcessingCoroutine = null;
        }

        /// <summary>
        /// Select candidates using weighted probability without replacement
        /// </summary>
        private List<EmotionCandidate> SelectCandidatesByWeight(List<EmotionCandidate> candidates, int maxCount)
        {
            var selected = new List<EmotionCandidate>();
            var remaining = new List<EmotionCandidate>(candidates);

            int count = Mathf.Min(maxCount, remaining.Count);

            for (int i = 0; i < count; i++)
            {
                if (remaining.Count == 0)
                    break;

                // Calculate total weight
                float totalWeight = 0f;
                foreach (var c in remaining)
                    totalWeight += c.weight;

                if (totalWeight <= 0f)
                    break;

                // Random selection based on weight
                float randomValue = UnityEngine.Random.Range(0f, totalWeight);
                float cumulative = 0f;

                for (int j = 0; j < remaining.Count; j++)
                {
                    cumulative += remaining[j].weight;
                    if (randomValue <= cumulative)
                    {
                        selected.Add(remaining[j]);
                        remaining.RemoveAt(j);
                        break;
                    }
                }
            }

            return selected;
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
