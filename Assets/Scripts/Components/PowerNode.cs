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

        // 极其好用的内置 GUI：在模型头顶直接绘制 2D 数据面板
        private void OnGUI()
        {
            if (Camera.main == null || data == null) return;

            // 将 3D 世界坐标转换为 2D 屏幕坐标 (头顶上方1.5米处)
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);

            // 确保物体在摄像机前方才绘制
            if (screenPos.z > 0)
            {
                screenPos.y = Screen.height - screenPos.y; // 翻转 Y 轴适应 GUI 系统

                GUIStyle style = new GUIStyle();
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 14;
                style.fontStyle = FontStyle.Bold;

                Color textColor;
                string statusText = "";

                if (IsProtectionTripped)
                {
                    statusText = "[过载保护]\n";
                    textColor = new Color(1f, 0.4f, 0.4f); // 柔和的红色
                }
                else if (CurrentPowerInput > 0 || (data.category == ComponentCategory.Generation && data.powerGeneration > 0))
                {
                    statusText = "[运行中]\n";
                    textColor = new Color(0.4f, 1f, 0.4f); // 科技感的绿色
                }
                else
                {
                    statusText = "[离线/无输入]\n";
                    textColor = new Color(0.7f, 0.7f, 0.7f); // 灰色
                }

                string displayText = $"{statusText}{data.componentName}\n输入: {CurrentPowerInput} MW\n稳定度: {CurrentStability}";

                // 绘制黑色文字阴影，防止在亮色背景下看不清
                Rect rect = new Rect(screenPos.x - 100, screenPos.y - 50, 200, 100);
                style.normal.textColor = Color.black;
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), displayText, style);

                // 绘制带状态颜色的主文字
                style.normal.textColor = textColor;
                GUI.Label(rect, displayText, style);
            }
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