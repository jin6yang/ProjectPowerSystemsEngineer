using UnityEngine;
using ProjectPowerSystemsEngineer.UI;
using ProjectPowerSystemsEngineer.Data; // 引入组件数据命名空间

namespace ProjectPowerSystemsEngineer.Systems
{
    // 【核心魔法】设置执行优先级为 -50。
    // 这保证了 LevelManager 会在 UIManager 和 BuilderController 之前执行 Awake，
    // 从而完美地把数据提前注入进去，防止 UI 生成时找不到数据！
    [DefaultExecutionOrder(-50)]
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("本关任务目标配置")]
        public string missionTitle = "目标";
        [TextArea(3, 5)]
        public string missionDescription = "向 AI 核心提供稳定的电力";

        [Header("本关可用建筑 (Hotbar)")]
        [Tooltip("在这里配置本关卡允许玩家建造的设施。")]
        public ComponentData[] availableComponents;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            // ==========================================
            // 数据注入逻辑：把本关的配置发给核心系统
            // ==========================================
            BuilderController builder = FindAnyObjectByType<BuilderController>();
            if (builder != null)
            {
                // 将本关的建筑列表覆盖给 BuilderController
                builder.availableComponents = this.availableComponents;
                Debug.Log($"[LevelManager] 已成功向建造系统注入 {availableComponents.Length} 个可用建筑。");
            }
            else
            {
                Debug.LogWarning("[LevelManager] 场景中未找到 BuilderController，注入失败！");
            }
        }

        private void Start()
        {
            // 给 UI 系统一点点初始化时间，然后推送本关任务
            Invoke(nameof(PushObjectiveToUI), 0.5f);
        }

        private void PushObjectiveToUI()
        {
            MissionObjectivePanel panel = FindAnyObjectByType<MissionObjectivePanel>();
            if (panel != null)
            {
                panel.SetObjective(missionTitle, missionDescription);
            }
            else
            {
                Debug.LogWarning("[LevelManager] 本关卡找不到任务目标面板！");
            }
        }
    }
}