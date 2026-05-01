using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;
using UnityEngine.SceneManagement;

namespace ProjectPowerSystemsEngineer.UI
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Panels")]
        public CanvasGroup panelSelectUser;
        public CanvasGroup panelMain;

        [Header("Top Bar UI")]
        public TextMeshProUGUI txtSystemTime;

        [Header("Buttons")]
        public Button btnLogin;
        public Button btnStartGame;

        private void Start()
        {
            // 初始化界面：显示登录页，隐藏主菜单
            SetCanvasGroupAlpha(panelSelectUser, 1f, true);
            SetCanvasGroupAlpha(panelMain, 0f, false);

            // 绑定按钮事件
            if (btnLogin != null) btnLogin.onClick.AddListener(OnLoginClicked);
            if (btnStartGame != null) btnStartGame.onClick.AddListener(OnStartGameClicked);

            // 唤醒 ONYX (如果在场景中的话)
            if (OnyxController.Instance != null)
            {
                OnyxController.Instance.SetState(OnyxState.Sleepy); // 没登录时，ONYX 是困的
            }
        }

        private void Update()
        {
            // 实时更新右上角的系统时间 (格式: 18:00)
            if (txtSystemTime != null)
            {
                txtSystemTime.text = DateTime.Now.ToString("HH:mm");
            }
        }

        private void OnLoginClicked()
        {
            StartCoroutine(TransitionToMainMenu());
        }

        private IEnumerator TransitionToMainMenu()
        {
            // 禁用登录按钮防止连点
            btnLogin.interactable = false;

            // 唤醒 ONYX
            if (OnyxController.Instance != null)
            {
                OnyxController.Instance.SetState(OnyxState.Watching);
            }

            // 交叉淡入淡出动画 (0.5秒)
            float duration = 0.5f;
            float time = 0;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;

                SetCanvasGroupAlpha(panelSelectUser, 1f - t, false);
                SetCanvasGroupAlpha(panelMain, t, false);

                yield return null;
            }

            SetCanvasGroupAlpha(panelSelectUser, 0f, false);
            SetCanvasGroupAlpha(panelMain, 1f, true);
        }

        private void OnStartGameClicked()
        {
            // TODO: 这里之后会接你的“关卡选择面板”或者“Loading条”
            Debug.Log("[System] 准备进入模拟系统...");

            // 暂时做个假跳转测试
            // SceneManager.LoadScene("Level_DevTest_01"); 
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