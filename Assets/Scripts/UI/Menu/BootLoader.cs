using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using System.Collections; // 引入协程

namespace ProjectPowerSystemsEngineer.Core
{
    public class BootLoader : MonoBehaviour
    {
        private VideoPlayer videoPlayer;
        private AsyncOperation asyncLoad;

        private void Start()
        {
            videoPlayer = GetComponent<VideoPlayer>();

            // 1. 游戏一启动，立刻在后台静默加载主菜单场景！
            StartCoroutine(PreloadMainMenu());

            if (videoPlayer != null)
            {
                // 2. 监听视频播放结束事件
                videoPlayer.loopPointReached += OnVideoFinished;
            }
            else
            {
                ActivateMainMenu();
            }
        }

        private IEnumerator PreloadMainMenu()
        {
            // 开始异步加载目标场景
            asyncLoad = SceneManager.LoadSceneAsync("MainMenuScene");

            if (asyncLoad != null)
            {
                // 【核心魔法】阻止场景加载完毕后自动跳转！
                // 这样它会静静地停留在 90% 的进度，等待我们发号施令
                asyncLoad.allowSceneActivation = false;
            }

            yield return null;
        }

        private void OnVideoFinished(VideoPlayer vp)
        {
            // 视频播完了，是时候展现真正的技术了
            ActivateMainMenu();
        }

        private void ActivateMainMenu()
        {
            if (asyncLoad != null)
            {
                // 允许跳转！因为场景已经在后台加载好了，所以这一瞬间会丝滑切入，没有任何卡顿！
                asyncLoad.allowSceneActivation = true;
            }
            else
            {
                // 兜底防御
                SceneManager.LoadScene("MainMenuScene");
            }
        }
    }
}