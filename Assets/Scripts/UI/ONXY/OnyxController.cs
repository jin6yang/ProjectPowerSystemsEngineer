using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace ProjectPowerSystemsEngineer.UI
{
    public enum OnyxState
    {
        Idle,       // 待机 [ ] [ ]
        Watching,   // 观察中 (平移，随机变窄高)
        Confused,   // 疑惑 (一高一矮)
        Sleepy,     // 困了 - - (压扁)
        Fail,       // 失败 > < (旋转成叉)
        Success     // 成功 ^ ^ (开心眼)
    }

    public class OnyxController : MonoBehaviour
    {
        // === 单例模式，保证全局唯一且不被销毁 ===
        public static OnyxController Instance { get; private set; }

        [Header("UI References")]
        public RectTransform eyeLeft;
        public RectTransform eyeRight;

        [Header("Wobble Settings (Your Custom Params)")]
        public float wobbleIntensity = 0.15f;
        public float wobbleSpeed = 0.3f;
        public float wobblePixelMultiplier = 15f;

        [Header("Autonomous Behavior (AFK)")]
        public float minIdleChangeTime = 3f;
        public float maxIdleChangeTime = 8f;

        private OnyxState currentState = OnyxState.Idle;
        private Coroutine expressionCoroutine;

        private float idleTimer = 0f;
        private float nextIdleTargetTime = 5f;

        // 默认中心基准坐标
        private readonly Vector2 defaultPosLeft = new Vector2(-25f, 0f);
        private readonly Vector2 defaultPosRight = new Vector2(25f, 0f);

        // 当前眼睛的“逻辑基础位置”，晃动会在此基础上叠加，并在状态切换时平滑移动
        private Vector2 basePosLeft;
        private Vector2 basePosRight;

        // 目标表现参数
        private Vector2 targetScaleLeft = Vector2.one;
        private Vector2 targetScaleRight = Vector2.one;
        private float targetRotLeft = 0f;
        private float targetRotRight = 0f;
        private Vector2 targetPosLeft;
        private Vector2 targetPosRight;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                basePosLeft = defaultPosLeft;
                basePosRight = defaultPosRight;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            SetState(OnyxState.Idle);
            nextIdleTargetTime = Random.Range(minIdleChangeTime, maxIdleChangeTime);
        }

        private void Update()
        {
            if (eyeLeft == null || eyeRight == null) return;

            float noiseX = Mathf.PerlinNoise(Time.unscaledTime * wobbleSpeed, 0f) * 2f - 1f;
            float noiseY = Mathf.PerlinNoise(0f, Time.unscaledTime * wobbleSpeed) * 2f - 1f;

            Vector2 wobbleOffset = new Vector2(noiseX, noiseY) * wobbleIntensity * wobblePixelMultiplier;

            // 将晃动叠加在基础位置上
            eyeLeft.anchoredPosition = basePosLeft + wobbleOffset;
            eyeRight.anchoredPosition = basePosRight + wobbleOffset;

            // === 自主挂机状态机 (AFK Logic) ===
            if (currentState == OnyxState.Idle || currentState == OnyxState.Watching ||
                currentState == OnyxState.Confused || currentState == OnyxState.Sleepy)
            {
                idleTimer += Time.unscaledDeltaTime;

                if (idleTimer >= nextIdleTargetTime)
                {
                    idleTimer = 0f;
                    nextIdleTargetTime = Random.Range(minIdleChangeTime, maxIdleChangeTime);

                    OnyxState[] idleStates = { OnyxState.Idle, OnyxState.Watching, OnyxState.Confused, OnyxState.Sleepy };
                    OnyxState randomState = idleStates[Random.Range(0, idleStates.Length)];

                    if (currentState != randomState)
                    {
                        currentState = randomState;
                        if (expressionCoroutine != null) StopCoroutine(expressionCoroutine);
                        expressionCoroutine = StartCoroutine(AnimateExpression());
                    }
                    else
                    {
                        // 即使随机到了相同的状态，也重新执行动画以触发新的随机组合（例如从看左边变成看右边）
                        if (expressionCoroutine != null) StopCoroutine(expressionCoroutine);
                        expressionCoroutine = StartCoroutine(AnimateExpression());
                    }
                }
            }
        }

        public void SetState(OnyxState newState)
        {
            if (currentState == newState) return;
            currentState = newState;

            idleTimer = 0f;
            nextIdleTargetTime = Random.Range(minIdleChangeTime, maxIdleChangeTime);

            if (expressionCoroutine != null) StopCoroutine(expressionCoroutine);
            expressionCoroutine = StartCoroutine(AnimateExpression());
        }

        private IEnumerator AnimateExpression()
        {
            // 每次切换时，默认让位置归中，除非在特定状态下被修改
            targetPosLeft = defaultPosLeft;
            targetPosRight = defaultPosRight;

            switch (currentState)
            {
                case OnyxState.Idle:
                    targetScaleLeft = Vector2.one; targetScaleRight = Vector2.one;
                    targetRotLeft = 0f; targetRotRight = 0f;
                    break;

                case OnyxState.Watching:
                    targetRotLeft = 0f; targetRotRight = 0f;

                    // 1. 决定方向：左移(-15) 还是 右移(+15)
                    float offsetX = Random.value > 0.5f ? -15f : 15f;
                    targetPosLeft = defaultPosLeft + new Vector2(offsetX, 0f);
                    targetPosRight = defaultPosRight + new Vector2(offsetX, 0f);

                    // 2. 决定形状：变高变窄 还是 保持原样
                    if (Random.value > 0.5f)
                    {
                        targetScaleLeft = new Vector2(0.6f, 1.4f);
                        targetScaleRight = new Vector2(0.6f, 1.4f);
                    }
                    else
                    {
                        targetScaleLeft = Vector2.one;
                        targetScaleRight = Vector2.one;
                    }
                    break;

                case OnyxState.Confused:
                    targetRotLeft = 0f; targetRotRight = 0f; // 取消歪头

                    // 随机决定哪只眼睛变高，哪只眼睛变矮
                    if (Random.value > 0.5f)
                    {
                        targetScaleLeft = new Vector2(1f, 1.4f);   // 左眼高
                        targetScaleRight = new Vector2(1f, 0.6f);  // 右眼矮
                    }
                    else
                    {
                        targetScaleLeft = new Vector2(1f, 0.6f);   // 左眼矮
                        targetScaleRight = new Vector2(1f, 1.4f);  // 右眼高
                    }
                    break;

                case OnyxState.Sleepy:
                    targetScaleLeft = new Vector2(1f, 0.2f); targetScaleRight = new Vector2(1f, 0.2f);
                    targetRotLeft = 0f; targetRotRight = 0f;
                    break;

                case OnyxState.Fail:
                    targetScaleLeft = new Vector2(1.2f, 0.2f); targetScaleRight = new Vector2(1.2f, 0.2f);
                    targetRotLeft = -30f; targetRotRight = 30f;
                    break;

                case OnyxState.Success:
                    targetScaleLeft = new Vector2(1.2f, 0.2f); targetScaleRight = new Vector2(1.2f, 0.2f);
                    targetRotLeft = 30f; targetRotRight = -30f;
                    break;
            }

            float duration = 0.3f;
            float time = 0;

            Vector2 startScaleL = eyeLeft.localScale;
            Vector2 startScaleR = eyeRight.localScale;
            Quaternion startRotL = eyeLeft.localRotation;
            Quaternion startRotR = eyeRight.localRotation;

            // 记录平滑移动的起始位置
            Vector2 startPosL = basePosLeft;
            Vector2 startPosR = basePosRight;

            Quaternion endRotL = Quaternion.Euler(0, 0, targetRotLeft);
            Quaternion endRotR = Quaternion.Euler(0, 0, targetRotRight);

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = time / duration;
                t = 1f - Mathf.Pow(1f - t, 3);

                // 平滑改变位置（基准坐标）
                basePosLeft = Vector2.Lerp(startPosL, targetPosLeft, t);
                basePosRight = Vector2.Lerp(startPosR, targetPosRight, t);

                // 平滑改变缩放和旋转
                eyeLeft.localScale = Vector2.Lerp(startScaleL, targetScaleLeft, t);
                eyeRight.localScale = Vector2.Lerp(startScaleR, targetScaleRight, t);
                eyeLeft.localRotation = Quaternion.Lerp(startRotL, endRotL, t);
                eyeRight.localRotation = Quaternion.Lerp(startRotR, endRotR, t);

                yield return null;
            }
        }
    }
}