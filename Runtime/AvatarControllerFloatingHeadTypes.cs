using System.Collections.Generic;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Idle animation entry with weight for weighted random selection.
    /// </summary>
    [System.Serializable]
    public class IdleAnimationEntry
    {
        public AnimationClip clip;
        [Range(0f, 10f)] public float weight = 1f;
        [Tooltip("Prevent this clip from playing twice in a row")]
        public bool preventRepeat;
        [Tooltip("When enabled, Look Target Eye Control is automatically disabled during this clip's playback so that the clip's eye BlendShape curves take priority.")]
        public bool overrideEyeControl;
    }

    /// <summary>
    /// One-shot motion entry that can be triggered on demand via PlayOneShotMotion(motionId).
    /// </summary>
    [System.Serializable]
    public class OneShotMotionEntry
    {
        [Tooltip("Unique identifier used to trigger this motion from external systems (e.g. \"wave\", \"nod\", \"bow\")")]
        public string motionId;
        public AnimationClip clip;
        [Range(0f, 1f)]
        public float blendWeight = 1f;
        [Tooltip("When enabled, Look Target Eye Control is automatically disabled during this motion's playback so that the clip's eye BlendShape curves take priority.")]
        public bool overrideEyeControl;
    }

    /// <summary>
    /// Entry within a one-shot motion group for weighted random selection.
    /// </summary>
    [System.Serializable]
    public class OneShotMotionGroupEntry
    {
        public AnimationClip clip;
        [Range(0f, 10f)]
        public float weight = 1f;
        [Range(0f, 1f)]
        public float blendWeight = 1f;
        [Tooltip("When enabled, Look Target Eye Control is automatically disabled during this clip's playback so that the clip's eye BlendShape curves take priority.")]
        public bool overrideEyeControl;
    }

    /// <summary>
    /// A group of one-shot motions that plays random clips in a loop until stopped.
    /// Weight-based random selection determines clip frequency.
    /// </summary>
    [System.Serializable]
    public class OneShotMotionGroup
    {
        [Tooltip("Unique identifier used to trigger this group from external systems")]
        public string groupId;
        public List<OneShotMotionGroupEntry> entries = new List<OneShotMotionGroupEntry>();
    }

    /// <summary>
    /// Emotion tag to gesture animation mapping.
    /// Multiple animation clips can be assigned per tag for random variant selection.
    /// </summary>
    [System.Serializable]
    public class GestureMapping
    {
        public string emotionTag;
        public List<AnimationClip> animationClips = new List<AnimationClip>();
        [Range(0f, 1f)]
        public float blendWeight = 1f;
        [Tooltip("When enabled, Look Target Eye Control is automatically disabled during this gesture so that the clip's eye BlendShape curves take priority.")]
        public bool overrideEyeControl;
    }
}
