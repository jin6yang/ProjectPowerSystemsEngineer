using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems; // 引入 UI 事件系统命名空间

namespace ProjectPowerSystemsEngineer.Core
{
    /// <summary>
    /// 全局手柄虚拟光标管理器。
    /// </summary>
    [DefaultExecutionOrder(-100)] // 【关键】：确保我们的脚本在 UI EventSystem 之前运行，以进行事件拦截！
    public class GamepadCursorManager : MonoBehaviour
    {
        [Header("Settings")]
        public float cursorSpeed = 1000f;

        private static GamepadCursorManager Instance;

        // 智能模式状态机：标记当前玩家是否正在使用摇杆操控鼠标
        private bool isVirtualCursorMode = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[System] GamepadCursorManager");
                Instance = go.AddComponent<GamepadCursorManager>();
                DontDestroyOnLoad(go);
            }
        }

        private void Update()
        {
            Gamepad gamepad = Gamepad.current;
            Mouse mouse = Mouse.current;

            if (gamepad == null || mouse == null) return;

            Vector2 rightStick = gamepad.rightStick.ReadValue();
            Vector2 dpad = gamepad.dpad.ReadValue();

            // ==========================================
            // 智能模式切换
            // 如果玩家推动右摇杆，进入“虚拟鼠标模式”
            // 如果玩家按十字方向键，退出该模式，把焦点交还给原生 UI 导航
            // ==========================================
            if (rightStick.sqrMagnitude > 0.05f) isVirtualCursorMode = true;
            else if (dpad.sqrMagnitude > 0.05f) isVirtualCursorMode = false;

            if (isVirtualCursorMode)
            {
                bool isSouthPressed = gamepad.buttonSouth.isPressed;
                bool wasSouthReleased = gamepad.buttonSouth.wasReleasedThisFrame;

                // 只要摇杆在动，或者A键处于按下/抬起状态，都视为“光标活跃期”
                bool isCursorActive = rightStick.sqrMagnitude > 0.05f || isSouthPressed || wasSouthReleased;

                if (isCursorActive)
                {
                    // 【修复核心 1：双重坐标点击 Bug】
                    // 强制清空 UI 系统的选中状态 (Selected GameObject)！
                    // 这样，手柄的原生 A 键 (Submit) 因为找不到选中的目标，就会变成“哑弹”。
                    // 系统将完美地只响应我们下方发出的物理鼠标点击！
                    if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                    }

                    Vector2 currentPos = mouse.position.ReadValue();

                    if (rightStick.sqrMagnitude > 0.05f)
                    {
                        currentPos += rightStick * cursorSpeed * Time.unscaledDeltaTime;
                        currentPos.x = Mathf.Clamp(currentPos.x, 0, Screen.width);
                        currentPos.y = Mathf.Clamp(currentPos.y, 0, Screen.height);
                        mouse.WarpCursorPosition(currentPos);
                    }

                    // 【修复核心 2：物理鼠标覆盖导致双击/无法拖拽 Bug】
                    // 不再仅仅是按下/抬起时才发送数据！
                    // 只要处于活跃期，我们【每一帧】都向底层强行轰炸并覆盖当前的虚拟状态。
                    // 这样即使硬件鼠标在桌面上发生轻微震动发送了 buttons=0 的假象，也会瞬间被我们覆写纠正！
                    MouseState virtualMouseState = new MouseState();
                    virtualMouseState.position = currentPos;
                    virtualMouseState.delta = mouse.delta.ReadValue();
                    virtualMouseState.scroll = mouse.scroll.ReadValue();

                    ushort buttonsMask = 0;

                    if (isSouthPressed) buttonsMask |= 1;
                    if (mouse.rightButton.isPressed) buttonsMask |= 2;
                    if (mouse.middleButton.isPressed) buttonsMask |= 4;

                    virtualMouseState.buttons = buttonsMask;
                    InputSystem.QueueStateEvent(mouse, virtualMouseState);
                }
            }
        }
    }
}