using System.Collections.Generic;
using FluentT.APIClient.V3;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Energy Matching partial class.
    /// Compares audio RMS curve vs precomputed motion energy curves to select
    /// the best-matching talking animation clip automatically.
    /// </summary>
    public partial class FluentTAvatarControllerFloatingHead
    {
        // Energy matching constants
        private const int ENERGY_SAMPLE_COUNT = 16;

        #region Data Structures

        private struct MotionEnergyCache
        {
            public int clipIndex;
            public float[] energyCurve;
        }

        private struct BoneTransformData
        {
            public Transform transform;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        #endregion

        // Minimum remaining audio duration (seconds) to attempt energy matching
        private const float MIN_REMAINING_AUDIO = 0.5f;

        // Precomputed motion energy caches
        private List<MotionEnergyCache> motionEnergyCaches;

        // Bone cache for SampleAnimation safety
        private Transform[] cachedBoneTransforms;

        // Current sentence audio RMS (updated per sentence and at each swap point)
        private float[] currentAudioRMSCurve;

        // Cached PCM data for sub-range RMS computation (avoids repeated AudioClip.GetData)
        private float[] cachedPCMData;
        private AudioClip currentSentenceAudioClip;
        private float sentenceAudioStartTime;
        private int cachedPCMSampleCount; // audioClip.samples (mono sample count)
        private int cachedPCMChannels;
        private int cachedPCMFrequency;

        #region Initialization

        /// <summary>
        /// Initialize energy matching system.
        /// Skips if disabled or insufficient talking animations.
        /// Called from Start() after InitializeTalkingAnimations().
        /// </summary>
        private void InitializeEnergyMatching()
        {
            if (!enableEnergyMatching)
                return;

            if (talkingAnimations == null || talkingAnimations.Count < 2)
            {
                Debug.Log("[FluentTAvatarControllerFloatingHead] Energy matching requires 2+ talking clips. Skipping precomputation.");
                return;
            }

            if (animator == null)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Energy matching requires Animator. Disabled.");
                return;
            }

            // Cache bone transforms for SampleAnimation safety
            cachedBoneTransforms = GetComponentsInChildren<Transform>(true);

            PrecomputeMotionEnergyCurves();
        }

        #endregion

        #region Motion Energy Precomputation

        /// <summary>
        /// Precompute motion energy curves for all talking animation clips.
        /// Uses SampleAnimation which modifies actual transforms, so we save/restore all bone state.
        /// </summary>
        private void PrecomputeMotionEnergyCurves()
        {
            motionEnergyCaches = new List<MotionEnergyCache>(talkingAnimations.Count);

            // Save current bone state before SampleAnimation
            BoneTransformData[] savedBones = SaveAllBoneTransforms();
            float[][] savedBlendShapes = SaveBlendShapes();

            for (int i = 0; i < talkingAnimations.Count; i++)
            {
                if (talkingAnimations[i] == null || talkingAnimations[i].clip == null)
                    continue;

                MotionEnergyCache cache = ComputeMotionEnergy(talkingAnimations[i].clip, i);
                motionEnergyCaches.Add(cache);
            }

            // Restore bone state after all precomputation
            RestoreAllBoneTransforms(savedBones);
            RestoreBlendShapes(savedBlendShapes);

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Precomputed energy curves for {motionEnergyCaches.Count} talking clips.");
        }

        /// <summary>
        /// Compute motion energy curve for a single animation clip.
        /// Samples N timepoints, measures bone movement delta between consecutive samples.
        /// </summary>
        private MotionEnergyCache ComputeMotionEnergy(AnimationClip clip, int clipIndex)
        {
            float clipLength = clip.length;
            float[] energyCurve = new float[ENERGY_SAMPLE_COUNT];

            if (clipLength <= 0f || cachedBoneTransforms == null || cachedBoneTransforms.Length == 0)
            {
                return new MotionEnergyCache { clipIndex = clipIndex, energyCurve = energyCurve };
            }

            // Sample N+1 timepoints to get N deltas
            int sampleCount = ENERGY_SAMPLE_COUNT + 1;
            Vector3[][] positionSamples = new Vector3[sampleCount][];

            for (int s = 0; s < sampleCount; s++)
            {
                float time = (clipLength * s) / ENERGY_SAMPLE_COUNT;
                clip.SampleAnimation(animator.gameObject, time);

                positionSamples[s] = new Vector3[cachedBoneTransforms.Length];
                for (int b = 0; b < cachedBoneTransforms.Length; b++)
                {
                    if (cachedBoneTransforms[b] != null)
                        positionSamples[s][b] = cachedBoneTransforms[b].localPosition;
                }
            }

            // Compute energy: sum of sqrMagnitude deltas between consecutive samples
            for (int s = 0; s < ENERGY_SAMPLE_COUNT; s++)
            {
                float energy = 0f;
                for (int b = 0; b < cachedBoneTransforms.Length; b++)
                {
                    Vector3 delta = positionSamples[s + 1][b] - positionSamples[s][b];
                    energy += delta.sqrMagnitude;
                }
                energyCurve[s] = energy;
            }

            // Normalize [0, 1]
            float maxEnergy = 0f;
            for (int s = 0; s < ENERGY_SAMPLE_COUNT; s++)
            {
                if (energyCurve[s] > maxEnergy)
                    maxEnergy = energyCurve[s];
            }

            if (maxEnergy > 0f)
            {
                float invMax = 1f / maxEnergy;
                for (int s = 0; s < ENERGY_SAMPLE_COUNT; s++)
                {
                    energyCurve[s] *= invMax;
                }
            }

            return new MotionEnergyCache { clipIndex = clipIndex, energyCurve = energyCurve };
        }

        #endregion

        #region Audio RMS Computation

        /// <summary>
        /// Compute normalized RMS curve from cached PCM data for a given sample range.
        /// Used for both full-sentence and remaining-portion RMS computation.
        /// </summary>
        /// <param name="startSample">Start index in cachedPCMData (inclusive)</param>
        /// <param name="endSample">End index in cachedPCMData (exclusive)</param>
        private float[] ComputeRMSFromCachedPCM(int startSample, int endSample)
        {
            int rangeSamples = endSample - startSample;
            if (rangeSamples <= 0 || cachedPCMData == null)
                return null;

            float[] rmsCurve = new float[ENERGY_SAMPLE_COUNT];
            int windowSize = rangeSamples / ENERGY_SAMPLE_COUNT;

            if (windowSize <= 0)
            {
                for (int i = 0; i < ENERGY_SAMPLE_COUNT; i++)
                    rmsCurve[i] = 1f;
                return rmsCurve;
            }

            for (int w = 0; w < ENERGY_SAMPLE_COUNT; w++)
            {
                int wStart = startSample + w * windowSize;
                int wEnd = (w == ENERGY_SAMPLE_COUNT - 1) ? endSample : wStart + windowSize;

                float sumSquares = 0f;
                int count = wEnd - wStart;
                for (int i = wStart; i < wEnd; i++)
                {
                    sumSquares += cachedPCMData[i] * cachedPCMData[i];
                }

                rmsCurve[w] = Mathf.Sqrt(sumSquares / count);
            }

            // Normalize [0, 1]
            float maxRMS = 0f;
            for (int i = 0; i < ENERGY_SAMPLE_COUNT; i++)
            {
                if (rmsCurve[i] > maxRMS)
                    maxRMS = rmsCurve[i];
            }

            if (maxRMS > 0f)
            {
                float invMax = 1f / maxRMS;
                for (int i = 0; i < ENERGY_SAMPLE_COUNT; i++)
                {
                    rmsCurve[i] *= invMax;
                }
            }

            return rmsCurve;
        }

        /// <summary>
        /// Cache PCM data and compute initial full-sentence RMS curve.
        /// Called from OnSentenceStarted — PCM is cached for sub-range recomputation at swap points.
        /// </summary>
        private void UpdateAudioRMSForEnergyMatching(TalkMotionData data)
        {
            if (!enableEnergyMatching)
                return;

            if (data == null || data.audioClip == null)
            {
                currentAudioRMSCurve = null;
                currentSentenceAudioClip = null;
                cachedPCMData = null;
                return;
            }

            // Cache audio metadata
            currentSentenceAudioClip = data.audioClip;
            sentenceAudioStartTime = Time.time;
            cachedPCMSampleCount = data.audioClip.samples;
            cachedPCMChannels = data.audioClip.channels;
            cachedPCMFrequency = data.audioClip.frequency;

            // Cache PCM data (one-time per sentence)
            int totalSamples = cachedPCMSampleCount * cachedPCMChannels;
            if (cachedPCMData == null || cachedPCMData.Length != totalSamples)
                cachedPCMData = new float[totalSamples];
            data.audioClip.GetData(cachedPCMData, 0);

            // Compute full-sentence RMS for the first clip selection
            currentAudioRMSCurve = ComputeRMSFromCachedPCM(0, totalSamples);
        }

        /// <summary>
        /// Recompute RMS curve for the remaining audio portion.
        /// Called from SwapInactiveTalkingSlot before clip selection.
        /// Uses cached PCM data — no AudioClip.GetData() overhead.
        /// </summary>
        private void RefreshRemainingAudioRMS()
        {
            if (!enableEnergyMatching || cachedPCMData == null || currentSentenceAudioClip == null)
                return;

            float elapsed = Time.time - sentenceAudioStartTime;
            float totalDuration = currentSentenceAudioClip.length;
            float remaining = totalDuration - elapsed;

            if (remaining < MIN_REMAINING_AUDIO)
            {
                // Too little audio left — fall back to weighted random
                currentAudioRMSCurve = null;
                return;
            }

            // Convert elapsed time to sample index
            int startSample = Mathf.Clamp(
                (int)(elapsed * cachedPCMFrequency) * cachedPCMChannels,
                0, cachedPCMData.Length);
            int endSample = cachedPCMData.Length;

            if (endSample - startSample <= ENERGY_SAMPLE_COUNT)
            {
                currentAudioRMSCurve = null;
                return;
            }

            currentAudioRMSCurve = ComputeRMSFromCachedPCM(startSample, endSample);

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Refreshed RMS for remaining {remaining:F1}s audio ({startSample}/{endSample} samples)");
        }

        #endregion

        #region Similarity Matching

        /// <summary>
        /// Select the best matching clip based on combined weight and energy similarity score.
        /// Score = (1 - blendRatio) * normalizedWeight + blendRatio * cosineSimilarity
        /// </summary>
        /// <param name="audioRMS">Current sentence RMS curve</param>
        /// <param name="excludeIndex">Index to exclude if preventRepeat is set. -1 to exclude nothing.</param>
        /// <returns>Best matching clip index</returns>
        private int SelectBestMatchingClip(float[] audioRMS, int excludeIndex)
        {
            if (motionEnergyCaches == null || motionEnergyCaches.Count == 0)
                return 0;

            // Compute max weight for normalization
            float maxWeight = 0f;
            for (int i = 0; i < talkingAnimations.Count; i++)
            {
                if (talkingAnimations[i].weight > maxWeight)
                    maxWeight = talkingAnimations[i].weight;
            }
            if (maxWeight <= 0f) maxWeight = 1f;
            float invMaxWeight = 1f / maxWeight;

            int bestIndex = -1;
            float bestScore = -1f;
            float bestSimilarity = -1f;

            for (int c = 0; c < motionEnergyCaches.Count; c++)
            {
                int clipIdx = motionEnergyCaches[c].clipIndex;

                // Respect preventRepeat
                if (clipIdx == excludeIndex && talkingAnimations[clipIdx].preventRepeat && talkingAnimations.Count > 1)
                    continue;

                // Skip zero-weight clips
                if (talkingAnimations[clipIdx].weight <= 0f)
                    continue;

                float similarity = CosineSimilarity(audioRMS, motionEnergyCaches[c].energyCurve);
                float normalizedWeight = talkingAnimations[clipIdx].weight * invMaxWeight;
                float score = (1f - energyBlendRatio) * normalizedWeight + energyBlendRatio * similarity;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = clipIdx;
                    bestSimilarity = similarity;
                }
            }

            // Fallback if all excluded
            if (bestIndex < 0)
                bestIndex = 0;

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Energy match: selected '{talkingAnimations[bestIndex].clip.name}' (similarity={bestSimilarity:F3}, score={bestScore:F3})");
            return bestIndex;
        }

        /// <summary>
        /// Cosine similarity between two curves.
        /// Both curves are resampled to the same length before comparison.
        /// Returns value in [0, 1] (clamped — negative similarity treated as 0).
        /// </summary>
        private float CosineSimilarity(float[] curveA, float[] curveB)
        {
            if (curveA == null || curveB == null || curveA.Length == 0 || curveB.Length == 0)
                return 0f;

            // Resample to common length if different
            float[] a = curveA;
            float[] b = curveB;

            if (a.Length != b.Length)
            {
                int targetLen = Mathf.Min(a.Length, b.Length);
                if (a.Length != targetLen) a = ResampleCurve(a, targetLen);
                if (b.Length != targetLen) b = ResampleCurve(b, targetLen);
            }

            float dotProduct = 0f;
            float magnitudeA = 0f;
            float magnitudeB = 0f;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                magnitudeA += a[i] * a[i];
                magnitudeB += b[i] * b[i];
            }

            float denominator = Mathf.Sqrt(magnitudeA) * Mathf.Sqrt(magnitudeB);
            if (denominator <= 0f)
                return 0f;

            float similarity = dotProduct / denominator;
            return Mathf.Max(0f, similarity); // Clamp negative to 0
        }

        /// <summary>
        /// Resample a curve to a different target length using linear interpolation.
        /// No LINQ — manual loop for GC-free runtime.
        /// </summary>
        private float[] ResampleCurve(float[] source, int targetLength)
        {
            if (source == null || source.Length == 0 || targetLength <= 0)
                return new float[targetLength];

            if (source.Length == targetLength)
                return source;

            float[] result = new float[targetLength];
            float ratio = (float)(source.Length - 1) / (targetLength - 1);

            for (int i = 0; i < targetLength; i++)
            {
                float srcIdx = i * ratio;
                int lower = (int)srcIdx;
                int upper = Mathf.Min(lower + 1, source.Length - 1);
                float t = srcIdx - lower;
                result[i] = source[lower] + (source[upper] - source[lower]) * t;
            }

            return result;
        }

        #endregion

        #region Bone Transform Save/Restore

        /// <summary>
        /// Save all bone transforms (localPosition, localRotation, localScale).
        /// Used before SampleAnimation to prevent visual side effects.
        /// </summary>
        private BoneTransformData[] SaveAllBoneTransforms()
        {
            if (cachedBoneTransforms == null)
                return null;

            BoneTransformData[] saved = new BoneTransformData[cachedBoneTransforms.Length];
            for (int i = 0; i < cachedBoneTransforms.Length; i++)
            {
                if (cachedBoneTransforms[i] != null)
                {
                    saved[i] = new BoneTransformData
                    {
                        transform = cachedBoneTransforms[i],
                        localPosition = cachedBoneTransforms[i].localPosition,
                        localRotation = cachedBoneTransforms[i].localRotation,
                        localScale = cachedBoneTransforms[i].localScale
                    };
                }
            }
            return saved;
        }

        /// <summary>
        /// Restore all bone transforms from saved data.
        /// </summary>
        private void RestoreAllBoneTransforms(BoneTransformData[] saved)
        {
            if (saved == null)
                return;

            for (int i = 0; i < saved.Length; i++)
            {
                if (saved[i].transform != null)
                {
                    saved[i].transform.localPosition = saved[i].localPosition;
                    saved[i].transform.localRotation = saved[i].localRotation;
                    saved[i].transform.localScale = saved[i].localScale;
                }
            }
        }

        #endregion

        #region BlendShape Save/Restore

        /// <summary>
        /// Save all blend shape values from cached SkinnedMeshRenderers.
        /// Returns jagged array: [rendererIndex][blendShapeIndex] = value.
        /// </summary>
        private float[][] SaveBlendShapes()
        {
            if (head_skmr == null || head_skmr.Count == 0)
                return null;

            float[][] saved = new float[head_skmr.Count][];
            for (int r = 0; r < head_skmr.Count; r++)
            {
                if (head_skmr[r] == null || head_skmr[r].sharedMesh == null)
                    continue;

                int count = head_skmr[r].sharedMesh.blendShapeCount;
                saved[r] = new float[count];
                for (int b = 0; b < count; b++)
                {
                    saved[r][b] = head_skmr[r].GetBlendShapeWeight(b);
                }
            }
            return saved;
        }

        /// <summary>
        /// Restore all blend shape values from saved data.
        /// </summary>
        private void RestoreBlendShapes(float[][] saved)
        {
            if (saved == null || head_skmr == null)
                return;

            for (int r = 0; r < saved.Length && r < head_skmr.Count; r++)
            {
                if (saved[r] == null || head_skmr[r] == null)
                    continue;

                for (int b = 0; b < saved[r].Length; b++)
                {
                    head_skmr[r].SetBlendShapeWeight(b, saved[r][b]);
                }
            }
        }

        #endregion
    }
}
