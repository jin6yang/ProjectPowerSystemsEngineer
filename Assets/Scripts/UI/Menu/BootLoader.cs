using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

namespace ProjectPowerSystemsEngineer.Core
{
    public class BootLoader : MonoBehaviour
    {
        private VideoPlayer videoPlayer;

        private void Start()
        {
            videoPlayer = GetComponent<VideoPlayer>();
            if (videoPlayer != null)
            {
                // 监听视频播放结束事件
                videoPlayer.loopPointReached += OnVideoFinished;
            }
            else
            {
                // 如果没有视频，直接跳转
                LoadMainMenu();
            }
        }

        private void OnVideoFinished(VideoPlayer vp)
        {
            LoadMainMenu();
        }

        private void LoadMainMenu()
        {
            // 必须在 Build Settings 中将 MainMenuScene 添加进去才能跳转
            SceneManager.LoadScene("MainMenuScene");
        }
    }
}