using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace ProjectPowerSystemsEngineer.UI
{
    public class MissionObjectivePanel : MonoBehaviour
    {
        [Header("UI References")]
        public RectTransform panelRect;
        public Button btnToggle;
        public CanvasGroup textContentGroup;

        [Header("Icon & Colors (新增)")]
        [Tooltip("竖线按钮里面的图标 Image 组件")]
        public Image iconImage;
        public Color colorDefault = new Color(1f, 0.84f, 0f, 1f); // 工业黄
        public Color colorSuccess = new Color(0.2f, 0.8f, 0.2f, 1f); // 成功绿
        public Color colorFailed = new Color(0.9f, 0.2f, 0.2f, 1f);  // 失败红

        [Header("Text References")]
        public TextMeshProUGUI txtTitle;
        public TextMeshProUGUI txtObjective;

        [Header("Animation Settings")]
        public float expandedWidth = 400f;
        public float collapsedWidth = 25f;
        public float slideDuration = 0.25f;
        public float textFadeDuration = 0.15f;

        private bool isExpanded = false;
        private Coroutine sequenceCoroutine;

        private void Start()
        {
            if (panelRect == null) panelRect = GetComponent<RectTransform>();
            if (btnToggle != null) btnToggle.onClick.AddListener(TogglePanel);

            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, collapsedWidth);
            if (textContentGroup != null) textContentGroup.alpha = 0f;

            // 初始化图标颜色
            if (iconImage != null) iconImage.color = colorDefault;
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
                yield return StartCoroutine(AnimateWidth(expandedWidth));
                yield return StartCoroutine(AnimateTextAlpha(1f));
            }
            else
            {
                yield return StartCoroutine(AnimateTextAlpha(0f));
                yield return StartCoroutine(AnimateWidth(collapsedWidth));
            }
        }

        private IEnumerator AnimateWidth(float targetWidth)
        {
            float startWidth = panelRect.rect.width;
            if (Mathf.Abs(startWidth - targetWidth) < 0.1f) yield break;

            float totalDist = Mathf.Abs(expandedWidth - collapsedWidth);
            if (totalDist < 0.1f) totalDist = 0.1f;

            float timer = 0f;
            float duration = slideDuration * (Mathf.Abs(targetWidth - startWidth) / totalDist);
            if (duration <= 0.01f) duration = 0.01f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;
                t = 1f - Mathf.Pow(1f - t, 3);

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

            if (!isExpanded) TogglePanel();
        }

        // ==========================================
        // 【新增】供系统调用，修改任务状态和图标颜色
        // ==========================================
        public void SetMissionStatus(bool isSuccess)
        {
            if (iconImage != null)
            {
                iconImage.color = isSuccess ? colorSuccess : colorFailed;
            }

            // 如果结算时面板是收纳的，强制弹出来展示最终结果
            if (!isExpanded)
            {
                TogglePanel();
            }
        }
    }
}