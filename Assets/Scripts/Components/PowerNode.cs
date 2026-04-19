using UnityEngine;
using System.Collections.Generic;
using ProjectPowerSystemsEngineer.Data;
using ProjectPowerSystemsEngineer.Grid;
using ProjectPowerSystemsEngineer.UI; // 【新增】引入 UI 命名空间以获取全局变量

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

        public virtual void Initialize(Vector2Int pos)
        {
            GridPosition = pos;
            CurrentPowerInput = 0f;
            CurrentStability = 10f;
            IsProtectionTripped = false;
            OutgoingConnections.Clear();
        }

        public virtual void InitializeAsCable(PowerNode startNode, PowerNode endNode)
        {
            GridPosition = startNode.GridPosition;
            CurrentPowerInput = 0f;
            CurrentStability = 10f;
            IsProtectionTripped = false;
            OutgoingConnections.Clear();

            startNode.OutgoingConnections.Add(this);
            this.OutgoingConnections.Add(endNode);

            LineRenderer lr = GetComponent<LineRenderer>();
            if (lr != null)
            {
                Vector3 startPos = startNode.transform.position + Vector3.up * 1f;
                Vector3 endPos = endNode.transform.position + Vector3.up * 1f;
                lr.SetPosition(0, startPos);
                lr.SetPosition(1, endPos);
            }
        }

        public virtual void ReceivePower(float power, float stability)
        {
            // 核心修复：多路电流汇合时，纯净度(稳定性)取最差的那一路
            if (CurrentPowerInput == 0) CurrentStability = stability;
            else CurrentStability = Mathf.Min(CurrentStability, stability);

            CurrentPowerInput = power;

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

        // ==================== 动态缩放浮空 GUI ====================
        private void OnGUI()
        {
            // 【核心修改】如果 UIManager 里的开关被关闭，直接跳过绘制，实现瞬间隐藏
            if (!UIManager.ShowFloatingUI || Camera.main == null || data == null) return;

            Vector3 worldPos = transform.position + Vector3.up * 1.5f;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            if (screenPos.z > 0)
            {
                screenPos.y = Screen.height - screenPos.y;

                // 核心计算：计算摄像机与此建筑的物理距离，反比计算缩放倍率
                float distance = Vector3.Distance(Camera.main.transform.position, worldPos);
                // 15f 是我们设定的理想视觉基准距离
                float scaleFactor = 15f / Mathf.Max(distance, 1f);

                // 动态计算字体大小，并限制最小和最大值防止穿模
                int dynamicFontSize = Mathf.RoundToInt(14 * scaleFactor);
                dynamicFontSize = Mathf.Clamp(dynamicFontSize, 8, 32);

                GUIStyle style = new GUIStyle();
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = dynamicFontSize;
                style.fontStyle = FontStyle.Bold;

                Color textColor;
                string statusText = "";

                if (IsProtectionTripped)
                {
                    statusText = "[过载保护]\n";
                    textColor = new Color(1f, 0.4f, 0.4f);
                }
                else if (CurrentPowerInput > 0 || (data.category == ComponentCategory.Generation && data.powerGeneration > 0))
                {
                    statusText = "[运行中]\n";
                    textColor = new Color(0.4f, 1f, 0.4f);
                }
                else
                {
                    statusText = "[无输入]\n";
                    textColor = new Color(0.7f, 0.7f, 0.7f);
                }

                string displayText = $"{statusText}{data.componentName}\n{CurrentPowerInput} MW | S:{CurrentStability}";

                // 动态调整背景矩形框的大小，以适应大号字体
                float rectWidth = 150 * scaleFactor;
                float rectHeight = 80 * scaleFactor;
                // 给框体一个下限，防止拉得太远缩没了
                rectWidth = Mathf.Max(rectWidth, 100);
                rectHeight = Mathf.Max(rectHeight, 50);

                Rect rect = new Rect(screenPos.x - rectWidth / 2, screenPos.y - rectHeight / 2, rectWidth, rectHeight);

                // 画个纯黑底影，防强光看不清
                style.normal.textColor = Color.black;
                GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), displayText, style);

                // 画带颜色的本体
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