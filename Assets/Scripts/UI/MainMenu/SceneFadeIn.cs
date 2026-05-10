using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace ProjectPowerSystemsEngineer.UI
{
    /// <summary>
    /// 挂载在任何新场景的根节点(如 Main Camera)上，场景启动时会自动从指定颜色淡出画面。
    /// </summary>
    public class SceneFadeIn : MonoBehaviour
    {
        // 【新增全局状态】：向全系统广播“当前是否正在进行过渡动画”
        public static bool IsFading { get; private set; } = false;

        [Tooltip("从全屏颜色淡出画面的持续时间(秒)")]
        public float fadeDuration = 0.6f;

        [Tooltip("转场过渡的底色(与Loading界面的最终颜色保持一致)")]
        public Color transitionColor = new Color(1f, 0.84f, 0f, 1f);

        private void Start()
        {
            StartCoroutine(FadeInRoutine());
        }

        private IEnumerator FadeInRoutine()
        {
            IsFading = true; // 上锁：宣告全系统现在正处于转场状态

            GameObject canvasObj = new GameObject("SceneFadeInCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            GameObject imgObj = new GameObject("FadeImage");
            imgObj.transform.SetParent(canvasObj.transform, false);
            Image fadeImage = imgObj.AddComponent<Image>();

            fadeImage.color = transitionColor;

            RectTransform rect = fadeImage.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;

            float time = 0;
            while (time < fadeDuration)
            {
                // 【核心修复】：使用 unscaledDeltaTime 真实时间。这样即使 TimeScale 被意外设为 0，动画也能强制播完防卡死！
                time += Time.unscaledDeltaTime;

                fadeImage.color = new Color(transitionColor.r, transitionColor.g, transitionColor.b, 1f - (time / fadeDuration));
                yield return null;
            }

            Destroy(canvasObj);
            IsFading = false; // 解锁：转场结束
        }

        private void OnDestroy()
        {
            // 防御性编程：如果该物体在加载途中被意外摧毁，强制解锁，防止整个游戏永久无法暂停
            IsFading = false;
        }
    }
}