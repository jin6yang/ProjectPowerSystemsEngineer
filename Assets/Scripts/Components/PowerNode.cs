using UnityEngine;
using System.Collections.Generic;
using ProjectPowerSystemsEngineer.Data;
using ProjectPowerSystemsEngineer.Grid;

namespace ProjectPowerSystemsEngineer.Components
{
    public class PowerNode : MonoBehaviour
    {
        [Header("Data Profile")]
        public ComponentData data;

        public Vector2Int GridPosition { get; private set; }

        public float CurrentPowerInput { get; private set; }
        public float CurrentStability { get; private set; }
        public bool IsProtectionTripped { get; private set; }

        // 【新增】存储当前节点连接到的所有下游节点（拓扑图的“边”）
        public List<PowerNode> OutgoingConnections { get; private set; } = new List<PowerNode>();

        /// <summary>
        /// 常规地面建筑的初始化
        /// </summary>
        public virtual void Initialize(Vector2Int pos)
        {
            GridPosition = pos;
            CurrentPowerInput = 0f;
            CurrentStability = 10f;
            IsProtectionTripped = false;
            OutgoingConnections.Clear();
        }

        /// <summary>
        /// 【新增】电缆专用的初始化逻辑
        /// </summary>
        public virtual void InitializeAsCable(PowerNode startNode, PowerNode endNode)
        {
            // 电线不严格占据独立网格，暂时沿用起点的坐标
            GridPosition = startNode.GridPosition;
            CurrentPowerInput = 0f;
            CurrentStability = 10f;
            IsProtectionTripped = false;
            OutgoingConnections.Clear();

            // 建立逻辑关联： 起点建筑 -> 这根电线 -> 终点建筑
            startNode.OutgoingConnections.Add(this);
            this.OutgoingConnections.Add(endNode);

            // 如果电线预制体上挂载了 LineRenderer（线段渲染器），则自动设置两端位置
            LineRenderer lr = GetComponent<LineRenderer>();
            if (lr != null)
            {
                // 抬高一点 Y 轴，防止电线埋在地里
                Vector3 startPos = startNode.transform.position + Vector3.up * 1f;
                Vector3 endPos = endNode.transform.position + Vector3.up * 1f;
                lr.SetPosition(0, startPos);
                lr.SetPosition(1, endPos);
            }

            Debug.Log($"[电网建立] 铺设了一条 {data.componentName}：由 {startNode.data.componentName} 连向 {endNode.data.componentName}");
        }

        public virtual void ReceivePower(float power, float stability)
        {
            CurrentPowerInput = power;
            CurrentStability = stability;

            CheckOverload();
        }

        protected virtual void CheckOverload()
        {
            if (data == null) return;

            if (CurrentPowerInput > data.maxPowerCapacity && !IsProtectionTripped)
            {
                TriggerProtection();
            }
        }

        protected virtual void TriggerProtection()
        {
            IsProtectionTripped = true;
            Debug.LogWarning($"[警报] {data.componentName} 过载！输入:{CurrentPowerInput}MW / 上限:{data.maxPowerCapacity}MW。已触发保护系统，切断输出！");
        }

        public virtual float GetPowerOutput()
        {
            if (IsProtectionTripped) return 0f;

            if (data.category == ComponentCategory.Generation)
            {
                return data.powerGeneration;
            }

            return Mathf.Max(0f, CurrentPowerInput - data.powerConsumption);
        }

        public virtual float GetStabilityOutput()
        {
            if (IsProtectionTripped) return 0f;
            return Mathf.Clamp(CurrentStability + data.stabilityModifier, 0f, 10f);
        }
    }
}