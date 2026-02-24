using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
        private void DrawEmotionTaggingSettings()
        {
            EditorGUILayout.LabelField("Text Emotion Detection", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Text Emotion Detection\n\n" +
                "Detects emotions from subtitle text using client-side regex pattern matching.\n\n" +
                "How it works:\n" +
                "1. When subtitle text is built, it is analyzed using regex patterns\n" +
                "2. Matched emotions are selected via weighted probability\n" +
                "3. Detected tags trigger gesture animations configured in the 'Gesture Animation' tab\n\n" +
                "Setup:\n" +
                "1. Create an EmotionKeywordDataset asset (Assets > Create > FluentT > Emotion Keyword Dataset)\n" +
                "2. Add regex patterns mapped to emotion tags (e.g. pattern: \"happy|glad\", tag: \"happy\")\n" +
                "3. Configure gesture mappings in the 'Gesture Animation' tab",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableTextEmotionDetection"),
                new GUIContent("Enable Text Emotion Detection", "Enable client-side regex-based emotion detection from subtitle text"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Emotion Detection", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxEmotionTagsPerSentence"),
                new GUIContent("Max Tags Per Sentence", "Maximum number of emotion tags to apply per sentence"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("emotionKeywordDataset"),
                new GUIContent("Keyword Dataset", "ScriptableObject containing regex patterns mapped to emotion tags"));
        }
    }
}
