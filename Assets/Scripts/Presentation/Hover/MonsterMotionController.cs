using UnityEngine;

namespace TheCall
{
    /// <summary>
    /// 怪物运动控制器：待机摆动、抓取拖拽、计分反应三层叠加在同一副骨架上。
    /// 抓住的是身体中心，不是按下的那个像素。按下点若偏离中心，中心会在约零点一秒内移到指针上；
    /// 这段时间里指针移动多少，中心就移动多少。对齐之后中心与指针重合，不再用弹簧去追。
    /// 身体倾斜和头、手、脚、尾巴仍由拖拽速度驱动的弹簧摆动。松手后中心弹簧回到原位，待机再接回来。
    /// 关节的拖拽角度是硬上限：弹簧过冲时会被夹住并小幅回弹。
    /// 固定 1/120 秒子步长积分；网页编辑器的 motion.js 按同样的步骤实现，两边数值一致。
    /// </summary>
    public sealed class MonsterMotionController
    {
        public const float StepSeconds = 1f / 120f;
        public const float MaxFrameSeconds = 0.1f;

        /// <summary>按下点偏离身体中心时，偏差按这个时间常数收掉。大约零点一秒内收到位。</summary>
        public const float CenterCatchSeconds = 0.02f;

        const float CenterCatchSnap = 0.35f;
        const float Bounce = 0.2f;
        const float StepEpsilon = 1e-5f;

        readonly MonsterRigSkeleton _skeleton;
        readonly MonsterMotionProfile _profile;
        readonly float _seed;
        readonly float[] _angle;
        readonly float[] _speed;
        readonly float[] _omega;
        readonly float[] _zeta;
        readonly float[] _composed;
        readonly float _rootOmega;
        readonly float _rootZeta;
        readonly float _gripOmega;
        readonly float _gripZeta;
        readonly float _reactionOmega;
        readonly float _reactionZeta;

        float _time;
        float _accumulator;
        float _weight;
        float _driver;
        bool _holding;
        float _gripX;
        float _gripY;
        float _targetX;
        float _targetY;
        float _alignX;
        float _alignY;
        float _pointerSampleX;
        float _pointerSampleY;
        float _pivotX;
        float _pivotY;
        float _pivotSpeedX;
        float _pivotSpeedY;
        float _tilt;
        float _tiltSpeed;
        float _reactHead;
        float _reactHeadSpeed;
        float _reactFeet;
        float _reactFeetSpeed;
        float _targetHead;
        float _targetFeet;

        public MonsterMotionController(MonsterRigSkeleton skeleton, MonsterMotionProfile profile, float seed)
        {
            _skeleton = skeleton;
            _profile = profile != null ? profile : MonsterMotionProfile.Fallback;
            _seed = seed;

            var count = skeleton.Bones.Count;
            _angle = new float[count];
            _speed = new float[count];
            _omega = new float[count];
            _zeta = new float[count];
            _composed = new float[count];

            var rootInertia = count > 0 ? Mathf.Sqrt(skeleton.Bones[0].Inertia) : 1f;
            _rootOmega = _profile.rootTiltResponse / rootInertia;
            _rootZeta = _profile.rootTiltDamping;
            _gripOmega = _profile.gripResponse / rootInertia;
            _gripZeta = _profile.gripDamping;
            _reactionOmega = _profile.reactionResponse;
            _reactionZeta = _profile.reactionDamping;

            for (var i = 1; i < count; i++)
            {
                var bone = skeleton.Bones[i];
                if (!IsGrab(bone.Joint))
                    continue;

                var tuning = Tuning(bone.Joint);
                _omega[i] = _profile.jointResponse * tuning.x / Mathf.Sqrt(bone.Inertia);
                _zeta[i] = _profile.jointDamping * tuning.y;
            }
        }

        public bool Holding => _holding;

        public float Weight => _weight;

        /// <summary>
        /// 抓住身体中心。x、y 是指针相对身体安装点的位置。
        /// 这一下不挪怪物：中心和指针的偏差记下来，随后收到零。
        /// </summary>
        public void Grab(float x, float y)
        {
            _alignX = _pivotX - x;
            _alignY = _pivotY - y;
            _targetX = x;
            _targetY = y;
            _pointerSampleX = x;
            _pointerSampleY = y;
            _holding = true;
            PlaceBody();
        }

        public void Hold(float x, float y)
        {
            if (!_holding)
                return;

            _targetX = x;
            _targetY = y;
            PlaceBody();
        }

        public void Release()
        {
            _holding = false;
        }

        /// <summary>计分等外部反应：头和脚的目标角度（度），由弹簧平滑过去；传 0,0 即回到静止。</summary>
        public void SetReaction(float headAngle, float feetAngle)
        {
            _targetHead = headAngle;
            _targetFeet = feetAngle;
        }

        /// <summary>抓取分量（抓点、根倾斜、各关节、反应）都回到静止。待机是另一层，不影响这里的判断。</summary>
        public bool Settled
        {
            get
            {
                if (_holding)
                    return false;
                if (Mathf.Abs(_pivotX - _gripX) > 0.02f || Mathf.Abs(_pivotY - _gripY) > 0.02f)
                    return false;
                if (Mathf.Abs(_pivotSpeedX) > 0.5f || Mathf.Abs(_pivotSpeedY) > 0.5f)
                    return false;
                if (Mathf.Abs(_tilt) > 0.02f || Mathf.Abs(_tiltSpeed) > 0.5f)
                    return false;
                if (Mathf.Abs(_reactHead) > 0.02f || Mathf.Abs(_reactFeet) > 0.02f)
                    return false;

                for (var i = 1; i < _angle.Length; i++)
                {
                    if (!IsGrab(_skeleton.Bones[i].Joint))
                        continue;
                    if (Mathf.Abs(_angle[i]) > 0.02f || Mathf.Abs(_speed[i]) > 0.5f)
                        return false;
                }

                return true;
            }
        }

        public void Advance(float deltaSeconds)
        {
            if (_skeleton.Bones.Count == 0)
                return;

            var dt = Mathf.Clamp(deltaSeconds, 0f, MaxFrameSeconds);
            _time += dt;
            if (_holding && dt > 0f)
                CatchUp(dt);
            _accumulator += dt;
            while (_accumulator >= StepSeconds - StepEpsilon)
            {
                Step(StepSeconds);
                _accumulator -= StepSeconds;
            }
        }

        /// <summary>把当前时刻的待机、抓取和反应叠加起来，写进 frame。</summary>
        public void Compose(MonsterRigFrame frame)
        {
            if (_skeleton.Bones.Count == 0)
                return;

            var idle = _profile.idleEnabled ? 1f - _weight : 0f;
            var cps = _profile.idleCyclesPerSecond;
            var clock = _time + _seed;
            for (var i = 0; i < _composed.Length; i++)
            {
                var bone = _skeleton.Bones[i];
                if (bone.Joint == MonsterJoint.Root)
                {
                    _composed[i] = 0f;
                    continue;
                }

                var wave = Mathf.Sin(Mathf.PI * 2f * cps * clock + PhaseOf(bone.Joint));
                var angle = idle * bone.Swing * wave;
                if (IsGrab(bone.Joint))
                    angle += _angle[i];
                if (bone.Kind == MonsterPartKind.Head)
                    angle += _reactHead;
                if (bone.Kind == MonsterPartKind.Foot)
                    angle += _reactFeet;
                _composed[i] = angle;
            }

            var root = _skeleton.Bones[0];
            var slow = Mathf.Sin(Mathf.PI * cps * (_time + 0.7f * _seed));
            var fast = Mathf.Sin(Mathf.PI * 2f * cps * clock);
            MonsterRigKinematics.Solve(
                _skeleton,
                frame,
                _composed,
                _tilt + idle * root.Swing * slow,
                root.MountX + _pivotX,
                root.MountY + _pivotY,
                root.MountX + _gripX,
                root.MountY + _gripY,
                idle * _profile.idleBodyLift * fast);
        }

        void Step(float h)
        {
            var blend = _holding ? _profile.blendInSeconds : _profile.blendOutSeconds;
            _weight += ((_holding ? 1f : 0f) - _weight) * (1f - Mathf.Exp(-h / Mathf.Max(0.001f, blend)));

            var reference = Mathf.Max(1f, _profile.dragSpeedReference);
            var raw = Mathf.Clamp(_pivotSpeedX / reference, -1f, 1f) * _weight;
            _driver += (raw - _driver) * (1f - Mathf.Exp(-h / Mathf.Max(0.001f, _profile.driverSmoothing)));

            if (!_holding)
            {
                Spring(ref _pivotX, ref _pivotSpeedX, _gripX, h, _gripOmega, _gripZeta, float.MaxValue);
                Spring(ref _pivotY, ref _pivotSpeedY, _gripY, h, _gripOmega, _gripZeta, float.MaxValue);
            }

            var root = _skeleton.Bones[0];
            Spring(ref _tilt, ref _tiltSpeed, -root.Drag * _driver, h, _rootOmega, _rootZeta, Mathf.Max(0f, root.Drag));

            for (var i = 1; i < _angle.Length; i++)
            {
                var bone = _skeleton.Bones[i];
                if (!IsGrab(bone.Joint))
                    continue;

                Spring(ref _angle[i], ref _speed[i], bone.Drag * _driver, h, _omega[i], _zeta[i], Mathf.Max(0f, bone.Drag));
            }

            Spring(ref _reactHead, ref _reactHeadSpeed, _targetHead, h, _reactionOmega, _reactionZeta, float.MaxValue);
            Spring(ref _reactFeet, ref _reactFeetSpeed, _targetFeet, h, _reactionOmega, _reactionZeta, float.MaxValue);
        }

        /// <summary>身体中心 = 指针 + 尚未收完的按下偏差。旋转仍绕安装点，也就是身体中心。</summary>
        void PlaceBody()
        {
            _pivotX = _targetX + _alignX;
            _pivotY = _targetY + _alignY;
        }

        void CatchUp(float dt)
        {
            var decay = Mathf.Exp(-dt / CenterCatchSeconds);
            _alignX *= decay;
            _alignY *= decay;
            if (_alignX * _alignX + _alignY * _alignY <= CenterCatchSnap * CenterCatchSnap)
            {
                _alignX = 0f;
                _alignY = 0f;
            }

            PlaceBody();
            _pivotSpeedX = (_targetX - _pointerSampleX) / dt;
            _pivotSpeedY = (_targetY - _pointerSampleY) / dt;
            _pointerSampleX = _targetX;
            _pointerSampleY = _targetY;
        }

        static void Spring(ref float value, ref float speed, float target, float h, float omega, float zeta, float limit)
        {
            var acceleration = omega * omega * (target - value) - 2f * zeta * omega * speed;
            speed += acceleration * h;
            value += speed * h;
            if (value > limit)
            {
                value = limit;
                if (speed > 0f)
                    speed = -speed * Bounce;
            }
            else if (value < -limit)
            {
                value = -limit;
                if (speed < 0f)
                    speed = -speed * Bounce;
            }
        }

        static bool IsGrab(MonsterJoint joint)
        {
            return joint == MonsterJoint.Head
                || joint == MonsterJoint.Hand
                || joint == MonsterJoint.Foot
                || joint == MonsterJoint.Tail;
        }

        static float PhaseOf(MonsterJoint joint)
        {
            switch (joint)
            {
                case MonsterJoint.Hand: return 1f;
                case MonsterJoint.Foot: return 2f;
                case MonsterJoint.Tail: return 3.4f;
                case MonsterJoint.Trim: return 0.5f;
                default: return 0f;
            }
        }

        /// <summary>关节的响应倍率（x）与阻尼倍率（y）。尾巴更软，手更利落。</summary>
        static Vector2 Tuning(MonsterJoint joint)
        {
            switch (joint)
            {
                case MonsterJoint.Hand: return new Vector2(1.25f, 1f);
                case MonsterJoint.Foot: return new Vector2(1.15f, 1f);
                case MonsterJoint.Tail: return new Vector2(0.75f, 0.8f);
                default: return new Vector2(1f, 1f);
            }
        }
    }
}
