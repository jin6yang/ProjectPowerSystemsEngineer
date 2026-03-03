using UnityEngine;

namespace ProjectPowerSystemsEngineer.Data
{
    // 组件的四大分类，完全对应你的 GDD
    public enum ComponentCategory
    {
        Generation,    // A. 生产类
        Transmission,  // B. 传输类
        Storage,       // C. 存储与调节类
        Consumer       // D. 终端/目标类
    }

    [CreateAssetMenu(fileName = "New Component Data", menuName = "ProjectPowerSystemsEngineer/Component Data")]
    public class ComponentData : ScriptableObject
    {
        [Header("Basic Info")]
        public string componentName = "Unknown Component";
        public ComponentCategory category;
        public GameObject prefab; // 对应的真实 3D 模型
        public GameObject ghostPrefab; // 对应的预览残影模型

        [Header("Economy")]
        public int buildCost = 100; // 建造花费 (对应第五阶段试用期)

        [Header("Power Attributes (P)")]
        [Tooltip("基础产生功率 (仅生产类有效，如聚变堆的 150MW)")]
        public float powerGeneration = 0f;
        [Tooltip("基础消耗/需求功率 (如 AI 服务器需要 50MW，或稳定器自身消耗 10MW)")]
        public float powerConsumption = 0f;
        [Tooltip("承载上限 (超过此值导致过载爆炸。如普通电线 100MW)")]
        public float maxPowerCapacity = 100f;

        [Header("Stability Attributes (S)")]
        [Tooltip("对流经电流稳定性的影响 (如稳定器 +20%，或恶劣环境 -10%)")]
        public float stabilityModifier = 0f;
        [Tooltip("最低稳定性要求 (仅终端类有效，如 AI 要求 99%)")]
        [Range(0f, 100f)]
        public float requiredStability = 0f;

        [Header("Grid Rules")]
        [Tooltip("是否允许被其他物体覆盖 (通常为 false)")]
        public bool isWalkable = false;
        // 未来可以加入占地尺寸 public Vector2Int size = new Vector2Int(1, 1);
    }
}