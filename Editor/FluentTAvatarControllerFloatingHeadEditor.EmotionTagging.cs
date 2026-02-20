using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
        private void DrawEmotionTaggingSettings()
        {
            EditorGUILayout.LabelField("Client-Side Emotion Tagging Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Client-Side Emotion Tagging\n\n" +
                "How it works:\n" +
                "1. When a sentence starts, the text is analyzed using regex patterns\n" +
                "2. Matched emotions are selected via weighted probability\n" +
                "3. Emotion tags are passed to Emotion Motion Mapping for animation playback\n\n" +
                "Setup:\n" +
                "1. Create an EmotionKeywordDataset asset (Assets > Create > FluentT > Emotion Keyword Dataset)\n" +
                "2. Add regex patterns mapped to emotion tags (e.g. pattern: \"happy|glad\", tag: \"happy\")\n" +
                "3. Configure animation mappings in the 'Emotion Motion Mapping' tab",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableClientEmotionTagging"),
                new GUIContent("Enable Client Emotion Tagging", "Enable client-side regex-based emotion tagging"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Emotion Detection", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxEmotionTagsPerSentence"),
                new GUIContent("Max Tags Per Sentence", "Maximum number of emotion tags to apply per sentence"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("emotionKeywordDataset"),
                new GUIContent("Keyword Dataset", "ScriptableObject containing regex patterns mapped to emotion tags"));
        }
    }
}
