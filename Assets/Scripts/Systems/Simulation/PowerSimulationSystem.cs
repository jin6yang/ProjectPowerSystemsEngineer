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

                // 【核心新增】每次重新计算前，重置该节点的外部受电状态
                node.IsReceivingExternalPower = false;
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
                        // 【核心新增】如果上游有电传过来，明确标记它正在接收外部供电！
                        target.IsReceivingExternalPower = true;

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