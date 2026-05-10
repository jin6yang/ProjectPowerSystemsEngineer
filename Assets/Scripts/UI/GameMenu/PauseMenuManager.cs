using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;
using ProjectPowerSystemsEngineer.Systems;

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
            SetCanvasGroupAlpha(panelPause, 0f, false);

            btnResume.onClick.AddListener(ResumeGame);
            btnDesktop.onClick.AddListener(ReturnToDesktop);
            btnPowerOff.onClick.AddListener(PowerOffGame);
        }

        private void Update()
        {
            bool isCancelPressed = false;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                isCancelPressed = true;

            // 【核心修改】：将 buttonEast(B键) 修改为 startButton(汉堡菜单键/Options键)
            if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
                isCancelPressed = true;

            if (isCancelPressed)
            {
                HandleCancelInput();
            }
        }

        private void HandleCancelInput()
        {
            // 【新增防御机制】：如果游戏正在播放进入关卡的淡入动画，直接拦截，剥夺暂停权限！
            if (SceneFadeIn.IsFading)
            {
                Debug.LogWarning("[PauseMenu] 场景过渡中，已强行拦截玩家的暂停请求！");
                return;
            }

            if (isPaused)
            {
                ResumeGame();
                return;
            }

            bool isBuilderBusy = false;
            if (builderController != null)
            {
                isBuilderBusy = builderController.IsBuildingMode ||
                                builderController.SelectedNode != null ||
                                builderController.SelectedGridPosition.HasValue;
            }

            if (isBuilderBusy)
            {
                Debug.Log("[PauseMenu] 拦截指令：当前正在建造或选中物体，将取消权限移交给 BuilderController。");
                return;
            }

            PauseGame();
        }

        private void PauseGame()
        {
            isPaused = true;
            Time.timeScale = 0f;
            StartCoroutine(FadeMenu(panelPause, 1f, true));

            if (OnyxController.Instance != null) OnyxController.Instance.SetState(OnyxState.Watching);
        }

        private void ResumeGame()
        {
            isPaused = false;
            Time.timeScale = 1f;
            StartCoroutine(FadeMenu(panelPause, 0f, false));

            if (OnyxController.Instance != null) OnyxController.Instance.SetState(OnyxState.Idle);
        }

        private void ReturnToDesktop()
        {
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

        private IEnumerator FadeMenu(CanvasGroup cg, float targetAlpha, bool interactable)
        {
            cg.interactable = interactable;
            cg.blocksRaycasts = interactable;

            float startAlpha = cg.alpha;
            float duration = 0.15f;
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