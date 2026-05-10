using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using ProjectPowerSystemsEngineer.Data; // 引入数据命名空间

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

        // ==========================================
        // 【核心解耦】数据与UI槽位彻底分离
        // ==========================================
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

            // ==========================================
            // 动态组装：将卡带数据灌入 UI 按钮
            // ==========================================
            if (currentChapter != null && levelButtons != null)
            {
                HashSet<Button> boundButtons = new HashSet<Button>();
                int processCount = Mathf.Min(currentChapter.levels.Length, levelButtons.Length);

                for (int i = 0; i < processCount; i++)
                {
                    Button btn = levelButtons[i];
                    LevelInfo levelInfo = currentChapter.levels[i];

                    if (btn == null) continue;

                    // 防御：如果你不小心在槽位里拖了两次同一个按钮，黄字警告
                    if (boundButtons.Contains(btn))
                    {
                        Debug.LogWarning($"<color=yellow>[UI 警告] UI 按钮槽位中的第 {i} 项拖入了重复的按钮 ({btn.name})！请检查 Inspector！</color>");
                    }
                    boundButtons.Add(btn);

                    btn.onClick.RemoveAllListeners();

                    // 【解耦优势】不再传 index，直接把具体的路径传给加载函数
                    string targetScenePath = levelInfo.scenePath;
                    btn.onClick.AddListener(() => OnLevelButtonClicked(targetScenePath));

                    EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>();
                    if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();
                    else trigger.triggers.Clear();

                    EventTrigger.Entry entryEnter = new EventTrigger.Entry();
                    entryEnter.eventID = EventTriggerType.PointerEnter;
                    entryEnter.callback.AddListener((data) => {
                        if (txtLevelNameDisplay != null)
                        {
                            txtLevelNameDisplay.text = levelInfo.levelName;
                        }
                    });
                    trigger.triggers.Add(entryEnter);

                    EventTrigger.Entry entryExit = new EventTrigger.Entry();
                    entryExit.eventID = EventTriggerType.PointerExit;
                    entryExit.callback.AddListener((data) => {
                        if (txtLevelNameDisplay != null) txtLevelNameDisplay.text = "";
                    });
                    trigger.triggers.Add(entryExit);
                }
            }
            else
            {
                Debug.LogWarning("[UI系统] 未配置 Chapter Data 数据源或 UI 按钮槽位为空！");
            }

            if (txtLevelNameDisplay != null)
            {
                txtLevelNameDisplay.text = "";
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