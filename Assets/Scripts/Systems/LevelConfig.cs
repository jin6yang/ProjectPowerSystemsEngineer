using UnityEngine;
using ProjectPowerSystemsEngineer.Data; // 引入建筑组件数据

namespace ProjectPowerSystemsEngineer.Systems
{
    /// <summary>
    /// 关卡数据配置器（纯数据容器）。
    /// 每个关卡场景中应该只有一个带有此组件的物体。
    /// 它就像是一张“游戏卡带”，包含了本关的所有独特设定。
    /// </summary>
    public class LevelConfig : MonoBehaviour
    {
        [Header("本关任务目标配置")]
        public string missionTitle = "目标";
        [TextArea(3, 5)]
        public string missionDescription = "向 AI 核心提供稳定的电力";

        [Header("本关可用建筑 (Hotbar)")]
        [Tooltip("在这里配置本关卡允许玩家建造的设施。")]
        public ComponentData[] availableComponents;

        // 未来如果有“通关所需目标发电量”、“本关限时”等数值，都可以加在这里！
        // public int targetPower = 500;
    }
}