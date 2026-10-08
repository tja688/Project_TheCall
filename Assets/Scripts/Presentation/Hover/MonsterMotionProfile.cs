using UnityEngine;

namespace TheCall
{
    /// <summary>
    /// 怪物运动的全局动态参数。部件自己的待机摆动、拖拽角度和惯性存在 MonsterRig.json 里，在网页编辑器里调。
    /// 这里只放手感相关的弹簧与混合参数；网页端 motion.js 读取同一组字段，两边保持一致。
    /// 旧版的 IMGUI 工具还在用下面标注为“旧版”的字段，所以保留。
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterMotionProfile", menuName = "The Call/Monster Motion Profile")]
    public sealed class MonsterMotionProfile : ScriptableObject
    {
        [SerializeField, HideInInspector] bool _defaultsApplied;

        [Header("Grab · 抓住")]
        [Tooltip("松手后身体回到原位的快慢（越大越快）。按住时身体中心直接贴着指针，不用这个值。")]
        [Range(2f, 40f)] public float gripResponse = 16f;
        [Tooltip("松手回位的阻尼（越大越不晃）")]
        [Range(0.1f, 2f)] public float gripDamping = 0.75f;
        [Tooltip("指针速度达到这个值（画布像素/秒）时，拖拽角度打满")]
        [Range(40f, 1000f)] public float dragSpeedReference = 360f;
        [Tooltip("抓住后拖拽姿势接管的时间（秒）")]
        [Range(0.01f, 0.5f)] public float blendInSeconds = 0.08f;
        [Tooltip("松手后待机接回来的时间（秒）")]
        [Range(0.05f, 1.5f)] public float blendOutSeconds = 0.3f;
        [Tooltip("拖拽驱动量的平滑时间（秒）")]
        [Range(0.01f, 0.3f)] public float driverSmoothing = 0.05f;

        [Header("Body · 身体")]
        [Tooltip("身体倾斜的快慢（越大越干脆）")]
        [Range(1f, 40f)] public float rootTiltResponse = 9f;
        [Tooltip("身体倾斜阻尼（越大越少过冲）")]
        [Range(0.1f, 2f)] public float rootTiltDamping = 0.35f;

        [Header("Joints · 关节")]
        [Tooltip("头、手、脚、尾巴的基础响应（越大越利落）")]
        [Range(1f, 40f)] public float jointResponse = 11f;
        [Tooltip("关节阻尼（越大越少甩）")]
        [Range(0.1f, 2f)] public float jointDamping = 0.3f;

        [Header("Reaction · 计分反应")]
        [Tooltip("计分时头和脚弹一下的快慢")]
        [Range(1f, 40f)] public float reactionResponse = 14f;
        [Tooltip("计分反应阻尼")]
        [Range(0.1f, 2f)] public float reactionDamping = 0.5f;

        [Header("Idle · 待机")]
        public bool idleEnabled = true;
        [Tooltip("身体呼吸抬起的像素")]
        [Range(0f, 5f)] public float idleBodyLift = 1.2f;
        [Tooltip("待机摆动的频率（每秒周期数）")]
        [Range(0.05f, 4f)] public float idleCyclesPerSecond = 0.42f;

        [Header("Legacy · 旧版（只给旧 IMGUI 工具用，游戏里已不生效）")]
        [Range(0f, 20f)] public float headDragDegrees = 7f;
        [Range(0f, 20f)] public float feetDragDegrees = 5f;
        [Range(0f, 24f)] public float tailDragDegrees = 9f;
        [Range(0f, 12f)] public float bodyDragDegrees = 2.5f;
        [Range(1f, 240f)] public float springStiffness = 90f;
        [Range(1f, 80f)] public float springDamping = 18f;
        [Range(0f, 8f)] public float idleHeadDegrees = 1.35f;
        [Range(0f, 8f)] public float idleFeetDegrees = 0.35f;
        [Range(0f, 12f)] public float idleTailDegrees = 1.8f;
        [Range(0f, 8f)] public float idleBodyDegrees = 0.45f;

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
