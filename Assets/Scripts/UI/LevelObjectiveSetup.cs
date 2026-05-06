using UnityEngine;
using ProjectPowerSystemsEngineer.UI;

namespace ProjectPowerSystemsEngineer.Systems
{
    public class LevelObjectiveSetup : MonoBehaviour
    {
        [Header("本关任务目标配置")]
        public string missionTitle = "目标";
        [TextArea(3, 5)] // 在 Inspector 中变成一个多行大文本框
        public string missionDescription = "向 AI 核心提供稳定的电力";

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
                Debug.LogWarning("[系统] 本关卡找不到任务目标面板！");
            }
        }
    }
}