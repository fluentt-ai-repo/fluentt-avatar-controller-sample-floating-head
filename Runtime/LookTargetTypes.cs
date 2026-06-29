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
    /// How eye bones are aimed at the look target (Transform/TransformCorrected strategies).
    /// Some rigs author eye bones with negative (mirrored) scale; MultiAimConstraint cannot aim a
    /// mirrored bone (its world-up twist solve assumes a right-handed basis, so horizontal tracking
    /// collapses into roll about the gaze axis). The direct-drive solver re-aims the bone from a
    /// rest-calibrated LookRotation, which is robust to any axis orientation and any scale incl. mirrors.
    /// </summary>
    public enum EEyeAimMode
    {
        /// Detect mirrored/reflected eye bones at init; if any eye is mirrored, direct-drive both eyes
        /// (kept uniform so the pair stays consistent), otherwise use MultiAimConstraint.
        Auto,
        /// Always MultiAimConstraint (+ Auto-correct Eye Aim Axis). Cheapest/jobified; cannot aim mirrored eye bones.
        Constraint,
        /// Always the rest-calibrated direct-drive solver. Robust to any axis/scale incl. mirrored bones.
        DirectUniversal,
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
