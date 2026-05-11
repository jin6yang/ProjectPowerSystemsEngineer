using UnityEngine;
using ProjectPowerSystemsEngineer.Components;
using ProjectPowerSystemsEngineer.Systems;

namespace ProjectPowerSystemsEngineer.Simulation
{
    /// <summary>
    /// 关卡目标监控器：挂载在作为“通关目标”的预置建筑(Consumer)上
    /// </summary>
    [RequireComponent(typeof(PowerNode))]
    public class ConsumerObjective : MonoBehaviour
    {
        [Header("Objective Settings (任务设定)")]
        [Tooltip("是否必须达成此目标才能通关？\n如果不勾选，该建筑仍可正常运转，但不会计入左下角的通关总进度中。")]
        public bool isRequiredForVictory = true;

        [Tooltip("是否允许玩家移动或拆除此目标建筑？\n【极其重要】默认不勾选，以防止玩家把过关目标给误删了！")]
        public bool canBeDeletedByPlayer = false;

        [Header("Victory Conditions (通关条件)")]
        [Tooltip("需要达到的最小输入功率 (MW)")]
        public float targetPower = 200f;

        [Tooltip("需要达到的最低稳定度 (1-10)")]
        public float targetStability = 5f;

        [Tooltip("需要稳定维持该状态的时间(秒)，防止运气过关")]
        public float maintainDuration = 3f;

        private PowerNode powerNode;
        private float currentTimer = 0f;
        private bool isCompleted = false;

        private void Awake()
        {
            powerNode = GetComponent<PowerNode>();
        }

        private void Update()
        {
            // 如果已经完成，直接跳过计算（保持完成状态，不需要玩家一直维持供电）
            if (isCompleted || powerNode == null || LevelManager.Instance == null) return;

            // 检查是否满足双重条件，并且自身没有因为输入过大而短路保护
            if (powerNode.CurrentPowerInput >= targetPower &&
                powerNode.CurrentStability >= targetStability &&
                !powerNode.IsProtectionTripped)
            {
                currentTimer += Time.deltaTime;

                // 达到规定时间，本终端充能完毕！
                if (currentTimer >= maintainDuration)
                {
                    isCompleted = true;

                    if (isRequiredForVictory)
                    {
                        Debug.Log($"<color=cyan>[主线目标] 核心终端 {powerNode.data.componentName} 成功持续充能！</color>");
                        // 只有被勾选为“通关必须”的目标，才会向关卡管理器汇报进度
                        LevelManager.Instance.ReportObjectiveCompleted();
                    }
                    else
                    {
                        Debug.Log($"<color=yellow>[支线建筑] 可选设备 {powerNode.data.componentName} 已激活！(不计入通关条件)</color>");
                    }
                }
            }
            else
            {
                // 惩罚机制：一旦中途断电、电压不稳、或者过载炸了，读条瞬间清零！
                if (currentTimer > 0)
                {
                    currentTimer = 0f;
                }
            }
        }
    }
}