using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

namespace ProjectPowerSystemsEngineer.UI
{
    public class GameResultManager : MonoBehaviour
    {
        public static GameResultManager Instance { get; private set; }

        [Header("UI Panels")]
        public CanvasGroup panelSuccess;
        public CanvasGroup panelFailed;

        [Header("Buttons - Success")]
        public Button btnContinuePlay;
        public Button btnSuccessDesktop;

        [Header("Buttons - Failed")]
        public Button btnRetry;
        public Button btnFailedDesktop;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // 初始隐藏双面板
            SetCanvasGroupAlpha(panelSuccess, 0f, false);
            SetCanvasGroupAlpha(panelFailed, 0f, false);

            // 绑定按钮事件
            if (btnContinuePlay != null) btnContinuePlay.onClick.AddListener(OnContinuePlay);
            if (btnSuccessDesktop != null) btnSuccessDesktop.onClick.AddListener(OnReturnToDesktop);
            if (btnRetry != null) btnRetry.onClick.AddListener(OnRetryLevel);
            if (btnFailedDesktop != null) btnFailedDesktop.onClick.AddListener(OnReturnToDesktop);
        }

        public void ShowResult(bool isSuccess)
        {
            CanvasGroup targetPanel = isSuccess ? panelSuccess : panelFailed;
            StartCoroutine(FadePanel(targetPanel, 1f, true));

            // 如果你想在出结果时暂停时间，可以解除下面这行的注释
            // Time.timeScale = 0f; 
        }

        private void OnContinuePlay()
        {
            // 继续游玩：淡出成功面板，允许玩家继续在场景里欣赏
            StartCoroutine(FadePanel(panelSuccess, 0f, false));
        }

        private void OnRetryLevel()
        {
            Time.timeScale = 1f; // 确保时间恢复
            // 重新加载当前场景
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnReturnToDesktop()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenuScene");
        }

        private IEnumerator FadePanel(CanvasGroup cg, float targetAlpha, bool interactable)
        {
            cg.interactable = interactable;
            cg.blocksRaycasts = interactable;

            float startAlpha = cg.alpha;
            float duration = 0.5f;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime; // 即使暂停也能播放动画
                cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
                yield return null;
            }
            cg.alpha = targetAlpha;
        }

        private void SetCanvasGroupAlpha(CanvasGroup cg, float alpha, bool interactable)
        {
            cg.alpha = alpha;
            cg.interactable = interactable;
            cg.blocksRaycasts = interactable;
        }
    }
}