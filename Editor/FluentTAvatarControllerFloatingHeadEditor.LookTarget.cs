#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
using FluentT.Animation;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    /// <summary>
    /// Look Target Inspector UI drawing
    /// </summary>
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
        private static readonly GUIContent gc_idleLook = new("Idle Look Settings", "Look target behavior when idle (not talking)");
        private static readonly GUIContent gc_talkingLook = new("Talking Look Settings", "Look target behavior when talking");
        private static readonly GUIContent gc_eyeStrategy = new("Eye Control Strategy", "Choose between Transform (Animation Rigging) or BlendShape control");
        private static readonly GUIContent gc_eyeBlendShapes = new("Eye Blend Shapes", "Configure eye look blend shapes");
        private static readonly GUIContent gc_eyeAngleLimit = new("Eye Angle Limit", "Maximum angle the eyes can rotate");
        private static readonly GUIContent gc_eyeAngleThreshold = new("Eye Angle Threshold", "Hysteresis threshold for eye tracking");
        private static readonly GUIContent gc_headAngleLimit = new("Head Angle Limit", "Maximum angle the head can rotate toward the target (head MultiAimConstraint limit)");
        private static readonly GUIContent gc_eyeAngleLimitTransform = new("Eye Angle Limit", "Maximum angle the eyes can rotate toward the target (eye MultiAimConstraint limit)");
        private static readonly GUIContent gc_autoDetectEyeAim = new("Auto-correct Eye Aim Axis", "Detect each eye/head bone's real gaze axis at init and configure the MultiAimConstraint aim axis accordingly. Required for \"twisted\" rigs whose bone local +Z is not the gaze direction (otherwise left/right eye tracking does not work). Turn off to keep manually-authored constraint axes.");
        private static readonly GUIContent gc_eyeAimMode = new("Eye Aim Mode", "How eye bones are aimed (Transform strategies).\n\n• Auto: detect mirrored/reflected eye bones at init; if any eye is mirrored, direct-drive both eyes; otherwise keep the MultiAimConstraint path.\n• Constraint: always MultiAimConstraint (+ Auto-correct Eye Aim Axis). Cannot aim mirrored eye bones.\n• Direct Universal: always rest-calibrated direct drive. Robust to any axis/scale incl. mirrored bones; runs in LateUpdate.");
        private static readonly GUIContent[] gc_eyeStrategyOptions = { new("Blend Weight (Fluentt)"), new("Transform") };

        private void DrawLookTargetSettings()
        {
            var controller = (FluentTAvatarControllerFloatingHead)target;

            EditorGUILayout.LabelField("Look Target Settings", EditorStyles.boldLabel);

            // Track enableLookTarget changes
            bool wasEnabled = enableLookTargetProp.boolValue;

            EditorGUILayout.PropertyField(enableLookTargetProp);

            // Auto-setup rig structure when enableLookTarget is turned on
            if (enableLookTargetProp.boolValue && !wasEnabled)
            {
                serializedObject.ApplyModifiedProperties();
                SetupLookTargetRig(controller);
                serializedObject.Update();
            }
            // Disable rig when turned off
            else if (!enableLookTargetProp.boolValue && wasEnabled)
            {
                serializedObject.ApplyModifiedProperties();
                DisableLookTargetRig(controller);
                serializedObject.Update();
            }

            EditorGUILayout.PropertyField(lookTargetProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cached Head Renderers", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(headSkinnedMeshRenderersProp);

            if (GUILayout.Button("Find Head SkinnedMeshRenderers"))
            {
                // Find all SkinnedMeshRenderers (self + children) that have blend shapes
                var headRenderers = new List<SkinnedMeshRenderer>();

                // Check self first
                if (controller.TryGetComponent<SkinnedMeshRenderer>(out var selfSkmr))
                {
                    if (selfSkmr.sharedMesh != null && selfSkmr.sharedMesh.blendShapeCount > 0)
                    {
                        headRenderers.Add(selfSkmr);
                    }
                }

                // Then check children
                var skinnedMeshRenderers = controller.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
                {
                    if (skinnedMeshRenderer.sharedMesh != null && skinnedMeshRenderer.sharedMesh.blendShapeCount > 0)
                    {
                        headRenderers.Add(skinnedMeshRenderer);
                    }
                }

                SetFieldValue(controller, "headSkinnedMeshRenderers", headRenderers);

                EditorUtility.SetDirty(target);
                serializedObject.Update();
                Debug.Log($"{LogPrefix} Found {headRenderers.Count} SkinnedMeshRenderers with blend shapes");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Look Target Transforms", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(lookHeadProp);

            if (GUILayout.Button("Find Look Target Transforms"))
            {
                controller.FindLookTargetTransforms();
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animation Rigging Constraints", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(headAimConstraintProp);
            EditorGUILayout.PropertyField(leftEyeAimConstraintProp);
            EditorGUILayout.PropertyField(rightEyeAimConstraintProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Eye Transforms", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(lookLeftEyeBallProp);
            EditorGUILayout.PropertyField(lookRightEyeBallProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Look Target Strategy Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(idleLookSettingsProp, gc_idleLook);
            EditorGUILayout.PropertyField(talkingLookSettingsProp, gc_talkingLook);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Head Control Settings", EditorStyles.boldLabel);

            // Track enableHeadControl changes
            bool wasHeadEnabled = enableHeadControlProp.boolValue;
            EditorGUILayout.PropertyField(enableHeadControlProp);

            // Handle Head Control toggle (only in Editor, not Play mode)
            if (!Application.isPlaying && enableHeadControlProp.boolValue != wasHeadEnabled)
            {
                serializedObject.ApplyModifiedProperties();
                if (enableHeadControlProp.boolValue)
                {
                    SetupHeadTrackingOnly(controller);
                }
                else
                {
                    RemoveHeadTrackingOnly(controller);
                }
                serializedObject.Update();
            }

            EditorGUILayout.PropertyField(headSpeedProp);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(headAngleLimitProp, gc_headAngleLimit);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                ApplyAngleLimitsInEditor(controller);
                serializedObject.Update();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Eye Control Settings", EditorStyles.boldLabel);

            // Track enableEyeControl changes
            bool wasEyeEnabled = enableEyeControlProp.boolValue;
            EditorGUILayout.PropertyField(enableEyeControlProp);

            // Eye control strategy selection (2-item dropdown).
            // TransformCorrected is kept in the enum for backward-compat but hidden behind "Transform";
            // existing TransformCorrected/Transform values both display as "Transform" and are only
            // rewritten when the user actually changes the dropdown (so saved values are preserved).
            EEyeControlStrategy curStrategy = (EEyeControlStrategy)eyeControlStrategyProp.enumValueIndex;
            int displayIndex = curStrategy == EEyeControlStrategy.BlendWeightFluentt ? 0 : 1;
            int newIndex = EditorGUILayout.Popup(gc_eyeStrategy, displayIndex, gc_eyeStrategyOptions);
            if (newIndex != displayIndex)
                eyeControlStrategyProp.enumValueIndex = newIndex == 0
                    ? (int)EEyeControlStrategy.BlendWeightFluentt
                    : (int)EEyeControlStrategy.TransformCorrected;

            // Handle Eye Control toggle (only in Editor, not Play mode)
            if (!Application.isPlaying && enableEyeControlProp.boolValue != wasEyeEnabled)
            {
                serializedObject.ApplyModifiedProperties();
                if (enableEyeControlProp.boolValue)
                {
                    SetupEyeTrackingOnly(controller);
                }
                else
                {
                    RemoveEyeTrackingOnly(controller);
                }
                serializedObject.Update();
            }

            // Show different settings based on strategy
            var strategy = (EEyeControlStrategy)eyeControlStrategyProp.enumValueIndex;
            if (strategy == EEyeControlStrategy.BlendWeightFluentt)
            {
                EditorGUILayout.HelpBox(
                    "BlendShape Strategy: Controls eye movement using blend shapes (eyeLookUp, eyeLookDown, etc.)\n" +
                    "Make sure your avatar has the required eye blend shapes configured below.",
                    MessageType.Info);

                // Auto Find button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Auto Find Eye BlendShapes", GUILayout.Width(200), GUILayout.Height(25)))
                {
                    AutoFindEyeBlendShapes(controller);
                    serializedObject.Update();
                    EditorUtility.SetDirty(controller);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(5);

                EditorGUILayout.PropertyField(eyeBlendShapesProp, gc_eyeBlendShapes);
                EditorGUILayout.PropertyField(eyeAngleLimitProp, gc_eyeAngleLimit);
                EditorGUILayout.PropertyField(eyeAngleLimitThresholdProp, gc_eyeAngleThreshold);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Transform Strategy: Controls eye movement by rotating eye bone Transforms using Animation Rigging.\n" +
                    "Multi-Aim Constraints above must be configured.",
                    MessageType.Info);

                EditorGUILayout.PropertyField(eyeAimModeProp, gc_eyeAimMode);
                var aimMode = (EEyeAimMode)eyeAimModeProp.enumValueIndex;
                bool mirrored = AnyEyeBoneReflected();
                if (aimMode == EEyeAimMode.DirectUniversal || (aimMode == EEyeAimMode.Auto && mirrored))
                {
                    EditorGUILayout.HelpBox(
                        aimMode == EEyeAimMode.Auto
                            ? "Mirrored (negative-scale) eye bone detected — eyes will be direct-driven (rest-calibrated) at runtime; the eye Multi-Aim Constraints are disabled. The head keeps its constraint."
                            : "Direct (Universal) eye-aim: eyes are driven directly (rest-calibrated) for any bone axis/scale incl. mirrored bones. The eye Multi-Aim Constraints are disabled at runtime; the head keeps its constraint.",
                        MessageType.None);
                }
                else
                {
                    // Constraint path is in effect (Constraint mode, or Auto with no mirror detected).
                    if (aimMode == EEyeAimMode.Auto)
                        EditorGUILayout.HelpBox("No mirrored eye bone detected — the Multi-Aim Constraint path will be used.", MessageType.None);
                    EditorGUILayout.PropertyField(autoDetectEyeAimAxisProp, gc_autoDetectEyeAim);
                    DrawEyeTwistDiagnostics();
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(eyeTransformAngleLimitProp, gc_eyeAngleLimitTransform);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    ApplyAngleLimitsInEditor(controller);
                    serializedObject.Update();
                }
            }

            EditorGUILayout.PropertyField(eyeSpeedProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gizmo Visualization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(showTargetGizmosProp);

            if (showTargetGizmosProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(actualTargetGizmoSizeProp);
                EditorGUILayout.PropertyField(headVirtualTargetGizmoSizeProp);
                EditorGUILayout.PropertyField(eyeVirtualTargetGizmoSizeProp);
                EditorGUILayout.PropertyField(actualTargetColorProp);
                EditorGUILayout.PropertyField(headVirtualTargetColorProp);
                EditorGUILayout.PropertyField(eyeVirtualTargetColorProp);
                EditorGUI.indentLevel--;
            }

            // Runtime Rig Control (Play mode only)
            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Runtime Rig Control", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Setup Rig"))
                    controller.SetupLookTargetRigAtRuntime();
                if (GUILayout.Button("Destroy Rig"))
                    controller.DestroyLookTargetRigAtRuntime();
                if (GUILayout.Button("Rebuild"))
                    controller.RebuildRig();
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Warn (edit mode only) when an eye bone is "twisted" — its local +Z is not the gaze axis.
        /// Such rigs need auto aim-axis correction or left/right eye tracking will not work.
        /// </summary>
        private void DrawEyeTwistDiagnostics()
        {
            if (Application.isPlaying) return;
            Transform head = lookHeadProp.objectReferenceValue as Transform;
            Transform leftEye = lookLeftEyeBallProp.objectReferenceValue as Transform;
            Transform rightEye = lookRightEyeBallProp.objectReferenceValue as Transform;
            if (head == null || (leftEye == null && rightEye == null)) return;
            Vector3 gazeRef = head.forward;
            bool twisted = IsBoneTwisted(leftEye, gazeRef) || IsBoneTwisted(rightEye, gazeRef);
            if (!twisted) return;
            if (autoDetectEyeAimAxisProp.boolValue)
            {
                EditorGUILayout.HelpBox("Twisted eye bone detected — auto aim-axis correction is ON and will be applied at runtime.", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("Twisted eye bone detected (bone local +Z is not the gaze axis). 'Auto-correct Eye Aim Axis' is OFF, so horizontal (left/right) eye tracking will NOT work.", MessageType.Warning);
                if (GUILayout.Button("Enable Auto-correct Eye Aim Axis"))
                {
                    autoDetectEyeAimAxisProp.boolValue = true;
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        private static bool IsBoneTwisted(Transform bone, Vector3 gazeWorld)
        {
            if (bone == null) return false;
            Vector3 gazeLocal = (Quaternion.Inverse(bone.rotation) * gazeWorld).normalized;
            return Vector3.Dot(gazeLocal, Vector3.forward) < 0.99f;
        }

        /// <summary>
        /// True when either assigned eye bone has a mirrored/reflected (left-handed) basis
        /// (matrix determinant &lt; 0) — which MultiAimConstraint cannot aim. Edit-mode preview only.
        /// </summary>
        private bool AnyEyeBoneReflected()
        {
            var l = lookLeftEyeBallProp.objectReferenceValue as Transform;
            var r = lookRightEyeBallProp.objectReferenceValue as Transform;
            return (l != null && l.localToWorldMatrix.determinant < 0f) ||
                   (r != null && r.localToWorldMatrix.determinant < 0f);
        }
    }
}
#endif
