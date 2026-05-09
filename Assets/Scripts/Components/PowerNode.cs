using UnityEngine;
using System.Collections.Generic;
using ProjectPowerSystemsEngineer.Data;
using ProjectPowerSystemsEngineer.Grid;
using ProjectPowerSystemsEngineer.UI;

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

        // 【新增机制】游戏启动时，作为预置障碍物或终端，自动占领网格
        private void Start()
        {
            if (GridManager.Instance != null && data != null && !data.isPointToPointCable)
            {
                Vector2Int pos = GridManager.Instance.WorldToGridPosition(transform.position);
                GridCell cell = GridManager.Instance.GetCell(pos);

                if (cell != null && cell.PlacedObject == null)
                {
                    cell.PlacedObject = this.gameObject;
                    Initialize(pos);
                    // 自动吸附对齐到网格中心
                    transform.position = GridManager.Instance.GridToWorldPosition(pos);
                }
            }
        }

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

            // 【机制】发电机直连保护判定
            if (startNode != null && endNode != null &&
                startNode.data != null && endNode.data != null &&
                startNode.data.category == ComponentCategory.Generation &&
                endNode.data.category == ComponentCategory.Generation)
            {
                this.TriggerProtection("并网冲突：发电机不可直连！");
                startNode.TriggerProtection("并网短路！");
                endNode.TriggerProtection("并网短路！");
            }
        }

        public virtual void ReceivePower(float power, float stability)
        {
            if (CurrentPowerInput == 0) CurrentStability = stability;
            else CurrentStability = Mathf.Min(CurrentStability, stability);

            CurrentPowerInput = power;
            CheckOverload();
        }

        protected virtual void CheckOverload()
        {
            if (data == null) return;

            // 1. 功率过载保护
            if (CurrentPowerInput > data.maxPowerCapacity && !IsProtectionTripped)
            {
                TriggerProtection("输入功率超过承载上限！");
            }

            // 2. 【硬核机制】电网稳定度过低引发设备宕机！
            if (CurrentPowerInput > 0 && data.requiredStability > 0 && CurrentStability < data.requiredStability && !IsProtectionTripped)
            {
                TriggerProtection($"电网波动过大！要求: {data.requiredStability} / 当前: {CurrentStability:0.0}");
            }
        }

        public virtual void TriggerProtection(string customReason = null)
        {
            IsProtectionTripped = true;
            if (string.IsNullOrEmpty(customReason))
            {
                Debug.LogWarning($"[警报] {data?.componentName} 过载！输入:{CurrentPowerInput}MW。已切断输出！");
            }
            else
            {
                Debug.LogWarning($"[警报] {data?.componentName} 触发保护: {customReason}");
            }
        }

        // ==================== 动态缩放浮空 GUI ====================
        private void OnGUI()
        {
            if (!UIManager.ShowFloatingUI || Camera.main == null || data == null) return;

            Vector3 worldPos;
            if (data.isPointToPointCable)
            {
                LineRenderer lr = GetComponent<LineRenderer>();
                if (lr != null && lr.positionCount >= 2)
                {
                    worldPos = (lr.GetPosition(0) + lr.GetPosition(1)) / 2f + Vector3.up * 0.5f;
                }
                else
                {
                    worldPos = transform.position + Vector3.up * 1.5f;
                }
            }
            else
            {
                worldPos = transform.position + Vector3.up * 1.5f;
            }

            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            if (screenPos.z > 0)
            {
                screenPos.y = Screen.height - screenPos.y;

                float distance = Vector3.Distance(Camera.main.transform.position, worldPos);
                float scaleFactor = 15f / Mathf.Max(distance, 1f);

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

                float rectWidth = Mathf.Max(150 * scaleFactor, 100);
                float rectHeight = Mathf.Max(80 * scaleFactor, 50);

                Rect rect = new Rect(screenPos.x - rectWidth / 2, screenPos.y - rectHeight / 2, rectWidth, rectHeight);

                style.normal.textColor = Color.black;
                GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), displayText, style);

                style.normal.textColor = textColor;
                GUI.Label(rect, displayText, style);
            }
        }

        public virtual float GetPowerOutput()
        {
            if (IsProtectionTripped || data == null) return 0f;

            if (data.category == ComponentCategory.Generation)
            {
                return data.powerGeneration;
            }

            return Mathf.Max(0f, CurrentPowerInput - data.powerConsumption);
        }

        public virtual float GetStabilityOutput()
        {
            if (IsProtectionTripped || data == null) return 0f;
            return Mathf.Clamp(CurrentStability + data.stabilityModifier, 0f, 10f);
        }
    }
}