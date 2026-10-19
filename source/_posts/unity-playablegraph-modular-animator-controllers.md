---
title: 用 PlayableGraph 拆解大型动画状态机：方案对比、按需加载与距离分级更新
date: 2026-10-17 23:30:00
updated: 2026-10-19 18:00:00
categories:
  - Unity动画
tags:
  - Unity
  - PlayableGraph
  - Animator
  - 动画系统
excerpt: 对比传统 Animator Controller 与 PlayableGraph 的职责和取舍，说明小型控制器按需加载、运行时混合、手动更新频率及距离分级的实现方式。
---

随着角色动作不断增加，一个 Animator Controller 往往逐渐包含移动、交互、展示和各类特殊动作。状态结构越来越复杂，资源引用范围也越来越大，角色当前真正需要的内容却可能只是其中一部分。

一种处理方式是把控制器按动作域拆成较小的模块，由运行时代码按需加载，再用 PlayableGraph 统一装配、混合和输出。这个目标并不要求抛弃 Animator Controller：小型状态机可以继续负责动作域内部的状态与过渡，代码负责模块之间的组合和调度。

本文以 Unity **6.3** 的 Playables API 为背景，先比较两种组织方式，再说明控制器按需装配、参数写入、手动更新和距离分级的实现。示例用于表达关键接口与算法，资源系统和生命周期应按实际工程补齐。

## 1. 为什么要拆解大型状态机

当跑步、骑行、交互、展示、特殊动作等逻辑长期堆入一个 Animator Controller 时，状态、过渡和片段引用会持续增加。角色当前只用其中一小部分，控制器却仍与大量不相关内容建立资源引用。

状态没有激活，并不等于其资源依赖不会随加载流程被带入。具体加载范围还取决于资源系统、Bundle 依赖和引用关系；仅把状态移动到 Sub-State Machine，仍然属于同一个 Controller 资产，也没有建立独立的加载边界。

真正想调整的是装配单位：从“一个 Controller 包含所有动作”，转为“按需要选择一份较小的动作控制器”。以下是拆分思路示例，并不表示项目已实现所有模块：

```text
原有结构
角色 → 总 Controller → 跑步 / 骑行 / 交互 / 展示 / 特殊动作

目标结构
当前动作需求 → 加载对应的小 Controller
                    │
                    ▼
               运行时动画图 → Animator → 骨骼
```

拆分边界可以围绕玩法、运动类型或动作组确定。每个小 Controller 只负责本组的状态、过渡与必要片段；跨动作域的选择和切换由运行时代码安排。一次性特殊动作也可以直接加载 Clip，减少把所有例外动作塞回基础状态机的压力。

如果仍把完整的大 Controller 交给 AnimatorControllerPlayable，只是改变了它的运行载体，没有完成状态机资产拆分。

## 2. PlayableGraph 与传统 Animator Controller 的区别

传统 Animator Controller 把状态、过渡条件、Blend Tree 和层组织在控制器资产中，运行时由 Animator 根据参数驱动。PlayableGraph 则允许代码组织动画处理节点，决定当前连接哪些控制器或片段、如何混合，以及何时求值。

两者可以组合使用。`AnimatorControllerPlayable` 可以把现有 Controller 作为图中的一个节点，而 `AnimationPlayableOutput` 仍把结果输出给 Animator。差别主要在于：**哪些决策留在控制器资产中，哪些决策交给运行时代码。** [Unity 的 Playables 文档](https://docs.unity3d.com/6000.3/Documentation/Manual/Playables.html)将其定位为对 Mecanim 的补充，并提供运行时添加节点、动态混合和控制权重的能力。

| 对比维度 | 传统 Animator Controller 为主 | 运行时 PlayableGraph 为主 |
| --- | --- | --- |
| 状态组织 | 在资产中编辑状态、过渡、参数、Blend Tree 和层 | 代码装配控制器、片段与 Mixer；节点内部仍可使用状态机 |
| 动作切换 | 常通过参数、Trigger、状态过渡或 CrossFade 驱动 | 可以直接替换控制器节点、连接 Clip、调整输入权重 |
| 资源加载 | 由当前 Controller 的引用关系与资源加载策略决定；也可以动态切换 Controller | 代码更容易围绕当前动作模块显式加载和装配；实际依赖仍由资源组织决定 |
| 混合方式 | 使用控制器中预先配置的 Blend Tree、动画层和过渡 | 代码组合 Mixer、Clip 与控制器节点，运行时调整权重和连接 |
| 更新调度 | 通常交给 Animator 的自动更新流程，适合常规逐帧动画 | 可选择 Manual，由代码调用 Evaluate，给不同角色设置独立频率 |
| 单个特殊动作 | 常新增状态、Trigger 或层，也可结合 Playables | 可直接接入独立 Clip，无需为每个一次性动作增加固定状态 |
| 可视化与调试 | Animator 窗口直接展示状态和过渡，内容人员容易参与 | 图结构与时序更多由代码管理，需要额外关注节点、参数和资源生命周期 |
| 工程成本 | 控制器内部过渡较成熟；大型资产容易产生组织与维护压力 | 模块边界更灵活，但需自行处理异步请求、状态恢复、释放及跨模块过渡 |

### 哪些优势来自这套设计

1. **模块化装配**：当前玩法只装配所需的小型 Controller，减少把所有动作域长期堆在一个总状态机里的压力。
2. **资源与播放解耦**：资源层决定加载什么，图层决定如何连接与输出，便于建立清楚的加载和释放边界。
3. **特殊动作组合**：独立 Clip 可以与基础状态机混合，使一次性动作通过统一接口接入。
4. **按角色分配求值频率**：同一套图封装可以承接每帧更新、限频更新和后续距离分级策略。

这些收益依赖具体组织方式。把完整的大 Controller 原样放进图中，不会自动减少它的状态或资源依赖；手动求值也不保证比引擎自动更新更快。

### 哪些情况继续使用传统方案更合适

如果角色只有一个规模适中的动作域，状态和过渡关系清晰，且内容人员主要通过 Animator 窗口维护动画，直接使用 Controller 通常更容易实现和维护。

当需求已经包含多个可独立加载的动作域、动态组合的特殊片段，或每角色不同的更新策略时，运行时图更方便承担装配层。实际项目可以采用混合方式：**小 Controller 管局部状态，PlayableGraph 管模块组合与求值。**

## 3. 按需加载 Controller，OverrideController 又负责什么

运行时装配的目标流程是：

```text
业务决定当前动作组
    ↓
查询动作配置，得到对应 Controller 的资源地址
    ↓
异步加载 Controller / OverrideController
    ↓
在 PlayableGraph 中创建或替换 AnimatorControllerPlayable
    ↓
恢复新控制器需要的参数，继续输出到同一个 Animator
    ↓
旧节点不再使用，按资源系统规则释放旧控制器的加载引用
```

需要区分两件事：

| 手段 | 作用 |
| --- | --- |
| 将总 Controller 拆成独立的小 Controller | 划分状态结构和资源加载边界 |
| AnimatorOverrideController | 复用基础状态机结构，并替换其中的动画片段 |

OverrideController 保留基础 Controller 的结构、参数和状态机逻辑。它适合给一个较小的基础模板提供不同角色或动作风格的片段变体，**不会自动把一个庞大的基础状态机裁剪成小状态机**。[Unity：Animator Override Controller](https://docs.unity3d.com/6000.3/Documentation/Manual/AnimatorOverrideController.html)。

按需加载要落到资源层面，还需要让加载依赖与使用范围保持一致：

- 角色预制体、全局配置或常驻对象不应意外强引用全部动作控制器。
- 配置中的资源地址与直接持有资源对象引用，需要区分管理。
- 不同控制器如果被合入同一个大 Bundle，依赖加载范围仍可能大于预期。
- 销毁 Playable 节点只是解除图中的使用，不能代替资源系统 Handle 的配对释放。
- 缓存中的 Controller、Clip 和加载引用，也会影响旧资源能否释放。

因此，这套结构提供了缩小按需加载范围的条件；实际内存是否减少，还取决于控制器拆分、构建依赖和资源生命周期，不能仅由“改成 PlayableGraph”得出结论。

## 4. 把“加载”和“播放装配”分开

资源层负责“拿到哪份控制器”，动画图负责“如何使用控制器输出姿态”。两者通过 RuntimeAnimatorController 资源引用衔接：

```text
业务动作需求
    ↓
控制器资源地址 / 资源标识
    ↓
资源层异步加载，返回 Controller 及其持有凭据
    ↓
动画图替换 Controller 节点
    ↓
业务恢复参数，旧资源不再使用后配对释放
```

当一个小型控制器加载完成，可以断开旧节点、销毁旧节点，再创建并连接新节点。Mixer 和输出端继续使用：

```csharp
/// <summary>替换图中的动作控制器，保留 Mixer 与输出。</summary>
internal void SetController(RuntimeAnimatorController controller)
{
    if (controller == null || !_graph.IsValid()) return;

    if (_controllerPlayable.IsValid())
    {
        _graph.Disconnect(_mixer, 0);
        _graph.DestroyPlayable(_controllerPlayable);
    }

    _controllerPlayable = AnimatorControllerPlayable.Create(_graph, controller);
    _graph.Connect(_controllerPlayable, 0, _mixer, 0);
}
```

上面的示意是直接替换，并不提供两个 Controller 之间的交叉淡化。如果需要保留旧姿态并平滑过渡，可以临时为新旧控制器分配两个输入，在过渡完成后释放旧节点和资源；这会增加过渡期内的资源持有和求值工作。

异步加载还需要一个明确的“谁有权应用结果”的约定。例如动作请求 A 尚未完成时又发出 B，A 的较晚返回不应覆盖更新的 B。常见处理方式是请求序号校验或取消旧请求，销毁角色时也应取消未完成请求，并释放已获得但不再使用的资源持有凭据。

这使控制器拆分不仅是资产组织问题，也成为一条可管理的生命周期：**请求 → 加载 → 应用 → 使用 → 替换 → 释放**。

## 5. 先明确各层负责什么

角色动画可以划分为三层职责：

| 层次 | 职责 | 对应内容 |
| --- | --- | --- |
| 业务层 | 决定角色的运动意图 | 运动速度、停止、动作请求 |
| 动画控制层 | 设置参数、安排求值、控制混合 | 角色动画组件与动画图封装 |
| 输出层 | 将动画图的结果应用到角色 | Animator 与骨骼 |

业务层提供速度，动画控制层把它转换为状态机参数；角色世界坐标的移动由移动组件负责。这样，动画姿态更新频率与角色整体移动频率可以分别控制。

```text
运动数据 Speed
    │
    ▼
业务层平滑得到 AnimationSpeed
    │
    ▼
图内状态机的 Speed 参数
    │
    ▼
PlayableGraph 求值 → Animator → 骨骼姿态

角色根节点位移：由移动组件单独更新
```

在这种职责划分下，动画姿态可以低频更新，角色整体仍按移动组件的节奏连续前进。

## 6. 图结构：基础运动与特殊动作两路混合

一种实用的组合方式是保留当前动作域的基础状态机，并额外提供一路特殊动作输入：

```text
AnimatorControllerPlayable ── 输入 0 ──┐
基础运动：Speed 驱动 Blend Tree         │
                                      ├─ AnimationMixerPlayable
AnimationClipPlayable ─────── 输入 1 ──┘            │
特殊动作：代码请求播放                              ▼
                                      AnimationPlayableOutput
                                                  │
                                                  ▼
                                               Animator
                                                  │
                                                  ▼
                                                骨骼
```

Mixer 始终保留两个输入端口。基础运动默认权重为 1，特殊动作默认权重为 0；特殊动作请求到来时才创建对应的 Clip 节点。

这里的“图结构稳定”指两路输入和输出的骨架稳定。切换特殊片段时仍会销毁、重建 Clip 节点，替换 Controller 时也会替换 Controller 节点，不能理解为所有节点终身不变。

图封装的构造函数接收 Animator 和已加载的 RuntimeAnimatorController。建图的关键部分如下：

```csharp
// 创建由代码手动求值的动画图。
_graph = PlayableGraph.Create("CharacterAnimation");
_graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

// 用独立的运行时节点承载基础运动状态机。
_controllerPlayable = AnimatorControllerPlayable.Create(_graph, controller);

// 固定两个输入端口，第二路留给特殊动作。
_mixer = AnimationMixerPlayable.Create(_graph, 2);
_graph.Connect(_controllerPlayable, 0, _mixer, 0);
_mixer.SetInputWeight(0, 1f);
_mixer.SetInputWeight(1, 0f);

// Animator 负责接收动画图的输出。
_output = AnimationPlayableOutput.Create(_graph, "Animation", animator);
_output.SetSourcePlayable(_mixer);
_graph.Play();

// 图建立完成后，移除 Animator 自带控制器，由手动图统一输出。
animator.runtimeAnimatorController = null;
```

Manual 模式下，需要代码显式调用 `PlayableGraph.Evaluate(deltaTime)`。`Play()` 将图置于播放状态，并不把 Manual 模式改回 GameTime。[Unity：Manual 更新模式](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Playables.DirectorUpdateMode.Manual.html)。

## 7. Controller 引用与参数如何管理

角色动画组件先保存初始化所需的 Controller，再把它显式传入图的构造函数。构造函数完成节点和输出的连接后，统一清空 `animator.runtimeAnimatorController`，Animator 继续保持启用并接收图的输出。

```csharp
// 保存初始控制器资源，用于首次建图和重建。
_initialController = animator.runtimeAnimatorController;

// 构造函数接收显式资源，并在建图后清空 Animator 自带 Controller。
_animGraph = new AnimGraph(animator, _initialController);
```

状态参数直接写入图里的 AnimatorControllerPlayable：

```csharp
/// <summary>将运动参数写入图内状态机。</summary>
internal void SetFloat(int id, float value)
{
    if (_controllerPlayable.IsValid())
        _controllerPlayable.SetFloat(id, value);
}
```

业务层只需要调用图封装提供的参数接口，不需要关心状态机节点的具体连接。这样参数、状态推进和最终输出都归属于同一条运行时链路。[Unity：AnimatorControllerPlayable.SetFloat](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Animations.AnimatorControllerPlayable.SetFloat.html)。

重建时继续使用保存的控制器资源：

```csharp
// 释放旧图后，使用缓存资源重建，而不是从已清空的 Animator 读取。
_animGraph.Release();
_animGraph = new AnimGraph(animator, _initialController);
```

这里采用的是“重置到初始 Controller”的语义。如果需要恢复最后一次动态切换的控制器，应另外维护当前 Controller 及其资源持有关系。替换控制器节点后，新实例所需的速度、状态和播放进度也应明确恢复。

## 8. 基础运动还保留在状态机里

接入 PlayableGraph，不等于已经把状态机完全拆成代码。

例如，一个小型移动 Controller 可以保留待机、行走、跑步和速度 Blend Tree。当前控制器只需要描述移动动作域，交互、展示等其他动作域由独立资产承载。

同一动作域需要不同外观或动作风格时，可以用 OverrideController 复用较小的控制结构，再替换其中的片段。速度阈值和播放倍率属于动画内容配置，不需要变成所有业务共享的硬编码。

这一步拆分后，代码接管了更新调度和特殊动作混合，基础运动的片段选择、权重计算及播放倍率仍保留在 Controller 的 Blend Tree 中。进一步拆解时，才需要把这些计算移到运行时代码，并用多个 AnimationClipPlayable 直接构建运动混合图。

## 9. 特殊动作由运行时代码控制

特殊动作通过独立 Clip 节点与基础运动进行全身交叉混合。设特殊动作权重为 `w`：

```text
基础运动权重 = 1 - w
特殊动作权重 = w
```

播放流程为：

1. 若存在旧特殊片段，释放其节点。
2. 创建新的 AnimationClipPlayable，并接入 Mixer 的输入 1。
3. 将权重目标设为 1，按淡入时间逐步增加权重。
4. 停止时将权重目标设为 0，淡出完成后释放 Clip 节点。
5. 非循环片段到达末尾后请求淡出；循环片段在达到长度后回绕时间。

这是一种权重交叉混合，尚未引入 AvatarMask 或分层覆盖。连续替换特殊片段时，这里讨论的实现保留混合权重，但直接替换片段节点，并没有同时保留两个特殊片段做独立的片段间淡化。

如果特殊动作计时和权重推进也随 Graph 求值执行，降低求值频率会同时降低淡入、淡出和结束判断的时间分辨率。基础运动是否继续推进应成为明确策略，不能把特殊片段权重达到 1 等同于基础状态机已经暂停。

特殊动作和控制器替换是两种能力：前者混合一个独立 Clip，后者切换当前动作域的状态机。它们可以共用输出结构，但各自的切换和释放规则应独立设计。

## 10. 手动求值：降频，同时保留正确的播放时间

动画更新频率约定为每个游戏秒内的目标求值次数：

| 配置值 | 含义 |
| --- | --- |
| 0 | 每个渲染帧求值一次 |
| 30 | 目标每游戏秒求值 30 次 |
| 15 | 目标每游戏秒求值 15 次 |
| 1 | 目标每游戏秒求值 1 次，适合观察逐格效果 |

每帧最多求值一次，实际次数不超过当前渲染帧率。例如实际只有 20 FPS，配置 30 次/秒，也不会得到 30 个可见姿态。

这里需要两份计时：

- **动画经过时间**：累计尚未传给 Evaluate 的游戏时间。
- **调度计时**：判断是否到达下一次更新时机，并保留周期余量。

```csharp
// 以下字段由组件持有：目标频率、未求值时间和调度余量。
private float _animationUpdateRate;
private float _animationElapsedTime;
private double _animationUpdateTimer;

/// <summary>按目标频率推进动画图，合并跳过帧的时间。</summary>
private void Update()
{
    if (_animGraph == null) return;

    // 使用游戏时间，跟随 timeScale。
    float deltaTime = Time.deltaTime;
    _animationElapsedTime += deltaTime;

    if (_animationUpdateRate > 0f)
    {
        // 每个渲染帧最多求值一次。
        double interval = 1d / _animationUpdateRate;
        _animationUpdateTimer += deltaTime;
        if (_animationUpdateTimer < interval) return;

        // 保留周期余量，避免非整除频率长期偏低。
        _animationUpdateTimer %= interval;
    }
    else
    {
        _animationUpdateTimer = 0d;
    }

    // 一次推进完整的累计时间，保持整体播放节奏。
    _animGraph.Evaluate(_animationElapsedTime);
    _animationElapsedTime = 0f;
}
```

以 60 FPS、动画目标 15 次/秒为例，理想情况下每隔四帧求值一次，每次向动画传入约 `4 / 60` 秒。这样改变的是可见姿态的更新间隔，跑步循环的整体节奏保持一致。

如果跳过三帧后只传入当前帧的 `Time.deltaTime`，动画时间就会落后，表现成慢动作。

调度余量也不能每次简单清零。例如 60 FPS 下目标 40 次/秒，保留余量才能让更新间隔在不同渲染帧数之间分配，长期接近目标次数。

`Evaluate` 会求值图中的输出并更新连接的 Playable，并非仅修改一个计时变量。[Unity：PlayableGraph.Evaluate](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Playables.PlayableGraph.Evaluate.html)。

## 11. 基于距离的动画更新分级

当每个角色已经拥有可独立设置的 `AnimationUpdateRate`，上层就具备了按重要程度分配动画更新频率的基础。最直接的扩展是：其他角色距离自己越远，降低其动画姿态的更新频率。

每角色可配置的频率控制已经实现。距离分级可以作为上层策略接入：它负责选择频率，底层动画组件负责按该频率推进图。这里给出设计关系，不把距离自动分级描述为已经完成的功能。

分级策略可以从下面的关系开始，具体数值应结合镜头、角色屏幕占比和动作观感调整：

| 角色情况 | 更新策略示例 | 意图 |
| --- | --- | --- |
| 主角、当前重点观察角色 | 0，每帧求值 | 保持主要角色动作细节 |
| 距离自己较近的其他角色 | 30 次/游戏秒 | 保留较清晰的动作变化 |
| 中远距离角色 | 15 次/游戏秒 | 降低不易辨认的姿态更新密度 |
| 更远、仍需显示的角色 | 5～10 次/游戏秒 | 用更少的求值呈现远处运动 |

这些频率只是可配置的策略示例，需要按镜头和动作观感调整；它们改变骨骼姿态求值频率，不改变场景本身的渲染帧率。

```text
距离 / 角色重要性
        ↓
上层选择目标动画频率
        ↓
角色动画组件.AnimationUpdateRate
        ↓
累计未求值时间，到期后 Evaluate
        ↓
远处角色保持运动节奏，以较低频率更新骨骼姿态
```

这个策略调整的是动画姿态更新。业务运动数据和角色根节点位移仍按其原有流程处理，所以远处角色可以连续移动，同时减少手脚姿态求值次数。

落地时有几个边界需要明确：

- **距离参照**：可以使用其他角色到主角的距离；如果镜头能够自由移动，屏幕占比和到相机的距离也值得纳入策略。
- **分级抖动**：角色在距离阈值附近往返时，可使用不同的进入与退出阈值，避免频率来回切换。
- **策略更新频率**：距离等级可以定期刷新，没有必要为了调低动画频率而每帧重复修改配置。
- **时间连续性**：切换等级时保留尚未求值的动画经过时间，避免改变跑步节奏；相同频率重复赋值也不应打断当前周期。频率设置接口应遵守这两个约定。
- **角色批次**：大量角色同步切换到相同频率，可能集中在同一帧求值，可进一步设计不同调度相位。
- **完全不可见角色**：是否停止动画、回收模型或移出图，应属于独立的可见性与生命周期策略。当前频率值 0 表示每帧更新，不能用来表达停更。

距离分级的价值在于把已实现的底层控制能力转成上层策略：动画图负责“如何按频率正确推进”，业务负责“当前角色值得以多高的频率更新”。

## 12. 生命周期需要一起考虑

手动图接管后，组件生命周期需要明确管理：

- **Awake**：缓存建图资源，建立动画图。
- **Update**：写入对应实例的参数，按调度决定是否求值；参数写入与求值的先后要明确。
- **OnDisable**：这里讨论的实现清空累计动画时间和调度余量，图保留用于后续恢复。
- **Reset**：释放并重建图，使用保存的初始 Controller，清理频率计时。
- **OnDestroy**：释放 Graph，取消相关加载任务。

对于 Manual 图，跳过 Evaluate 的帧会保持上次输出的姿态；这里讨论的实现没有额外增加骨骼姿态插值。整体位移仍可每帧更新，因此低频动画配合连续移动时，近看会更容易察觉逐格效果。

如果运行中替换图内 Controller，原节点的状态和参数不会因为复用同一图而自动保留。需要哪些参数、当前动作或播放进度，都应由业务明确恢复。动态部件的骨骼绑定、特殊动作节点和资源持有也应纳入相应生命周期。

## 13. 这套实现的边界

运行时动画图可以统一管理创建与释放、控制器节点替换、特殊片段混合、参数入口及求值时机。每角色更新频率是已经实现的基础能力，距离分级则是可继续接入的上层策略。

目标是让角色按需装配小型动作控制器，而不是要求所有状态机都改写成代码。基础运动 Blend Tree 可以继续保留在较小的 Controller 内。各角色独立驱动或使用统一调度器，是另一个可以单独选择的调度层设计，不影响资产拆分原则。

要解决状态机过大的问题，核心是先划分 Controller 资产与资源依赖的边界，再由运行时代码按需装配。落到动画输出时，还要把驱动链路收敛到明确的位置：**Controller 资源由代码保留，状态机实例位于手动 Graph，参数写入该实例，Animator 接收输出。** 只有明确了这些归属，减少 Evaluate 次数才有清楚、可验证的含义。

## 参考资料

- [Unity 6.3：Playables API](https://docs.unity3d.com/6000.3/Documentation/Manual/Playables.html)
- [Unity 6.3：Animator Override Controller](https://docs.unity3d.com/6000.3/Documentation/Manual/AnimatorOverrideController.html)
- [Unity 6.3：DirectorUpdateMode.Manual](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Playables.DirectorUpdateMode.Manual.html)
- [Unity 6.3：PlayableGraph.Evaluate](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Playables.PlayableGraph.Evaluate.html)
- [Unity 6.3：AnimatorControllerPlayable.Create](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Animations.AnimatorControllerPlayable.Create.html)
- [Unity 6.3：AnimatorControllerPlayable.SetFloat](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Animations.AnimatorControllerPlayable.SetFloat.html)
