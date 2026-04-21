using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections; // 引入协程
using ProjectPowerSystemsEngineer.Systems;
using ProjectPowerSystemsEngineer.Components;

namespace ProjectPowerSystemsEngineer.UI
{
    public class UIManager : MonoBehaviour
    {
        public static bool ShowFloatingUI { get; private set; } = true;

        [Header("System References")]
        public BuilderController builderController;

        [Header("UI Containers")]
        [Tooltip("右下角的详情面板")]
        public CanvasGroup inspectPanelGroup;
        [Tooltip("顶部的滚动字幕面板")]
        public CanvasGroup topMarqueeGroup;

        [Header("Animation Elements")]
        [Tooltip("用于切换加载动画的工业黄背景块")]
        public RectTransform wipeBlock;
        private CanvasGroup wipeBlockGroup;

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

        // 状态追踪
        private PowerNode lastSelectedNode = null;
        private Coroutine fadeCoroutine;
        private Coroutine wipeCoroutine;

        private void Start()
        {
            if (builderController == null) builderController = FindAnyObjectByType<BuilderController>();

            if (wipeBlock != null) wipeBlockGroup = wipeBlock.GetComponent<CanvasGroup>();

            // 初始化时隐藏 UI，设置透明度为 0，并且关闭交互以阻挡点击
            SetCanvasGroupState(inspectPanelGroup, 0f, false);
            SetCanvasGroupState(topMarqueeGroup, 0f, false);

            if (wipeBlockGroup != null) SetCanvasGroupState(wipeBlockGroup, 0f, false);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleFloatingUI();
            }

            if (builderController == null) return;

            // 获取当前选中的节点（排除电线，因为电线不在此面板显示）
            PowerNode currentSelected = builderController.SelectedNode;
            if (currentSelected != null && currentSelected.data.isPointToPointCable)
            {
                currentSelected = null;
            }

            // 核心逻辑：检测选中状态是否发生了“变化”
            if (currentSelected != lastSelectedNode)
            {
                if (currentSelected != null && lastSelectedNode == null)
                {
                    // 1. 从“无”到“有” -> 淡入主面板，并执行擦除加载动画
                    UpdateInspectPanel(currentSelected);
                    PlayFadeAnimation(1f);
                    PlayWipeAnimation();
                }
                else if (currentSelected != null && lastSelectedNode != null)
                {
                    // 2. 连续切换不同的建筑 -> 保持面板显示，仅执行擦除加载动画
                    UpdateInspectPanel(currentSelected);
                    PlayWipeAnimation();
                }
                else if (currentSelected == null && lastSelectedNode != null)
                {
                    // 3. 取消选中 -> 淡出主面板
                    PlayFadeAnimation(0f);
                }

                lastSelectedNode = currentSelected;
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
                txtStatus.text = "SYS_STANDBY // 离线等待";
                txtStatus.color = colorOffline;
                txtPowerInput.color = colorOffline;
            }

            txtPowerInput.text = $"{node.CurrentPowerInput:000.0} <size=60%>MW</size>";
            txtMaxCapacity.text = $"MAX: {node.data.maxPowerCapacity} MW";
            txtStability.text = $"STB_LVL: {node.CurrentStability:0.0}";
        }

        public void ToggleFloatingUI()
        {
            ShowFloatingUI = !ShowFloatingUI;
        }

        // ==================== 原生代码动画引擎 ====================

        private void PlayFadeAnimation(float targetAlpha)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCanvasGroup(inspectPanelGroup, targetAlpha, 0.2f));

            // 安全检查：如果还没做 TopMarqueePanel，跳过它的动画，防止报错
            if (topMarqueeGroup != null)
            {
                StartCoroutine(FadeCanvasGroup(topMarqueeGroup, targetAlpha, 0.2f));
            }
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration)
        {
            if (cg == null) yield break;

            float startAlpha = cg.alpha;
            float time = 0;

            if (targetAlpha > 0) cg.interactable = cg.blocksRaycasts = true;

            while (time < duration)
            {
                time += Time.deltaTime;
                cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
                yield return null;
            }

            cg.alpha = targetAlpha;
            if (targetAlpha == 0) cg.interactable = cg.blocksRaycasts = false;
        }

        private void PlayWipeAnimation()
        {
            if (wipeBlock == null || wipeBlockGroup == null || inspectPanelGroup == null) return;
            if (wipeCoroutine != null) StopCoroutine(wipeCoroutine);
            wipeCoroutine = StartCoroutine(WipeRoutine());
        }

        private IEnumerator WipeRoutine()
        {
            wipeBlockGroup.alpha = 1f;

            // 获取父面板的宽度
            float panelWidth = inspectPanelGroup.GetComponent<RectTransform>().rect.width;

            // 确保起始点在面板最左侧往外一点（防止露馅）
            float startPosX = -panelWidth - 50f;
            wipeBlock.anchoredPosition = new Vector2(startPosX, 0);

            // 阶段 1：快速滑到原点 (0.15秒)
            float moveTime = 0.15f;
            float timer = 0;
            while (timer < moveTime)
            {
                timer += Time.deltaTime;
                float t = timer / moveTime;
                t = 1f - Mathf.Pow(1f - t, 3); // EaseOut 缓动曲线

                wipeBlock.anchoredPosition = new Vector2(Mathf.Lerp(startPosX, 0, t), 0);
                yield return null;
            }
            wipeBlock.anchoredPosition = Vector2.zero;

            // 阶段 2：短暂悬停模拟“读取中” (0.05秒)
            yield return new WaitForSeconds(0.05f);

            // 阶段 3：原地淡出 (0.2秒)
            float fadeTime = 0.2f;
            timer = 0;
            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                wipeBlockGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeTime);
                yield return null;
            }
            wipeBlockGroup.alpha = 0f;
        }

        private void SetCanvasGroupState(CanvasGroup cg, float alpha, bool interactable)
        {
            if (cg != null)
            {
                cg.alpha = alpha;
                cg.interactable = interactable;
                cg.blocksRaycasts = interactable;
            }
        }
    }
}