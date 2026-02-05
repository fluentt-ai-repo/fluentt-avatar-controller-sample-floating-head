using FluentT.Animation;
using FluentT.Talkmotion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Sample controller component that demonstrates how to integrate FluentTAvatar with body animation and look target control.
    /// This component is NOT part of the core TalkMotion SDK - it's a reference implementation showing how to use
    /// FluentTAvatar callbacks to control animator and look target behaviors.
    ///
    /// Use this as a starting point for your own custom avatar control logic.
    /// </summary>
    [RequireComponent(typeof(FluentTAvatar))]
    public partial class FluentTAvatarControllerFloatingHead : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FluentTAvatar avatar;
        [SerializeField] private RuntimeAnimatorController animatorController;

        [Header("Default Idle Animation")]
        [SerializeField] private AnimationClip defaultIdleAnimationClip;

        [Header("Look Target")]
        [SerializeField] private bool enableLookTarget = false;
        [SerializeField] private Transform lookTarget;

        [Header("Animation Rigging Multi-Aim Constraints")]
        [SerializeField] private MultiAimConstraint headAimConstraint;
        [SerializeField] private MultiAimConstraint leftEyeAimConstraint;
        [SerializeField] private MultiAimConstraint rightEyeAimConstraint;

        [Header("Look Target Transforms - For Auto-Find")]
        [SerializeField] private Transform lookHead;
        [SerializeField] private Transform lookLeftEyeBall;
        [SerializeField] private Transform lookRightEyeBall;
        [SerializeField] private LookTargetSetting idleLookSettings = new LookTargetSetting
        {
            headStrategy = ELookTargetStrategy.FocusedOnTarget,
            headLookIntoVoid = new Vector2(0, 0),
            headAngleVariance = new Vector2(0, 0),
            eyeStrategy = ELookTargetStrategy.FocusedOnTarget,
            eyeLookIntoVoid = new Vector2(0, 0),
            eyeAngleVariance = new Vector2(0, 0)
        };
        [SerializeField] private LookTargetSetting talkingLookSettings = new LookTargetSetting
        {
            headStrategy = ELookTargetStrategy.FocusedOnTarget,
            headLookIntoVoid = new Vector2(0, 0),
            headAngleVariance = new Vector2(0, 0),
            eyeStrategy = ELookTargetStrategy.FocusedOnTarget,
            eyeLookIntoVoid = new Vector2(0, 0),
            eyeAngleVariance = new Vector2(0, 0)
        };

        [Header("Client-Side Emotion Tagging")]
        [SerializeField] public bool enableClientEmotionTagging = false;
        [SerializeField] public int maxEmotionTagsPerSentence = 1;
        [SerializeField] public List<WordEmotionMapping> wordEmotionMappings = new List<WordEmotionMapping>();
        [SerializeField] public List<EmotionMotionMapping> emotionMotionMappings = new List<EmotionMotionMapping>();

        [Header("Server-Side Motion Tagging")]
        [SerializeField] public bool enableServerMotionTagging = false;
        [SerializeField] public List<ServerMotionTagMapping> serverMotionTagMappings = new List<ServerMotionTagMapping>();

        [Header("Eye Blink")]
        [SerializeField] public bool enableEyeBlink = false;
        [Tooltip("Eye blink animation clip (if null, uses default ARKit eyeBlink animation)")]
        [SerializeField] public TMAnimationClip blinkClip = null;
        [SerializeField] [Range(1f, 10f)] public float blinkInterval = 3f;
        [SerializeField] [Range(0f, 5f)] public float blinkIntervalVariance = 1f;

        // Private state
        private Animator animator;
        private AnimatorOverrideController overrideController;

        // Cached references
        [SerializeField] private List<SkinnedMeshRenderer> head_skmr;

        private void Reset()
        {
            // Auto-initialize references when component is added or reset
            if (avatar == null)
            {
                avatar = GetComponent<FluentTAvatar>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

#if UNITY_EDITOR
            // Auto-assign animator controller from package
            if (animatorController == null)
            {
                animatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Packages/com.fluentt.avatar-controller-sample-floating-head/Animation/FluentTAvatarFloatingHeadController.controller");
            }

            // Auto-assign default idle animation clip
            if (defaultIdleAnimationClip == null)
            {
                defaultIdleAnimationClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Packages/com.fluentt.avatar-controller-sample-floating-head/Animation/idle_sample.anim");
            }
#endif

            // Auto-create default blink clip
            if (blinkClip == null)
            {
                blinkClip = CreateDefaultBlinkClip();
            }

            // Cache head SkinnedMeshRenderers that have blend shapes
            head_skmr = new List<SkinnedMeshRenderer>();

            // Check self first
            if (TryGetComponent<SkinnedMeshRenderer>(out var selfSkmr))
            {
                if (selfSkmr.sharedMesh != null && selfSkmr.sharedMesh.blendShapeCount > 0)
                {
                    head_skmr.Add(selfSkmr);
                }
            }

            // Then check children
            var skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var skmr in skinnedMeshRenderers)
            {
                if (skmr.sharedMesh != null && skmr.sharedMesh.blendShapeCount > 0)
                {
                    head_skmr.Add(skmr);
                }
            }

            // Auto-find look target transforms
            FindLookTargetTransforms();
        }

        private void Awake()
        {
            if (avatar == null)
            {
                avatar = GetComponent<FluentTAvatar>();
            }
        }

        private void OnEnable()
        {
            if (avatar != null)
            {
                // Subscribe to avatar callbacks
                avatar.callback.onSentenceStarted.AddListener(OnSentenceStarted);
                avatar.callback.onSentenceEnded.AddListener(OnSentenceEnded);
                avatar.callback.onTranscriptionReceived.AddListener(OnTranscriptionReceived);
                avatar.callback.onServerMotionTag.AddListener(OnServerMotionTag);
            }
        }

        private void OnDisable()
        {
            if (avatar != null)
            {
                // Unsubscribe from avatar callbacks
                avatar.callback.onSentenceStarted.RemoveListener(OnSentenceStarted);
                avatar.callback.onSentenceEnded.RemoveListener(OnSentenceEnded);
                avatar.callback.onTranscriptionReceived.RemoveListener(OnTranscriptionReceived);
                avatar.callback.onServerMotionTag.RemoveListener(OnServerMotionTag);

                // Unsubscribe from LateUpdate completion callback
                avatar.OnLateUpdateCompleted -= OnAvatarLateUpdateCompleted;
            }
        }

        private void Start()
        {
            // Initialize Animator and Override Controller for Server Motion Tagging
            if (avatar != null)
            {
                animator = avatar.GetComponent<Animator>();
                if (animator != null && animatorController != null)
                {
                    animator.runtimeAnimatorController = animatorController;
                    overrideController = new AnimatorOverrideController(animatorController);
                    animator.runtimeAnimatorController = overrideController;

                    // Override default_dummy with defaultIdleAnimationClip if assigned
                    if (defaultIdleAnimationClip != null)
                    {
                        overrideController["default_dummy"] = defaultIdleAnimationClip;
                        Debug.Log($"[FluentTAvatarControllerFloatingHead] Overriding default_dummy with {defaultIdleAnimationClip.name}");
                    }
                }
            }

            InitializeLookTarget();
            InitializeEmotionTagging();
            InitializeServerMotionTagging();
            InitializeEyeBlink();

            // Subscribe to avatar's LateUpdate completion callback
            // This ensures eye BlendShapes are updated AFTER face+head animation
            if (avatar != null)
            {
                avatar.OnLateUpdateCompleted += OnAvatarLateUpdateCompleted;
            }
        }

        private void Update()
        {
            UpdateTimelines(Time.deltaTime);
        }

        private void LateUpdate()
        {
            LateUpdateTimelines(Time.deltaTime);

            if (enableLookTarget && lookTargetController != null)
            {
                UpdateLookTarget();
                // LateUpdateLookTarget(); // Moved to OnAvatarLateUpdateCompleted callback
            }
        }

        /// <summary>
        /// Called after FluentTAvatar's LateUpdate completes
        /// This ensures eye BlendShapes are updated AFTER face+head animation from Timeline
        /// </summary>
        private void OnAvatarLateUpdateCompleted()
        {
            if (enableLookTarget && lookTargetController != null)
            {
                LateUpdateLookTarget();
            }

            // Eye blink is handled by TMAnimationComponent on dedicated layer
        }

        private void OnDestroy()
        {
            // Unsubscribe from avatar callbacks
            if (avatar != null)
            {
                avatar.OnLateUpdateCompleted -= OnAvatarLateUpdateCompleted;
            }

            // Clean up virtual targets when avatar is destroyed
            if (enableLookTarget)
            {
                CleanupVirtualTargets();
            }
        }

        #region Callback Handlers

        public void OnSentenceStarted(FluentT.APIClient.V3.TalkMotionData data)
        {
            // Process client-side emotion tagging if enabled
            if (enableClientEmotionTagging && data != null && data.audioClip != null && !string.IsNullOrEmpty(data.text))
            {
                float startTime = Time.time;
                ProcessClientEmotionTagging(data.text, data.audioClip.length, startTime);
            }

            // NOTE: Server motion tagging is now handled by OnServerMotionTag callback (called at exact timing from timeline)
        }

        public void OnSentenceEnded(FluentT.APIClient.V3.TalkMotionData data)
        {
            // Sentence ended
        }

        public void OnTranscriptionReceived(FluentT.Talkmotion.TranscriptionData data)
        {
            // Real-time transcription callback - called for each text chunk
            // This is especially useful for LiveKit mode where text arrives in small chunks

            if (data.isAgent)
            {
                // Agent is speaking - show real-time transcription
                // Example: Update UI subtitle in real-time
                // subtitleText.text = data.text;
            }
            else
            {
                // User is speaking - show what they said
                // Example: Update user input display
                // userInputText.text = data.text;
            }
        }

        #endregion

        #region Animation Updates

        private void UpdateTimelines(float deltaTime)
        {
            // Update emotion tagging timeline
            UpdateEmotionTaggingTimeline(deltaTime);

            // Server motion tagging uses Animator triggers, no timeline update needed
        }

        private void LateUpdateTimelines(float deltaTime)
        {
            // LateUpdate emotion tagging timeline
            LateUpdateEmotionTaggingTimeline(deltaTime);

            // Server motion tagging uses Animator triggers, no timeline update needed
        }

        #endregion
    }

    #region Data Structures for Emotion Tagging

    /// <summary>
    /// Word to emotion tag mapping
    /// </summary>
    [System.Serializable]
    public class WordEmotionMapping
    {
        public string word;
        public string emotionTag;
        public int priority = 1;
        public bool partialMatch = false;
    }

    /// <summary>
    /// Emotion tag to motion mapping
    /// </summary>
    [System.Serializable]
    public class EmotionMotionMapping
    {
        public string emotionTag;
        public TMAnimationClip animationClip;
        [Range(0f, 1f)]
        public float blendWeight = 1f;
        public float durationOverride = 0f;
    }

    /// <summary>
    /// Server motion tag to animation clip mapping
    /// </summary>
    [System.Serializable]
    public class ServerMotionTagMapping
    {
        public string emotionTag;
        public AnimationClip animationClip;
        [Range(0f, 1f)]
        public float blendWeight = 1f;
        public float durationOverride = 0f;
    }

    #endregion
}
