using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
        private void DrawServerMotionTaggingSettings()
        {
            EditorGUILayout.LabelField("Server-Side Motion Tagging Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Server-Side Motion Tagging\n\n" +
                "How it works:\n" +
                "1. Server sends emotion tags at specific timestamps during speech\n" +
                "2. Tags are received via onServerMotionTag callback at exact timing\n" +
                "3. Triggers Unity AnimationClip animations for body movement\n\n" +
                "Example:\n" +
                "• Server tag 'nod' at 0.5s → Play nodding animation\n" +
                "• Server tag 'wave' at 2.0s → Play waving animation\n\n" +
                "Note: Uses Unity AnimationClip for body animations (full skeleton).",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableServerMotionTagging"),
                new GUIContent("Enable Server Motion Tagging", "Enable server-side motion tagging system"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Motion Tag Mappings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("serverMotionTagMappings"),
                new GUIContent("Server Motion Tag Mappings", "Map server emotion tags to AnimationClip animations"));
        }
    }
}
