using UnityEngine;
using TMPro;
using UnityEngine.InputSystem; // 引入输入系统以绑定快捷键
using ProjectPowerSystemsEngineer.Systems;
using ProjectPowerSystemsEngineer.Components;

namespace ProjectPowerSystemsEngineer.UI
{
    public class UIManager : MonoBehaviour
    {
        // 【新增】全局浮空 UI 开关，默认开启
        public static bool ShowFloatingUI { get; private set; } = true;

        [Header("System References")]
        [Tooltip("需要获取建造控制器里的 SelectedNode")]
        public BuilderController builderController;

        [Header("UI Panels")]
        [Tooltip("整个右下角的详情面板容器")]
        public GameObject inspectPanel;

        [Header("Text Elements")]
        public TextMeshProUGUI txtComponentName;
        public TextMeshProUGUI txtStatus;
        public TextMeshProUGUI txtPowerInput;
        public TextMeshProUGUI txtStability;
        public TextMeshProUGUI txtMaxCapacity;

        [Header("Colors")]
        public Color colorNormal = new Color(0.4f, 1f, 0.4f);
        public Color colorOverload = new Color(1f, 0.4f, 0.4f);
        public Color colorOffline = new Color(0.6f, 0.6f, 0.6f);

        private void Start()
        {
            if (builderController == null)
            {
                builderController = FindAnyObjectByType<BuilderController>();
            }

            if (inspectPanel != null) inspectPanel.SetActive(false);
        }

        private void Update()
        {
            // 【新增】监听 Tab 键，快捷切换浮空信息的展示
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleFloatingUI();
            }

            if (builderController == null) return;

            PowerNode selectedNode = builderController.SelectedNode;

            if (selectedNode != null && !selectedNode.data.isPointToPointCable)
            {
                if (!inspectPanel.activeSelf) inspectPanel.SetActive(true);
                UpdateInspectPanel(selectedNode);
            }
            else
            {
                if (inspectPanel.activeSelf) inspectPanel.SetActive(false);
            }
        }

        private void UpdateInspectPanel(PowerNode node)
        {
            txtComponentName.text = node.data.componentName.ToUpper();

            if (node.IsProtectionTripped)
            {
                txtStatus.text = "SYS_OVERLOAD // 保护熔断";
                txtStatus.color = colorOverload;
                txtPowerInput.color = colorOverload;
            }
            else if (node.CurrentPowerInput > 0 || node.data.powerGeneration > 0)
            {
                txtStatus.text = "SYS_ONLINE // 正常运行";
                txtStatus.color = colorNormal;
                txtPowerInput.color = Color.white;
            }
            else
            {
                txtStatus.text = "SYS_OFFLINE // 离线等待";
                txtStatus.color = colorOffline;
                txtPowerInput.color = colorOffline;
            }

            txtPowerInput.text = $"{node.CurrentPowerInput:000.0} <size=60%>MW</size>";
            txtMaxCapacity.text = $"MAX: {node.data.maxPowerCapacity} MW";
            txtStability.text = $"STB_LVL: {node.CurrentStability:0.0}";
        }

        // 【新增】供未来 UI 按钮调用的公开方法
        public void ToggleFloatingUI()
        {
            ShowFloatingUI = !ShowFloatingUI;
            Debug.Log($"<color=yellow>[UI系统] 全局浮空信息展示: {(ShowFloatingUI ? "开启" : "关闭")}</color>");
        }
    }
}