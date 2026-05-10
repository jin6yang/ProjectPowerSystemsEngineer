using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using ProjectPowerSystemsEngineer.Data;

namespace ProjectPowerSystemsEngineer.UI
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Panels")]
        public CanvasGroup panelSelectUser;
        public CanvasGroup panelMain;
        public CanvasGroup panelLevelSelect;
        public CanvasGroup panelLoading;
        public CanvasGroup panelPowerMenu;

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

        [Header("数据源 (Data Source)")]
        [Tooltip("拖入创建好的章节数据资产 (ScriptableObject)")]
        public ChapterData currentChapter;

        [Header("UI 按钮槽位 (UI Slots)")]
        [Tooltip("按顺序拖入界面上的关卡按钮，将自动与数据源中的关卡一一对应")]
        public Button[] levelButtons;

        [Header("Loading UI")]
        public Image imgYellowBar;
        public TextMeshProUGUI txtLoadingPercent;

        private float initialBarWidth;
        private bool isPowerMenuOpen = false;

        private static MainMenuManager _instance;

        // 【新增】用于管理文字淡入淡出的协程，防止玩家快速滑动鼠标导致动画冲突
        private Coroutine nameFadeCoroutine;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogError($"<color=red>[严重警告] 场景中存在多个 MainMenuManager！这是导致文字被异常覆盖的元凶！已自动摧毁多余组件：{gameObject.name}</color>");
                Destroy(this);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            SetCanvasGroupAlpha(panelSelectUser, 1f, true);
            SetCanvasGroupAlpha(panelMain, 0f, false);
            SetCanvasGroupAlpha(panelLevelSelect, 0f, false);
            SetCanvasGroupAlpha(panelLoading, 0f, false);
            SetCanvasGroupAlpha(panelPowerMenu, 0f, false);

            if (btnLogin != null) btnLogin.onClick.AddListener(OnLoginClicked);
            if (btnStartGame != null) btnStartGame.onClick.AddListener(OnStartGameClicked);
            if (btnBack != null) btnBack.onClick.AddListener(OnBackClicked);

            if (btnPowerIcon != null) btnPowerIcon.onClick.AddListener(OpenPowerMenu);
            if (btnClosePower != null) btnClosePower.onClick.AddListener(ClosePowerMenu);
            if (btnLogout != null) btnLogout.onClick.AddListener(OnLogoutClicked);
            if (btnShutdown != null) btnShutdown.onClick.AddListener(OnShutdownClicked);

            if (OnyxController.Instance != null) OnyxController.Instance.SetState(OnyxState.Sleepy);

            if (currentChapter != null && levelButtons != null)
            {
                HashSet<Button> boundButtons = new HashSet<Button>();
                int processCount = Mathf.Min(currentChapter.levels.Length, levelButtons.Length);

                for (int i = 0; i < processCount; i++)
                {
                    Button btn = levelButtons[i];
                    LevelInfo levelInfo = currentChapter.levels[i];

                    if (btn == null) continue;

                    if (boundButtons.Contains(btn))
                    {
                        Debug.LogWarning($"<color=yellow>[UI 警告] UI 按钮槽位中的第 {i} 项拖入了重复的按钮 ({btn.name})！请检查 Inspector！</color>");
                    }
                    boundButtons.Add(btn);

                    btn.onClick.RemoveAllListeners();

                    string targetScenePath = levelInfo.scenePath;
                    btn.onClick.AddListener(() => OnLevelButtonClicked(targetScenePath));

                    EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>();
                    if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();
                    else trigger.triggers.Clear();

                    // 【优化】悬停进入：调用专属的淡入函数
                    EventTrigger.Entry entryEnter = new EventTrigger.Entry();
                    entryEnter.eventID = EventTriggerType.PointerEnter;
                    entryEnter.callback.AddListener((data) => {
                        ShowLevelName(levelInfo.levelName);
                    });
                    trigger.triggers.Add(entryEnter);

                    // 【优化】悬停离开：调用专属的淡出函数
                    EventTrigger.Entry entryExit = new EventTrigger.Entry();
                    entryExit.eventID = EventTriggerType.PointerExit;
                    entryExit.callback.AddListener((data) => {
                        HideLevelName();
                    });
                    trigger.triggers.Add(entryExit);
                }
            }
            else
            {
                Debug.LogWarning("[UI系统] 未配置 Chapter Data 数据源或 UI 按钮槽位为空！");
            }

            // 【优化】初始状态不仅清空文字，还将 TMP 的透明度设为 0
            if (txtLevelNameDisplay != null)
            {
                txtLevelNameDisplay.text = "";
                txtLevelNameDisplay.alpha = 0f;
            }

            if (imgYellowBar != null)
            {
                initialBarWidth = imgYellowBar.rectTransform.rect.width;
            }
        }

        private void Update()
        {
            if (txtSystemTime != null) txtSystemTime.text = System.DateTime.Now.ToString("HH:mm");

            bool isCancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                                   (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

            if (isCancelPressed)
            {
                if (isPowerMenuOpen) ClosePowerMenu();
                else if (panelLevelSelect.alpha > 0.5f) OnBackClicked();
            }

            if (panelLevelSelect.alpha > 0.5f && Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                StartCoroutine(TransitionToLoadingAndLoad("Level_DevTest_01"));
            }
        }

        // ==========================================
        // 【核心新增】优雅的关卡名字淡入淡出动画控制
        // ==========================================
        private void ShowLevelName(string name)
        {
            if (txtLevelNameDisplay == null) return;

            // 打断之前可能正在进行的淡出动画
            if (nameFadeCoroutine != null) StopCoroutine(nameFadeCoroutine);

            // 瞬间切换文字内容，然后平滑提高透明度 (0.15秒极速淡入)
            txtLevelNameDisplay.text = name;
            nameFadeCoroutine = StartCoroutine(FadeTextAlphaRoutine(1f, 0.15f));
        }

        private void HideLevelName()
        {
            if (txtLevelNameDisplay == null) return;

            // 打断淡入，平滑降低透明度 (0.1秒极速淡出，比淡入更快显得干净利落)
            if (nameFadeCoroutine != null) StopCoroutine(nameFadeCoroutine);
            nameFadeCoroutine = StartCoroutine(FadeTextAlphaRoutine(0f, 0.1f));
        }

        private IEnumerator FadeTextAlphaRoutine(float targetAlpha, float duration)
        {
            // 获取当前文本的真实透明度，而不是写死从0开始，这保证了连续滑动的丝滑感
            float startAlpha = txtLevelNameDisplay.alpha;
            float time = 0;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime; // 使用不受时间暂停影响的 DeltaTime
                txtLevelNameDisplay.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
                yield return null;
            }

            txtLevelNameDisplay.alpha = targetAlpha;

            // 只有当文字彻底透明后，才清空文本，防止在淡出途中发生字体的闪烁/偏移
            if (targetAlpha <= 0f)
            {
                txtLevelNameDisplay.text = "";
            }
        }

        // ==========================================
        // 以下为原有控制与加载逻辑
        // ==========================================
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

        private void OnLevelButtonClicked(string targetScenePath)
        {
            if (!string.IsNullOrEmpty(targetScenePath))
            {
                StartCoroutine(TransitionToLoadingAndLoad(targetScenePath));
            }
            else
            {
                Debug.LogWarning("[UI系统] 该关卡未配置 Scene File，无法加载！");
            }
        }

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