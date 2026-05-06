using UnityEngine;
using TMPro;

namespace ProjectPowerSystemsEngineer.UI
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class MarqueeText : MonoBehaviour
    {
        [Header("Settings")]
        public float scrollSpeed = 50f;

        [Tooltip("重复的文本内容")]
        public string textContent = "// OBSIDIAN INDUSTRIAL // POWER GRID SYSTEM ";

        [Tooltip("需要重复拼接多少次才能填满屏幕")]
        public int repeatCount = 5;

        private TextMeshProUGUI textComponent;
        private RectTransform rectTransform;
        private float resetXPosition;

        void Start()
        {
            textComponent = GetComponent<TextMeshProUGUI>();
            rectTransform = GetComponent<RectTransform>();

            // 拼接长字符串
            string fullText = "";
            for (int i = 0; i < repeatCount; i++)
            {
                fullText += textContent;
            }
            textComponent.text = fullText;

            // 强制更新网格以获取真实的文字渲染宽度
            textComponent.ForceMeshUpdate();

            // 计算重置点：当文字向左移动超过了一段文字的长度时，瞬间拉回来，实现无缝循环
            // 我们取整体宽度除以重复次数，得到单一循环段的长度
            resetXPosition = textComponent.preferredWidth / repeatCount;
        }

        void Update()
        {
            // 向左移动
            rectTransform.anchoredPosition += Vector2.left * scrollSpeed * Time.deltaTime;

            // 如果向左移动的距离超过了一个文本段的长度，将其重置回 0
            if (rectTransform.anchoredPosition.x <= -resetXPosition)
            {
                rectTransform.anchoredPosition = new Vector2(0, rectTransform.anchoredPosition.y);
            }
        }
    }
}