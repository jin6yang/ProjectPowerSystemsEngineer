using UnityEngine;
using System.Collections.Generic;
using ProjectPowerSystemsEngineer.Grid;
using ProjectPowerSystemsEngineer.Components;
using ProjectPowerSystemsEngineer.Data;

namespace ProjectPowerSystemsEngineer.Simulation
{
    /// <summary>
    /// 核心电网模拟中枢 (Power Simulation System)
    /// 负责每一帧的图论计算、闭环探测、死锁打破以及宏节点(资源池)的能量分配。
    /// </summary>
    public class PowerSimulationSystem : MonoBehaviour
    {
        public static PowerSimulationSystem Instance { get; private set; }

        private List<StoragePool> activePools = new List<StoragePool>();
        private bool needsRecalculation = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Update()
        {
            bool poolStateChanged = false;

            // 驱动所有储能池的时间流逝
            foreach (var pool in activePools)
            {
                if (pool.Tick(Time.deltaTime))
                {
                    poolStateChanged = true;
                }
            }

            // 如果池子发生重大状态翻转（充满或耗尽），触发全网重算
            if (poolStateChanged || needsRecalculation)
            {
                needsRecalculation = false;
                RecalculatePowerGrid();
            }
        }

        /// <summary>
        /// 执行全网图论拓扑排序及能量分配 (核心引擎)
        /// </summary>
        public void RecalculatePowerGrid()
        {
            PowerNode[] rawNodes = FindObjectsByType<PowerNode>(FindObjectsSortMode.None);
            List<PowerNode> allNodes = new List<PowerNode>();

            // 过滤掉所有未注入Data的残影(Ghost)节点，防止空指针异常
            foreach (var node in rawNodes)
            {
                if (node.data != null) allNodes.Add(node);
            }

            // ==========================================
            // 阶段 1：状态保存与自愈重置
            // ==========================================
            foreach (var node in allNodes)
            {
                // 如果是储能建筑，在打碎旧池子前，先继承它的电量百分比
                if (node.data.category == ComponentCategory.Storage)
                {
                    node.SavedChargePercent = (node.MyPool != null && node.MyPool.TotalChargeTime > 0)
                        ? Mathf.Clamp01(node.MyPool.CurrentChargeTimer / node.MyPool.TotalChargeTime)
                        : 0f;
                }

                // 【核心修复1】：强行洗去所有节点的资源池记忆！
                // 这彻底防止了玩家拆除阵列中间的连线后，两端的电池依然残留着“我们是一个阵列”的幽灵Bug。
                node.MyPool = null;

                // 每次重算前无条件解除保护，如果违规依然存在后续会自动再次红灯，从而实现“危机解除自动自愈”。
                node.ResetProtection();
            }
            activePools.Clear();

            // ==========================================
            // 阶段 2：寻找无向图连通块 (划分物理电网大陆)
            // ==========================================
            Dictionary<PowerNode, List<PowerNode>> undirectedGraph = new Dictionary<PowerNode, List<PowerNode>>();
            foreach (var node in allNodes) undirectedGraph[node] = new List<PowerNode>();

            // 将所有单向连接映射为无向图的双向连接
            foreach (var node in allNodes)
            {
                foreach (var target in node.OutgoingConnections)
                {
                    if (undirectedGraph.ContainsKey(target))
                    {
                        undirectedGraph[node].Add(target);
                        undirectedGraph[target].Add(node);
                    }
                }
            }

            HashSet<PowerNode> visited = new HashSet<PowerNode>();
            List<List<PowerNode>> networks = new List<List<PowerNode>>();

            // 广度优先搜索 (BFS) 找出所有的物理大陆
            foreach (var node in allNodes)
            {
                if (!visited.Contains(node))
                {
                    List<PowerNode> net = new List<PowerNode>();
                    Queue<PowerNode> q = new Queue<PowerNode>();
                    q.Enqueue(node);
                    visited.Add(node);

                    while (q.Count > 0)
                    {
                        var curr = q.Dequeue();
                        net.Add(curr);

                        foreach (var neighbor in undirectedGraph[curr])
                        {
                            if (!visited.Contains(neighbor))
                            {
                                visited.Add(neighbor);
                                q.Enqueue(neighbor);
                            }
                        }
                    }
                    networks.Add(net);
                }
            }

            // ==========================================
            // 阶段 2.5：基于 DFS 的电网合规性检测 (彻底合法化储能闭环)
            // ==========================================
            foreach (var net in networks)
            {
                foreach (var node in net)
                {
                    // 规则 1：发电机之间严禁通过电线直接相连
                    if (node.data.category == ComponentCategory.Generation)
                    {
                        foreach (var cable in node.OutgoingConnections)
                        {
                            if (cable.data != null && cable.data.isPointToPointCable)
                            {
                                foreach (var target in cable.OutgoingConnections)
                                {
                                    if (target.data != null && target.data.category == ComponentCategory.Generation)
                                    {
                                        node.TriggerProtection("违规：发电机直连");
                                        cable.TriggerProtection("违规：发电机直连");
                                        target.TriggerProtection("违规：发电机直连");
                                    }
                                }
                            }
                        }
                    }
                }

                // 规则 2：DFS 精准探测非法闭环
                bool hasNonStorageCycle = false;

                HashSet<PowerNode> visitedDFS = new HashSet<PowerNode>();
                HashSet<PowerNode> recursionStack = new HashSet<PowerNode>();
                List<PowerNode> cyclePath = new List<PowerNode>();

                // 深度优先搜索探路器
                void DetectDirectedCycle(PowerNode curr)
                {
                    visitedDFS.Add(curr);
                    recursionStack.Add(curr);
                    cyclePath.Add(curr);

                    foreach (var neighbor in curr.OutgoingConnections)
                    {
                        if (!visitedDFS.Contains(neighbor))
                        {
                            DetectDirectedCycle(neighbor);
                        }
                        else if (recursionStack.Contains(neighbor))
                        {
                            // 探测到了物理闭环！开始溯源检查闭环成分
                            int startIndex = cyclePath.IndexOf(neighbor);
                            bool isPureStorage = true;

                            // 检查闭环内是否【只有】储能建筑和电线
                            for (int i = startIndex; i < cyclePath.Count; i++)
                            {
                                var cycleNode = cyclePath[i];
                                if (cycleNode.data.category != ComponentCategory.Storage && !cycleNode.data.isPointToPointCable)
                                {
                                    isPureStorage = false;
                                    break;
                                }
                            }

                            // 只要混入了任何非储能设备（比如发电机、消费者、稳压站），直接判定为非法死环！
                            if (!isPureStorage) hasNonStorageCycle = true;
                        }
                    }

                    recursionStack.Remove(curr);
                    cyclePath.RemoveAt(cyclePath.Count - 1);
                }

                foreach (var node in net)
                {
                    if (!visitedDFS.Contains(node)) DetectDirectedCycle(node);
                }

                // 【核心执行】纯储能集群闭环绝对合法，我们只绞杀非储能设备的违规闭环！
                if (hasNonStorageCycle)
                {
                    foreach (var node in net) node.TriggerProtection("电网违规：严禁非储能设备形成闭环回路！");
                }
            }

            // ==========================================
            // 阶段 3：建立真正的物理储能阵列 (相邻聚合 BFS 算法)
            // ==========================================
            foreach (var net in networks)
            {
                HashSet<PowerNode> pooledNodes = new HashSet<PowerNode>();

                foreach (var node in net)
                {
                    // 【关键修复 2】从每一个尚未归入阵列的储能建筑出发，像水波一样蔓延寻找它物理相邻的兄弟！
                    if (node.data.category == ComponentCategory.Storage && !pooledNodes.Contains(node))
                    {
                        StoragePool pool = new StoragePool();
                        Queue<PowerNode> clusterQueue = new Queue<PowerNode>();

                        clusterQueue.Enqueue(node);
                        pooledNodes.Add(node);

                        // BFS 蔓延寻找
                        while (clusterQueue.Count > 0)
                        {
                            var curr = clusterQueue.Dequeue();

                            if (curr.data.category == ComponentCategory.Storage)
                            {
                                pool.Nodes.Add(curr);
                            }
                            else if (curr.data.isPointToPointCable)
                            {
                                pool.InternalCables.Add(curr);
                            }

                            // 遍历与当前节点物理相连的所有设备
                            foreach (var neighbor in undirectedGraph[curr])
                            {
                                if (!pooledNodes.Contains(neighbor))
                                {
                                    // 1. 如果相邻的是另一个电池？把它吞进这个池子！
                                    if (neighbor.data.category == ComponentCategory.Storage)
                                    {
                                        pooledNodes.Add(neighbor);
                                        clusterQueue.Enqueue(neighbor);
                                    }
                                    // 2. 如果相邻的是电线？检查这根电线是不是连接两个电池的“阵列内部桥梁”
                                    else if (curr.data.category == ComponentCategory.Storage && neighbor.data.isPointToPointCable)
                                    {
                                        bool isInternalCable = false;
                                        foreach (var cableEnd in undirectedGraph[neighbor])
                                        {
                                            // 只要电线的另一头也是电池，它就是一条光荣的内部连线！
                                            if (cableEnd != curr && cableEnd.data.category == ComponentCategory.Storage)
                                            {
                                                isInternalCable = true;
                                                break;
                                            }
                                        }

                                        if (isInternalCable)
                                        {
                                            pooledNodes.Add(neighbor);
                                            clusterQueue.Enqueue(neighbor);
                                        }
                                    }
                                }
                            }
                        }

                        // 完成了一个阵列的打包，进行初始化和状态继承
                        if (pool.Nodes.Count > 0)
                        {
                            foreach (var sNode in pool.Nodes)
                            {
                                pool.TotalChargeTime += sNode.data.chargeTime;
                                pool.TotalDischargeTime += sNode.data.dischargeTime;
                                pool.CurrentChargeTimer += sNode.SavedChargePercent * sNode.data.chargeTime;
                                sNode.MyPool = pool;
                            }

                            // 将找出的内部连线划归为本池子的私有财产
                            foreach (var cNode in pool.InternalCables)
                            {
                                cNode.MyPool = pool;
                            }

                            if (pool.TotalChargeTime > 0 && pool.CurrentChargeTimer >= pool.TotalChargeTime * 0.99f)
                            {
                                pool.CurrentChargeTimer = pool.TotalChargeTime;
                                pool.IsCharged = true;
                            }
                            else
                            {
                                pool.IsCharged = false;
                            }
                            activePools.Add(pool);
                        }
                    }
                }
            }

            // ==========================================
            // 阶段 4：宏节点拓扑计算 (彻底解决死锁和幽灵电量)
            // ==========================================
            Dictionary<PowerNode, int> nodeInDegrees = new Dictionary<PowerNode, int>();
            Dictionary<StoragePool, int> poolInDegrees = new Dictionary<StoragePool, int>();

            // 重置所有普通节点的输入状态
            foreach (var node in allNodes)
            {
                node.ReceivePower(0f, 10f);
                node.IsPoweredByGenerator = false;
                if (node.MyPool == null) nodeInDegrees[node] = 0;
            }

            // 重置所有宏节点(储能阵列)的输入状态
            foreach (var pool in activePools)
            {
                pool.IsReceivingExternalPower = false;
                pool.SharedPowerInput = 0f;
                pool.SharedStability = 10f;
                pool.IsPoweredByGenerator = false;
                poolInDegrees[pool] = 0;
            }

            // 统计“宏观入度”：彻底忽略所有阵列内部的建筑连接！
            foreach (var node in allNodes)
            {
                foreach (var target in node.OutgoingConnections)
                {
                    // 内部边（比如电池A到内部连线，再到电池B）被彻底剥离算法！它们不再制造死环死锁。
                    if (node.MyPool != null && target.MyPool != null && node.MyPool == target.MyPool)
                        continue;

                    if (target.MyPool != null) poolInDegrees[target.MyPool]++;
                    else nodeInDegrees[target]++;
                }
            }

            // 混合队列：既可以装普通的单体建筑，也可以装庞大的电池阵列
            Queue<object> queue = new Queue<object>();

            // 寻找源头：将发电机和没有外部输入的宏节点推入队列
            foreach (var node in allNodes)
            {
                if (node.MyPool == null && nodeInDegrees[node] == 0)
                {
                    if (node.data.category == ComponentCategory.Generation)
                    {
                        node.IsPoweredByGenerator = true;
                        node.ReceivePower(node.data.powerGeneration, node.data.stabilityModifier);
                    }
                    queue.Enqueue(node);
                }
            }
            foreach (var pool in activePools)
            {
                if (poolInDegrees[pool] == 0) queue.Enqueue(pool);
            }

            // 执行拓扑分发
            while (true)
            {
                while (queue.Count > 0)
                {
                    object currentObj = queue.Dequeue();

                    // === 支线 A：处理单体的非储能设备 ===
                    if (currentObj is PowerNode current)
                    {
                        float outPower = current.GetPowerOutput();
                        float outStability = current.GetStabilityOutput();
                        int validPaths = current.OutgoingConnections.Count;

                        if (validPaths > 0 && outPower > 0)
                        {
                            float powerPerPath = outPower / validPaths;

                            foreach (var target in current.OutgoingConnections)
                            {
                                target.IsPoweredByGenerator |= current.IsPoweredByGenerator;

                                if (target.MyPool != null)
                                {
                                    // 必须是有电、由发电机供电、且目标没烧毁，才算有效充电 (拦截幽灵电量)
                                    if (current.IsPoweredByGenerator && powerPerPath > 0 && !target.IsProtectionTripped)
                                    {
                                        target.MyPool.IsReceivingExternalPower = true;
                                    }

                                    target.MyPool.SharedPowerInput += powerPerPath;
                                    target.MyPool.SharedStability = Mathf.Min(target.MyPool.SharedStability, outStability);
                                    target.MyPool.IsPoweredByGenerator |= current.IsPoweredByGenerator;

                                    poolInDegrees[target.MyPool]--;
                                    if (poolInDegrees[target.MyPool] == 0) queue.Enqueue(target.MyPool);
                                }
                                else
                                {
                                    target.ReceivePower(target.CurrentPowerInput + powerPerPath, outStability);
                                    nodeInDegrees[target]--;
                                    if (nodeInDegrees[target] == 0) queue.Enqueue(target);
                                }
                            }
                        }
                        else
                        {
                            foreach (var target in current.OutgoingConnections)
                            {
                                if (target.MyPool != null)
                                {
                                    poolInDegrees[target.MyPool]--;
                                    if (poolInDegrees[target.MyPool] == 0) queue.Enqueue(target.MyPool);
                                }
                                else
                                {
                                    nodeInDegrees[target]--;
                                    if (nodeInDegrees[target] == 0) queue.Enqueue(target);
                                }
                            }
                        }
                    }
                    // === 支线 B：处理极其庞大的储能宏节点(StoragePool) ===
                    else if (currentObj is StoragePool pool)
                    {
                        // 1. 同步数据：强制让阵列内所有的电池和内部电线拥有绝对统一的输入功率！杜绝功率各异的Bug。
                        foreach (var pNode in pool.Nodes)
                        {
                            pNode.IsPoweredByGenerator = pool.IsPoweredByGenerator;
                            pNode.ReceivePower(pool.SharedPowerInput, pool.SharedStability);
                        }
                        foreach (var cNode in pool.InternalCables)
                        {
                            cNode.IsPoweredByGenerator = pool.IsPoweredByGenerator;
                            cNode.ReceivePower(pool.SharedPowerInput, pool.SharedStability);
                        }

                        // 2. 结算整个宏观矩阵对外的总输出能力
                        float poolOutPower = 0f;
                        float poolOutStability = 0f;

                        if (pool.IsReceivingExternalPower)
                        {
                            float totalConsumption = 0f;
                            float totalMod = 0f; // 从 max 改为 sum 累加，电池越多净化的能力越强！

                            foreach (var pNode in pool.Nodes)
                            {
                                if (!pNode.IsProtectionTripped)
                                {
                                    totalConsumption += pNode.data.powerConsumption;
                                    totalMod += pNode.data.storageStabilityModifier;
                                }
                            }
                            poolOutPower = Mathf.Max(0f, pool.SharedPowerInput - totalConsumption);
                            poolOutStability = pool.IsCharged ? Mathf.Clamp(pool.SharedStability + totalMod, 0f, 10f) : pool.SharedStability;
                        }
                        else
                        {
                            if (pool.IsCharged)
                            {
                                // 放电模式：所有电池合力供电，形成庞大的备用输出
                                foreach (var pNode in pool.Nodes)
                                {
                                    if (!pNode.IsProtectionTripped) poolOutPower += pNode.data.powerGeneration;
                                }
                            }
                            poolOutStability = 0f; // 丧失稳压功能
                        }

                        // 将计算结果缓存回池子，供 UI 极速调用展示
                        pool.CachedOutPower = poolOutPower;
                        pool.CachedOutStability = poolOutStability;

                        // 3. 寻找宏观阵列对外的“外部出口连接”
                        List<PowerNode> externalTargets = new List<PowerNode>();
                        foreach (var pNode in pool.Nodes)
                        {
                            if (pNode.IsProtectionTripped) continue;
                            foreach (var target in pNode.OutgoingConnections)
                            {
                                // 只关注流向阵列外部的连接
                                if (target.MyPool != pool) externalTargets.Add(target);
                            }
                        }

                        // 4. 将宏观电量分发给所有出口
                        int validPaths = externalTargets.Count;
                        if (validPaths > 0 && poolOutPower > 0)
                        {
                            float powerPerPath = poolOutPower / validPaths;
                            foreach (var target in externalTargets)
                            {
                                target.IsPoweredByGenerator |= pool.IsPoweredByGenerator;
                                if (target.MyPool != null)
                                {
                                    if (pool.IsPoweredByGenerator && powerPerPath > 0 && !target.IsProtectionTripped)
                                    {
                                        target.MyPool.IsReceivingExternalPower = true;
                                    }
                                    target.MyPool.SharedPowerInput += powerPerPath;
                                    target.MyPool.SharedStability = Mathf.Min(target.MyPool.SharedStability, poolOutStability);
                                    target.MyPool.IsPoweredByGenerator |= pool.IsPoweredByGenerator;

                                    poolInDegrees[target.MyPool]--;
                                    if (poolInDegrees[target.MyPool] == 0) queue.Enqueue(target.MyPool);
                                }
                                else
                                {
                                    target.ReceivePower(target.CurrentPowerInput + powerPerPath, poolOutStability);
                                    nodeInDegrees[target]--;
                                    if (nodeInDegrees[target] == 0) queue.Enqueue(target);
                                }
                            }
                        }
                        else
                        {
                            foreach (var target in externalTargets)
                            {
                                if (target.MyPool != null)
                                {
                                    poolInDegrees[target.MyPool]--;
                                    if (poolInDegrees[target.MyPool] == 0) queue.Enqueue(target.MyPool);
                                }
                                else
                                {
                                    nodeInDegrees[target]--;
                                    if (nodeInDegrees[target] == 0) queue.Enqueue(target);
                                }
                            }
                        }
                    }
                }

                // ==========================================
                // 死锁解开器
                // ==========================================
                // 打破常规组件的死环（由于储能闭环已经被完美打包进入宏节点，
                // 如果这里队列空了但还有节点入度>0，说明它是一个非法的、由稳压器或发电机等组成的死锁环）
                object cycleObj = null;
                foreach (var kvp in nodeInDegrees)
                {
                    if (kvp.Value > 0) { cycleObj = kvp.Key; break; }
                }

                if (cycleObj != null)
                {
                    // 强行抽调一个被死环锁住的节点推入队列，让它继续进行拓扑分发
                    if (cycleObj is PowerNode n) nodeInDegrees[n] = 0;
                    queue.Enqueue(cycleObj);
                }
                else
                {
                    // 所有合法的节点、宏节点以及被破坏的违规闭环都计算完毕，结束全网重算！
                    break;
                }
            }
        }
    }
}