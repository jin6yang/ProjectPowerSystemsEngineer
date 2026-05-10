using UnityEngine;
using System.Collections.Generic;
using ProjectPowerSystemsEngineer.Grid;
using ProjectPowerSystemsEngineer.Components;
using ProjectPowerSystemsEngineer.Data;

namespace ProjectPowerSystemsEngineer.Simulation
{
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
            foreach (var pool in activePools)
            {
                if (pool.Tick(Time.deltaTime)) poolStateChanged = true;
            }

            if (poolStateChanged || needsRecalculation)
            {
                needsRecalculation = false;
                RecalculatePowerGrid();
            }
        }

        public void RecalculatePowerGrid()
        {
            PowerNode[] rawNodes = FindObjectsByType<PowerNode>(FindObjectsSortMode.None);
            List<PowerNode> allNodes = new List<PowerNode>();

            foreach (var node in rawNodes)
            {
                if (node.data != null) allNodes.Add(node);
            }

            // ==========================================
            // 阶段 1：状态保存与自愈重置
            // ==========================================
            foreach (var node in allNodes)
            {
                if (node.data.category == ComponentCategory.Storage)
                {
                    node.SavedChargePercent = (node.MyPool != null && node.MyPool.TotalChargeTime > 0)
                        ? Mathf.Clamp01(node.MyPool.CurrentChargeTimer / node.MyPool.TotalChargeTime)
                        : 0f;
                }
                node.ResetProtection();
            }
            activePools.Clear();

            // ==========================================
            // 阶段 2：寻找物理电网大陆
            // ==========================================
            Dictionary<PowerNode, List<PowerNode>> undirectedGraph = new Dictionary<PowerNode, List<PowerNode>>();
            foreach (var node in allNodes) undirectedGraph[node] = new List<PowerNode>();

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
            // 阶段 2.5：基于 DFS 的电网合规性检测 (彻底剥离发电机惩罚)
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

                // 规则 2：DFS 精准探测非储能闭环
                bool hasNonStorageCycle = false;

                HashSet<PowerNode> visitedDFS = new HashSet<PowerNode>();
                HashSet<PowerNode> recursionStack = new HashSet<PowerNode>();
                List<PowerNode> cyclePath = new List<PowerNode>();

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
                            int startIndex = cyclePath.IndexOf(neighbor);
                            bool isPureStorage = true;

                            // 检查闭环内是否只包含储能建筑和电线
                            for (int i = startIndex; i < cyclePath.Count; i++)
                            {
                                var cycleNode = cyclePath[i];
                                if (cycleNode.data.category != ComponentCategory.Storage && !cycleNode.data.isPointToPointCable)
                                {
                                    isPureStorage = false;
                                    break;
                                }
                            }

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

                // 【核心修正】我们彻底移除了多台发电机的闭环惩罚，纯储能集群闭环绝对合法！
                if (hasNonStorageCycle)
                {
                    foreach (var node in net) node.TriggerProtection("电网违规：严禁非储能设备形成闭环回路！");
                }
            }

            // ==========================================
            // 阶段 3：建立资源池与内部连接识别
            // ==========================================
            foreach (var net in networks)
            {
                List<PowerNode> storageNodes = new List<PowerNode>();
                foreach (var n in net)
                {
                    if (n.data.category == ComponentCategory.Storage) storageNodes.Add(n);
                }

                if (storageNodes.Count > 0)
                {
                    StoragePool pool = new StoragePool();
                    pool.Nodes = storageNodes;

                    foreach (var sNode in storageNodes)
                    {
                        pool.TotalChargeTime += sNode.data.chargeTime;
                        pool.TotalDischargeTime += sNode.data.dischargeTime;
                        pool.CurrentChargeTimer += sNode.SavedChargePercent * sNode.data.chargeTime;
                        sNode.MyPool = pool;
                    }

                    // 【核心机制】：找出连接在池子内部的“内部电线”，将其纳入宏节点保护范围！
                    foreach (var sNode in storageNodes)
                    {
                        foreach (var cable in sNode.OutgoingConnections)
                        {
                            if (cable.data != null && cable.data.isPointToPointCable && cable.OutgoingConnections.Count > 0)
                            {
                                var target = cable.OutgoingConnections[0];
                                if (target.MyPool == pool)
                                {
                                    pool.InternalCables.Add(cable);
                                    cable.MyPool = pool; // 赐予内部电线同样的资源池标记
                                }
                            }
                        }
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

            // ==========================================
            // 阶段 4：宏节点拓扑计算 (彻底解决死锁和幽灵电量)
            // ==========================================
            Dictionary<PowerNode, int> nodeInDegrees = new Dictionary<PowerNode, int>();
            Dictionary<StoragePool, int> poolInDegrees = new Dictionary<StoragePool, int>();

            foreach (var node in allNodes)
            {
                node.ReceivePower(0f, 10f);
                node.IsPoweredByGenerator = false;
                if (node.MyPool == null) nodeInDegrees[node] = 0;
            }

            foreach (var pool in activePools)
            {
                pool.IsReceivingExternalPower = false;
                pool.SharedPowerInput = 0f;
                pool.SharedStability = 10f;
                pool.IsPoweredByGenerator = false;
                poolInDegrees[pool] = 0;
            }

            // 统计“宏观入度”：彻底忽略所有池内的建筑连接！
            foreach (var node in allNodes)
            {
                foreach (var target in node.OutgoingConnections)
                {
                    // 内部边（比如电池A到电池B）被彻底剥离算法！它们不再制造死环死锁。
                    if (node.MyPool != null && target.MyPool != null && node.MyPool == target.MyPool)
                        continue;

                    if (target.MyPool != null) poolInDegrees[target.MyPool]++;
                    else nodeInDegrees[target]++;
                }
            }

            Queue<object> queue = new Queue<object>(); // 这个队列可以装普通节点，也可以装超大节点(资源池)

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

            while (true)
            {
                while (queue.Count > 0)
                {
                    object currentObj = queue.Dequeue();

                    // === 处理非储能设备 ===
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
                    // === 处理超大节点(储能矩阵) ===
                    else if (currentObj is StoragePool pool)
                    {
                        // 1. 同步数据：强制让池内所有电池和电线拥有绝对统一的输入功率，杜绝功率不一
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

                        // 2. 结算整个宏观矩阵对外的总输出
                        float poolOutPower = 0f;
                        float poolOutStability = 0f;

                        if (pool.IsReceivingExternalPower)
                        {
                            float totalConsumption = 0f;
                            float maxMod = 0f;
                            foreach (var pNode in pool.Nodes)
                            {
                                if (!pNode.IsProtectionTripped)
                                {
                                    totalConsumption += pNode.data.powerConsumption;
                                    maxMod = Mathf.Max(maxMod, pNode.data.storageStabilityModifier);
                                }
                            }
                            poolOutPower = Mathf.Max(0f, pool.SharedPowerInput - totalConsumption);
                            poolOutStability = pool.IsCharged ? Mathf.Clamp(pool.SharedStability + maxMod, 0f, 10f) : pool.SharedStability;
                        }
                        else
                        {
                            if (pool.IsCharged)
                            {
                                foreach (var pNode in pool.Nodes)
                                {
                                    if (!pNode.IsProtectionTripped) poolOutPower += pNode.data.powerGeneration;
                                }
                            }
                            poolOutStability = 0f;
                        }

                        // 3. 寻找对外的宏观连接
                        List<PowerNode> externalTargets = new List<PowerNode>();
                        foreach (var pNode in pool.Nodes)
                        {
                            if (pNode.IsProtectionTripped) continue;
                            foreach (var target in pNode.OutgoingConnections)
                            {
                                if (target.MyPool != pool) externalTargets.Add(target);
                            }
                        }

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

                // 打破常规组件的死环（由于储能闭环已经被打包，这里只会抓出玩家违规拼接的非储能死环）
                object cycleObj = null;
                foreach (var kvp in nodeInDegrees)
                {
                    if (kvp.Value > 0) { cycleObj = kvp.Key; break; }
                }

                if (cycleObj != null)
                {
                    if (cycleObj is PowerNode n) nodeInDegrees[n] = 0;
                    queue.Enqueue(cycleObj);
                }
                else
                {
                    break;
                }
            }
        }
    }
}