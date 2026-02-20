using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Emotion Tagging control partial class
    /// Handles client-side emotion detection using regex-based pattern matching
    /// and adds motion tag markers to FluentTAvatar's timeline for accurate playback timing.
    /// </summary>
    public partial class FluentTAvatarControllerFloatingHead
    {
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

        // Track TalkMotionData instances that have already been emotion-tagged
        // Prevents duplicate marker additions when onSentenceStarted re-fires after RebuildGraph
        private HashSet<FluentT.APIClient.V3.TalkMotionData> emotionTaggedData = new HashSet<FluentT.APIClient.V3.TalkMotionData>();

        #region Emotion Tagging Initialization

        private void InitializeEmotionTagging()
        {
            if (!enableClientEmotionTagging)
                return;

            // Compile regex patterns from dataset
            CompileEmotionPatterns();

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Client emotion tagging initialized with {compiledPatterns?.Count ?? 0} pattern groups compiled");
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
        /// Process emotion tagging: analyze text with regex, select candidates,
        /// and add motion tag markers to FluentTAvatar's timeline.
        /// </summary>
        private void ProcessEmotionTagging(string text, float audioDuration, FluentT.APIClient.V3.TalkMotionData data)
        {
            if (!enableClientEmotionTagging || string.IsNullOrEmpty(text))
                return;

            if (compiledPatterns == null || compiledPatterns.Count == 0)
                return;

            if (avatar == null)
                return;

            // Skip if this data has already been emotion-tagged (prevents duplicate markers)
            if (data != null && !emotionTaggedData.Add(data))
                return;

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
            {
                Debug.Log($"[FluentTAvatarControllerFloatingHead] Emotion tagging: no regex matches in \"{text}\"");
                return;
            }

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Emotion tagging: {candidates.Count} regex matches found");

            // Weighted probability selection
            var selectedCandidates = SelectCandidatesByWeight(candidates, maxEmotionTagsPerSentence);

            // Add motion tag markers to timeline
            int totalChars = text.Length;

            foreach (var candidate in selectedCandidates)
            {
                // Estimate timing based on character position ratio
                float estimatedTime = totalChars > 0
                    ? (candidate.matchCharIndex / (float)totalChars) * audioDuration
                    : 0f;

                // Add marker to FluentTAvatar's timeline
                avatar.AddMotionTagMarker(candidate.emotionTag, estimatedTime, data);

                Debug.Log($"[FluentTAvatarControllerFloatingHead] Added motion tag marker: '{candidate.emotionTag}' at char {candidate.matchCharIndex} → time offset {estimatedTime:F2}s");
            }
        }

        #endregion

        #region Candidate Selection

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

        #endregion
    }
}
