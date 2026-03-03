using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Look target strategy enum
    /// </summary>
    [Serializable]
    public enum ELookTargetStrategy
    {
        LookIntoVoid,
        FocusedOnTarget,
    }

    /// <summary>
    /// Look target setting
    /// </summary>
    [Serializable]
    public class LookTargetSetting
    {
        public ELookTargetStrategy headStrategy;
        public Vector2 headLookIntoVoid;
        public Vector2 headAngleVariance;

        public ELookTargetStrategy eyeStrategy;
        public Vector2 eyeLookIntoVoid;
        public Vector2 eyeAngleVariance;
    }

    /// <summary>
    /// Eye control strategy enum
    /// </summary>
    public enum EEyeControlStrategy
    {
        BlendWeightFluentt,
        Transform,
        TransformCorrected,
    }

    /// <summary>
    /// Eye blend shape data
    /// </summary>
    [Serializable]
    public class EyeBlendShape
    {
        [FormerlySerializedAs("skmr")]
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public string blendShapeName;
        public int blendShapeIdx;
        public float scale;
    }

    /// <summary>
    /// Eye blend shapes collection
    /// </summary>
    [Serializable]
    public class EyeBlendShapes
    {
        [Range(0f, 10f)]
        public float globalScale = 1.0f;

        public List<EyeBlendShape> eyeLookUpLeftIdx;
        public List<EyeBlendShape> eyeLookDownLeftIdx;
        public List<EyeBlendShape> eyeLookInLeftIdx;
        public List<EyeBlendShape> eyeLookOutLeftIdx;
        public List<EyeBlendShape> eyeLookUpRightIdx;
        public List<EyeBlendShape> eyeLookDownRightIdx;
        public List<EyeBlendShape> eyeLookInRightIdx;
        public List<EyeBlendShape> eyeLookOutRightIdx;
    }
}
