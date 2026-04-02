using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
        private static readonly GUIContent gc_enableServerMotion = new("Enable Server Motion Tagging", "Also receive and play emotion tags from server at exact timing");
        private static readonly GUIContent gc_autoReset = new("Auto Reset on Speech End", "Automatically blend gesture back to Idle when the last sentence finishes");
        private static readonly GUIContent gc_gestureMappings = new("Gesture Mappings", "Map emotion tags to gesture animations with per-clip weight and override settings");

        private void DrawGestureAnimationSettings()
        {
            EditorGUILayout.LabelField("Gesture Animation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Gesture Animation\n\n" +
                "Maps emotion tags to gesture animations.\n" +
                "Used by both Text Emotion Detection and Server Motion Tagging.\n\n" +
                "• Each tag can have multiple clips with weights for random variant selection\n" +
                "• Per-clip overrideEyeControl and overrideEyeBlink settings\n\n" +
                "Setup:\n" +
                "1. Add emotion tag entries (e.g. tag: \"강조\")\n" +
                "2. Assign one or more clips per tag with weight\n" +
                "3. Enable Server Motion Tagging if server provides emotion tags",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(enableServerMotionTaggingProp, gc_enableServerMotion);
            EditorGUILayout.PropertyField(enableAutoEmotionResetProp, gc_autoReset);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tag → Gesture Mappings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(gestureMappingsProp, gc_gestureMappings);
            AutoDetectOverridesForGestures(gestureMappingsProp);

            // Drag & drop for each gesture mapping's clips list
            DrawDragDropForGestureMappings(gestureMappingsProp);
        }

        /// <summary>
        /// Auto-detect eye BlendShape and blink curves in GestureMapping clips.
        /// Each entry in clips has its own overrideEyeControl and overrideEyeBlink.
        /// </summary>
        private void AutoDetectOverridesForGestures(SerializedProperty mappingsProp)
        {
            if (mappingsProp == null) return;

            for (int i = 0; i < mappingsProp.arraySize; i++)
            {
                var mapping = mappingsProp.GetArrayElementAtIndex(i);
                var clipsProp = mapping.FindPropertyRelative("clips");
                if (clipsProp == null) continue;

                for (int j = 0; j < clipsProp.arraySize; j++)
                {
                    var entryProp = clipsProp.GetArrayElementAtIndex(j);
                    AutoDetectOverridesForEntry(entryProp);
                }
            }
        }

        /// <summary>
        /// Draw drag-drop zones for each expanded gesture mapping's clips list.
        /// </summary>
        private void DrawDragDropForGestureMappings(SerializedProperty mappingsProp)
        {
            if (mappingsProp == null || !mappingsProp.isExpanded) return;

            for (int i = 0; i < mappingsProp.arraySize; i++)
            {
                var mapping = mappingsProp.GetArrayElementAtIndex(i);
                if (!mapping.isExpanded) continue;

                var clipsProp = mapping.FindPropertyRelative("clips");
                if (clipsProp == null) continue;

                var tag = mapping.FindPropertyRelative("emotionTag")?.stringValue ?? $"[{i}]";
                DrawAnimationClipDropArea(clipsProp, $"Drop AnimationClips here ({tag})");
            }
        }
    }
}
