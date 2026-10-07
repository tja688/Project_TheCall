using UnityEngine;

namespace TheCall
{
    /// <summary>
    /// Shared tuning for the small, non-physical monster motion.
    /// Keep this asset deliberately small: it is a presentation profile, not gameplay state.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterMotionProfile", menuName = "The Call/Monster Motion Profile")]
    public sealed class MonsterMotionProfile : ScriptableObject
    {
        [SerializeField, HideInInspector] bool _defaultsApplied;

        [Header("Drag")]
        [Range(0f, 20f)] public float headDragDegrees = 7f;
        [Range(0f, 20f)] public float feetDragDegrees = 5f;
        [Range(0f, 24f)] public float tailDragDegrees = 9f;
        [Range(0f, 12f)] public float bodyDragDegrees = 2.5f;
        [Range(1f, 240f)] public float springStiffness = 90f;
        [Range(1f, 80f)] public float springDamping = 18f;

        [Header("Idle")]
        public bool idleEnabled = true;
        [Range(0f, 8f)] public float idleHeadDegrees = 1.35f;
        [Range(0f, 8f)] public float idleFeetDegrees = 0.35f;
        [Range(0f, 12f)] public float idleTailDegrees = 1.8f;
        [Range(0f, 8f)] public float idleBodyDegrees = 0.45f;
        [Range(0f, 5f)] public float idleBodyLift = 1.2f;
        [Range(0.05f, 4f)] public float idleCyclesPerSecond = 0.42f;

        static MonsterMotionProfile _fallback;

        public static MonsterMotionProfile Fallback => _fallback ??= CreateFallback();

        public void SetDefaultsIfUninitialized()
        {
            if (_defaultsApplied)
                return;

            headDragDegrees = 7f;
            feetDragDegrees = 5f;
            tailDragDegrees = 9f;
            bodyDragDegrees = 2.5f;
            springStiffness = 90f;
            springDamping = 18f;
            idleEnabled = true;
            idleHeadDegrees = 1.35f;
            idleFeetDegrees = 0.35f;
            idleTailDegrees = 1.8f;
            idleBodyDegrees = 0.45f;
            idleBodyLift = 1.2f;
            idleCyclesPerSecond = 0.42f;
            _defaultsApplied = true;
        }

        static MonsterMotionProfile CreateFallback()
        {
            var profile = CreateInstance<MonsterMotionProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            return profile;
        }

        public MonsterMotionProfile CloneRuntime()
        {
            var clone = Instantiate(this);
            clone.hideFlags = HideFlags.HideAndDontSave;
            return clone;
        }
    }
}
