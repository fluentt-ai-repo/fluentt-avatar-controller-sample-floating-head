using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
        private void DrawEmotionMotionMappingSettings()
        {
            EditorGUILayout.LabelField("Emotion Motion Mapping", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Emotion Motion Mapping\n\n" +
                "Maps emotion tags to AnimationClip animations.\n" +
                "Used by both Client Emotion Tagging and Server Motion Tagging.\n\n" +
                "• Client: Regex-detected emotion tags trigger animations at estimated timing\n" +
                "• Server: Server-provided emotion tags trigger animations at exact timing\n\n" +
                "Setup:\n" +
                "1. Add emotion tag entries (e.g. tag: \"강조\", clip: emphasis_anim)\n" +
                "2. Assign AnimationClip and blend weight for each tag\n" +
                "3. Enable Server Motion Tagging if server provides emotion tags",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableServerMotionTagging"),
                new GUIContent("Enable Server Motion Tagging", "Also receive and play emotion tags from server at exact timing"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tag → Animation Mappings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("emotionMotionMappings"),
                new GUIContent("Emotion Motion Mappings", "Map emotion tags to AnimationClip animations"));
        }
    }
}
