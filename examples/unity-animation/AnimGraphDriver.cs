using UnityEngine;

namespace AnimationGraphExample
{
    /// <summary>
    /// 动画图的最小驱动示例：累计游戏时间，并按目标频率求值。
    /// 将组件挂到带有 Animator 的角色上，在 Inspector 中指定独立 Controller 资源。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class AnimGraphDriver : MonoBehaviour
    {
        #region 配置与运行时字段

        [SerializeField, Tooltip("由动画图持有的控制器资源，不需要配置在 Animator 的 Controller 槽位。")]
        private RuntimeAnimatorController _controller; // 当前动作域使用的控制器资源。

        [SerializeField, Min(0f), Tooltip("每游戏秒的目标求值次数；0 表示每个渲染帧求值。")]
        private float _animationUpdateRate = 15f; // 默认演示每秒15次的动画更新。

        private AnimGraph _animGraph; // 本组件创建并负责释放的动画图。
        private float _animationElapsedTime; // 尚未传给图的累计游戏时间。
        private double _animationUpdateTimer; // 保留求值周期余量的调度计时。

        /// <summary>
        /// 获取或调整动画更新频率，供上层距离分级等逻辑调用。
        /// 0 表示逐帧求值，实际求值次数不会超过渲染帧率。
        /// </summary>
        public float AnimationUpdateRate
        {
            get => _animationUpdateRate;
            set => _animationUpdateRate = Mathf.Max(0f, value);
        }

        #endregion

        #region 生命周期与调度

        /// <summary>
        /// 启用时使用显式配置的控制器建图，并输出初始姿态。
        /// 重新启用会新建状态机实例，从其初始状态开始播放。
        /// </summary>
        private void OnEnable()
        {
            if (_controller == null)
            {
                Debug.LogError("请在 AnimGraphDriver 中指定 Controller 资源。", this);
                enabled = false;
                return;
            }

            _animationElapsedTime = 0f;
            _animationUpdateTimer = 0d;
            _animGraph = new AnimGraph(GetComponent<Animator>(), _controller);
            _animGraph.Evaluate(0f);
        }

        /// <summary>
        /// 每帧累计游戏时间，到期后最多求值一次。
        /// 合并跳过帧的时间，使低频动画保持原有播放速度。
        /// </summary>
        private void Update()
        {
            if (_animGraph == null) return;

            float deltaTime = Time.deltaTime; // 使用受 timeScale 影响的游戏时间。
            _animationElapsedTime += deltaTime;

            if (_animationUpdateRate > 0f)
            {
                double interval = 1d / _animationUpdateRate; // 相邻求值时机的目标间隔。
                _animationUpdateTimer += deltaTime;
                if (_animationUpdateTimer < interval) return;

                // 保留余量，使60 FPS下的40次/秒等非整除频率也能接近目标。
                _animationUpdateTimer %= interval;
            }
            else
            {
                _animationUpdateTimer = 0d;
            }

            _animGraph.Evaluate(_animationElapsedTime);
            _animationElapsedTime = 0f;
        }

        /// <summary>
        /// 禁用或销毁组件时释放图，并清空未消费的计时。
        /// Animator 的 Controller 保持为空，由下次启用时重新建图。
        /// </summary>
        private void OnDisable()
        {
            _animGraph?.Release();
            _animGraph = null;
            _animationElapsedTime = 0f;
            _animationUpdateTimer = 0d;
        }

        #endregion

        #region 调用示例接口

        /// <summary>
        /// 装配外部已经取得的新控制器；此处不负责资源加载和卸载。
        /// 替换会创建新的状态机实例，不保留旧实例的状态与参数。
        /// </summary>
        public void SetController(RuntimeAnimatorController controller)
        {
            if (controller == null) return;
            _controller = controller;
            _animGraph?.SetController(controller);
        }

        /// <summary>
        /// 将浮点参数直接写入图内的状态机实例。
        /// 参数哈希可以通过 Animator.StringToHash 获取。
        /// </summary>
        public void SetFloat(int parameterId, float value)
        {
            _animGraph?.SetFloat(parameterId, value);
        }

        /// <summary>
        /// 将一个特殊片段淡入到基础控制器输出之上。
        /// 非循环片段播完后自动淡出，循环片段由调用方停止。
        /// </summary>
        public void PlaySpecialClip(AnimationClip clip, float fade = 0.15f, bool loop = false)
        {
            _animGraph?.PlaySpecialClip(clip, fade, loop);
        }

        /// <summary>
        /// 将特殊片段淡出，恢复基础控制器的输出。
        /// 淡出和片段结束判断均随图求值频率推进。
        /// </summary>
        public void StopSpecialClip(float fade = 0.15f)
        {
            _animGraph?.StopSpecialClip(fade);
        }

        #endregion
    }
}
