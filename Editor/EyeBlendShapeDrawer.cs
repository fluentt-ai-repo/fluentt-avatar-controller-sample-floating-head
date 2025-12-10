using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    [CustomPropertyDrawer(typeof(EyeBlendShape))]
    public class EyeBlendShapeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 2f;

            // Draw label
            Rect labelRect = new Rect(position.x, position.y, position.width, lineHeight);
            EditorGUI.LabelField(labelRect, label, EditorStyles.boldLabel);

            // Don't make child fields be indented
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;

            // Line 1: SkinnedMeshRenderer (read-only)
            Rect skmrRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);

            // Line 2: BlendShape dropdown, index, scale
            float fieldSpacing = 5f;
            float scaleWidth = 50f;
            float idxWidth = 50f;
            float nameWidth = position.width - idxWidth - scaleWidth - fieldSpacing * 2;

            Rect nameRect = new Rect(position.x, position.y + lineHeight * 2 + spacing * 2, nameWidth, lineHeight);
            Rect idxRect = new Rect(position.x + nameWidth + fieldSpacing, position.y + lineHeight * 2 + spacing * 2, idxWidth, lineHeight);
            Rect scaleRect = new Rect(position.x + nameWidth + idxWidth + fieldSpacing * 2, position.y + lineHeight * 2 + spacing * 2, scaleWidth, lineHeight);

            // Get properties
            var skmrProp = property.FindPropertyRelative("skmr");
            var blendShapeNameProp = property.FindPropertyRelative("blendShapeName");
            var blendShapeIdxProp = property.FindPropertyRelative("blendShapeIdx");
            var scaleProp = property.FindPropertyRelative("scale");

            // Draw SkinnedMeshRenderer (editable)
            EditorGUI.PropertyField(skmrRect, skmrProp, new GUIContent("SKMR"));

            // Draw blend shape dropdown
            SkinnedMeshRenderer skmr = skmrProp.objectReferenceValue as SkinnedMeshRenderer;
            if (skmr != null && skmr.sharedMesh != null)
            {
                var mesh = skmr.sharedMesh;
                int blendShapeCount = mesh.blendShapeCount;

                if (blendShapeCount > 0)
                {
                    // Build blend shape names array
                    string[] blendShapeNames = new string[blendShapeCount];
                    for (int i = 0; i < blendShapeCount; i++)
                    {
                        blendShapeNames[i] = mesh.GetBlendShapeName(i);
                    }

                    // Current index
                    int currentIdx = blendShapeIdxProp.intValue;
                    if (currentIdx < 0 || currentIdx >= blendShapeCount)
                        currentIdx = 0;

                    // Draw popup
                    EditorGUI.BeginChangeCheck();
                    int newIdx = EditorGUI.Popup(nameRect, currentIdx, blendShapeNames);
                    if (EditorGUI.EndChangeCheck())
                    {
                        blendShapeIdxProp.intValue = newIdx;
                        blendShapeNameProp.stringValue = mesh.GetBlendShapeName(newIdx);
                    }

                    // Draw index (read-only)
                    GUI.enabled = false;
                    EditorGUI.IntField(idxRect, newIdx);
                    GUI.enabled = true;
                }
                else
                {
                    // No blend shapes available
                    GUI.enabled = false;
                    EditorGUI.TextField(nameRect, "No Blend Shapes");
                    EditorGUI.IntField(idxRect, 0);
                    GUI.enabled = true;
                }
            }
            else
            {
                // No SkinnedMeshRenderer set
                GUI.enabled = false;
                EditorGUI.TextField(nameRect, blendShapeNameProp.stringValue);
                EditorGUI.IntField(idxRect, blendShapeIdxProp.intValue);
                GUI.enabled = true;
            }

            // Draw scale
            EditorGUI.PropertyField(scaleRect, scaleProp, GUIContent.none);

            // Set indent back to what it was
            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Label + SKMR line + BlendShape line
            return EditorGUIUtility.singleLineHeight * 3 + 4f; // 4f for spacing
        }
    }
}
