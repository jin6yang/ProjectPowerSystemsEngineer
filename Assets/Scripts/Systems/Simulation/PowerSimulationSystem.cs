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

        public void RecalculatePowerGrid()
        {
            PowerNode[] rawNodes = FindObjectsByType<PowerNode>(FindObjectsSortMode.None);

            // 过滤掉所有没有数据的游离节点（比如充当鼠标残影的 Ghost 物体）
            List<PowerNode> allNodes = new List<PowerNode>();
            foreach (var node in rawNodes)
            {
                if (node.data != null)
                {
                    allNodes.Add(node);
                }
            }

            Dictionary<PowerNode, int> inDegrees = new Dictionary<PowerNode, int>();
            foreach (var node in allNodes)
            {
                node.ReceivePower(0f, 10f);
                inDegrees[node] = 0;
            }

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

            Queue<PowerNode> queue = new Queue<PowerNode>();
            foreach (var node in allNodes)
            {
                if (inDegrees[node] == 0)
                {
                    if (node.data.category == ComponentCategory.Generation)
                    {
                        // 【硬核修正】发电机产生的初始稳定度，取决于它的自身属性 (Stability Modifier)
                        // 不再是无脑完美的 10 稳定度！
                        float baseStability = node.data.stabilityModifier;
                        node.ReceivePower(node.data.powerGeneration, baseStability);
                    }
                    queue.Enqueue(node);
                }
            }

            while (queue.Count > 0)
            {
                PowerNode current = queue.Dequeue();

                float outPower = current.GetPowerOutput();
                float outStability = current.GetStabilityOutput();

                int validPaths = current.OutgoingConnections.Count;

                if (validPaths > 0 && outPower > 0)
                {
                    float powerPerPath = outPower / validPaths;

                    foreach (var target in current.OutgoingConnections)
                    {
                        target.ReceivePower(target.CurrentPowerInput + powerPerPath, outStability);

                        if (inDegrees.ContainsKey(target))
                        {
                            inDegrees[target]--;
                            if (inDegrees[target] == 0) queue.Enqueue(target);
                        }
                    }
                }
                else
                {
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
        }
    }
}