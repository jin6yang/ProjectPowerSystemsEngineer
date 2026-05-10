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

        // 核心安全标记：一旦为 True，该节点将拒绝输出任何电力
        public bool IsProtectionTripped { get; private set; }

        // === 储能池化系统注入的数据 ===
        public StoragePool MyPool { get; set; }
        public float SavedChargePercent { get; set; }
        public bool IsPoweredByGenerator { get; set; }

        public List<PowerNode> OutgoingConnections { get; private set; } = new List<PowerNode>();

        // 【新增自愈接口】用于在每次图论重算前，清空错误状态，实现危机解除后的自动恢复
        public void ResetProtection()
        {
            IsProtectionTripped = false;
        }

        private void Start()
        {
            // 游戏启动时，如果是放置在场景中的地标/目标终端，自动占领网格
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
            // 如果是多路汇流，稳定度取“最脏”的那一路（木桶效应）
            if (CurrentPowerInput == 0) CurrentStability = stability;
            else CurrentStability = Mathf.Min(CurrentStability, stability);

            CurrentPowerInput = power;
            CheckOverload(); // 每次收到电都进行安检
        }

        protected virtual void CheckOverload()
        {
            if (data == null) return;

            // 1. 功率爆表检测
            if (CurrentPowerInput > data.maxPowerCapacity && !IsProtectionTripped)
            {
                TriggerProtection("输入功率超过承载上限！");
            }

            // 2. 电压不稳宕机检测 (仅对有要求的设备生效)
            if (CurrentPowerInput > 0 && data.requiredStability > 0 && CurrentStability < data.requiredStability && !IsProtectionTripped)
            {
                TriggerProtection($"电网波动过大！要求: {data.requiredStability} / 当前: {CurrentStability:0.0}");
            }
        }

        public virtual void TriggerProtection(string customReason = null)
        {
            IsProtectionTripped = true;

            // 注意：我们绝对不再清空 MyPool！
            // 这保证了单个电池的烧毁只会让它自己变成死铁，绝不污染或清空整个储能阵列的电量！

            if (string.IsNullOrEmpty(customReason))
                Debug.LogWarning($"[警报] {data?.componentName} 过载！输入:{CurrentPowerInput}MW。已切断输出！");
            else
                Debug.LogWarning($"[警报] {data?.componentName} 触发保护: {customReason}");
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

                // 强制初始化，彻底解决未赋值编译报错
                Color textColor = Color.white;
                string statusText = "";

                if (IsProtectionTripped)
                {
                    statusText = "[SYS_OVERLOAD]\n";
                    textColor = new Color(1f, 0.4f, 0.4f);
                }
                else if (data.category == ComponentCategory.Storage)
                {
                    if (MyPool != null)
                    {
                        if (MyPool.IsReceivingExternalPower)
                        {
                            if (MyPool.IsCharged)
                            {
                                statusText = $"[中转储能 / 满载 {MyPool.Nodes.Count}x]\n";
                                textColor = new Color(0.2f, 0.8f, 1f);
                            }
                            else
                            {
                                statusText = $"[中转充能 {Mathf.Clamp01(MyPool.CurrentChargeTimer / MyPool.TotalChargeTime) * 100:0}%]\n";
                                textColor = new Color(0.2f, 0.8f, 1f);
                            }
                        }
                        else
                        {
                            if (MyPool.IsCharged)
                            {
                                statusText = $"[备用放电中 {MyPool.TotalDischargeTime - MyPool.CurrentDischargeTimer:0.0}s]\n";
                                textColor = new Color(1f, 0.6f, 0.2f);
                            }
                            else
                            {
                                statusText = "[阵列电量耗尽]\n";
                                textColor = new Color(0.7f, 0.7f, 0.7f);
                            }
                        }
                    }
                    else
                    {
                        statusText = "[系统初始化...]\n";
                        textColor = new Color(0.7f, 0.7f, 0.7f);
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

                // 【视觉修复】清晰区分充电透传与备用放电的真实输出功率
                float displayPower = CurrentPowerInput;
                if (data.category == ComponentCategory.Generation)
                    displayPower = data.powerGeneration;
                else if (data.category == ComponentCategory.Storage && MyPool != null)
                {
                    if (MyPool.IsReceivingExternalPower)
                        displayPower = CurrentPowerInput;
                    else
                        displayPower = GetPowerOutput();
                }

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
        // 【核心机制修复】严格区分 储能透传 与 放电产出 模式
        // ==========================================
        public virtual float GetPowerOutput()
        {
            // 只要触发保护烧毁，无视任何规则，强行输出 0
            if (IsProtectionTripped || data == null) return 0f;

            if (data.category == ComponentCategory.Generation)
                return data.powerGeneration;

            if (data.category == ComponentCategory.Storage && MyPool != null)
            {
                if (MyPool.IsReceivingExternalPower)
                {
                    // 储能模式：仅仅是一个透传中转站，绝不无中生有叠加自身发电量
                    return Mathf.Max(0f, CurrentPowerInput - data.powerConsumption);
                }
                else
                {
                    // 放电模式：只有在池子有电时才作为备用电源输出，否则一滴电都没有
                    if (MyPool.IsCharged) return data.powerGeneration;
                    else return 0f;
                }
            }

            return Mathf.Max(0f, CurrentPowerInput - data.powerConsumption);
        }

        public virtual float GetStabilityOutput()
        {
            if (IsProtectionTripped || data == null) return 0f;

            if (data.category == ComponentCategory.Generation)
                return data.stabilityModifier;

            if (data.category == ComponentCategory.Storage && MyPool != null)
            {
                if (MyPool.IsReceivingExternalPower)
                {
                    // 【机制修复】充电满载模式：它是一个强力稳压器，使用专用的 storageStabilityModifier
                    if (MyPool.IsCharged) return Mathf.Clamp(CurrentStability + data.storageStabilityModifier, 0f, 10f);
                    else return CurrentStability;
                }
                else
                {
                    // 【机制修复】放电模式：它是最原始的备用电源，不提供任何稳定度加成！必须通过外部变电站稳压
                    return 0f;
                }
            }

            return Mathf.Clamp(CurrentStability + data.stabilityModifier, 0f, 10f);
        }
    }
}