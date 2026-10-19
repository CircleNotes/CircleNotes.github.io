# PlayableGraph 动画示例

配套文章：[用 PlayableGraph 拆解大型动画状态机](https://circlenotes.github.io/posts/unity-playablegraph-modular-animator-controllers/)。示例仅依赖 Unity 自带 API，使用通用命名，不包含项目资源、内部路径或业务系统。

## 示例目标与结构

- [AnimGraph.cs](AnimGraph.cs)：手动动画图，负责 Controller 与单个特殊 Clip 的装配、混合、参数写入和释放。
- [AnimGraphDriver.cs](AnimGraphDriver.cs)：MonoBehaviour 驱动，负责生命周期、游戏时间累计与按目标频率求值。

需求是让文章读者复制两份脚本后，就能用自己的模型和控制器观察低频动画，并理解后续如何接入控制器切换与距离分级。设计上保持资源取得、图装配、更新调度三个职责分开：调用方提供资源，`AnimGraph` 处理动画节点，`AnimGraphDriver` 决定何时求值。

图的混合骨架为 `Controller / Special Clip → Mixer → Animator`。构造时将 Animator 自带的 Controller 置空，参数直接写入 `AnimatorControllerPlayable`。这里的“按需”指装配接口；示例用 Inspector 引用演示，不包含异步加载、资源卸载或资源依赖拆分。

## 在 Unity 中使用

1. 将 `AnimGraph.cs` 和 `AnimGraphDriver.cs` 复制到 Unity 项目的普通脚本目录，两份文件放在同一个程序集，不能放到 `Editor` 目录。
2. 准备带有有效 Animator 和 Avatar 的角色，以及匹配的 RuntimeAnimatorController 资源。Controller 默认状态应引用一段能够循环播放的动画。
3. 在 Animator 所在的 GameObject 上添加 `AnimGraphDriver`，将 Controller 资源拖到驱动脚本的 **Controller** 字段。Animator 自身的 Controller 槽位可以留空。
4. 进入 Play Mode，调整 **Animation Update Rate**：`0` 为逐帧，`15` 为每游戏秒目标15次，`1` 为便于观察的逐格输出。
5. 在相同渲染帧率下对比这些配置：低频时姿态更稀疏，但循环播放节奏应基本一致。实际求值次数不会超过渲染帧率。

默认使用 `Time.deltaTime`，跟随 `timeScale`。驱动用两份计时分别保存“尚未求值的游戏时间”和“调度周期余量”；到期后只求值一次，把完整累计时间传入 `Evaluate`，不逐帧追赶补算，也不补插骨骼姿态。启用时额外用 `Evaluate(0)` 输出初始姿态。

禁用组件会释放图，再次启用时重新建图并从 Controller 的初始状态播放，不恢复先前的状态机进度；禁用后 Animator 的 Controller 保持为空。用于接入此示例的 Animator 应由这一份驱动独占。

## 从其他脚本调用

通过已有组件引用 `driver` 调用即可：

```csharp
// 上层决定更新频率，例如根据角色与相机的距离分级。
driver.AnimationUpdateRate = 15f;

// Controller 中需要存在名为 Speed 的 Float 参数。
driver.SetFloat(Animator.StringToHash("Speed"), 1.5f);

// 调用方先取得新控制器资源，再交给图装配。
driver.SetController(loadedController);

// 片段需要与角色骨骼匹配；非循环片段播完后自动淡出。
driver.PlaySpecialClip(specialClip, fade: 0.15f, loop: false);
driver.StopSpecialClip(fade: 0.15f);
```

`driver`、`loadedController` 和 `specialClip` 是调用方已经持有的引用，上面的片段用于说明调用方式。切换 Controller 会重建状态机实例；新状态机的参数需要调用方重新设置。特殊片段权重达到1时，基础状态机仍继续推进。持续替换特殊片段会直接替换该输入节点，不提供两个特殊片段之间的独立交叉淡化。

## 验证范围

脚本以 Unity 6.3 API 为背景。C# 编译检查验证类型与 API 引用，博客构建检查文章及源码链接的生成；它们不代替 Play Mode 的实际骨骼播放验证。复制后按上面的步骤，用自己的模型、Controller 和 Clip 检查姿态、切换和生命周期。
