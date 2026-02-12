using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    [CustomEditor(typeof(EmotionKeywordDataset))]
    public class EmotionKeywordDatasetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Show entry count
            var entriesProp = serializedObject.FindProperty("entries");
            EditorGUILayout.LabelField("Emotion Keyword Dataset", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Total entries: {entriesProp.arraySize}\n\n" +
                "Each entry maps a regex pattern to an emotion tag.\n" +
                "Entries with the same emotion tag are combined into a single regex at runtime.",
                MessageType.Info);

            EditorGUILayout.Space();

            // Draw default inspector for entries list
            EditorGUILayout.PropertyField(entriesProp, new GUIContent("Keyword Entries"), true);

            EditorGUILayout.Space();

            // Import/Export buttons
            EditorGUILayout.LabelField("JSON Import / Export", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Import JSON", GUILayout.Height(30)))
            {
                ImportFromJson();
            }

            if (GUILayout.Button("Export JSON", GUILayout.Height(30)))
            {
                ExportToJson();
            }

            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private void ImportFromJson()
        {
            string path = EditorUtility.OpenFilePanel("Import Emotion Keywords JSON", "", "json");
            if (string.IsNullOrEmpty(path))
                return;

            string json = System.IO.File.ReadAllText(path);

            try
            {
                JsonUtility.FromJsonOverwrite(json, target);
                EditorUtility.SetDirty(target);
                serializedObject.Update();

                var dataset = (EmotionKeywordDataset)target;
                Debug.Log($"[EmotionKeywordDataset] Imported {dataset.Entries.Count} entries from {path}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmotionKeywordDataset] Failed to import JSON: {ex.Message}");
                EditorUtility.DisplayDialog("Import Failed", $"Failed to parse JSON file:\n{ex.Message}", "OK");
            }
        }

        private void ExportToJson()
        {
            string path = EditorUtility.SaveFilePanel("Export Emotion Keywords JSON", "", "EmotionKeywordDataset", "json");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                string json = JsonUtility.ToJson(target, true);
                System.IO.File.WriteAllText(path, json);

                var dataset = (EmotionKeywordDataset)target;
                Debug.Log($"[EmotionKeywordDataset] Exported {dataset.Entries.Count} entries to {path}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmotionKeywordDataset] Failed to export JSON: {ex.Message}");
                EditorUtility.DisplayDialog("Export Failed", $"Failed to write JSON file:\n{ex.Message}", "OK");
            }
        }
    }
}
