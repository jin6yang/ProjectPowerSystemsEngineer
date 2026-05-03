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

            // 【终极锚点重置魔法】无论 Editor 里 Pivot 怎么歪，这里强行将框体锁定为：宽50，紧贴左上角，高为0
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.offsetMin = new Vector2(0f, 0f);
            barRect.offsetMax = new Vector2(50f, 0f);

            txtLoadingPercent.text = "000%";

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene);
            asyncLoad.allowSceneActivation = false;

            // ==========================================
            // 阶段一：定制的伪加载节奏曲线
            // 000 -> 024 (快) -> 停顿 -> 089 (极快) -> 等待真实加载 -> 100 (结算)
            // ==========================================
            float displayProgress = 0f;
            int phase = 0;

            while (displayProgress < 1f)
            {
                if (phase == 0)
                {
                    displayProgress = Mathf.MoveTowards(displayProgress, 0.24f, Time.deltaTime * 1.5f); // 快
                    if (displayProgress >= 0.24f) phase = 1;
                }
                else if (phase == 1)
                {
                    yield return new WaitForSeconds(0.15f); // 极短停顿，营造卡顿感
                    phase = 2;
                }
                else if (phase == 2)
                {
                    displayProgress = Mathf.MoveTowards(displayProgress, 0.89f, Time.deltaTime * 3.5f); // 极快
                    if (displayProgress >= 0.89f) phase = 3;
                }
                else if (phase == 3)
                {
                    // 真实加载门槛：只有后台加载达到 90% (即0.9) 时才放行
                    if (asyncLoad.progress >= 0.9f) phase = 4;
                }
                else if (phase == 4)
                {
                    displayProgress = Mathf.MoveTowards(displayProgress, 1f, Time.deltaTime * 1.5f); // 顺滑结算
                }

                // 通过 anchorMin 的 Y 轴平滑向下降，实现从上往下的完美填充
                barRect.anchorMin = new Vector2(0f, 1f - displayProgress);
                // 必须在每帧强行修正 offset，防止因锚点变动而产生的奇怪拉伸
                barRect.offsetMin = new Vector2(0f, 0f);
                barRect.offsetMax = new Vector2(50f, 0f);

                // 强制格式化为 3 位数字，如 000%, 024%
                txtLoadingPercent.text = Mathf.RoundToInt(displayProgress * 100).ToString("D3") + "%";

                yield return null;
            }

            yield return new WaitForSeconds(0.15f); // 满 100% 后停顿一瞬间爆发

            // ==========================================
            // 阶段二：向右全屏铺开
            // ==========================================
            float expandDuration = 0.35f;
            float t = 0;
            while (t < expandDuration)
            {
                t += Time.deltaTime;
                float easeT = 1f - Mathf.Pow(1f - (t / expandDuration), 3);

                barRect.anchorMax = new Vector2(easeT, 1f);
                barRect.anchorMin = new Vector2(0f, 0f); // 高度彻底拉满

                // 将宽度由 50 平滑归零，交给锚点完全接管宽度
                float currentWidth = Mathf.Lerp(50f, 0f, easeT);
                barRect.offsetMin = Vector2.zero;
                barRect.offsetMax = new Vector2(currentWidth, 0f);

                yield return null;
            }

            // 彻底全屏
            barRect.anchorMax = Vector2.one;
            barRect.offsetMax = Vector2.zero;

            // ==========================================
            // 阶段三：跨场景的无缝淡出系统 (完美取代 SceneFadeIn.cs)
            // ==========================================
            GameObject faderObj = new GameObject("GlobalYellowFader");
            DontDestroyOnLoad(faderObj); // 带着这块黄幕去下一个场景！

            Canvas canvas = faderObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            GameObject imgObj = new GameObject("YellowFill");
            imgObj.transform.SetParent(faderObj.transform, false);
            Image img = imgObj.AddComponent<Image>();
            // 提取出你的工业黄颜色，如果有微小色差，请在这里微调 RGB
            img.color = new Color(1f, 0.84f, 0f, 1f);

            RectTransform rect = img.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;

            // 放行场景跳转！
            asyncLoad.allowSceneActivation = true;

            // 在新场景里触发淡出逻辑
            GlobalTransitionFader fader = faderObj.AddComponent<GlobalTransitionFader>();
            fader.StartFadeOut();
        }

        private void SetCanvasGroupAlpha(CanvasGroup cg, float alpha, bool interactable)
        {
            cg.alpha = alpha;
            cg.interactable = interactable;
            cg.blocksRaycasts = interactable;
        }
    }

    // ==========================================
    // 跨场景专用的隐形淡出控制器
    // ==========================================
    public class GlobalTransitionFader : MonoBehaviour
    {
        public void StartFadeOut()
        {
            StartCoroutine(FadeRoutine());
        }

        private IEnumerator FadeRoutine()
        {
            // 给新场景的摄像机和物体一点渲染时间，防止闪出第一帧的丑图
            yield return new WaitForSeconds(0.1f);

            Image img = GetComponentInChildren<Image>();
            float duration = 0.6f;
            float time = 0;

            Color startColor = img.color;

            while (time < duration)
            {
                time += Time.deltaTime;
                img.color = new Color(startColor.r, startColor.g, startColor.b, 1f - (time / duration));
                yield return null;
            }

            // 淡出完毕，功成身退
            Destroy(gameObject);
        }
    }
}