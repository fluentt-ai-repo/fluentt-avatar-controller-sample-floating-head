using System;
using System.Collections.Generic;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// ScriptableObject that stores emotion keyword patterns for client-side emotion tagging.
    /// Each entry maps a regex pattern to an emotion tag with a selection weight.
    /// </summary>
    [CreateAssetMenu(fileName = "EmotionKeywordDataset", menuName = "FluentT/Emotion Keyword Dataset")]
    public class EmotionKeywordDataset : ScriptableObject
    {
        [SerializeField] private List<EmotionKeywordEntry> entries = new();

        /// <summary>
        /// Read-only access to keyword entries
        /// </summary>
        public IReadOnlyList<EmotionKeywordEntry> Entries => entries;
    }

    /// <summary>
    /// A single keyword entry mapping a regex pattern to an emotion tag
    /// </summary>
    [Serializable]
    public class EmotionKeywordEntry
    {
        [Tooltip("Regex pattern to match in text (e.g. \"happy|glad|joyful\")")]
        public string pattern;

        [Tooltip("Emotion tag identifier (e.g. \"happy\", \"sad\", \"angry\")")]
        public string emotionTag;

        [Tooltip("Selection weight - higher weight increases probability of being chosen")]
        [Range(0.1f, 100f)]
        public float weight = 1f;
    }
}
