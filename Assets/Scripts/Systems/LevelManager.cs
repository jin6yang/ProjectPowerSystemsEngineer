using UnityEngine;
using UnityEngine.InputSystem;
using ProjectPowerSystemsEngineer.UI;

namespace ProjectPowerSystemsEngineer.Systems
{
    /// <summary>
    /// 关卡逻辑调度中心。
    /// 负责读取关卡配置、分发数据、监听作弊指令、以及呼叫通关/失败结算界面。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        private LevelConfig currentConfig; // 动态获取的当前关卡配置
        private bool hasFinished = false;
        private float cheatHoldTimer = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            // 1. 寻找当前场景中的“关卡配置卡带”
            currentConfig = FindAnyObjectByType<LevelConfig>();

            if (currentConfig == null)
            {
                Debug.LogError("[LevelManager] 致命错误：当前场景中没有找到 LevelConfig 组件！");
                return;
            }

            // 2. 将配置数据注入给 BuilderController
            BuilderController builder = FindAnyObjectByType<BuilderController>();
            if (builder != null)
            {
                builder.availableComponents = currentConfig.availableComponents;
                Debug.Log($"[LevelManager] 成功读取 LevelConfig，已注入 {currentConfig.availableComponents.Length} 个可用建筑。");
            }
        }

        private void Start()
        {
            // 给 UI 系统一点点初始化时间，然后推送本关任务
            Invoke(nameof(PushObjectiveToUI), 0.5f);
        }

        private void Update()
        {
            if (hasFinished) return;

            // === 开发者测试逻辑：~ 键控制胜负 ===
            if (Keyboard.current != null)
            {
                if (Keyboard.current.backquoteKey.isPressed)
                {
                    cheatHoldTimer += Time.deltaTime;
                    if (cheatHoldTimer >= 3f)
                    {
                        TriggerDefeat();
                    }
                }
                else if (Keyboard.current.backquoteKey.wasReleasedThisFrame)
                {
                    if (cheatHoldTimer > 0f && cheatHoldTimer < 1f)
                    {
                        TriggerVictory();
                    }
                    cheatHoldTimer = 0f;
                }
            }
        }

        private void PushObjectiveToUI()
        {
            if (currentConfig == null) return;

            MissionObjectivePanel panel = FindAnyObjectByType<MissionObjectivePanel>();
            if (panel != null)
            {
                panel.SetObjective(currentConfig.missionTitle, currentConfig.missionDescription);
            }
        }

        // ==========================================
        // 公开接口：供本关卡特定的检测脚本调用
        // ==========================================
        public void TriggerVictory()
        {
            if (hasFinished) return;
            hasFinished = true;
            Debug.Log("<color=green>[LevelManager] 目标达成！</color>");

            MissionObjectivePanel objectivePanel = FindAnyObjectByType<MissionObjectivePanel>();
            if (objectivePanel != null) objectivePanel.SetMissionStatus(true);

            if (GameResultManager.Instance != null)
            {
                GameResultManager.Instance.ShowResult(true);
            }
        }

        public void TriggerDefeat()
        {
            if (hasFinished) return;
            hasFinished = true;
            Debug.Log("<color=red>[LevelManager] 目标失败！</color>");

            MissionObjectivePanel objectivePanel = FindAnyObjectByType<MissionObjectivePanel>();
            if (objectivePanel != null) objectivePanel.SetMissionStatus(false);

            if (GameResultManager.Instance != null)
            {
                GameResultManager.Instance.ShowResult(false);
            }
        }
    }
}