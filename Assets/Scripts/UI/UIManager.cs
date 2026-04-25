using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;
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
        public CanvasGroup inspectPanelGroup;
        public CanvasGroup topMarqueeGroup;

        [Header("Animation Elements")]
        public RectTransform wipeBlock;
        private CanvasGroup wipeBlockGroup;

        [Header("Text Elements")]
        public TextMeshProUGUI txtComponentName;
        public TextMeshProUGUI txtStatus;
        public TextMeshProUGUI txtPowerInput;
        public TextMeshProUGUI txtStability;
        [Tooltip("用于独立排版显示最大承载量 (例如: / 1.0 G)")]
        public TextMeshProUGUI txtMaxCapacity;

        [Header("Colors")]
        public Color colorNormal = new Color(0.4f, 1f, 0.4f);
        public Color colorOverload = new Color(1f, 0.4f, 0.4f);
        public Color colorOffline = new Color(0.6f, 0.6f, 0.6f);

        private PowerNode lastSelectedNode = null;
        private Coroutine fadeCoroutine;
        private Coroutine wipeCoroutine;

        private void Start()
        {
            if (builderController == null) builderController = FindAnyObjectByType<BuilderController>();
            if (wipeBlock != null) wipeBlockGroup = wipeBlock.GetComponent<CanvasGroup>();

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

            PowerNode currentSelected = builderController.SelectedNode;
            if (currentSelected != null && currentSelected.data.isPointToPointCable)
            {
                currentSelected = null;
            }

            if (currentSelected != lastSelectedNode)
            {
                if (currentSelected != null && lastSelectedNode == null)
                {
                    UpdateInspectPanel(currentSelected);
                    PlayFadeAnimation(1f);
                    PlayWipeAnimation();
                }
                else if (currentSelected != null && lastSelectedNode != null)
                {
                    UpdateInspectPanel(currentSelected);
                    PlayWipeAnimation();
                }
                else if (currentSelected == null && lastSelectedNode != null)
                {
                    PlayFadeAnimation(0f);
                }

                lastSelectedNode = currentSelected;
            }
        }

        private void UpdateInspectPanel(PowerNode node)
        {
            // 1. 组件名称
            txtComponentName.text = node.data.componentName.ToUpper();

            // 2. 极简状态码更新
            if (node.IsProtectionTripped)
            {
                txtStatus.text = "[SYS_OVERLOAD]";
                txtStatus.color = colorOverload;
                txtPowerInput.color = colorOverload;
            }
            else if (node.CurrentPowerInput > 0 || node.data.powerGeneration > 0)
            {
                txtStatus.text = "[SYS_ONLINE]";
                txtStatus.color = colorNormal;
                txtPowerInput.color = Color.white;
            }
            else
            {
                txtStatus.text = "[SYS_STANDBY]";
                txtStatus.color = colorOffline;
                txtPowerInput.color = colorOffline;
            }

            // 3. 动态电力单位排版 (重新拆分为独立文本)
            string currentPowerStr = FormatPowerValue(node.CurrentPowerInput);
            string maxPowerStr = FormatPowerValue(node.data.maxPowerCapacity);

            txtPowerInput.text = $"⚡ {currentPowerStr}";

            if (txtMaxCapacity != null)
            {
                txtMaxCapacity.text = $"/ {maxPowerStr}";
            }

            // 4. 稳定度
            txtStability.text = $"STB_LVL: {node.CurrentStability:0.0}";
        }

        // --- 核心换算逻辑：大于等于 1000 自动转 G ---
        private string FormatPowerValue(float mwValue)
        {
            if (mwValue >= 1000f)
            {
                return $"{mwValue / 1000f:0.0} G";
            }
            else
            {
                return $"{mwValue:0.0} M";
            }
        }

        public void ToggleFloatingUI()
        {
            ShowFloatingUI = !ShowFloatingUI;
        }

        private void PlayFadeAnimation(float targetAlpha)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCanvasGroup(inspectPanelGroup, targetAlpha, 0.2f));
            if (topMarqueeGroup != null) StartCoroutine(FadeCanvasGroup(topMarqueeGroup, targetAlpha, 0.2f));
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
            float panelWidth = inspectPanelGroup.GetComponent<RectTransform>().rect.width;

            float startPosX = -panelWidth - 50f;
            wipeBlock.anchoredPosition = new Vector2(startPosX, 0);

            float moveTime = 0.15f;
            float timer = 0;
            while (timer < moveTime)
            {
                timer += Time.deltaTime;
                float t = timer / moveTime;
                t = 1f - Mathf.Pow(1f - t, 3);

                wipeBlock.anchoredPosition = new Vector2(Mathf.Lerp(startPosX, 0, t), 0);
                yield return null;
            }
            wipeBlock.anchoredPosition = Vector2.zero;

            yield return new WaitForSeconds(0.05f);

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