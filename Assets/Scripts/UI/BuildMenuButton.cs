using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections; // 【新增】引入协程命名空间

namespace ProjectPowerSystemsEngineer.UI
{
    public class BuildMenuButton : MonoBehaviour
    {
        [Header("UI References")]
        public Image iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI costText;

        [Header("Marquee Settings")]
        public float scrollSpeed = 30f;
        public float pauseDuration = 1f; // 【新增】在两端停顿的时间

        private RectTransform textRect;
        private float originalX;
        private float maxScrollDistance;
        private Coroutine marqueeCoroutine;

        public void Setup(Data.ComponentData data, UnityEngine.Events.UnityAction onClickAction)
        {
            if (iconImage != null)
            {
                if (data.uiIcon != null)
                {
                    iconImage.sprite = data.uiIcon;
                    iconImage.color = Color.white;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            if (nameText != null)
            {
                nameText.text = data.componentName;
                nameText.ForceMeshUpdate();

                textRect = nameText.GetComponent<RectTransform>();
                originalX = textRect.anchoredPosition.x;

                float textWidth = nameText.preferredWidth;
                // 获取当前文本框的设定宽度作为“可视容器宽度”
                float containerWidth = textRect.rect.width;

                // 如果文字的真实宽度大于文本框的宽度，开启乒乓跑马灯
                if (textWidth > containerWidth)
                {
                    nameText.alignment = TextAlignmentOptions.Left;
                    nameText.textWrappingMode = TextWrappingModes.NoWrap;
                    nameText.overflowMode = TextOverflowModes.Overflow;

                    // 计算需要滚动的最大距离（额外加 5f 作为一个小小的边距缓冲）
                    maxScrollDistance = textWidth - containerWidth + 5f;

                    if (marqueeCoroutine != null) StopCoroutine(marqueeCoroutine);
                    marqueeCoroutine = StartCoroutine(PingPongMarqueeRoutine());
                }
                else
                {
                    nameText.alignment = TextAlignmentOptions.Center;
                }
            }

            if (costText != null)
            {
                costText.text = $"$ {data.buildCost}";
            }

            Button btn = GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(onClickAction);
            }
        }

        // 【新增】乒乓滚动的协程
        private IEnumerator PingPongMarqueeRoutine()
        {
            // 初始停顿 1 秒
            yield return new WaitForSeconds(pauseDuration);

            float targetX = originalX - maxScrollDistance;

            while (true) // 无限循环
            {
                // 1. 向左平滑滚动
                while (textRect.anchoredPosition.x > targetX)
                {
                    textRect.anchoredPosition += Vector2.left * scrollSpeed * Time.deltaTime;
                    if (textRect.anchoredPosition.x <= targetX)
                    {
                        textRect.anchoredPosition = new Vector2(targetX, textRect.anchoredPosition.y);
                    }
                    yield return null; // 等待下一帧
                }

                // 2. 到达左侧尽头，停顿 1 秒
                yield return new WaitForSeconds(pauseDuration);

                // 3. 向右平滑滚动回起点
                while (textRect.anchoredPosition.x < originalX)
                {
                    textRect.anchoredPosition += Vector2.right * scrollSpeed * Time.deltaTime;
                    if (textRect.anchoredPosition.x >= originalX)
                    {
                        textRect.anchoredPosition = new Vector2(originalX, textRect.anchoredPosition.y);
                    }
                    yield return null;
                }

                // 4. 回到起点，再次停顿 1 秒，然后进入下一轮循环
                yield return new WaitForSeconds(pauseDuration);
            }
        }

        private void OnDisable()
        {
            // 如果按钮被隐藏或销毁，停止协程防止报错
            if (marqueeCoroutine != null) StopCoroutine(marqueeCoroutine);
        }
    }
}