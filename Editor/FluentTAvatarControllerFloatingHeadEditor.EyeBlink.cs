using FluentT.Animation;
using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
        private static readonly GUIContent gc_enableBlink = new("Enable Eye Blink", "Enable automatic eye blinking");
        private static readonly GUIContent gc_blinkClip = new("Blink Clip", "Custom TMAnimationClip for eye blink. If null, uses default ARKit eyeBlink animation.");
        private static readonly GUIContent gc_blinkBlendMode = new("Blend Mode", "How blink layer values combine with face animation.\n\n\u2022 Override: Replace current value\n\u2022 Additive: Add to current value\n\u2022 SoftMax2D: Soft blend (prevents saturation)\n\u2022 Max: Take the larger value");
        private static readonly GUIContent gc_blinkInterval = new("Blink Interval", "Average time between blinks (seconds)");
        private static readonly GUIContent gc_blinkVariance = new("Interval Variance", "Random variance in blink timing (\u00b1seconds)");

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

            EditorGUILayout.PropertyField(enableEyeBlinkProp, gc_enableBlink);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Blink Animation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(blinkClipProp, gc_blinkClip);
            EditorGUILayout.PropertyField(blinkBlendModeProp, gc_blinkBlendMode);

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

            blinkIntervalProp.floatValue = EditorGUILayout.FloatField(gc_blinkInterval, blinkIntervalProp.floatValue);
            blinkIntervalVarianceProp.floatValue = EditorGUILayout.FloatField(gc_blinkVariance, blinkIntervalVarianceProp.floatValue);

            // Show calculated range
            float interval = blinkIntervalProp.floatValue;
            float variance = blinkIntervalVarianceProp.floatValue;
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

            const float blinkCloseDuration = 0.06f;
            const float blinkHoldDuration = 0.08f;
            const float blinkOpenDuration = 0.18f;
            const float blinkMaxWeight = 100f;

            // Create curves for both eyes
            string[] blinkShapes = { "eyeBlinkLeft", "eyeBlinkRight" };

            foreach (string shapeName in blinkShapes)
            {
                TMAnimationCurve curve = new()
                {
                    key = shapeName
                };

                // Keyframes: close quickly -> hold -> open slowly
                curve.AddKeyFrame(new TMKeyframe { t = 0f, v = 0f });
                curve.AddKeyFrame(new TMKeyframe { t = blinkCloseDuration, v = blinkMaxWeight });
                curve.AddKeyFrame(new TMKeyframe { t = blinkHoldDuration, v = blinkMaxWeight });
                curve.AddKeyFrame(new TMKeyframe { t = blinkOpenDuration, v = 0f });

                curveData.AddBlendCurve(curve);
            }

            clip.AddCurveData(curveData);

            return clip;
        }
    }
}
