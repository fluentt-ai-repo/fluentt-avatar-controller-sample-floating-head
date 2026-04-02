using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
        private static readonly GUIContent gc_enableServerMotion = new("Enable Server Motion Tagging", "Also receive and play emotion tags from server at exact timing");
        private static readonly GUIContent gc_autoReset = new("Auto Reset on Speech End", "Automatically blend gesture back to Idle when the last sentence finishes");
        private static readonly GUIContent gc_gestureMappings = new("Gesture Mappings", "Map emotion tags to gesture AnimationClip animations");

        private void DrawGestureAnimationSettings()
        {
            EditorGUILayout.LabelField("Gesture Animation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Gesture Animation\n\n" +
                "Maps emotion tags to gesture AnimationClip animations.\n" +
                "Used by both Text Emotion Detection and Server Motion Tagging.\n\n" +
                "• Text Detection: Regex-detected emotion tags trigger gestures at estimated timing\n" +
                "• Server: Server-provided emotion tags trigger gestures at exact timing\n\n" +
                "Setup:\n" +
                "1. Add emotion tag entries (e.g. tag: \"강조\")\n" +
                "2. Assign one or more AnimationClips per tag (random variant selection)\n" +
                "3. Enable Server Motion Tagging if server provides emotion tags",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(enableServerMotionTaggingProp, gc_enableServerMotion);
            EditorGUILayout.PropertyField(enableAutoEmotionResetProp, gc_autoReset);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tag → Gesture Mappings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(gestureMappingsProp, gc_gestureMappings);
            AutoDetectEyeOverrideForGestures(gestureMappingsProp);
        }

        /// <summary>
        /// Auto-detect eye BlendShape curves in GestureMapping clips.
        /// If any clip in the mapping has eye curves, auto-set overrideEyeControl.
        /// </summary>
        private void AutoDetectEyeOverrideForGestures(SerializedProperty mappingsProp)
        {
            if (mappingsProp == null) return;

            for (int i = 0; i < mappingsProp.arraySize; i++)
            {
                var mapping = mappingsProp.GetArrayElementAtIndex(i);
                var clipsProp = mapping.FindPropertyRelative("animationClips");
                var overrideProp = mapping.FindPropertyRelative("overrideEyeControl");
                if (clipsProp == null || overrideProp == null) continue;

                for (int j = 0; j < clipsProp.arraySize; j++)
                {
                    var clipProp = clipsProp.GetArrayElementAtIndex(j);
                    AutoDetectEyeOverrideForGestureClip(clipProp, overrideProp);
                }
            }
        }

        /// <summary>
        /// Check if a gesture clip reference changed and auto-set overrideEyeControl.
        /// Uses the shared previousClipInstanceIds dictionary from OneShotMotion editor.
        /// </summary>
        private void AutoDetectEyeOverrideForGestureClip(SerializedProperty clipProp, SerializedProperty overrideProp)
        {
            if (clipProp == null || overrideProp == null) return;

            var clip = clipProp.objectReferenceValue as AnimationClip;
            int currentId = clip != null ? clip.GetInstanceID() : 0;
            string key = clipProp.propertyPath;

            if (previousClipInstanceIds.TryGetValue(key, out int prevId))
            {
                if (prevId != currentId && clip != null)
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    bool hasEyeCurves = bindings.Any(b =>
                        EYE_BLEND_SHAPE_PREFIXES.Any(prefix => b.propertyName.StartsWith(prefix)));
                    if (hasEyeCurves)
                    {
                        overrideProp.boolValue = true;
                    }
                }
            }

            previousClipInstanceIds[key] = currentId;
        }
    }
}
