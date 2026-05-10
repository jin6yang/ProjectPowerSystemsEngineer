using UnityEngine;

namespace ProjectPowerSystemsEngineer.Data
{
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
        [Tooltip("组件的名称，将在UI中显示")]
        public string componentName = "Unknown Component";

        [Tooltip("组件的类别（生产、传输、存储、终端）")]
        public ComponentCategory category;

        [Tooltip("对应的真实 3D 模型预制体")]
        public GameObject prefab;

        [Tooltip("建造时跟随鼠标移动的预览残影预制体")]
        public GameObject ghostPrefab;

        [Header("UI & Presentation")]
        [Tooltip("在建造菜单中显示的 2D 图标 (如果不填则只显示文字)")]
        public Sprite uiIcon;

        [Header("Economy")]
        [Tooltip("建造该组件的花费 (对应GDD第五阶段试用期经济系统)")]
        public int buildCost = 100;

        [Header("Power Attributes (P)")]
        [Tooltip("基础产生功率 (仅生产类有效，如微型聚变堆产生大量功率)")]
        public float powerGeneration = 0f;

        [Tooltip("基础消耗/需求功率 (如 AI 核心服务器需要50MW，或长距离稳定器的自身损耗)")]
        public float powerConsumption = 0f;

        [Tooltip("承载上限 (超过此值将触发系统保护并切断输出。如标准线缆为 100MW)")]
        public float maxPowerCapacity = 100f;

        [Header("Stability Attributes (S)")]
        [Tooltip("对流经电流稳定度的影响 (如长距离稳定器提供 +5 稳定度)")]
        public float stabilityModifier = 0f;

        [Tooltip("设备运作所需的最低稳定度要求 (仅终端类有效，如 AI 服务器要求 8 以上)")]
        [Range(0f, 10f)]
        public float requiredStability = 0f;

        // ==========================================
        // 【新增】储能类专属属性 (Storage Attributes)
        // ==========================================
        [Header("Storage Attributes (C)")]
        [Tooltip("储能建筑充能所需的时间 (秒)。满足后才能发挥稳定度加成并储能。")]
        public float chargeTime = 2f;

        [Tooltip("储能建筑断网后，能维持反向供电及稳定度加成的时间 (秒)。")]
        public float dischargeTime = 4f;

        [Tooltip("【新增】储能模式下的稳定度加成 (仅充电并满载时生效，放电模式下稳定度强制为 0)")]
        public float storageStabilityModifier = 5f;

        [Header("Grid Rules")]
        [Tooltip("是否允许其他物体在此地块上穿行或重叠 (普通建筑通常为 false)")]
        public bool isWalkable = false;

        [Tooltip("【核心机制】勾选此项，表示该组件是电缆或连线，必须通过先后点击起点和终点来建造")]
        public bool isPointToPointCable = false;

        [Tooltip("电线的最大连接距离（网格数，包括边角）。填 0 表示无限长！")]
        public int maxCableLength = 4;
    }
}