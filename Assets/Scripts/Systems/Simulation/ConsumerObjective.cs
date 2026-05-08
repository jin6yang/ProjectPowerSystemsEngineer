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
            if (isCompleted || powerNode == null || LevelManager.Instance == null) return;

            // 【核心判定】检查是否满足双重条件，并且自身没有因为输入过大而短路保护
            if (powerNode.CurrentPowerInput >= targetPower &&
                powerNode.CurrentStability >= targetStability &&
                !powerNode.IsProtectionTripped)
            {
                currentTimer += Time.deltaTime;

                // 达到规定时间，触发通关！
                if (currentTimer >= maintainDuration)
                {
                    isCompleted = true;
                    Debug.Log($"<color=cyan>[关卡目标] 终端设备 {powerNode.data.componentName} 成功持续充能！</color>");
                    LevelManager.Instance.TriggerVictory();
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