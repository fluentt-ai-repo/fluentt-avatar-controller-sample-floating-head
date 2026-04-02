using System.Collections.Generic;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Base class for all animation entries. Provides common fields for clip, weight, and override controls.
    /// </summary>
    [System.Serializable]
    public class AnimationEntryBase
    {
        public AnimationClip clip;
        [Range(0f, 10f)] public float weight = 1f;
        [Tooltip("When enabled, Look Target Eye Control is automatically disabled during this clip's playback so that the clip's eye BlendShape curves take priority.")]
        public bool overrideEyeControl;
        [Tooltip("When enabled, Eye Blink is automatically paused during this clip's playback so that the clip's blink curves take priority.")]
        public bool overrideEyeBlink;
    }

    /// <summary>
    /// Idle animation entry with weight for weighted random selection.
    /// </summary>
    [System.Serializable]
    public class IdleAnimationEntry : AnimationEntryBase
    {
        [Tooltip("Prevent this clip from playing twice in a row")]
        public bool preventRepeat;
    }

    /// <summary>
    /// One-shot motion entry that can be triggered on demand via PlayOneShotMotion(motionId).
    /// </summary>
    [System.Serializable]
    public class OneShotMotionEntry : AnimationEntryBase
    {
        [Tooltip("Unique identifier used to trigger this motion from external systems (e.g. \"wave\", \"nod\", \"bow\")")]
        public string motionId;
    }

    /// <summary>
    /// Entry within a one-shot motion group for weighted random selection.
    /// </summary>
    [System.Serializable]
    public class OneShotMotionGroupEntry : AnimationEntryBase
    {
        // AnimationEntryBase fields are sufficient
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
    /// Multiple animation entries can be assigned per tag for weighted random variant selection.
    /// </summary>
    [System.Serializable]
    public class GestureMapping
    {
        public string emotionTag;
        public List<AnimationEntryBase> clips = new List<AnimationEntryBase>();
    }
}
