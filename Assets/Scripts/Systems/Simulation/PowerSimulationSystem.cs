using UnityEngine;
using System.Collections.Generic;
using ProjectPowerSystemsEngineer.Grid;
using ProjectPowerSystemsEngineer.Components;
using ProjectPowerSystemsEngineer.Data;

namespace ProjectPowerSystemsEngineer.Simulation
{
    /// <summary>
    /// 核心电网模拟中枢 (Power Simulation System)
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

            foreach (var pool in activePools)
            {
                if (pool.Tick(Time.deltaTime))
                {
                    poolStateChanged = true;
                }
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

                node.MyPool = null;
                node.ResetProtection();
            }
            activePools.Clear();

            // ==========================================
            // 阶段 2：寻找无向图连通块 (划分物理电网大陆)
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
            // 阶段 2.5：基于 DFS 的电网合规性检测 & 全局稳压能力扫描
            // ==========================================
            Dictionary<PowerNode, float> nodeToNetStorageStability = new Dictionary<PowerNode, float>();

            foreach (var net in networks)
            {
                // 【核心修复】：提前扫描整个物理大陆内所有的储能稳压能力！
                // 彻底打破“有向图”的限制，让后端电池也能拯救前端的电网。
                float netStorageStability = 0f;
                foreach (var node in net)
                {
                    if (node.data.category == ComponentCategory.Storage)
                    {
                        netStorageStability += node.data.storageStabilityModifier;
                    }
                }
                foreach (var node in net)
                {
                    nodeToNetStorageStability[node] = netStorageStability;
                }

                foreach (var node in net)
                {
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
                    if (node.data.category == ComponentCategory.Storage && !pooledNodes.Contains(node))
                    {
                        StoragePool pool = new StoragePool();
                        Queue<PowerNode> clusterQueue = new Queue<PowerNode>();

                        clusterQueue.Enqueue(node);
                        pooledNodes.Add(node);

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

                            foreach (var neighbor in undirectedGraph[curr])
                            {
                                if (!pooledNodes.Contains(neighbor))
                                {
                                    if (neighbor.data.category == ComponentCategory.Storage)
                                    {
                                        pooledNodes.Add(neighbor);
                                        clusterQueue.Enqueue(neighbor);
                                    }
                                    else if (curr.data.category == ComponentCategory.Storage && neighbor.data.isPointToPointCable)
                                    {
                                        bool isInternalCable = false;
                                        foreach (var cableEnd in undirectedGraph[neighbor])
                                        {
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

                        if (pool.Nodes.Count > 0)
                        {
                            foreach (var sNode in pool.Nodes)
                            {
                                pool.TotalChargeTime += sNode.data.chargeTime;
                                pool.TotalDischargeTime += sNode.data.dischargeTime;
                                pool.CurrentChargeTimer += sNode.SavedChargePercent * sNode.data.chargeTime;
                                sNode.MyPool = pool;
                            }

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
            // 阶段 4：宏节点拓扑计算 (彻底解决死锁和负载不均)
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

            foreach (var node in allNodes)
            {
                foreach (var target in node.OutgoingConnections)
                {
                    if (node.MyPool != null && target.MyPool != null && node.MyPool == target.MyPool)
                        continue;

                    if (target.MyPool != null) poolInDegrees[target.MyPool]++;
                    else nodeInDegrees[target]++;
                }
            }

            Queue<object> queue = new Queue<object>();

            foreach (var node in allNodes)
            {
                if (node.MyPool == null && nodeInDegrees[node] == 0)
                {
                    if (node.data.category == ComponentCategory.Generation)
                    {
                        node.IsPoweredByGenerator = true;

                        // 【究极修复】：将该物理电网上所有并联储能站的“全网电容补偿”，直接在源头强行注入发电机！
                        float extraStability = nodeToNetStorageStability.ContainsKey(node) ? nodeToNetStorageStability[node] : 0f;
                        float finalGenStability = Mathf.Clamp(node.data.stabilityModifier + extraStability, 0f, 10f);

                        node.ReceivePower(node.data.powerGeneration, finalGenStability);
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
                    else if (currentObj is StoragePool pool)
                    {
                        float powerPerNode = pool.Nodes.Count > 0 ? pool.SharedPowerInput / pool.Nodes.Count : 0f;

                        foreach (var pNode in pool.Nodes)
                        {
                            pNode.IsPoweredByGenerator = pool.IsPoweredByGenerator;
                            pNode.ReceivePower(powerPerNode, pool.SharedStability);
                        }
                        foreach (var cNode in pool.InternalCables)
                        {
                            cNode.IsPoweredByGenerator = pool.IsPoweredByGenerator;
                            cNode.ReceivePower(powerPerNode, pool.SharedStability);
                        }

                        float poolOutPower = 0f;
                        float poolOutStability = 0f;

                        if (pool.IsReceivingExternalPower)
                        {
                            float totalConsumption = 0f;

                            foreach (var pNode in pool.Nodes)
                            {
                                if (!pNode.IsProtectionTripped)
                                {
                                    totalConsumption += pNode.data.powerConsumption;
                                }
                            }
                            poolOutPower = Mathf.Max(0f, pool.SharedPowerInput - totalConsumption);

                            // 【究极修复】：因为储能建筑的宏观叠加稳定度，已经在源头被强行注入到了发电机里，
                            // 所以这里只要原封不动地“透传”当前的稳定度（SharedStability）即可！
                            // 彻底解决下游电池没法支援上游消费者的致命图论 Bug。
                            poolOutStability = pool.SharedStability;
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
                            poolOutStability = 0f; // 断电放电模式时，丧失所有稳压功能，极其真实
                        }

                        pool.CachedOutPower = poolOutPower;
                        pool.CachedOutStability = poolOutStability;

                        List<PowerNode> externalTargets = new List<PowerNode>();

                        foreach (var pNode in pool.Nodes)
                        {
                            if (pNode.IsProtectionTripped) continue;
                            foreach (var target in pNode.OutgoingConnections)
                            {
                                if (target.MyPool != pool) externalTargets.Add(target);
                            }
                        }

                        foreach (var cNode in pool.InternalCables)
                        {
                            if (cNode.IsProtectionTripped) continue;
                            foreach (var target in cNode.OutgoingConnections)
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

                object cycleObj = null;
                foreach (var kvp in nodeInDegrees)
                {
                    if (kvp.Value > 0) { cycleObj = kvp.Key; break; }
                }

                if (cycleObj == null)
                {
                    foreach (var kvp in poolInDegrees)
                    {
                        if (kvp.Value > 0) { cycleObj = kvp.Key; break; }
                    }
                }

                if (cycleObj != null)
                {
                    if (cycleObj is PowerNode n) nodeInDegrees[n] = 0;
                    else if (cycleObj is StoragePool p) poolInDegrees[p] = 0;
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