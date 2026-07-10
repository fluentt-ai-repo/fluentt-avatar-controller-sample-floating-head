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
    /// The rest-calibrated direct-drive solver measures each bone's true gaze direction against the
    /// avatar root at bind pose and re-aims it via LookRotation each frame — robust to ANY bone axis
    /// orientation (incl. non-cardinal gaze axes) and ANY scale incl. mirrored bones, which the
    /// MultiAimConstraint cannot handle (its world-up twist solve assumes a right-handed basis and its
    /// aim axis must be a cardinal local axis).
    /// </summary>
    public enum EEyeAimMode
    {
        /// Rest-calibrated direct-drive solver (recommended). Same as DirectUniversal.
        Auto,
        /// Legacy MultiAimConstraint (+ Auto-correct Eye Aim Axis). Jobified; cannot aim mirrored eye
        /// bones, and leaves a residual error on rigs whose gaze is not exactly on a cardinal local axis.
        Constraint,
        /// Always the rest-calibrated direct-drive solver. Robust to any axis/scale incl. mirrored bones.
        DirectUniversal,
    }

    /// <summary>
    /// How the head bone is aimed at the look target.
    /// </summary>
    public enum EHeadAimMode
    {
        /// Rest-calibrated direct drive (recommended). At init the head bone's true facial forward is
        /// measured against the avatar root (bind pose faces root forward), so rigs whose head-bone
        /// local +Z is NOT the gaze direction (e.g. Rigify DEF-spine.005 tilted ~7.5deg down, or rigs
        /// with yawed/flipped head axes) still aim exactly at the target. Aims from the eye midpoint,
        /// which also removes the pivot-vs-eye parallax error at close range.
        DirectCalibrated,
        /// Legacy MultiAimConstraint path. Assumes the head bone's local +Z is the facial forward;
        /// any rest-pose tilt of that axis becomes a constant aiming error.
        Constraint,
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
