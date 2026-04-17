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

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// 基于图论“拓扑排序(Topological Sort)”的全网电力计算
        /// 能完美解决多路汇流、分流以及防止闭环死循环的问题
        /// </summary>
        public void RecalculatePowerGrid()
        {
            PowerNode[] allNodes = FindObjectsByType<PowerNode>(FindObjectsSortMode.None);

            // 1. 初始化状态，并计算所有节点的“入度” (In-Degree)
            Dictionary<PowerNode, int> inDegrees = new Dictionary<PowerNode, int>();
            foreach (var node in allNodes)
            {
                node.ReceivePower(0f, 10f); // 每次计算前，重置所有节点的输入为 0
                inDegrees[node] = 0;
            }

            // 遍历所有有向连线，累加目标的入度
            foreach (var node in allNodes)
            {
                foreach (var target in node.OutgoingConnections)
                {
                    if (inDegrees.ContainsKey(target))
                    {
                        inDegrees[target]++;
                    }
                }
            }

            // 2. 将所有入度为 0 的节点（通常是发电机，或者是没连源头的断头线）加入队列
            Queue<PowerNode> queue = new Queue<PowerNode>();
            foreach (var node in allNodes)
            {
                if (inDegrees[node] == 0)
                {
                    // 如果是发电机，给自己赋予初始电量
                    if (node.data.category == ComponentCategory.Generation)
                    {
                        node.ReceivePower(node.data.powerGeneration, 10f);
                    }
                    queue.Enqueue(node);
                }
            }

            // 3. 开始如流水般往下游分配电力
            while (queue.Count > 0)
            {
                PowerNode current = queue.Dequeue();

                // 获取该节点经过损耗和过载判断后，能输出的真实能力
                float outPower = current.GetPowerOutput();
                float outStability = current.GetStabilityOutput();

                int validPaths = current.OutgoingConnections.Count;

                // 如果有输出电量，并且连了下游节点
                if (validPaths > 0 && outPower > 0)
                {
                    float powerPerPath = outPower / validPaths; // 电流平分定律

                    foreach (var target in current.OutgoingConnections)
                    {
                        // 目标节点接收电力（注意这里是累加目标已有的电量，实现多根线汇流加算）
                        target.ReceivePower(target.CurrentPowerInput + powerPerPath, outStability);

                        // 扣除目标的入度。当它的上游全部计算完毕时，加入队列
                        if (inDegrees.ContainsKey(target))
                        {
                            inDegrees[target]--;
                            if (inDegrees[target] == 0) queue.Enqueue(target);
                        }
                    }
                }
                else
                {
                    // 即使没有电传下去，也要把下游节点的依赖解除，防止下游死锁无法计算
                    foreach (var target in current.OutgoingConnections)
                    {
                        if (inDegrees.ContainsKey(target))
                        {
                            inDegrees[target]--;
                            if (inDegrees[target] == 0) queue.Enqueue(target);
                        }
                    }
                }
            }

            // 拓扑排序的精妙之处：如果玩家胡乱连了一个 A->B->A 的循环死环，它们会因为入度永远不为 0 而卡住。
            // 在我们的游戏里，这表现为死环里的设备全部失去电力（值为0），完美符合“短路”的物理直觉！
        }
    }
}