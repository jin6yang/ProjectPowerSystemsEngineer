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

        // 【新增】自动检测是否是关卡预置建筑
        private void Start()
        {
            // 如果游戏开始时，它是普通建筑且存在于网格系统中
            if (GridManager.Instance != null && data != null && !data.isPointToPointCable)
            {
                // 获取自身所在的网格坐标
                Vector2Int pos = GridManager.Instance.WorldToGridPosition(transform.position);
                GridCell cell = GridManager.Instance.GetCell(pos);

                // 如果这个格子还没被别人占领，自动占据！(用于处理你在编辑器里手动摆放的建筑)
                if (cell != null && cell.PlacedObject == null)
                {
                    cell.PlacedObject = this.gameObject;
                    Initialize(pos);
                    // 自动吸附对齐到网格中心，方便你在编辑器里随便摆，运行会自动对齐！
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

            // 【新增核心机制】发电机直连保护判定 (已加入 null 防御)
            if (startNode != null && endNode != null &&
                startNode.data != null && endNode.data != null &&
                startNode.data.category == ComponentCategory.Generation &&
                endNode.data.category == ComponentCategory.Generation)
            {
                // 触发并网短路保护！电线和两端的发电机全部锁死瘫痪！
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
            if (CurrentPowerInput > data.maxPowerCapacity && !IsProtectionTripped)
            {
                TriggerProtection();
            }
        }

        // 基础保护触发
        public virtual void TriggerProtection()
        {
            TriggerProtection(null);
        }

        // 带原因的保护触发
        public virtual void TriggerProtection(string customReason)
        {
            IsProtectionTripped = true;
            if (string.IsNullOrEmpty(customReason))
            {
                // 使用 ?. 防止残影没有 data 时报错
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

            // 【核心修复】区分普通建筑和电线的坐标获取方式
            Vector3 worldPos;
            if (data.isPointToPointCable)
            {
                LineRenderer lr = GetComponent<LineRenderer>();
                if (lr != null && lr.positionCount >= 2)
                {
                    // 电线的坐标取两端的中点，并且高度稍微降低一点以防止挡住建筑
                    worldPos = (lr.GetPosition(0) + lr.GetPosition(1)) / 2f + Vector3.up * 0.5f;
                }
                else
                {
                    worldPos = transform.position + Vector3.up * 1.5f;
                }
            }
            else
            {
                // 普通建筑取自身坐标正上方
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