using FluentT.Animation;
using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
        private void DrawEyeBlinkSettings()
        {
            EditorGUILayout.LabelField("Eye Blink Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "TMAnimationClip-based automatic eye blinking.\n\n" +
                "How it works:\n" +
                "1. Uses TMAnimationClip for flexible blink animations\n" +
                "2. Plays on dedicated animation layer (Layer 2) via TMAnimationComponent\n" +
                "3. Default timing: close quickly (0.06s) → hold (0.02s) → open slowly (0.10s)\n" +
                "4. Custom blink clips can be assigned for different avatar types\n\n" +
                "Requirements:\n" +
                "• Default clip uses 'eyeBlinkLeft' and 'eyeBlinkRight' ARKit blend shapes\n" +
                "• Custom clips can use any blend shapes supported by your avatar",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableEyeBlink"),
                new GUIContent("Enable Eye Blink", "Enable automatic eye blinking"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Blink Animation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("blinkClip"),
                new GUIContent("Blink Clip", "Custom TMAnimationClip for eye blink. If null, uses default ARKit eyeBlink animation."));

            // Create Blink Clip button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Create Default Blink Clip", GUILayout.Width(180)))
            {
                var blinkController = (FluentTAvatarControllerFloatingHead)target;
                blinkController.blinkClip = CreateDefaultBlinkClip();

                EditorUtility.SetDirty(blinkController);
                serializedObject.Update();
                Debug.Log($"{LogPrefix} Created default blink clip with ARKit eyeBlinkLeft/Right");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Timing Settings", EditorStyles.boldLabel);

            var blinkIntervalProp = serializedObject.FindProperty("blinkInterval");
            blinkIntervalProp.floatValue = EditorGUILayout.FloatField(
                new GUIContent("Blink Interval", "Average time between blinks (seconds)"),
                blinkIntervalProp.floatValue);

            var blinkVarianceProp = serializedObject.FindProperty("blinkIntervalVariance");
            blinkVarianceProp.floatValue = EditorGUILayout.FloatField(
                new GUIContent("Interval Variance", "Random variance in blink timing (±seconds)"),
                blinkVarianceProp.floatValue);

            // Show calculated range
            float interval = serializedObject.FindProperty("blinkInterval").floatValue;
            float variance = serializedObject.FindProperty("blinkIntervalVariance").floatValue;
            float minInterval = Mathf.Max(0.1f, interval - variance);
            float maxInterval = interval + variance;
            EditorGUILayout.HelpBox(
                $"Blink will occur every {minInterval:F1}s to {maxInterval:F1}s\n" +
                $"Average: {interval:F1}s",
                MessageType.None);
        }

        /// <summary>
        /// Create default ARKit-based blink animation clip
        /// Timing: 0s(0) -> 0.06s(100) -> 0.08s(100) -> 0.18s(0)
        /// </summary>
        private TMAnimationClip CreateDefaultBlinkClip()
        {
            TMAnimationClip clip = new()
            {
                name = "DefaultEyeBlink",
                repeat = false
            };

            clip.Type = TMAnimationClip.AnimationType.Humanoid;

            // Create curve data for ARKit format (empty relative path)
            TMCurveData curveData = new("");

            const float maxWeight = 100f;

            // Create curves for both eyes
            string[] blinkShapes = { "eyeBlinkLeft", "eyeBlinkRight" };

            foreach (string shapeName in blinkShapes)
            {
                TMAnimationCurve curve = new()
                {
                    key = shapeName
                };

                // Keyframes: 0s(0) -> 0.06s(100) -> 0.08s(100) -> 0.18s(0)
                curve.AddKeyFrame(new TMKeyframe { t = 0f, v = 0f });
                curve.AddKeyFrame(new TMKeyframe { t = 0.06f, v = maxWeight });
                curve.AddKeyFrame(new TMKeyframe { t = 0.08f, v = maxWeight });
                curve.AddKeyFrame(new TMKeyframe { t = 0.18f, v = 0f });

                curveData.AddBlendCurve(curve);
            }

            clip.AddCurveData(curveData);

            return clip;
        }
    }
}
