using System.Collections;
using FluentT.Animation;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Eye Blink control partial class
    /// TMAnimationClip-based eye blinking with customizable animation
    /// </summary>
    public partial class FluentTAvatarControllerFloatingHead
    {
        private Coroutine blinkCoroutine;
        private TMAnimationClip activeBlinkClip;

        // Blink animation layer index (uses external layer for eye blink)
        // Internal layers (negative): -3=Base face, -2=Face animation, -1=Head animation
        // External layers (0+): Available for user/controller use
        private const int BLINK_LAYER_INDEX = 0;

        #region Eye Blink Initialization

        private void InitializeEyeBlink()
        {
            if (!enableEyeBlink)
                return;

            if (avatar == null)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Avatar reference not set");
                return;
            }

            // Use provided clip or create default
            activeBlinkClip = blinkClip ?? CreateDefaultBlinkClip();

            if (activeBlinkClip == null)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Failed to create blink clip");
                return;
            }

            // Start blink coroutine
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
            }
            blinkCoroutine = StartCoroutine(BlinkRoutine());

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Eye blink initialized with clip: {activeBlinkClip.name}");
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

        #endregion

        #region Eye Blink Coroutine

        /// <summary>
        /// Main blink loop - runs continuously while enabled
        /// </summary>
        private IEnumerator BlinkRoutine()
        {
            while (true)
            {
                // Wait for random interval before next blink
                float variance = Random.Range(-blinkIntervalVariance, blinkIntervalVariance);
                float delay = Mathf.Max(0.1f, blinkInterval + variance);
                yield return new WaitForSeconds(delay);

                // Play blink animation
                PlayBlinkAnimation();
            }
        }

        /// <summary>
        /// Play the blink animation clip
        /// </summary>
        private void PlayBlinkAnimation()
        {
            if (avatar == null || avatar.TMAnimationComponent == null || activeBlinkClip == null)
                return;

            // Play on dedicated blink layer with Override mode
            avatar.TMAnimationComponent.Play(
                activeBlinkClip,
                BLINK_LAYER_INDEX,
                TMAnimationLayer.UpdatePhase.LateUpdate,
                startImmediately: true
            );
        }

        #endregion

        #region Eye Blink Control

        /// <summary>
        /// Enable or disable eye blink at runtime
        /// </summary>
        public void SetEyeBlinkEnabled(bool enabled)
        {
            if (enableEyeBlink == enabled)
                return;

            enableEyeBlink = enabled;

            if (enabled)
            {
                // Initialize if not already done
                if (activeBlinkClip == null)
                {
                    InitializeEyeBlink();
                }
                else if (blinkCoroutine == null)
                {
                    blinkCoroutine = StartCoroutine(BlinkRoutine());
                }
            }
            else
            {
                // Stop blinking
                if (blinkCoroutine != null)
                {
                    StopCoroutine(blinkCoroutine);
                    blinkCoroutine = null;
                }

                // Stop any playing blink animation
                if (avatar != null && avatar.TMAnimationComponent != null)
                {
                    var layer = avatar.TMAnimationComponent.GetLayer(BLINK_LAYER_INDEX, TMAnimationLayer.UpdatePhase.LateUpdate);
                    layer?.Stop();
                }
            }
        }

        /// <summary>
        /// Set a custom blink clip at runtime
        /// </summary>
        public void SetBlinkClip(TMAnimationClip clip)
        {
            blinkClip = clip;
            activeBlinkClip = clip ?? CreateDefaultBlinkClip();
        }

        /// <summary>
        /// Trigger a single blink immediately
        /// </summary>
        public void TriggerBlink()
        {
            if (activeBlinkClip == null)
            {
                activeBlinkClip = blinkClip ?? CreateDefaultBlinkClip();
            }
            PlayBlinkAnimation();
        }

        #endregion
    }
}
