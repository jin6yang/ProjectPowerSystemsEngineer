using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // 引入UI命名空间
using System.Collections;

namespace ProjectPowerSystemsEngineer.Core
{
    public class BootLoader : MonoBehaviour
    {
        [Tooltip("淡出到黑屏的持续时间(秒)")]
        public float fadeDuration = 0.6f;

        private VideoPlayer videoPlayer;
        private AsyncOperation asyncLoad;

        private void Start()
        {
            // 【新增】隐藏系统级鼠标指针，完美保持全黑和视频播放时的沉浸感
            Cursor.visible = false;

            videoPlayer = GetComponent<VideoPlayer>();

            // 1. 游戏一启动，立刻在后台静默加载主菜单场景
            StartCoroutine(PreloadMainMenu());

            if (videoPlayer != null)
            {
                // 2. 监听视频播放结束事件
                videoPlayer.loopPointReached += OnVideoFinished;
            }
            else
            {
                StartCoroutine(FadeOutAndActivate());
            }
        }

        private IEnumerator PreloadMainMenu()
        {
            asyncLoad = SceneManager.LoadSceneAsync("MainMenuScene");

            if (asyncLoad != null)
            {
                // 阻止场景加载完毕后自动跳转
                asyncLoad.allowSceneActivation = false;
            }

            yield return null;
        }

        private void OnVideoFinished(VideoPlayer vp)
        {
            // 视频播完了，不直接切，而是先执行渐黑动画
            StartCoroutine(FadeOutAndActivate());
        }

        private IEnumerator FadeOutAndActivate()
        {
            // ==========================================
            // 核心魔法：用代码动态生成一个全屏黑幕，无需你在Editor里配置！
            // ==========================================
            GameObject canvasObj = new GameObject("TransitionFadeCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // 保证遮住所有东西

            GameObject imgObj = new GameObject("FadeImage");
            imgObj.transform.SetParent(canvasObj.transform, false);
            Image fadeImage = imgObj.AddComponent<Image>();
            fadeImage.color = new Color(0, 0, 0, 0); // 初始全透明

            // 强制填满全屏
            RectTransform rect = fadeImage.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 执行渐黑插值动画
            float time = 0;
            while (time < fadeDuration)
            {
                time += Time.deltaTime;
                fadeImage.color = new Color(0, 0, 0, time / fadeDuration);
                yield return null;
            }
            fadeImage.color = Color.black;

            // 画面完全黑透后，瞬间释放场景跳转！
            if (asyncLoad != null)
            {
                asyncLoad.allowSceneActivation = true;
            }
            else
            {
                SceneManager.LoadScene("MainMenuScene");
            }
        }
    }
}