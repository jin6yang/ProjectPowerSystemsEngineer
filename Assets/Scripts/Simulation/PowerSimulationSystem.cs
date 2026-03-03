using UnityEngine;
using System.Collections.Generic;
using ProjectPowerSystemsEngineer.Grid;
using ProjectPowerSystemsEngineer.Components;
using ProjectPowerSystemsEngineer.Data;

namespace ProjectPowerSystemsEngineer.Simulation
{
    /// <summary>
    /// 全局电力模拟系统：负责计算电力如何从发电机流向终端
    /// </summary>
    public class PowerSimulationSystem : MonoBehaviour
    {
        public static PowerSimulationSystem Instance { get; private set; }

        // 定义上下左右四个相邻方向
        private readonly Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),  // 上
            new Vector2Int(0, -1), // 下
            new Vector2Int(-1, 0), // 左
            new Vector2Int(1, 0)   // 右
        };

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Update()
        {
            // 测试按键：按 P 键手动触发一次全局电力模拟计算
            if (UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame)
            {
                RecalculatePowerGrid();
            }
        }

        /// <summary>
        /// 核心算法：重新计算全网电力流向
        /// </summary>
        public void RecalculatePowerGrid()
        {
            Debug.Log(">>> 开始进行全网电力拓扑计算...");

            // 1. 获取全图所有有物体的格子，并提取出 PowerNode
            List<PowerNode> allNodes = new List<PowerNode>();
            List<PowerNode> generators = new List<PowerNode>();

            // 遍历整个地图 (可以通过给 GridManager 增加一个 GetAllOccupiedCells() 方法来优化，这里先做全图遍历)
            for (int x = -GridManager.Instance.width / 2; x < GridManager.Instance.width / 2; x++)
            {
                for (int z = -GridManager.Instance.height / 2; z < GridManager.Instance.height / 2; z++)
                {
                    GridCell cell = GridManager.Instance.GetCell(new Vector2Int(x, z));
                    if (cell != null && cell.IsOccupied)
                    {
                        PowerNode node = cell.PlacedObject.GetComponent<PowerNode>();
                        if (node != null)
                        {
                            // 每次重新计算前，把所有节点的输入清零
                            node.ReceivePower(0f, 100f);
                            allNodes.Add(node);

                            // 如果是发电机，加入源头列表
                            if (node.data.category == ComponentCategory.Generation)
                            {
                                generators.Add(node);
                            }
                        }
                    }
                }
            }

            // 2. 从所有发电机开始进行 BFS (广度优先搜索) 泛洪算法
            foreach (var gen in generators)
            {
                SimulateFlowFromGenerator(gen);
            }

            Debug.Log("<<< 电力拓扑计算完成！");
        }

        private void SimulateFlowFromGenerator(PowerNode generator)
        {
            // 队列用于 BFS 寻路 (存储：当前处理的节点)
            Queue<PowerNode> queue = new Queue<PowerNode>();
            // 记录已经访问过的节点，防止无限死循环
            HashSet<PowerNode> visited = new HashSet<PowerNode>();

            // 发电机自身产生电力并入队
            generator.ReceivePower(generator.GetPowerOutput(), generator.GetStabilityOutput());
            queue.Enqueue(generator);
            visited.Add(generator);

            while (queue.Count > 0)
            {
                PowerNode currentNode = queue.Dequeue();

                // 节点当前的输出能力
                float outPower = currentNode.GetPowerOutput();
                float outStability = currentNode.GetStabilityOutput();

                // 如果经过消耗后没有电了，就不往下传了
                if (outPower <= 0) continue;

                // 寻找四周相邻的相连节点
                List<PowerNode> connectedNeighbors = GetConnectedNeighbors(currentNode.GridPosition);

                // 根据你的GDD：“分流器将输入电流平均分配到出口”。
                // 在初级版本中，我们假设如果一根线分叉了，电流就平分（比如分两路，每路拿 50%）
                int validPaths = 0;
                foreach (var neighbor in connectedNeighbors)
                {
                    if (!visited.Contains(neighbor)) validPaths++;
                }

                if (validPaths > 0)
                {
                    float powerPerPath = outPower / validPaths;

                    foreach (var neighbor in connectedNeighbors)
                    {
                        if (!visited.Contains(neighbor))
                        {
                            // 邻居节点接收电力！
                            neighbor.ReceivePower(neighbor.CurrentPowerInput + powerPerPath, outStability);

                            visited.Add(neighbor);

                            // 只有传输类组件 (电线) 才能继续往外传电，终端(Consumer)收到电就截止了
                            if (neighbor.data.category == ComponentCategory.Transmission)
                            {
                                queue.Enqueue(neighbor);
                            }
                        }
                    }
                }
            }
        }

        // 查找当前坐标上下左右四个方向是否有合法的 PowerNode
        private List<PowerNode> GetConnectedNeighbors(Vector2Int pos)
        {
            List<PowerNode> neighbors = new List<PowerNode>();

            foreach (var dir in directions)
            {
                Vector2Int neighborPos = pos + dir;
                GridCell neighborCell = GridManager.Instance.GetCell(neighborPos);

                if (neighborCell != null && neighborCell.IsOccupied && !neighborCell.IsOnFire)
                {
                    PowerNode nNode = neighborCell.PlacedObject.GetComponent<PowerNode>();
                    if (nNode != null)
                    {
                        neighbors.Add(nNode);
                    }
                }
            }
            return neighbors;
        }
    }
}