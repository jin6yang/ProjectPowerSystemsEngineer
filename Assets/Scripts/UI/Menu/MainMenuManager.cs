using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems; // 用于自动绑定悬停事件
using UnityEngine.InputSystem;  // 用于检测 ~ 键

namespace ProjectPowerSystemsEngineer.UI
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Panels")]
        public CanvasGroup panelSelectUser;
        public CanvasGroup panelMain;
        public CanvasGroup panelLevelSelect; // 新增：关卡选择面板
        public CanvasGroup panelLoading;     // 新增：加载面板

        [Header("Top Bar UI")]
        public TextMeshProUGUI txtSystemTime;

        [Header("Buttons - Main")]
        public Button btnLogin;
        public Button btnStartGame;

        [Header("Level Select UI")]
        public Button btnBack;
        public TextMeshProUGUI txtLevelNameDisplay;
        public Button[] btnLevels; // 把你的几个关卡按钮拖进来

        // 这里的名字和场景名要一一对应
        private string[] levelNames = { "反应堆初始化测试", "高压电网搭建", "沙盒模式" };
        private string[] sceneNames = { "Level_DevTest_01", "Level_02", "Level_03" };

        [Header("Loading UI")]
        public Image imgYellowBar; // 挂载那个靠左侧的黄色Image
        public TextMeshProUGUI txtLoadingPercent;

        private void Start()
        {
            // 初始化界面状态
            SetCanvasGroupAlpha(panelSelectUser, 1f, true);
            SetCanvasGroupAlpha(panelMain, 0f, false);
            SetCanvasGroupAlpha(panelLevelSelect, 0f, false);
            SetCanvasGroupAlpha(panelLoading, 0f, false);

            if (btnLogin != null) btnLogin.onClick.AddListener(OnLoginClicked);
            if (btnStartGame != null) btnStartGame.onClick.AddListener(OnStartGameClicked);
            if (btnBack != null) btnBack.onClick.AddListener(OnBackClicked);

            if (OnyxController.Instance != null) OnyxController.Instance.SetState(OnyxState.Sleepy);

            // 【核心魔法】全自动为每个关卡按钮绑定“鼠标悬停”和“点击”事件，无需Editor连线！
            for (int i = 0; i < btnLevels.Length; i++)
            {
                int index = i; // 捕获局部变量供Lambda表达式使用

                // 1. 绑定点击事件
                btnLevels[i].onClick.AddListener(() => OnLevelButtonClicked(index));

                // 2. 绑定鼠标悬停事件 (PointerEnter)
                EventTrigger trigger = btnLevels[i].gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerEnter;
                entry.callback.AddListener((data) => { txtLevelNameDisplay.text = levelNames[index]; });
                trigger.triggers.Add(entry);
            }

            txtLevelNameDisplay.text = "请选择模拟项目"; // 默认文本
        }

        private void Update()
        {
            if (txtSystemTime != null) txtSystemTime.text = System.DateTime.Now.ToString("HH:mm");

            // 监听 `~` (波浪号/反引号) 快捷键。仅在关卡选择界面生效
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

        // === 动画流转核心协程 ===

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
            // 1. 淡出关卡选择，淡入加载界面
            yield return StartCoroutine(CrossFadePanels(panelLevelSelect, panelLoading));
            panelLoading.interactable = true; panelLoading.blocksRaycasts = true;

            // ==========================================
            // 纯净色块形变魔法：不需要任何 Sprite 贴图
            // ==========================================
            RectTransform barRect = imgYellowBar.GetComponent<RectTransform>();

            // 初始状态设置：靠左对齐，固定宽度 50，高度为 0 (停在最上方)
            barRect.anchorMax = new Vector2(0f, 1f); // (左, 上)
            barRect.anchorMin = new Vector2(0f, 1f); // (左, 上) -> 此时 Min.y 和 Max.y 都是 1，高度为 0
            barRect.sizeDelta = new Vector2(50f, 0f); // 宽度 50
            barRect.anchoredPosition = Vector2.zero;  // 紧贴左上边缘

            txtLoadingPercent.text = "0%";

            // 开始后台异步加载目标场景
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene);
            asyncLoad.allowSceneActivation = false; // 憋住不准跳转！

            // ==========================================
            // 阶段一：进度条从上往下填充 (0% -> 100%)
            // ==========================================
            float displayProgress = 0f;
            while (asyncLoad.progress < 0.9f)
            {
                displayProgress = Mathf.MoveTowards(displayProgress, asyncLoad.progress, Time.deltaTime * 0.5f);
                float fillPercent = displayProgress / 0.9f;

                // 魔法：把 anchorMin 的 Y 值从 1 平滑降到 0，纯色框体就会从上往下被拉长！
                barRect.anchorMin = new Vector2(0f, 1f - fillPercent);

                txtLoadingPercent.text = Mathf.RoundToInt(fillPercent * 100) + "%";
                yield return null;
            }

            // 确保显示 100%，并且进度条彻底抵到底部 (anchorMin.y = 0)
            barRect.anchorMin = new Vector2(0f, 0f);
            txtLoadingPercent.text = "100%";
            yield return new WaitForSeconds(0.2f); // 停顿一下，营造压迫感

            // ==========================================
            // 阶段二：完全加载后，黄色进度条向右暴力扩展至全屏！
            // ==========================================
            float expandDuration = 0.4f;
            float t = 0;
            while (t < expandDuration)
            {
                t += Time.deltaTime;
                float easeT = 1f - Mathf.Pow(1f - (t / expandDuration), 3); // 爆发式的缓动函数

                // 魔法：把 anchorMax 的 X 值从 0 拉到 1 (向右填满屏幕)
                // 同时把原来写死的 50 宽度平滑归零，完全把控制权交给锚点
                barRect.anchorMax = new Vector2(easeT, 1f);
                barRect.sizeDelta = new Vector2(Mathf.Lerp(50f, 0f, easeT), 0f);

                yield return null;
            }

            // 确保最终填满全屏
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.sizeDelta = Vector2.zero;

            // 5. 满屏黄色后，释放场景跳转指令！
            asyncLoad.allowSceneActivation = true;
        }

        private void SetCanvasGroupAlpha(CanvasGroup cg, float alpha, bool interactable)
        {
            cg.alpha = alpha;
            cg.interactable = interactable;
            cg.blocksRaycasts = interactable;
        }
    }
}