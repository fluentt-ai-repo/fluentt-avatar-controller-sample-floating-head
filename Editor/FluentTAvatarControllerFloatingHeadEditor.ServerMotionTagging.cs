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
        }
    }
}
