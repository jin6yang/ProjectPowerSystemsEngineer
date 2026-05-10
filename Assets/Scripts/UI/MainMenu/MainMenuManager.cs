using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectPowerSystemsEngineer.UI
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Panels")]
        public CanvasGroup panelSelectUser;
        public CanvasGroup panelMain;
        public CanvasGroup panelLevelSelect;
        public CanvasGroup panelLoading;
        public CanvasGroup panelPowerMenu; // 新增：电源菜单面板

        [Header("Top Bar UI")]
        public TextMeshProUGUI txtSystemTime;

        [Header("Buttons - Main")]
        public Button btnLogin;
        public Button btnStartGame;

        [Header("Buttons - Power Menu")]
        public Button btnPowerIcon;
        public Button btnLogout;
        public Button btnShutdown;
        public Button btnClosePower;

        [Header("Level Select UI")]
        public Button btnBack;
        public TextMeshProUGUI txtLevelNameDisplay;
        public Button[] btnLevels;

        // 场景名称配置
        private string[] levelNames = { "反应堆初始化测试", "高压电网搭建", "沙盒模式" };
        private string[] sceneNames = { "Level_DevTest_01", "Level_02", "Level_03" };

        [Header("Loading UI")]
        public Image imgYellowBar;
        public TextMeshProUGUI txtLoadingPercent;

        private float initialBarWidth;
        private bool isPowerMenuOpen = false;

        private void Start()
        {
            // 初始状态强制设定
            SetCanvasGroupAlpha(panelSelectUser, 1f, true);
            SetCanvasGroupAlpha(panelMain, 0f, false);
            SetCanvasGroupAlpha(panelLevelSelect, 0f, false);
            SetCanvasGroupAlpha(panelLoading, 0f, false);
            SetCanvasGroupAlpha(panelPowerMenu, 0f, false);

            if (btnLogin != null) btnLogin.onClick.AddListener(OnLoginClicked);
            if (btnStartGame != null) btnStartGame.onClick.AddListener(OnStartGameClicked);
            if (btnBack != null) btnBack.onClick.AddListener(OnBackClicked);

            // 绑定电源菜单按钮
            if (btnPowerIcon != null) btnPowerIcon.onClick.AddListener(OpenPowerMenu);
            if (btnClosePower != null) btnClosePower.onClick.AddListener(ClosePowerMenu);
            if (btnLogout != null) btnLogout.onClick.AddListener(OnLogoutClicked);
            if (btnShutdown != null) btnShutdown.onClick.AddListener(OnShutdownClicked);

            if (OnyxController.Instance != null) OnyxController.Instance.SetState(OnyxState.Sleepy);

            // 自动绑定关卡选择按钮的悬停事件
            for (int i = 0; i < btnLevels.Length; i++)
            {
                int index = i;
                btnLevels[i].onClick.AddListener(() => OnLevelButtonClicked(index));

                // 获取或添加 EventTrigger 组件
                EventTrigger trigger = btnLevels[i].gameObject.GetComponent<EventTrigger>();
                if (trigger == null) trigger = btnLevels[i].gameObject.AddComponent<EventTrigger>();

                // 1. 悬停进入 (PointerEnter)：显示对应的关卡名
                EventTrigger.Entry entryEnter = new EventTrigger.Entry();
                entryEnter.eventID = EventTriggerType.PointerEnter;
                entryEnter.callback.AddListener((data) => { txtLevelNameDisplay.text = levelNames[index]; });
                trigger.triggers.Add(entryEnter);

                // 2. 悬停离开 (PointerExit)：清空关卡名文字
                EventTrigger.Entry entryExit = new EventTrigger.Entry();
                entryExit.eventID = EventTriggerType.PointerExit;
                entryExit.callback.AddListener((data) => { txtLevelNameDisplay.text = ""; });
                trigger.triggers.Add(entryExit);
            }

            // 【核心修改】初始状态下，默认显示文字为空
            txtLevelNameDisplay.text = "";

            // 锁定在Editor中设定的加载条宽度
            if (imgYellowBar != null)
            {
                initialBarWidth = imgYellowBar.rectTransform.rect.width;
            }
        }

        private void Update()
        {
            if (txtSystemTime != null) txtSystemTime.text = System.DateTime.Now.ToString("HH:mm");

            // 监听 ESC 键 或 手柄 B 键
            bool isCancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                                   (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

            if (isCancelPressed)
            {
                if (isPowerMenuOpen)
                {
                    ClosePowerMenu();
                }
                else if (panelLevelSelect.alpha > 0.5f)
                {
                    OnBackClicked(); // 在关卡选择界面按ESC可返回主菜单
                }
            }

            // 开发者快捷通道：按 ~ 键进入测试关卡
            if (panelLevelSelect.alpha > 0.5f && Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                StartCoroutine(TransitionToLoadingAndLoad("Level_DevTest_01"));
            }
        }

        // ==================== 电源菜单控制逻辑 ====================
        private void OpenPowerMenu()
        {
            if (isPowerMenuOpen) return;
            isPowerMenuOpen = true;
            StartCoroutine(CrossFadePanels(null, panelPowerMenu));
        }

        private void ClosePowerMenu()
        {
            if (!isPowerMenuOpen) return;
            isPowerMenuOpen = false;
            StartCoroutine(CrossFadePanels(panelPowerMenu, null));
        }

        private void OnLogoutClicked()
        {
            ClosePowerMenu();
            StartCoroutine(CrossFadePanels(panelMain, panelSelectUser, () => {
                if (OnyxController.Instance != null) OnyxController.Instance.SetState(OnyxState.Sleepy);
            }));
        }

        private void OnShutdownClicked()
        {
            Debug.Log("[System] 执行系统关机...");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ==================== 主菜单导航逻辑 ====================
        private void OnLoginClicked()
        {
            StartCoroutine(CrossFadePanels(panelSelectUser, panelMain, () => {
                if (OnyxController.Instance != null) OnyxController.Instance.SetState(OnyxState.Watching);
            }));
        }

        private void OnStartGameClicked()
        {
            StartCoroutine(CrossFadePanels(panelMain, panelLevelSelect));
        }

        private void OnBackClicked()
        {
            StartCoroutine(CrossFadePanels(panelLevelSelect, panelMain));
        }

        private void OnLevelButtonClicked(int index)
        {
            if (index < sceneNames.Length)
            {
                StartCoroutine(TransitionToLoadingAndLoad(sceneNames[index]));
            }
        }

        // 核心UI交叉淡入淡出 (支持传 null 以实现单边淡入/淡出)
        private IEnumerator CrossFadePanels(CanvasGroup fadeOut, CanvasGroup fadeIn, System.Action onComplete = null)
        {
            if (fadeOut != null) fadeOut.interactable = false;
            if (fadeIn != null) fadeIn.interactable = false;

            float duration = 0.3f; float time = 0;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = time / duration;
                if (fadeOut != null) SetCanvasGroupAlpha(fadeOut, 1f - t, false);
                if (fadeIn != null) SetCanvasGroupAlpha(fadeIn, t, false);
                yield return null;
            }

            if (fadeOut != null) SetCanvasGroupAlpha(fadeOut, 0f, false);
            if (fadeIn != null) SetCanvasGroupAlpha(fadeIn, 1f, true);
            onComplete?.Invoke();
        }

        // ==================== 终极解耦加载系统 ====================
        private IEnumerator TransitionToLoadingAndLoad(string targetScene)
        {
            yield return StartCoroutine(CrossFadePanels(panelLevelSelect, panelLoading));
            panelLoading.interactable = true; panelLoading.blocksRaycasts = true;

            RectTransform barRect = imgYellowBar.GetComponent<RectTransform>();

            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.offsetMin = new Vector2(0f, 0f);
            barRect.offsetMax = new Vector2(initialBarWidth, 0f);

            txtLoadingPercent.text = "000%";

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene);
            asyncLoad.allowSceneActivation = false;

            float displayProgress = 0f;
            int phase = 0;

            // 伪加载曲线节奏控制
            while (displayProgress < 1f)
            {
                if (phase == 0)
                {
                    displayProgress = Mathf.MoveTowards(displayProgress, 0.24f, Time.deltaTime * 1.5f);
                    if (displayProgress >= 0.24f) phase = 1;
                }
                else if (phase == 1)
                {
                    yield return new WaitForSeconds(0.15f);
                    phase = 2;
                }
                else if (phase == 2)
                {
                    displayProgress = Mathf.MoveTowards(displayProgress, 0.89f, Time.deltaTime * 3.5f);
                    if (displayProgress >= 0.89f) phase = 3;
                }
                else if (phase == 3)
                {
                    if (asyncLoad.progress >= 0.9f) phase = 4;
                }
                else if (phase == 4)
                {
                    displayProgress = Mathf.MoveTowards(displayProgress, 1f, Time.deltaTime * 1.5f);
                }

                barRect.anchorMin = new Vector2(0f, 1f - displayProgress);
                barRect.offsetMin = new Vector2(0f, 0f);
                barRect.offsetMax = new Vector2(initialBarWidth, 0f);

                txtLoadingPercent.text = Mathf.RoundToInt(displayProgress * 100).ToString("D3") + "%";
                yield return null;
            }

            yield return new WaitForSeconds(0.15f);

            float expandDuration = 0.35f;
            float t = 0;
            while (t < expandDuration)
            {
                t += Time.deltaTime;
                float easeT = 1f - Mathf.Pow(1f - (t / expandDuration), 3);

                barRect.anchorMax = new Vector2(easeT, 1f);
                barRect.anchorMin = new Vector2(0f, 0f);

                float currentWidth = Mathf.Lerp(initialBarWidth, 0f, easeT);
                barRect.offsetMin = Vector2.zero;
                barRect.offsetMax = new Vector2(currentWidth, 0f);

                yield return null;
            }

            barRect.anchorMax = Vector2.one;
            barRect.offsetMax = Vector2.zero;

            // 解耦执行：交接棒给下一个场景里的 SceneFadeIn.cs
            asyncLoad.allowSceneActivation = true;
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