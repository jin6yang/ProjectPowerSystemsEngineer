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

        [Header("Top Bar UI")]
        public TextMeshProUGUI txtSystemTime;

        [Header("Buttons - Main")]
        public Button btnLogin;
        public Button btnStartGame;

        [Header("Level Select UI")]
        public Button btnBack;
        public TextMeshProUGUI txtLevelNameDisplay;
        public Button[] btnLevels;

        private string[] levelNames = { "反应堆初始化测试", "高压电网搭建", "沙盒模式" };
        private string[] sceneNames = { "Level_DevTest_01", "Level_02", "Level_03" };

        [Header("Loading UI")]
        public Image imgYellowBar;
        public TextMeshProUGUI txtLoadingPercent;

        // 【新增】自动记录你在Editor里设置的进度条初始宽度
        private float initialBarWidth;

        private void Start()
        {
            SetCanvasGroupAlpha(panelSelectUser, 1f, true);
            SetCanvasGroupAlpha(panelMain, 0f, false);
            SetCanvasGroupAlpha(panelLevelSelect, 0f, false);
            SetCanvasGroupAlpha(panelLoading, 0f, false);

            if (btnLogin != null) btnLogin.onClick.AddListener(OnLoginClicked);
            if (btnStartGame != null) btnStartGame.onClick.AddListener(OnStartGameClicked);
            if (btnBack != null) btnBack.onClick.AddListener(OnBackClicked);

            if (OnyxController.Instance != null) OnyxController.Instance.SetState(OnyxState.Sleepy);

            for (int i = 0; i < btnLevels.Length; i++)
            {
                int index = i;
                btnLevels[i].onClick.AddListener(() => OnLevelButtonClicked(index));

                EventTrigger trigger = btnLevels[i].gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerEnter;
                entry.callback.AddListener((data) => { txtLevelNameDisplay.text = levelNames[index]; });
                trigger.triggers.Add(entry);
            }

            txtLevelNameDisplay.text = "请选择模拟项目";

            // 【新增】在Start时读取并锁定你配置的UI宽度
            if (imgYellowBar != null)
            {
                initialBarWidth = imgYellowBar.rectTransform.rect.width;
            }
        }

        private void Update()
        {
            if (txtSystemTime != null) txtSystemTime.text = System.DateTime.Now.ToString("HH:mm");

            if (panelLevelSelect.alpha > 0.5f && Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                Debug.Log("[System] 检测到开发者覆写指令，强制进入 DevTest_01...");
                StartCoroutine(TransitionToLoadingAndLoad("Level_DevTest_01"));
            }
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

        private void OnLevelButtonClicked(int index)
        {
            if (index < sceneNames.Length)
            {
                StartCoroutine(TransitionToLoadingAndLoad(sceneNames[index]));
            }
        }

        private IEnumerator CrossFadePanels(CanvasGroup fadeOut, CanvasGroup fadeIn, System.Action onComplete = null)
        {
            fadeOut.interactable = false; fadeIn.interactable = false;
            float duration = 0.3f; float time = 0;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;
                SetCanvasGroupAlpha(fadeOut, 1f - t, false);
                SetCanvasGroupAlpha(fadeIn, t, false);
                yield return null;
            }
            SetCanvasGroupAlpha(fadeOut, 0f, false);
            SetCanvasGroupAlpha(fadeIn, 1f, true);
            onComplete?.Invoke();
        }

        private IEnumerator TransitionToLoadingAndLoad(string targetScene)
        {
            yield return StartCoroutine(CrossFadePanels(panelLevelSelect, panelLoading));
            panelLoading.interactable = true; panelLoading.blocksRaycasts = true;

            RectTransform barRect = imgYellowBar.GetComponent<RectTransform>();

            // 使用你在 Editor 中设定的动态宽度 initialBarWidth，彻底解绑硬编码的 50f
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

                // 再次使用 initialBarWidth
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

                // 将宽度由 initialBarWidth 平滑归零
                float currentWidth = Mathf.Lerp(initialBarWidth, 0f, easeT);
                barRect.offsetMin = Vector2.zero;
                barRect.offsetMax = new Vector2(currentWidth, 0f);

                yield return null;
            }

            barRect.anchorMax = Vector2.one;
            barRect.offsetMax = Vector2.zero;

            // ==========================================
            // 解耦核心：主菜单的任务到此为止，交接棒交给下一个场景的 SceneFadeIn.cs！
            // 删除了原本那些跨越边界手捏 Canvas 的臃肿代码。
            // ==========================================

            // 瞬间放行场景跳转
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