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
        [Tooltip("从全屏颜色淡出画面的持续时间(秒)")]
        public float fadeDuration = 0.6f;

        [Tooltip("转场过渡的底色(与Loading界面的最终颜色保持一致)")]
        public Color transitionColor = new Color(1f, 0.84f, 0f, 1f); // 默认设为我们之前用的工业黄

        private void Start()
        {
            StartCoroutine(FadeInRoutine());
        }

        private IEnumerator FadeInRoutine()
        {
            // 动态生成一个覆盖全屏的画布
            GameObject canvasObj = new GameObject("SceneFadeInCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // 保证遮住所有东西

            GameObject imgObj = new GameObject("FadeImage");
            imgObj.transform.SetParent(canvasObj.transform, false);
            Image fadeImage = imgObj.AddComponent<Image>();

            // 初始颜色设为你定义的颜色 (工业黄)
            fadeImage.color = transitionColor;

            RectTransform rect = fadeImage.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;

            // 执行渐变插值动画
            float time = 0;
            while (time < fadeDuration)
            {
                time += Time.deltaTime;
                // 仅平滑降低 Alpha 透明度，保留 RGB 底色，渐渐透出背后的游戏场景
                fadeImage.color = new Color(transitionColor.r, transitionColor.g, transitionColor.b, 1f - (time / fadeDuration));
                yield return null;
            }

            // 动画彻底结束后，销毁这个一次性画布，释放资源并恢复 UI 点击
            Destroy(canvasObj);
        }
    }
}