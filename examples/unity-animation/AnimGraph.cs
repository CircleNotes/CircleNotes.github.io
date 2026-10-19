using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace AnimationGraphExample
{
    /// <summary>
    /// 动画融合图：状态机（runtimeAnimatorController）与单个特殊片段双路混合。
    /// 两路混合结构固定：Controller / 特殊 Clip → Mixer → Animator。
    /// 切换控制器或片段时替换对应输入节点，淡入淡出通过权重实现。
    /// </summary>
    public sealed class AnimGraph
    {
        #region 私有字段

        private const int ControllerInputIndex = 0;  // mixer 上状态机输入的端口索引
        private const int SpecialClipInputIndex = 1; // mixer 上特殊片段输入的端口索引
        private const float FullWeight = 1f; // 融合权重全量

        private PlayableGraph _graph; // 手动推进的动画图。
        private AnimatorControllerPlayable _animatorPlayable; // 当前控制器的运行时实例。
        private AnimationClipPlayable _specialClipPlayable; // 按需创建的特殊片段节点。
        private AnimationMixerPlayable _mixer; // 控制器与特殊片段的双路混合器。
        private AnimationPlayableOutput _output; // 向 Animator 输出最终姿态。

        private AnimationClip _specialClip;      // 当前播放的特殊片段（null = 无特殊片段，纯状态机输出）
        private bool _specialLoop;               // 特殊片段是否循环（false = 播完一次自动淡出回状态机）
        private float _specialWeight;            // 特殊片段当前融合权重（0 = 纯状态机，1 = 纯特殊片段）
        private float _specialWeightTarget;      // 特殊片段权重目标值（淡入为 1，淡出为 0）
        private float _fadeSpeed;                // 权重渐变速度（1 / 淡入淡出时长）

        #endregion

        #region 公共属性

        /// <summary>当前是否有特殊片段在播放或淡出。</summary>
        public bool IsSpecialPlaying => _specialClip != null;

        /// <summary>获取底层动画图，便于观察图结构。</summary>
        public PlayableGraph Graph => _graph;

        #endregion

        #region 生命周期

        /// <summary>
        /// 建立双路混合骨架：状态机接输入 0（初始权重 1），特殊片段输入 1 留空（权重 0，播放时才建节点）。
        /// 图建立后移除 Animator 自带控制器，统一由手动图驱动动画输出。
        /// </summary>
        /// <param name="animator">接收动画图骨骼输出的 Animator。</param>
        /// <param name="controller">显式提供图内状态机资源，不依赖 Animator 自带控制器配置。</param>
        public AnimGraph(Animator animator, RuntimeAnimatorController controller)
        {
            _graph = PlayableGraph.Create("Animation Graph Example");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            _animatorPlayable = AnimatorControllerPlayable.Create(_graph, controller);
            _mixer = AnimationMixerPlayable.Create(_graph, 2);

            _graph.Connect(_animatorPlayable, 0, _mixer, ControllerInputIndex);
            _mixer.SetInputWeight(ControllerInputIndex, FullWeight);
            _mixer.SetInputWeight(SpecialClipInputIndex, 0f);

            _output = AnimationPlayableOutput.Create(_graph, "AnimGraph", animator);
            _output.SetSourcePlayable(_mixer);

            _graph.Play();
            animator.runtimeAnimatorController = null;
        }

        /// <summary>推进混合逻辑和图求值；deltaTime 应包含跳过帧的累计游戏时间。</summary>
        public void Evaluate(float deltaTime)
        {
            if (_graph.IsValid())
            {
                Tick(deltaTime);
                _graph.Evaluate(deltaTime);
            }
        }

        /// <summary>
        /// 销毁整张图，由持有者在禁用或销毁时调用。
        /// </summary>
        public void Release()
        {
            _specialClip = null;
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }

        /// <summary>
        /// 替换状态机 controller
        /// </summary>
        public void SetController(RuntimeAnimatorController controller)
        {
            if (controller == null || !_graph.IsValid()) return;

            if (_animatorPlayable.IsValid())
            {
                _graph.Disconnect(_mixer, ControllerInputIndex);
                _graph.DestroyPlayable(_animatorPlayable);
            }

            _animatorPlayable = AnimatorControllerPlayable.Create(_graph, controller);
            _graph.Connect(_animatorPlayable, 0, _mixer, ControllerInputIndex);
        }

        /// <summary>
        /// 直接写入手动图内状态机的浮点参数，避免依赖 Animator 自带 Controller。
        /// </summary>
        /// <param name="id">动画参数哈希。</param>
        /// <param name="value">动画参数值。</param>
        public void SetFloat(int id, float value)
        {
            if (_animatorPlayable.IsValid()) _animatorPlayable.SetFloat(id, value);
        }

        #endregion

        #region 公共方法 (特殊片段播放)

        /// <summary>
        /// 播放特殊片段：与状态机交叉淡入。融合中再调用直接换片段，不打断当前权重。
        /// </summary>
        /// <param name="clip">要播放的动画片段</param>
        /// <param name="fade">淡入时长（秒）</param>
        /// <param name="loop">true 循环播放；false 播完一次自动淡出回状态机</param>
        public void PlaySpecialClip(AnimationClip clip, float fade = 0.15f, bool loop = false)
        {
            if (clip == null) return;

            // 换片段：销毁旧 clip 节点重建，mixer 拓扑不动
            if (_specialClipPlayable.IsValid())
            {
                _graph.DestroyPlayable(_specialClipPlayable);
            }

            _specialClip = clip;
            _specialLoop = loop;

            _specialClipPlayable = AnimationClipPlayable.Create(_graph, clip);
            _specialClipPlayable.SetApplyFootIK(false);
            _graph.Connect(_specialClipPlayable, 0, _mixer, SpecialClipInputIndex);

            FadeSpecialTo(FullWeight, fade);
        }

        /// <summary>
        /// 停止特殊片段：淡出回状态机。权重归零后销毁 clip 节点。
        /// </summary>
        /// <param name="fade">淡出时长（秒）</param>
        public void StopSpecialClip(float fade = 0.15f)
        {
            if (_specialClip == null) return;
            FadeSpecialTo(0f, fade);
        }

        #endregion

        #region 私有方法 (求值推进)

        /// <summary>
        /// 求值前推进权重渐变，并处理特殊片段播完后的退出。
        /// </summary>
        /// <param name="deltaTime">本次求值的累计时间（秒）。</param>
        private void Tick(float deltaTime)
        {
            if (_specialClip == null) return;
            TickSpecialClipTime();
            TickWeightFade(deltaTime);
        }

        #endregion

        #region 私有方法 (播放推进)

        /// <summary>非循环片段播完自动触发淡出；循环片段手动回绕时间。</summary>
        private void TickSpecialClipTime()
        {
            if (!_specialClipPlayable.IsValid()) return;

            double time = _specialClipPlayable.GetTime(); // 片段已经推进的时间。
            if (time < _specialClip.length) return;

            if (_specialLoop)
            {
                _specialClipPlayable.SetTime(time % _specialClip.length);
            }
            else
            {
                StopSpecialClip();
            }
        }

        /// <summary>权重向目标渐变，双路权重互补（w0 + w1 = 1）；淡出归零后销毁 clip 节点。</summary>
        private void TickWeightFade(float deltaTime)
        {
            if (Mathf.Approximately(_specialWeight, _specialWeightTarget)) return;

            _specialWeight = Mathf.MoveTowards(_specialWeight, _specialWeightTarget, _fadeSpeed * deltaTime);
            ApplyWeights();

            if (_specialWeightTarget <= 0f && _specialWeight <= 0f)
            {
                DestroySpecialClipPlayable();
            }
        }

        #endregion

        #region 私有方法 (工具)

        /// <summary>
        /// 设置权重渐变目标与速度
        /// </summary>
        private void FadeSpecialTo(float target, float fade)
        {
            _specialWeightTarget = target;
            _fadeSpeed = fade > 0f ? FullWeight / fade : float.MaxValue;
        }

        /// <summary>
        /// 把当前权重写入 mixer，保持双路权重互补。
        /// </summary>
        private void ApplyWeights()
        {
            _mixer.SetInputWeight(ControllerInputIndex, FullWeight - _specialWeight);
            _mixer.SetInputWeight(SpecialClipInputIndex, _specialWeight);
        }

        /// <summary>
        /// 销毁特殊片段节点并清空状态（完全淡出后调用）
        /// </summary>
        private void DestroySpecialClipPlayable()
        {
            if (_specialClipPlayable.IsValid())
            {
                _graph.DestroyPlayable(_specialClipPlayable);
            }
            _specialClip = null;
        }

        #endregion
    }
}
