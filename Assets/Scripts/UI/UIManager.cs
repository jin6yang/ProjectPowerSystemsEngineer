using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using ProjectPowerSystemsEngineer.Systems;
using ProjectPowerSystemsEngineer.Components;
using ProjectPowerSystemsEngineer.Data;

namespace ProjectPowerSystemsEngineer.UI
{
    public class UIManager : MonoBehaviour
    {
        public static bool ShowFloatingUI { get; private set; } = true;

        [Header("System References")]
        public BuilderController builderController;

        [Header("UI Containers - Inspect")]
        public CanvasGroup inspectPanelGroup;
        public CanvasGroup topMarqueeGroup;
        public RectTransform wipeBlock;
        private CanvasGroup wipeBlockGroup;

        [Header("UI Containers - Build Menu")]
        public RectTransform buildPanel;
        private CanvasGroup buildPanelGroup;
        public Transform buildButtonContainer;
        public Image imgToggleMenuIcon;
        public Sprite iconMenuOpen;
        public Sprite iconMenuClose;
        public GameObject buildButtonPrefab;

        [Header("Inspect Text & Icons")]
        public TextMeshProUGUI txtComponentName;
        public TextMeshProUGUI txtStatus;
        public TextMeshProUGUI txtPowerInput;
        public TextMeshProUGUI txtStability;
        public TextMeshProUGUI txtMaxCapacity;
        public Image imgPowerIcon;
        public Sprite iconOnline;
        public Sprite iconOffline;
        public Sprite iconOverload;

        [Header("Colors")]
        public Color colorNormal = new Color(0.4f, 1f, 0.4f);
        public Color colorWarning = new Color(1f, 0.84f, 0f);
        public Color colorOverload = new Color(1f, 0.4f, 0.4f);
        public Color colorOffline = new Color(0.6f, 0.6f, 0.6f);

        private PowerNode lastSelectedNode = null;
        private Coroutine fadeCoroutine;
        private Coroutine wipeCoroutine;

        private bool isBuildMenuOpen = false;
        private Coroutine buildMenuAnimCoroutine;

        private float buildPanelHiddenY;
        private float buildPanelShownY;

        private void Start()
        {
            if (builderController == null) builderController = FindAnyObjectByType<BuilderController>();
            if (wipeBlock != null) wipeBlockGroup = wipeBlock.GetComponent<CanvasGroup>();

            if (buildPanel != null)
            {
                buildPanelGroup = buildPanel.GetComponent<CanvasGroup>();
                buildPanelShownY = buildPanel.anchoredPosition.y;
                buildPanelHiddenY = buildPanelShownY - buildPanel.rect.height - 20f;
                buildPanel.anchoredPosition = new Vector2(buildPanel.anchoredPosition.x, buildPanelHiddenY);
                if (buildPanelGroup != null) SetCanvasGroupState(buildPanelGroup, 0f, false);
            }

            SetCanvasGroupState(inspectPanelGroup, 0f, false);
            SetCanvasGroupState(topMarqueeGroup, 0f, false);
            if (wipeBlockGroup != null) SetCanvasGroupState(wipeBlockGroup, 0f, false);

            GenerateBuildMenu();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleFloatingUI();
            }

            if (builderController == null) return;

            PowerNode currentSelected = builderController.SelectedNode;
            if (currentSelected != null && currentSelected.data.isPointToPointCable)
            {
                currentSelected = null;
            }

            if (currentSelected != lastSelectedNode)
            {
                if (currentSelected != null && lastSelectedNode == null)
                {
                    UpdateInspectPanel(currentSelected);
                    PlayFadeAnimation(1f);
                    PlayWipeAnimation();
                }
                else if (currentSelected != null && lastSelectedNode != null)
                {
                    UpdateInspectPanel(currentSelected);
                    PlayWipeAnimation();
                }
                else if (currentSelected == null && lastSelectedNode != null)
                {
                    PlayFadeAnimation(0f);
                }

                lastSelectedNode = currentSelected;
            }
        }

        private void GenerateBuildMenu()
        {
            if (builderController == null || builderController.availableComponents == null || buildButtonPrefab == null || buildButtonContainer == null) return;

            foreach (Transform child in buildButtonContainer) Destroy(child.gameObject);

            for (int i = 0; i < builderController.availableComponents.Length; i++)
            {
                int capturedIndex = i;
                ComponentData data = builderController.availableComponents[i];

                GameObject btnObj = Instantiate(buildButtonPrefab, buildButtonContainer);

                // 【核心修改】通过新写的专用脚本进行精准数据绑定！
                BuildMenuButton btnScript = btnObj.GetComponent<BuildMenuButton>();
                if (btnScript != null)
                {
                    btnScript.Setup(data, () => builderController.EnterBuildModeFromUI(capturedIndex));
                }
                else
                {
                    Debug.LogError($"[UI系统] 你的 BuildButton 预制体上漏挂载了 BuildMenuButton 脚本！");
                }
            }
        }

        public void ToggleBuildMenu()
        {
            isBuildMenuOpen = !isBuildMenuOpen;

            if (imgToggleMenuIcon != null)
            {
                imgToggleMenuIcon.sprite = isBuildMenuOpen ? iconMenuClose : iconMenuOpen;
            }

            if (buildMenuAnimCoroutine != null) StopCoroutine(buildMenuAnimCoroutine);
            buildMenuAnimCoroutine = StartCoroutine(SlideBuildMenuRoutine(isBuildMenuOpen));
        }

        private IEnumerator SlideBuildMenuRoutine(bool show)
        {
            if (buildPanel == null) yield break;

            float targetY = show ? buildPanelShownY : buildPanelHiddenY;
            float targetAlpha = show ? 1f : 0f;
            float startY = buildPanel.anchoredPosition.y;
            float startAlpha = buildPanelGroup != null ? buildPanelGroup.alpha : 1f;

            float duration = 0.25f;
            float timer = 0f;

            if (show && buildPanelGroup != null)
            {
                buildPanelGroup.interactable = true;
                buildPanelGroup.blocksRaycasts = true;
            }

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;
                t = 1f - Mathf.Pow(1f - t, 3);

                buildPanel.anchoredPosition = new Vector2(buildPanel.anchoredPosition.x, Mathf.Lerp(startY, targetY, t));
                if (buildPanelGroup != null) buildPanelGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

                yield return null;
            }

            buildPanel.anchoredPosition = new Vector2(buildPanel.anchoredPosition.x, targetY);
            if (buildPanelGroup != null)
            {
                buildPanelGroup.alpha = targetAlpha;
                if (!show)
                {
                    buildPanelGroup.interactable = false;
                    buildPanelGroup.blocksRaycasts = false;
                }
            }
        }

        private void UpdateInspectPanel(PowerNode node)
        {
            txtComponentName.text = node.data.componentName.ToUpper();

            if (node.IsProtectionTripped)
            {
                txtStatus.text = "[SYS_OVERLOAD]";
                txtStatus.color = colorOverload;
                txtPowerInput.color = colorOverload;

                if (imgPowerIcon != null && iconOverload != null)
                {
                    imgPowerIcon.sprite = iconOverload;
                    imgPowerIcon.color = Color.white;
                }
            }
            else if (node.CurrentPowerInput > 0 || node.data.powerGeneration > 0)
            {
                txtStatus.text = "[SYS_ONLINE]";
                txtStatus.color = colorNormal;
                txtPowerInput.color = Color.white;

                if (imgPowerIcon != null && iconOnline != null)
                {
                    imgPowerIcon.sprite = iconOnline;
                    imgPowerIcon.color = Color.white;
                }
            }
            else
            {
                txtStatus.text = "[SYS_STANDBY]";
                txtStatus.color = colorOffline;
                txtPowerInput.color = colorOffline;

                if (imgPowerIcon != null && iconOffline != null)
                {
                    imgPowerIcon.sprite = iconOffline;
                    imgPowerIcon.color = Color.white;
                }
            }

            txtPowerInput.text = FormatPowerValue(node.CurrentPowerInput);
            if (txtMaxCapacity != null) txtMaxCapacity.text = $"/ {FormatPowerValue(node.data.maxPowerCapacity)}";
            txtStability.text = $"STB_LVL: {node.CurrentStability:0.0}";
        }

        private string FormatPowerValue(float mwValue)
        {
            if (mwValue >= 1000f) return $"{mwValue / 1000f:0.0} G";
            else return $"{mwValue:0.0} M";
        }

        public void ToggleFloatingUI() { ShowFloatingUI = !ShowFloatingUI; }

        private void PlayFadeAnimation(float targetAlpha)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCanvasGroup(inspectPanelGroup, targetAlpha, 0.2f));
            if (topMarqueeGroup != null) StartCoroutine(FadeCanvasGroup(topMarqueeGroup, targetAlpha, 0.2f));
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration)
        {
            if (cg == null) yield break;
            float startAlpha = cg.alpha;
            float time = 0;
            if (targetAlpha > 0) cg.interactable = cg.blocksRaycasts = true;

            while (time < duration)
            {
                time += Time.deltaTime;
                cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
                yield return null;
            }
            cg.alpha = targetAlpha;
            if (targetAlpha == 0) cg.interactable = cg.blocksRaycasts = false;
        }

        private void PlayWipeAnimation()
        {
            if (wipeBlock == null || wipeBlockGroup == null || inspectPanelGroup == null) return;
            if (wipeCoroutine != null) StopCoroutine(wipeCoroutine);
            wipeCoroutine = StartCoroutine(WipeRoutine());
        }

        private IEnumerator WipeRoutine()
        {
            wipeBlockGroup.alpha = 1f;
            float panelWidth = inspectPanelGroup.GetComponent<RectTransform>().rect.width;
            float startPosX = -panelWidth - 50f;
            wipeBlock.anchoredPosition = new Vector2(startPosX, 0);

            float moveTime = 0.15f;
            float timer = 0;
            while (timer < moveTime)
            {
                timer += Time.deltaTime;
                float t = 1f - Mathf.Pow(1f - timer / moveTime, 3);
                wipeBlock.anchoredPosition = new Vector2(Mathf.Lerp(startPosX, 0, t), 0);
                yield return null;
            }
            wipeBlock.anchoredPosition = Vector2.zero;
            yield return new WaitForSeconds(0.05f);

            float fadeTime = 0.2f;
            timer = 0;
            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                wipeBlockGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeTime);
                yield return null;
            }
            wipeBlockGroup.alpha = 0f;
        }

        private void SetCanvasGroupState(CanvasGroup cg, float alpha, bool interactable)
        {
            if (cg != null)
            {
                cg.alpha = alpha;
                cg.interactable = interactable;
                cg.blocksRaycasts = interactable;
            }
        }
    }
}