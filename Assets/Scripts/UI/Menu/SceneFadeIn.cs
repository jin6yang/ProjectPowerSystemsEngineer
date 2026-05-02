using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace ProjectPowerSystemsEngineer.UI
{
    /// <summary>
    /// 挂载在任何新场景的根节点(如 Main Camera)上，场景启动时会自动从黑屏淡入。
    /// </summary>
    public class SceneFadeIn : MonoBehaviour
    {
        [Tooltip("从黑屏淡入画面的持续时间(秒)")]
        public float fadeDuration = 0.6f;

        private void Start()
        {
            StartCoroutine(FadeInRoutine());
        }

        private IEnumerator FadeInRoutine()
        {
            // 动态生成一个覆盖全屏的黑色画布
            GameObject canvasObj = new GameObject("SceneFadeInCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // 必须大于 ONYX 的 100，保证也能遮住 ONYX 一起淡入

            GameObject imgObj = new GameObject("FadeImage");
            imgObj.transform.SetParent(canvasObj.transform, false);
            Image fadeImage = imgObj.AddComponent<Image>();
            fadeImage.color = Color.black; // 初始全黑

            RectTransform rect = fadeImage.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 执行渐亮插值动画
            float time = 0;
            while (time < fadeDuration)
            {
                time += Time.deltaTime;
                fadeImage.color = new Color(0, 0, 0, 1f - (time / fadeDuration));
                yield return null;
            }

            // 动画彻底结束后，销毁这个一次性黑幕画布，释放资源并恢复 UI 点击
            Destroy(canvasObj);
        }
    }
}