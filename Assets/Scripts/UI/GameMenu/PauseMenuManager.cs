using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;
using ProjectPowerSystemsEngineer.Systems; // 引入建造系统

namespace ProjectPowerSystemsEngineer.UI
{
    public class PauseMenuManager : MonoBehaviour
    {
        [Header("UI References")]
        public CanvasGroup panelPause;
        public Button btnResume;
        public Button btnDesktop;
        public Button btnPowerOff;

        [Header("System References")]
        [Tooltip("拖入场景中的 BuilderController，用于优先级仲裁")]
        public BuilderController builderController;

        private bool isPaused = false;

        private void Start()
        {
            // 初始隐藏暂停菜单
            SetCanvasGroupAlpha(panelPause, 0f, false);

            btnResume.onClick.AddListener(ResumeGame);
            btnDesktop.onClick.AddListener(ReturnToDesktop);
            btnPowerOff.onClick.AddListener(PowerOffGame);
        }

        private void Update()
        {
            // 监听 ESC 或 手柄 B 键
            bool isCancelPressed = false;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                isCancelPressed = true;

            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
                isCancelPressed = true;

            if (isCancelPressed)
            {
                HandleCancelInput();
            }
        }

        private void HandleCancelInput()
        {
            // 1. 如果游戏已经暂停，无条件恢复游戏
            if (isPaused)
            {
                ResumeGame();
                return;
            }

            // 2. 【核心优先级判断】：检查建造控制器是否正在“忙碌”
            bool isBuilderBusy = false;
            if (builderController != null)
            {
                // 只要处于建造模式，或者选中了某个设施/空地，都视为“忙碌”
                isBuilderBusy = builderController.IsBuildingMode ||
                                builderController.SelectedNode != null ||
                                builderController.SelectedGridPosition.HasValue;
            }

            if (isBuilderBusy)
            {
                // 让步给 BuilderController 去处理取消/退出建造逻辑
                Debug.Log("[PauseMenu] 拦截指令：当前正在建造或选中物体，将取消权限移交给 BuilderController。");
                return;
            }

            // 3. 优先级兜底：呼出暂停菜单
            PauseGame();
        }

        private void PauseGame()
        {
            isPaused = true;
            Time.timeScale = 0f; // 冻结物理与时间
            StartCoroutine(FadeMenu(panelPause, 1f, true));

            if (OnyxController.Instance != null) OnyxController.Instance.SetState(OnyxState.Watching);
        }

        private void ResumeGame()
        {
            isPaused = false;
            Time.timeScale = 1f; // 恢复时间流速
            StartCoroutine(FadeMenu(panelPause, 0f, false));

            if (OnyxController.Instance != null) OnyxController.Instance.SetState(OnyxState.Idle);
        }

        private void ReturnToDesktop()
        {
            // 极其重要：跨场景前必须恢复时间，否则新场景的加载和动画会彻底卡死
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenuScene");
        }

        private void PowerOffGame()
        {
            Debug.Log("[System] 执行系统关机...");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // 独立的淡入淡出协程（使用 unscaledDeltaTime 确保在暂停时也能正常播放UI动画）
        private IEnumerator FadeMenu(CanvasGroup cg, float targetAlpha, bool interactable)
        {
            cg.interactable = interactable;
            cg.blocksRaycasts = interactable;

            float startAlpha = cg.alpha;
            float duration = 0.15f; // 暂停菜单呼出应当非常迅速
            float time = 0;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
                yield return null;
            }
            cg.alpha = targetAlpha;
        }

        private void SetCanvasGroupAlpha(CanvasGroup cg, float alpha, bool interactable)
        {
            if (cg == null) return;
            cg.alpha = alpha;
            cg.interactable = interactable;
            cg.blocksRaycasts = interactable;
        }
    }
}