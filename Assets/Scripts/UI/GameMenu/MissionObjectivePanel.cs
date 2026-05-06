using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace ProjectPowerSystemsEngineer.UI
{
    public class MissionObjectivePanel : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("整个任务面板的父级 RectTransform")]
        public RectTransform panelRect;
        [Tooltip("右侧的竖线收纳按钮")]
        public Button btnToggle;

        [Tooltip("用于单独控制文字透明度的 CanvasGroup")]
        public CanvasGroup textContentGroup;

        [Header("Text References")]
        public TextMeshProUGUI txtTitle;
        public TextMeshProUGUI txtObjective;

        [Header("Animation Settings")]
        [Tooltip("展开时的总宽度 (如: 400)")]
        public float expandedWidth = 400f;
        [Tooltip("收纳时的宽度，即按钮的宽度 (如: 25)")]
        public float collapsedWidth = 25f;

        [Tooltip("面板物理宽度缩放的持续时间")]
        public float slideDuration = 0.25f;
        [Tooltip("文字透明度渐变的持续时间")]
        public float textFadeDuration = 0.15f;

        private bool isExpanded = false;
        private Coroutine sequenceCoroutine;

        private void Start()
        {
            if (panelRect == null) panelRect = GetComponent<RectTransform>();

            if (btnToggle != null)
            {
                btnToggle.onClick.AddListener(TogglePanel);
            }

            // 强制初始化状态为“已收纳”
            // 修复：使用更安全的 SetSizeWithCurrentAnchors 替代 sizeDelta
            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, collapsedWidth);
            if (textContentGroup != null) textContentGroup.alpha = 0f;
        }

        public void TogglePanel()
        {
            isExpanded = !isExpanded;

            if (sequenceCoroutine != null) StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            if (isExpanded)
            {
                // 展开：1. 面板变宽 -> 2. 文字淡入
                yield return StartCoroutine(AnimateWidth(expandedWidth));
                yield return StartCoroutine(AnimateTextAlpha(1f));
            }
            else
            {
                // 收纳：1. 文字淡出 -> 2. 面板变窄
                yield return StartCoroutine(AnimateTextAlpha(0f));
                yield return StartCoroutine(AnimateWidth(collapsedWidth));
            }
        }

        private IEnumerator AnimateWidth(float targetWidth)
        {
            float startWidth = panelRect.rect.width;
            if (Mathf.Abs(startWidth - targetWidth) < 0.1f) yield break;

            // 防除零保护：确保分母绝对不可能为 0
            float totalDist = Mathf.Abs(expandedWidth - collapsedWidth);
            if (totalDist < 0.1f) totalDist = 0.1f;

            float timer = 0f;
            float duration = slideDuration * (Mathf.Abs(targetWidth - startWidth) / totalDist);
            if (duration <= 0.01f) duration = 0.01f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;
                t = 1f - Mathf.Pow(1f - t, 3); // Ease-Out 缓动

                // 核心修复：绝对强制修改宽度，无视锚点状态
                panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Lerp(startWidth, targetWidth, t));
                yield return null;
            }

            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        }

        private IEnumerator AnimateTextAlpha(float targetAlpha)
        {
            if (textContentGroup == null) yield break;

            float startAlpha = textContentGroup.alpha;
            if (Mathf.Abs(startAlpha - targetAlpha) < 0.01f) yield break;

            float timer = 0f;
            float duration = textFadeDuration * Mathf.Abs(targetAlpha - startAlpha);
            if (duration <= 0f) duration = 0.01f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                textContentGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
                yield return null;
            }

            textContentGroup.alpha = targetAlpha;
        }

        public void SetObjective(string title, string description)
        {
            if (txtTitle != null) txtTitle.text = title;
            if (txtObjective != null) txtObjective.text = description;

            if (!isExpanded)
            {
                TogglePanel();
            }
        }
    }
}