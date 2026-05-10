using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ProjectPowerSystemsEngineer.Core
{
    /// <summary>
    /// 全局手柄虚拟光标管理器。
    /// 自动在游戏启动时加载，跨场景存活。负责用手柄摇杆模拟鼠标移动，用 A 键模拟鼠标左键。
    /// </summary>
    public class GamepadCursorManager : MonoBehaviour
    {
        [Header("Settings")]
        public float cursorSpeed = 1000f;

        private static GamepadCursorManager Instance;

        // 【黑科技】让这个脚本在游戏启动时“自动无中生有”，无需你手动挂载到场景里！
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[System] GamepadCursorManager");
                Instance = go.AddComponent<GamepadCursorManager>();
                DontDestroyOnLoad(go); // 保证跨越主菜单和关卡时依然存活
            }
        }

        private void Update()
        {
            Gamepad gamepad = Gamepad.current;
            Mouse mouse = Mouse.current;

            // 如果没接手柄或没接鼠标，直接跳过
            if (gamepad == null || mouse == null) return;

            // ==========================================
            // 1. 右摇杆模拟鼠标移动
            // ==========================================
            if (!gamepad.rightStickButton.isPressed)
            {
                Vector2 rightStick = gamepad.rightStick.ReadValue();

                // 摇杆死区，防漂移
                if (rightStick.sqrMagnitude > 0.05f)
                {
                    Vector2 currentMousePos = mouse.position.ReadValue();
                    Vector2 newMousePos = currentMousePos + rightStick * cursorSpeed * Time.unscaledDeltaTime;

                    newMousePos.x = Mathf.Clamp(newMousePos.x, 0, Screen.width);
                    newMousePos.y = Mathf.Clamp(newMousePos.y, 0, Screen.height);

                    // 瞬移鼠标位置
                    mouse.WarpCursorPosition(newMousePos);
                }
            }

            // ==========================================
            // 2. A 键 (Button South) 强行模拟鼠标物理左键点击
            // ==========================================
            if (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonSouth.wasReleasedThisFrame)
            {
                // 【修复核心】：Unity 6 禁止直接修改 Bitfield 内存结构
                // 我们通过构建一个完整的 MouseState，然后用队列事件的形式注入系统！

                MouseState virtualMouseState = new MouseState();

                // 1. 继承鼠标当前的物理坐标、滚轮等状态，防止突变
                virtualMouseState.position = mouse.position.ReadValue();
                virtualMouseState.delta = mouse.delta.ReadValue();
                virtualMouseState.scroll = mouse.scroll.ReadValue();

                // 2. 重新拼接按键状态位图 (Bitmask)
                ushort buttonsMask = 0;

                // 将手柄的 A 键映射为鼠标左键 (位 0)
                if (gamepad.buttonSouth.isPressed) buttonsMask |= 1;

                // 保留实体鼠标的右键(位 1)和中键(位 2)状态
                if (mouse.rightButton.isPressed) buttonsMask |= 2;
                if (mouse.middleButton.isPressed) buttonsMask |= 4;

                virtualMouseState.buttons = buttonsMask;

                // 3. 将组装好的虚拟状态塞入系统底层队列！
                InputSystem.QueueStateEvent(mouse, virtualMouseState);
            }
        }
    }
}