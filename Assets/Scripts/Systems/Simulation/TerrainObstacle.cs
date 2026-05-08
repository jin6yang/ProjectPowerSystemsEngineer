using UnityEngine;
using ProjectPowerSystemsEngineer.Grid;

namespace ProjectPowerSystemsEngineer.LevelDesign
{
    /// <summary>
    /// 地形障碍物标记。挂载到水坑、石头、深坑等模型上。
    /// 游戏启动时，它会自动在 GridManager 中注册并锁死该地块，禁止建造普通设施。
    /// </summary>
    public class TerrainObstacle : MonoBehaviour
    {
        [Tooltip("这是什么类型的地形？(供未来扩展水上建筑使用)")]
        public string terrainType = "Water";

        private void Start()
        {
            // 给网格系统一点初始化时间
            Invoke(nameof(OccupyGrid), 0.1f);
        }

        private void OccupyGrid()
        {
            if (GridManager.Instance != null)
            {
                Vector2Int pos = GridManager.Instance.WorldToGridPosition(transform.position);
                GridCell cell = GridManager.Instance.GetCell(pos);

                // 如果格子空着，用障碍物自身将其占领，彻底封死这里的建造权限！
                if (cell != null && cell.PlacedObject == null)
                {
                    cell.PlacedObject = this.gameObject;

                    // 可选：将障碍物精准对齐到网格中心
                    transform.position = GridManager.Instance.GridToWorldPosition(pos);
                }
            }
        }
    }
}