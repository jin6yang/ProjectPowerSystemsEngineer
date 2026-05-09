using UnityEngine;
using System.Collections.Generic;
using ProjectPowerSystemsEngineer.Data;
using ProjectPowerSystemsEngineer.Grid;
using ProjectPowerSystemsEngineer.UI;
using ProjectPowerSystemsEngineer.Simulation;

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

        // ==========================================
        // 【新增】储能建筑专属状态机
        // ==========================================
        public bool IsReceivingExternalPower { get; set; } // 是否正在接收上游传来的电
        public bool IsCharged { get; private set; } // 是否处于满载生效状态
        private float chargeTimer = 0f;
        private float dischargeTimer = 0f;

        public List<PowerNode> OutgoingConnections { get; private set; } = new List<PowerNode>();

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
                    transform.position = GridManager.Instance.GridToWorldPosition(pos);
                }
            }
        }

        // 【新增】每帧计算储能建筑的时间流逝
        private void Update()
        {
            if (data == null || data.category != ComponentCategory.Storage) return;

            // 如果正在接收外部电力，且没有因为输入过载而爆表
            if (IsReceivingExternalPower && !IsProtectionTripped)
            {
                dischargeTimer = 0f; // 停止放电并重置
                if (!IsCharged)
                {
                    chargeTimer += Time.deltaTime;
                    if (chargeTimer >= data.chargeTime)
                    {
                        IsCharged = true;
                        Debug.Log($"<color=cyan>[电网调度] {data.componentName} 充能完毕，成功并网提供稳定度！</color>");
                        PowerSimulationSystem.Instance?.RecalculatePowerGrid(); // 充能完毕，瞬间重算全网
                    }
                }
            }
            else
            {
                // 断电了！开始放电倒计时
                chargeTimer = 0f;
                if (IsCharged)
                {
                    dischargeTimer += Time.deltaTime;
                    if (dischargeTimer >= data.dischargeTime)
                    {
                        IsCharged = false;
                        Debug.Log($"<color=red>[电网调度] {data.componentName} 备用电量耗尽，宕机离线！</color>");
                        PowerSimulationSystem.Instance?.RecalculatePowerGrid(); // 电量耗尽，全网断电
                    }
                }
            }
        }

        public virtual void Initialize(Vector2Int pos)
        {
            GridPosition = pos;
            CurrentPowerInput = 0f;
            CurrentStability = 10f;
            IsProtectionTripped = false;
            IsCharged = false;
            chargeTimer = 0f;
            dischargeTimer = 0f;
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

            if (CurrentPowerInput > data.maxPowerCapacity && !IsProtectionTripped)
            {
                TriggerProtection("输入功率超过承载上限！");
            }

            if (CurrentPowerInput > 0 && data.requiredStability > 0 && CurrentStability < data.requiredStability && !IsProtectionTripped)
            {
                TriggerProtection($"电网波动过大！要求: {data.requiredStability} / 当前: {CurrentStability:0.0}");
            }
        }

        public virtual void TriggerProtection(string customReason = null)
        {
            IsProtectionTripped = true;
            IsCharged = false; // 触发保护瞬间清空储能
            if (string.IsNullOrEmpty(customReason))
            {
                Debug.LogWarning($"[警报] {data?.componentName} 过载！输入:{CurrentPowerInput}MW。已切断输出！");
            }
            else
            {
                Debug.LogWarning($"[警报] {data?.componentName} 触发保护: {customReason}");
            }
        }

        private void OnGUI()
        {
            if (!UIManager.ShowFloatingUI || Camera.main == null || data == null) return;

            Vector3 worldPos;
            if (data.isPointToPointCable)
            {
                LineRenderer lr = GetComponent<LineRenderer>();
                if (lr != null && lr.positionCount >= 2)
                    worldPos = (lr.GetPosition(0) + lr.GetPosition(1)) / 2f + Vector3.up * 0.5f;
                else
                    worldPos = transform.position + Vector3.up * 1.5f;
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
                int dynamicFontSize = Mathf.Clamp(Mathf.RoundToInt(14 * scaleFactor), 8, 32);

                GUIStyle style = new GUIStyle();
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = dynamicFontSize;
                style.fontStyle = FontStyle.Bold;

                Color textColor;
                string statusText = "";

                // ==========================================
                // 【核心视觉反馈】显示极其炫酷的充放电状态文本
                // ==========================================
                if (IsProtectionTripped)
                {
                    statusText = "[SYS_OVERLOAD]\n";
                    textColor = new Color(1f, 0.4f, 0.4f);
                }
                else if (data.category == ComponentCategory.Storage)
                {
                    if (IsCharged)
                    {
                        if (IsReceivingExternalPower)
                        {
                            statusText = "[储能满载 / 在线]\n";
                            textColor = new Color(0.2f, 0.8f, 1f); // 赛博青色
                        }
                        else
                        {
                            statusText = $"[备用放电中 {data.dischargeTime - dischargeTimer:0.0}s]\n";
                            textColor = new Color(1f, 0.6f, 0.2f); // 警示橙色
                        }
                    }
                    else
                    {
                        if (IsReceivingExternalPower)
                        {
                            statusText = $"[充能中 {Mathf.Clamp01(chargeTimer / data.chargeTime) * 100:0}%]\n";
                            textColor = new Color(0.2f, 0.8f, 1f);
                        }
                        else
                        {
                            statusText = "[电量耗尽 / 脱机]\n";
                            textColor = new Color(0.7f, 0.7f, 0.7f);
                        }
                    }
                }
                else if (CurrentPowerInput > 0 || GetPowerOutput() > 0)
                {
                    statusText = "[运行中]\n";
                    textColor = new Color(0.4f, 1f, 0.4f);
                }
                else
                {
                    statusText = "[无输入]\n";
                    textColor = new Color(0.7f, 0.7f, 0.7f);
                }

                // 修正放电时 UI 的输入数值显示：放电时虽然输入为 0，但为了美观我们显示它发出的电
                float displayPower = CurrentPowerInput;
                if (data.category == ComponentCategory.Generation) displayPower = data.powerGeneration;
                else if (data.category == ComponentCategory.Storage && IsCharged && !IsReceivingExternalPower) displayPower = data.powerGeneration;

                string displayText = $"{statusText}{data.componentName}\n{displayPower} MW | S:{GetStabilityOutput():0.0}";

                float rectWidth = Mathf.Max(150 * scaleFactor, 100);
                float rectHeight = Mathf.Max(80 * scaleFactor, 50);
                Rect rect = new Rect(screenPos.x - rectWidth / 2, screenPos.y - rectHeight / 2, rectWidth, rectHeight);

                style.normal.textColor = Color.black;
                GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), displayText, style);

                style.normal.textColor = textColor;
                GUI.Label(rect, displayText, style);
            }
        }

        // ==========================================
        // 【核心修改】充放电逻辑影响真实输出数据
        // ==========================================
        public virtual float GetPowerOutput()
        {
            if (IsProtectionTripped || data == null) return 0f;

            if (data.category == ComponentCategory.Generation)
                return data.powerGeneration;

            float outPower = Mathf.Max(0f, CurrentPowerInput - data.powerConsumption);

            // 如果是储能设备并且满载，额外输出它产生的功率（比如 60MW）
            if (data.category == ComponentCategory.Storage && IsCharged)
            {
                outPower += data.powerGeneration;
            }

            return outPower;
        }

        public virtual float GetStabilityOutput()
        {
            if (IsProtectionTripped || data == null) return 0f;

            if (data.category == ComponentCategory.Generation)
                return data.stabilityModifier;

            if (data.category == ComponentCategory.Storage && IsCharged)
            {
                // 如果断网了在放电，它作为一个独立的发电机，输出它本身的稳定度
                if (!IsReceivingExternalPower) return data.stabilityModifier;
                // 如果在网中，则作为一个稳压器加上这部分稳定度
                return Mathf.Clamp(CurrentStability + data.stabilityModifier, 0f, 10f);
            }

            return Mathf.Clamp(CurrentStability + data.stabilityModifier, 0f, 10f);
        }
    }
}