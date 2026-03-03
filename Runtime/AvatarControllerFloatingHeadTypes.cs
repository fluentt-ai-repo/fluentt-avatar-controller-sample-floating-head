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
    }
}
