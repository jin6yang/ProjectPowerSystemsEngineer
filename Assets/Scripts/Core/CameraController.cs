using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectPowerSystemsEngineer.Core
{
    public class CameraController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float panSpeed = 20f;
        public float panBorderThickness = 10f;
        public Vector2 panLimit = new Vector2(50f, 50f);

        [Header("Drag Settings")]
        public float dragPanSpeed = 0.5f;

        [Header("Zoom Settings")]
        public float zoomSpeed = 10f;
        public float minZoom = 5f;
        public float maxZoom = 40f;

        [Header("Rotation Settings")]
        public float rotationSpeed = 2f;
        public float minPitch = 20f;
        public float maxPitch = 85f;

        [Header("Gamepad Settings")]
        public float gamepadCursorSpeed = 1000f; // 手柄推动虚拟鼠标的速度
        public float gamepadRotationSpeed = 50f; // 手柄旋转视角的敏感度

        [Header("Smoothing (Lerp)")]
        public float smoothSpeed = 10f;

        // 核心目标变量
        private Vector3 targetPosition;
        private float targetYaw;
        private float targetPitch;
        private float targetZoom;

        private Transform childCamera;

        void Start()
        {
            childCamera = GetComponentInChildren<Camera>().transform;

            targetPosition = transform.position;
            targetYaw = transform.eulerAngles.y;
            targetPitch = childCamera.localEulerAngles.x;
            targetZoom = childCamera.localPosition.magnitude;
        }

        void LateUpdate()
        {
            // 安全检查：如果有鼠标键盘就读取，有手柄也会自动读取
            if (Keyboard.current == null || Mouse.current == null) return;

            HandleVirtualCursor(); // 必须最先处理虚拟光标的移动
            HandleMovementInput();
            HandleRotationInput();
            HandleZoomInput();

            ApplySmoothing();
        }

        private void HandleVirtualCursor()
        {
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null) return;

            // 如果推动了右摇杆，并且【没有】按下右摇杆 (R3)
            if (!gamepad.rightStickButton.isPressed)
            {
                Vector2 rightStick = gamepad.rightStick.ReadValue();

                // 给摇杆加一个死区(Deadzone)，防止手柄漂移导致鼠标自己乱动
                if (rightStick.sqrMagnitude > 0.05f)
                {
                    // 获取当前鼠标位置
                    Vector2 currentMousePos = Mouse.current.position.ReadValue();

                    // 计算新位置 (摇杆值 [-1, 1] * 速度 * DeltaTime)
                    Vector2 newMousePos = currentMousePos + rightStick * gamepadCursorSpeed * Time.deltaTime;

                    // 限制光标在屏幕范围内
                    newMousePos.x = Mathf.Clamp(newMousePos.x, 0, Screen.width);
                    newMousePos.y = Mathf.Clamp(newMousePos.y, 0, Screen.height);

                    // 【核心魔法】强行将系统鼠标光标传送到新位置
                    Mouse.current.WarpCursorPosition(newMousePos);
                }
            }
        }

        private void HandleMovementInput()
        {
            Vector3 movement = Vector3.zero;

            // 1. 键盘 WASD 移动
            if (Keyboard.current.wKey.isPressed) movement += transform.forward;
            if (Keyboard.current.sKey.isPressed) movement -= transform.forward;
            if (Keyboard.current.dKey.isPressed) movement += transform.right;
            if (Keyboard.current.aKey.isPressed) movement -= transform.right;

            // 2. 手柄左摇杆移动
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 leftStick = gamepad.leftStick.ReadValue();
                movement += transform.forward * leftStick.y;
                movement += transform.right * leftStick.x;
            }

            // 3. 鼠标边缘平移 (没按左键/右键，且手柄没在旋转时)
            bool isGamepadRotating = gamepad != null && gamepad.rightStickButton.isPressed;
            if (!Mouse.current.leftButton.isPressed && !Mouse.current.rightButton.isPressed && !isGamepadRotating)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                if (mousePos.y >= Screen.height - panBorderThickness) movement += transform.forward;
                if (mousePos.y <= panBorderThickness) movement -= transform.forward;
                if (mousePos.x >= Screen.width - panBorderThickness) movement += transform.right;
                if (mousePos.x <= panBorderThickness) movement -= transform.right;
            }

            movement.y = 0;
            if (movement.sqrMagnitude > 1f) movement.Normalize(); // 防止WASD和摇杆叠加导致超速

            targetPosition += movement * panSpeed * Time.deltaTime;

            // 4. 鼠标左键拖拽平移
            if (Mouse.current.leftButton.isPressed)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                Vector3 dragMovement = transform.right * mouseDelta.x + transform.forward * mouseDelta.y;
                dragMovement.y = 0;
                targetPosition -= dragMovement * dragPanSpeed * 0.1f;
            }

            targetPosition.x = Mathf.Clamp(targetPosition.x, -panLimit.x, panLimit.x);
            targetPosition.z = Mathf.Clamp(targetPosition.z, -panLimit.y, panLimit.y);
        }

        private void HandleRotationInput()
        {
            Gamepad gamepad = Gamepad.current;
            bool isGamepadRotating = gamepad != null && gamepad.rightStickButton.isPressed;

            // 如果按住了鼠标右键 OR 按下了手柄右摇杆(R3)
            if (Mouse.current.rightButton.isPressed || isGamepadRotating)
            {
                float deltaX = 0f;
                float deltaY = 0f;

                if (Mouse.current.rightButton.isPressed)
                {
                    deltaX = Mouse.current.delta.ReadValue().x * rotationSpeed * 0.1f;
                    deltaY = Mouse.current.delta.ReadValue().y * rotationSpeed * 0.1f;
                }
                else if (isGamepadRotating)
                {
                    // 手柄摇杆返回的是 [-1, 1] 的持续值，所以要乘 deltaTime 和专属的速度变量
                    deltaX = gamepad.rightStick.ReadValue().x * gamepadRotationSpeed * Time.deltaTime;
                    deltaY = gamepad.rightStick.ReadValue().y * gamepadRotationSpeed * Time.deltaTime;
                }

                targetYaw += deltaX;
                targetPitch -= deltaY;
                targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
            }
            else
            {
                if (Keyboard.current.qKey.isPressed) targetYaw += rotationSpeed * 50f * Time.deltaTime;
                if (Keyboard.current.eKey.isPressed) targetYaw -= rotationSpeed * 50f * Time.deltaTime;
            }
        }

        private void HandleZoomInput()
        {
            // 1. 鼠标滚轮缩放
            float scrollValue = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollValue) > 0.01f)
            {
                targetZoom -= Mathf.Sign(scrollValue) * zoomSpeed * 0.5f;
            }

            // 2. 手柄扳机键缩放 (RT拉近，LT推远)
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                float rtValue = gamepad.rightTrigger.ReadValue(); // [0, 1]
                float ltValue = gamepad.leftTrigger.ReadValue();  // [0, 1]

                if (rtValue > 0.1f) targetZoom -= zoomSpeed * rtValue * Time.deltaTime;
                if (ltValue > 0.1f) targetZoom += zoomSpeed * ltValue * Time.deltaTime;
            }

            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        private void ApplySmoothing()
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);

            Quaternion targetRigRotation = Quaternion.Euler(0, targetYaw, 0);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRigRotation, Time.deltaTime * smoothSpeed);

            Quaternion targetCamRotation = Quaternion.Euler(targetPitch, 0, 0);
            Vector3 targetCamLocalPos = targetCamRotation * new Vector3(0, 0, -targetZoom);

            childCamera.localPosition = Vector3.Lerp(childCamera.localPosition, targetCamLocalPos, Time.deltaTime * smoothSpeed);
            childCamera.localRotation = Quaternion.Lerp(childCamera.localRotation, targetCamRotation, Time.deltaTime * smoothSpeed);
        }
    }
}