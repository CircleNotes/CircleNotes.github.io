---
title: "[01]深度搜索和广度搜索"
date: 2026-11-23 18:26:00
categories:
  - 算法
  - 图论
tags:
  - 图论
  - BFS
  - DFS
  - CSharp
  - 游戏开发
excerpt: 从两个水格出发，给地图上每个地块填上到最近水格的距离。用 C# 源码和可单步播放的动画，观察队列与栈怎样改变搜索顺序，以及游戏里该怎样选用两种搜索。
---

给地图上的两个格子标上水。只能上下左右移动，每移动一格算一步。能不能给每个地块填上一个数字，表示它离最近的水有几步？

如果站在某个格子上找水，我们很容易想到向四周试走。但现在要给**整张图**填数字。换一个方向想，从两个水格同时出发，会发生什么？

<!-- more -->

## 一、把地块看成图，距离怎么算？

在上一篇七桥问题里，一块陆地对应一个顶点，一座桥对应一条边。这个练习也一样。每个格子是一个顶点，上下左右相邻的格子之间有一条可以双向通行的边。每条边的代价都是 `1`。代码里的方向偏移就是在现场计算邻居，省去了显式存储邻接表。[上一篇《[00]从七桥问题理解图论的诞生》](/posts/graph-theory-00-seven-bridges/)

示例中左边的 `1` 表示水格，右边的数字表示距离。两个水格在 `(0, 2)` 和 `(1, 0)`，坐标按 `(行, 列)` 从 `0` 开始。

```text
输入                输出
0  0  1             1  1  0
1  0  0      →      0  1  1
0  0  0             1  2  2
```

底行中间的 `(2, 1)` 到右上水格的曼哈顿距离是 `|2 - 0| + |1 - 2| = 3`，到左侧水格的距离是 `|2 - 1| + |1 - 0| = 2`，所以填 `2`。在没有障碍、只能四向移动的矩形网格中，这个距离正好等于最少移动步数。只有两个水格时，直接对每个格子取两次曼哈顿距离的较小值，就能在线性时间内算完整张图。这里用同一道题观察两种搜索的处理顺序。

## 二、让两个水格一起出发，为什么第一次就能填对？

先把两个水格都填为 `0`，依次放进队列。再从队首取出一个格子，给还未计算的邻格填上 `当前距离 + 1`，把它放到队尾。

当第 `k` 层的格子出队时，新发现的第 `k + 1` 层格子会排在所有仍待处理的第 `k` 层格子后面。假如有一条更短的路到某个格子，这条路的前一个格子应当更早出队，并且已经给它填值。因此，**首次填入的就是最短距离**。这种按层展开的顺序就是广度优先搜索（Breadth-First Search，BFS）。把多个起点一起放进第 `0` 层，就是多源 BFS。

跟着动画看 `(2, 1)`。从左侧水格出发，向右到 `(1, 1)`，再向下到 `(2, 1)`，一共走两步。

<style>
.graph-search-demo { display: block; width: 100%; height: 400px; border: 0; }
@media (max-width: 720px) { .graph-search-demo { height: 560px; } }
</style>

<iframe class="graph-search-demo" src="/graph-theory-01-animations.html?mode=bfs&amp;rev=content-height" title="广度优先搜索与队列动画" loading="lazy"></iframe>

[单独打开 BFS 动画](/graph-theory-01-animations.html?mode=bfs&rev=content-height)

## 三、沿一条分支先走，距离填大了怎么办？

把队列换成栈，新放进去的格子最先被取出。搜索因此能沿一个分支持续深入，之后再回到其他分支。这就是深度优先搜索（Depth-First Search，DFS）的处理思路。

看第一行中间的 `(0, 1)`。在这版实现的入栈顺序下，栈先处理左侧水格 `(1, 0)`，经 `(0, 0)` 给 `(0, 1)` 填了 `2`。但它紧挨着右上水格，正确答案是 `1`。如果还沿用「填过就跳过」的规则，它会停在 `2`。

这里的做法是**距离变短时重新入栈**。取出一个格子后，为每个邻格计算候选距离 `当前距离 + 1`。邻格还未计算，或者候选值更小，就写入新值，再把邻格入栈，让改进继续向外传。这一步叫作**松弛**。

这样得到的每个有限距离都对应某条实际走过的路线，所以至少等于最短距离。每次改写都让已有距离严格变小，有限网格上的改写最终会停下来。栈清空时，每个可达格子用自己的最新距离检查过邻居，因此每条可达边 `u → v` 都满足 `dis[v] ≤ dis[u] + 1`。沿着一条最短路径逐边应用这个条件，终点的记录值最多等于路径长度。这两个方向的约束合起来，结果就是最短距离。

<iframe class="graph-search-demo" src="/graph-theory-01-animations.html?mode=dfs&amp;rev=content-height" title="深度优先搜索与栈动画" loading="lazy"></iframe>

[单独打开 DFS 动画](/graph-theory-01-animations.html?mode=dfs&rev=content-height)

## 四、Unity 工程中的 C# 实现

下面摘出 `GraphLearn/BFS.cs` 和 `GraphLearn/DFS.cs` 中的 `Item` 与 `GetDis`。工程可以从 Unity 的 `Tools/BFS`、`Tools/DFS` 菜单运行同一组输入。`C` 对应二维数组的第一维下标（行），`R` 对应第二维下标（列）。

两份练习代码都按方形网格判断边界。要用于长方形网格，先确认行下标 `c` 有效，再把列上界从 `isWaters.Length` 改为 `dis[c].Length`。

### BFS 源码 · `GraphLearn/BFS.cs`

```csharp
using System.Collections.Generic;

public static class BFS
{
    struct Item
    {
        public int C;
        public int R;
    }

    public static int[][] GetDis(int [][] isWaters)
    {

        Queue<Item> queue = new Queue<Item>();
        int[] COffSet  = new int[4] {  0,0,1,-1 };
        int[] ROffSet  = new int[4] { 1,-1,0,0 };
        int[][] dis = new int[isWaters.Length][];

        for (int i = 0; i < isWaters.Length; i++)
        {
            dis[i] = new int[isWaters[i].Length];
            for (int j = 0; j < isWaters[i].Length; j++)
            {
                int value = isWaters[i][j];
                if (value == 1)
                {
                    queue.Enqueue(new Item { C = i, R = j });
                    dis[i][j] = 0;
                }
                else
                {
                    dis[i][j] = -1;
                }
            }
        }

        while (queue.Count > 0)
        {
            Item item = queue.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                int c = item.C + COffSet[i], r = item.R + ROffSet[i];
                if (c >= isWaters.Length || c < 0 || r >= isWaters.Length || r < 0 || dis[c][r] != -1)
                {
                    continue;
                }

                dis[c][r] = dis[item.C][item.R] + 1;
                queue.Enqueue(new Item { C = c, R = r });
            }
        }
        return dis;
    }
}
```

### DFS 源码 · `GraphLearn/DFS.cs`

```csharp
using System.Collections.Generic;

public class DFS
{
    struct Item
    {
        public int C;
        public int R;
    }

    public static int[][] GetDis(int [][] isWaters)
    {
        Stack<Item> stack = new Stack<Item>();
        int[] COffSet  = new int[4] {  0,0,1,-1 };
        int[] ROffSet  = new int[4] { 1,-1,0,0 };
        int[][] dis = new int[isWaters.Length][];

        for (int i = 0; i < isWaters.Length; i++)
        {
            dis[i] = new int[isWaters[i].Length];
            for (int j = 0; j < isWaters[i].Length; j++)
            {
                int value = isWaters[i][j];
                if (value == 1)
                {
                    stack.Push(new Item { C = i, R = j });
                    dis[i][j] = 0;
                }
                else
                {
                    dis[i][j] = -1;
                }
            }
        }

        while (stack.Count > 0)
        {
            var item = stack.Pop();
            int value = dis[item.C][item.R] + 1;
            for (int i = 0; i < COffSet.Length; i++)
            {
                int c = item.C + COffSet[i], r = item.R + ROffSet[i];
                if (c >= isWaters.Length || c < 0 || r >= isWaters.Length || r < 0)
                {
                    continue;
                }

                if (dis[c][r] == -1 || dis[c][r] > value)
                {
                    dis[c][r] = value;
                    stack.Push(new Item{C = c, R = r});
                }
            }
        }

        return dis;
    }
}
```

## 五、在游戏里怎样选？

首先问清楚「距离」是哪种成本。走过几个地块、消耗多少行动力、走了多少米，都可以用在同一张地图上，但会导向不同的搜索问题。

| 情境 | 怎样建图与搜索 |
| --- | --- |
| **战棋角色还有 3 步，能走到哪里？** | 把能站立的地块当作顶点，允许的一步移动当作边。每条边都消耗 1 步时，用 BFS 展开到第 3 层。首次入队时记录父节点，玩家选中目标后就能沿父节点还原一条最短步数路线。 |
| **每个格子离哪个补给点最近？** | 把多个补给点一起放进 BFS 队列，和水格题一样扩展。顺便记录传播距离的来源，就能给地图划分补给范围。墙壁和单向门由边表示，这时距离是实际可走的步数。 |
| **生成的地牢里，玩家能到出口吗？有几片独立区域？** | 从出生房间出发做 DFS 或 BFS，即可检查出口是否可达。对双向通行的房间图，从每个尚未访问的房间再发起一次搜索，可以标记所有连通区域。只做可达性时，每个节点访问一次就够了。 |
| **怎样生成一张有分叉和死路的迷宫？** | 用随机化 DFS。进入房间时，从未访问的邻室中随机选一个打通，走到无路可走就回退到最近的分叉点。每次打通都连接一个新房间，所以生成的通路没有环。如果设计需要绕行，可以再打通选定的额外通道。 |
| **任务或技能的前置关系会不会互相卡死？** | 把前置关系建成有向图。DFS 沿依赖走下去，同时标记当前递归路径。如果又遇到路径上的任务，就找到了一个循环依赖。 |

如果草地消耗 `1` 点行动力、沼泽消耗 `3` 点，BFS 仍能找到经过地块最少的路线。要找花费行动力最少的路线，就把行动力设成边权，用 Dijkstra 等加权最短路算法。
