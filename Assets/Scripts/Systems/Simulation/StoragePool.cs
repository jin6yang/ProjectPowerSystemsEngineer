using System.Collections.Generic;
using UnityEngine;
using ProjectPowerSystemsEngineer.Components;

namespace ProjectPowerSystemsEngineer.Simulation
{
    /// <summary>
    /// 储能资源池 (Storage Pool)
    /// 管理同一物理连通网络中所有储能建筑的共享生命周期，并在拓扑计算中作为“宏节点”存在
    /// </summary>
    public class StoragePool
    {
        public List<PowerNode> Nodes = new List<PowerNode>();
        // 【新增】用于收集连接电池之间的“内部连线”
        public List<PowerNode> InternalCables = new List<PowerNode>();

        public float TotalChargeTime = 0f;
        public float TotalDischargeTime = 0f;
        public float CurrentChargeTimer = 0f;
        public float CurrentDischargeTimer = 0f;

        public bool IsReceivingExternalPower = false;
        public bool IsCharged = false;

        // ==========================================
        // 【新增】用于宏节点拓扑计算的数据缓存
        // ==========================================
        public float SharedPowerInput = 0f;
        public float SharedStability = 10f;
        public bool IsPoweredByGenerator = false;

        // 【新增缓存】用于 UI 统一显示该池子的最终宏观总输出
        public float CachedOutPower = 0f;
        public float CachedOutStability = 0f;

        /// <summary>
        /// 每帧更新池子状态。如果状态发生重大翻转（充能完毕/耗尽宕机），返回 true 呼叫全网重算。
        /// </summary>
        public bool Tick(float dt)
        {
            bool stateChanged = false;

            if (IsReceivingExternalPower)
            {
                CurrentDischargeTimer = 0f; // 重置放电
                if (!IsCharged)
                {
                    CurrentChargeTimer += dt;
                    if (CurrentChargeTimer >= TotalChargeTime)
                    {
                        CurrentChargeTimer = TotalChargeTime;
                        IsCharged = true;
                        stateChanged = true;
                        Debug.Log($"<color=cyan>[电网调度] 储能阵列 (并联 {Nodes.Count} 单元) 充能完毕，并网提供稳定度！</color>");
                    }
                }
            }
            else
            {
                CurrentChargeTimer = 0f; // 重置充能
                if (IsCharged)
                {
                    CurrentDischargeTimer += dt;
                    if (CurrentDischargeTimer >= TotalDischargeTime)
                    {
                        IsCharged = false;
                        stateChanged = true;
                        Debug.Log($"<color=red>[电网调度] 储能阵列 (并联 {Nodes.Count} 单元) 备用电量耗尽，全阵列宕机！</color>");
                    }
                }
            }

            return stateChanged;
        }
    }
}