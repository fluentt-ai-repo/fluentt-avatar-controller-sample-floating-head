using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Eye Blink control partial class
    /// Simple coroutine-based eye blinking without Timeline dependency
    /// </summary>
    public partial class FluentTAvatarSampleController
    {
        private Coroutine blinkCoroutine;
        private List<BlendShapeInfo> blinkBlendShapes = new List<BlendShapeInfo>();

        /// <summary>
        /// Stores blend shape information for efficient access
        /// </summary>
        private class BlendShapeInfo
        {
            public SkinnedMeshRenderer renderer;
            public int blendShapeIndex;
            public string blendShapeName;

            public BlendShapeInfo(SkinnedMeshRenderer renderer, int index, string name)
            {
                this.renderer = renderer;
                this.blendShapeIndex = index;
                this.blendShapeName = name;
            }
        }

        #region Eye Blink Initialization

        private void InitializeEyeBlink()
        {
            if (!enableEyeBlink)
                return;

            if (avatar == null)
            {
                Debug.LogWarning("[FluentTAvatarSampleController] Avatar reference not set");
                return;
            }

            // Find blend shapes for eye blink (eyeBlinkLeft, eyeBlinkRight)
            FindBlinkBlendShapes();

            if (blinkBlendShapes.Count == 0)
            {
                Debug.LogWarning("[FluentTAvatarSampleController] No eye blink blend shapes found (eyeBlinkLeft, eyeBlinkRight)");
                return;
            }

            // Start blink coroutine
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
            }
            blinkCoroutine = StartCoroutine(BlinkRoutine());

            Debug.Log($"[FluentTAvatarSampleController] Eye blink initialized with {blinkBlendShapes.Count} blend shapes");
        }

        /// <summary>
        /// Find eyeBlinkLeft and eyeBlinkRight blend shapes in avatar's skinned mesh renderers
        /// </summary>
        private void FindBlinkBlendShapes()
        {
            blinkBlendShapes.Clear();

            if (head_skmr == null || head_skmr.Count == 0)
                return;

            string[] blinkShapeNames = { "eyeBlinkLeft", "eyeBlinkRight" };

            foreach (var skmr in head_skmr)
            {
                if (skmr == null || skmr.sharedMesh == null)
                    continue;

                for (int i = 0; i < skmr.sharedMesh.blendShapeCount; i++)
                {
                    string shapeName = skmr.sharedMesh.GetBlendShapeName(i);
                    foreach (string targetName in blinkShapeNames)
                    {
                        if (shapeName == targetName)
                        {
                            blinkBlendShapes.Add(new BlendShapeInfo(skmr, i, shapeName));
                            Debug.Log($"[FluentTAvatarSampleController] Found blink blend shape: {shapeName} at index {i}");
                            break;
                        }
                    }
                }
            }
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

                // Perform blink animation
                yield return StartCoroutine(PerformBlink());
            }
        }

        /// <summary>
        /// Performs a single blink animation
        /// Natural timing: close quickly (0.06s) -> hold (0.02s) -> open slowly (0.10s)
        /// </summary>
        private IEnumerator PerformBlink()
        {
            const float closeTime = 0.06f;  // Close quickly
            const float holdTime = 0.02f;   // Hold closed briefly
            const float openTime = 0.10f;   // Open slowly
            const float maxWeight = 100f;   // Full close

            // Phase 1: Close eyes quickly (0 -> 100)
            float elapsed = 0f;
            while (elapsed < closeTime)
            {
                float t = elapsed / closeTime;
                float weight = Mathf.Lerp(0f, maxWeight, t);
                SetBlinkWeight(weight);
                elapsed += Time.deltaTime;
                yield return null;
            }
            SetBlinkWeight(maxWeight);

            // Phase 2: Hold closed
            yield return new WaitForSeconds(holdTime);

            // Phase 3: Open eyes slowly (100 -> 0)
            elapsed = 0f;
            while (elapsed < openTime)
            {
                float t = elapsed / openTime;
                float weight = Mathf.Lerp(maxWeight, 0f, t);
                SetBlinkWeight(weight);
                elapsed += Time.deltaTime;
                yield return null;
            }
            SetBlinkWeight(0f);
        }

        /// <summary>
        /// Set blend shape weight for all blink blend shapes
        /// </summary>
        private void SetBlinkWeight(float weight)
        {
            foreach (var info in blinkBlendShapes)
            {
                if (info.renderer != null)
                {
                    info.renderer.SetBlendShapeWeight(info.blendShapeIndex, weight);
                }
            }
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
                // Start or resume blinking
                if (blinkBlendShapes.Count == 0)
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
                // Stop blinking and reset eyes to open
                if (blinkCoroutine != null)
                {
                    StopCoroutine(blinkCoroutine);
                    blinkCoroutine = null;
                }
                SetBlinkWeight(0f);
            }
        }

        #endregion
    }
}
