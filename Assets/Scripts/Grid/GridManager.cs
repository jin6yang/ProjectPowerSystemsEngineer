using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace ProjectPowerSystemsEngineer.Grid
{
    // 定义单个网格单元格的数据结构
    public class GridCell
    {
        public Vector2Int Position;
        public bool IsOccupied => PlacedObject != null;
        public GameObject PlacedObject; // 当前格子上放置的组件实例

        // 预留给GDD第四阶段中"网格着火/不可用"等灾害状态
        public bool IsOnFire = false;

        public GridCell(Vector2Int pos)
        {
            Position = pos;
        }
    }

    public class GridManager : MonoBehaviour
    {
        // 单例模式：方便其他系统（如建造系统、电力模拟系统）随时获取网格数据
        public static GridManager Instance { get; private set; }

        [Header("Grid Settings")]
        public int width = 20;   // M (横向宽度)
        public int height = 20;  // N (纵向高度)
        public float cellSize = 5f; // 将默认物理尺寸修改为 5米x5米

        // 存储网格数据的核心字典：通过坐标(x, z)极速查找对应格子数据
        private Dictionary<Vector2Int, GridCell> grid;

        // 用于极速射线检测的“数学平面”(高度 Y = 0)
        private Plane groundPlane;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            InitializeGrid();

            // 修复：让用于鼠标检测的数学平面，跟随 GridManager 自身的真实高度 (Y 轴)
            groundPlane = new Plane(Vector3.up, transform.position);
        }

        private void InitializeGrid()
        {
            grid = new Dictionary<Vector2Int, GridCell>();

            // 以(0,0)为中心生成网格
            int halfWidth = width / 2;
            int halfHeight = height / 2;

            for (int x = -halfWidth; x < width - halfWidth; x++)
            {
                for (int z = -halfHeight; z < height - halfHeight; z++)
                {
                    Vector2Int pos = new Vector2Int(x, z);
                    grid.Add(pos, new GridCell(pos));
                }
            }
        }

        // ==================== 核心数学转换 ====================

        /// <summary> 将世界坐标转换为网格整数坐标 </summary>
        public Vector2Int WorldToGridPosition(Vector3 worldPosition)
        {
            // 修复：减去 GridManager 的世界坐标，使得网格中心始终跟随这个 GameObject
            int x = Mathf.RoundToInt((worldPosition.x - transform.position.x) / cellSize);
            int z = Mathf.RoundToInt((worldPosition.z - transform.position.z) / cellSize);
            return new Vector2Int(x, z);
        }

        /// <summary> 将网格坐标转换回世界坐标 (用于放置模型时居中) </summary>
        public Vector3 GridToWorldPosition(Vector2Int gridPosition)
        {
            // 修复：加上 GridManager 的世界坐标
            return new Vector3(gridPosition.x * cellSize, 0, gridPosition.y * cellSize) + transform.position;
        }

        // ==================== 交互核心：鼠标指引 ====================

        /// <summary> 获取当前鼠标悬停的有效网格坐标。如果在网格外或未检测到，返回null </summary>
        public Vector2Int? GetMouseGridPosition()
        {
            if (Camera.main == null || Mouse.current == null) return null;

            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mouseScreenPos);

            // 纯数学射线测试：射线是否与地面平面相交？
            if (groundPlane.Raycast(ray, out float distanceToPlane))
            {
                // 获取相交点的精确三维坐标
                Vector3 hitPoint = ray.GetPoint(distanceToPlane);

                // 转换为网格坐标
                Vector2Int gridPos = WorldToGridPosition(hitPoint);

                // 检查这个坐标是否在我们生成的合法网格字典内
                if (grid.ContainsKey(gridPos))
                {
                    return gridPos;
                }
            }
            return null;
        }

        // ==================== 网格操作 API ====================

        public GridCell GetCell(Vector2Int position)
        {
            if (grid != null && grid.TryGetValue(position, out GridCell cell))
            {
                return cell;
            }
            return null;
        }

        public bool IsCellAvailable(Vector2Int position)
        {
            GridCell cell = GetCell(position);
            // 只有当格子存在，且没有被占用，且没有着火时，才允许放置
            return cell != null && !cell.IsOccupied && !cell.IsOnFire;
        }

        // ==================== 编辑器可视化 (Gizmos) ====================

        // 这个方法只在 Unity 编辑器的 Scene 窗口起作用，帮助开发者“看见”网格
        private void OnDrawGizmos()
        {
            // 修改为暗调、优雅的全息深蓝色 (R:0.1, G:0.3, B:0.6, Alpha:0.6)
            Gizmos.color = new Color(0.1f, 0.3f, 0.6f, 0.6f);

            int halfWidth = width / 2;
            int halfHeight = height / 2;

            // 修复：高度跟随物体的 Y 轴，并且稍微抬高 0.05 米防止被同高度的 Plane 遮挡
            float drawHeight = transform.position.y + 0.05f;

            // 修复：获取当前物体的水平坐标作为原点
            Vector3 origin = transform.position;

            // 画垂直线
            for (int x = -halfWidth; x <= width - halfWidth; x++)
            {
                Gizmos.DrawLine(
                    new Vector3(origin.x + x * cellSize - cellSize / 2f, drawHeight, origin.z - halfHeight * cellSize - cellSize / 2f),
                    new Vector3(origin.x + x * cellSize - cellSize / 2f, drawHeight, origin.z + (height - halfHeight) * cellSize - cellSize / 2f)
                );
            }

            // 画水平线
            for (int z = -halfHeight; z <= height - halfHeight; z++)
            {
                Gizmos.DrawLine(
                    new Vector3(origin.x - halfWidth * cellSize - cellSize / 2f, drawHeight, origin.z + z * cellSize - cellSize / 2f),
                    new Vector3(origin.x + (width - halfWidth) * cellSize - cellSize / 2f, drawHeight, origin.z + z * cellSize - cellSize / 2f)
                );
            }
        }
    }
}