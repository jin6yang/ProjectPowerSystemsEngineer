using UnityEngine;
using UnityEngine.InputSystem;
using ProjectPowerSystemsEngineer.UI;
using ProjectPowerSystemsEngineer.Simulation;

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

        private LevelConfig currentConfig;
        private bool hasFinished = false;
        private float cheatHoldTimer = 0f;

        // === 多目标进度管理 ===
        private int totalObjectives = 0;
        private int completedObjectives = 0;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            currentConfig = FindAnyObjectByType<LevelConfig>();

            if (currentConfig == null)
            {
                Debug.LogError("[LevelManager] 致命错误：当前场景中没有找到 LevelConfig 组件！");
                return;
            }

            BuilderController builder = FindAnyObjectByType<BuilderController>();
            if (builder != null)
            {
                builder.availableComponents = currentConfig.availableComponents;
            }
        }

        private void Start()
        {
            // 1. 自动扫描全图，精准统计【必须完成】的目标总数
            ConsumerObjective[] allObjectives = FindObjectsByType<ConsumerObjective>(FindObjectsSortMode.None);
            totalObjectives = 0;

            foreach (var obj in allObjectives)
            {
                // 【核心修改】只把勾选了 isRequiredForVictory 的算作总进度
                if (obj.isRequiredForVictory)
                {
                    totalObjectives++;
                }
            }

            completedObjectives = 0;

            // 2. 推送初始任务目标 UI
            Invoke(nameof(PushObjectiveToUI), 0.5f);
        }

        private void Update()
        {
            if (hasFinished) return;

            // 开发者测试逻辑：~ 键控制胜负
            if (Keyboard.current != null)
            {
                if (Keyboard.current.backquoteKey.isPressed)
                {
                    cheatHoldTimer += Time.deltaTime;
                    if (cheatHoldTimer >= 3f) TriggerDefeat();
                }
                else if (Keyboard.current.backquoteKey.wasReleasedThisFrame)
                {
                    if (cheatHoldTimer > 0f && cheatHoldTimer < 1f) TriggerVictory();
                    cheatHoldTimer = 0f;
                }
            }
        }

        public void ReportObjectiveCompleted()
        {
            if (hasFinished) return;

            completedObjectives++;

            // 每次有目标完成，刷新 UI 进度
            PushObjectiveToUI();

            // 检查是否所有必做目标都已达成 (并且排除沙盒模式 totalObjectives == 0 的情况)
            if (totalObjectives > 0 && completedObjectives >= totalObjectives)
            {
                TriggerVictory();
            }
        }

        private void PushObjectiveToUI()
        {
            if (currentConfig == null) return;

            MissionObjectivePanel panel = FindAnyObjectByType<MissionObjectivePanel>();
            if (panel != null)
            {
                // 【已移除强行拼接的进度文本】
                // 还原为纯净显示原本在 LevelConfig 中配置的文本，方便后续对接专门的进度追踪 UI
                panel.SetObjective(currentConfig.missionTitle, currentConfig.missionDescription);
            }
        }

        public void TriggerVictory()
        {
            if (hasFinished) return;
            hasFinished = true;
            Debug.Log("<color=green>[LevelManager] 所有主线目标达成！</color>");

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