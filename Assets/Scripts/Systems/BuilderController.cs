using UnityEngine;
using UnityEngine.InputSystem;
using ProjectPowerSystemsEngineer.Grid;
using ProjectPowerSystemsEngineer.Data;
using ProjectPowerSystemsEngineer.Components; // 引入 PowerNode

namespace ProjectPowerSystemsEngineer.Systems
{
    public class BuilderController : MonoBehaviour
    {
        [Header("Build Data")]
        [Tooltip("当前玩家选择要建造的组件数据卡片")]
        public ComponentData selectedComponent;

        [Header("Ghost Materials")]
        public Material validGhostMaterial;
        public Material invalidGhostMaterial;

        [Header("Interaction Settings")]
        public float dragThreshold = 10f;

        private GameObject ghostInstance;
        private MeshRenderer ghostRenderer;
        private bool isBuildingMode = false;

        private Vector2 mouseDownPosition;
        private bool isDragging = false;

        void Update()
        {
            if (Keyboard.current == null || Mouse.current == null) return;

            if (Keyboard.current.bKey.wasPressedThisFrame)
            {
                ToggleBuildMode();
            }

            if (isBuildingMode)
            {
                HandleGhostAndPlacement();
            }
        }

        private void ToggleBuildMode()
        {
            // 如果没有配置数据，拒绝进入建造模式
            if (selectedComponent == null)
            {
                Debug.LogWarning("未选择任何组件数据，无法建造！");
                return;
            }

            isBuildingMode = !isBuildingMode;
            if (isBuildingMode)
            {
                if (ghostInstance == null && selectedComponent.ghostPrefab != null)
                {
                    // 改为从 selectedComponent 读取残影预制体
                    ghostInstance = Instantiate(selectedComponent.ghostPrefab);
                    ghostRenderer = ghostInstance.GetComponentInChildren<MeshRenderer>();
                }
                ghostInstance.SetActive(true);
            }
            else
            {
                if (ghostInstance != null) ghostInstance.SetActive(false);
                // 退出建造模式时销毁残影，以便下次切换组件时重新生成
                Destroy(ghostInstance);
            }
        }

        private void HandleGhostAndPlacement()
        {
            Vector2Int? gridPos = GridManager.Instance.GetMouseGridPosition();

            if (gridPos.HasValue)
            {
                ghostInstance.SetActive(true);
                Vector3 worldPos = GridManager.Instance.GridToWorldPosition(gridPos.Value);
                ghostInstance.transform.position = worldPos;

                bool canPlace = GridManager.Instance.IsCellAvailable(gridPos.Value);

                if (ghostRenderer != null)
                {
                    ghostRenderer.material = canPlace ? validGhostMaterial : invalidGhostMaterial;
                }

                HandleClickToBuild(canPlace, gridPos.Value, worldPos);
            }
            else
            {
                if (ghostInstance != null && ghostInstance.activeSelf)
                    ghostInstance.SetActive(false);
            }
        }

        private void HandleClickToBuild(bool canPlace, Vector2Int pos, Vector3 worldPos)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                mouseDownPosition = Mouse.current.position.ReadValue();
                isDragging = false;
            }

            if (Mouse.current.leftButton.isPressed)
            {
                if (Vector2.Distance(mouseDownPosition, Mouse.current.position.ReadValue()) > dragThreshold)
                {
                    isDragging = true;
                }
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (canPlace && !isDragging)
                {
                    PlaceObject(pos, worldPos);
                }
            }
        }

        private void PlaceObject(Vector2Int pos, Vector3 worldPos)
        {
            if (selectedComponent != null && selectedComponent.prefab != null)
            {
                // 1. 从数据卡片中实例化真实的 3D 模型
                GameObject newObj = Instantiate(selectedComponent.prefab, worldPos, Quaternion.identity);

                // 2. 尝试获取模型上的 PowerNode 脚本，并进行初始化！
                PowerNode node = newObj.GetComponent<PowerNode>();
                if (node != null)
                {
                    // 核心关联：把刚才的数据卡片赋值给节点，并初始化坐标
                    node.data = selectedComponent;
                    node.Initialize(pos);
                }
                else
                {
                    Debug.LogWarning($"[Builder] 建造的 {selectedComponent.componentName} 模型上没有挂载 PowerNode 脚本！");
                }

                // 3. 将其注册到网格系统中
                GridCell cell = GridManager.Instance.GetCell(pos);
                if (cell != null)
                {
                    cell.PlacedObject = newObj;

                    // TODO: 触发全局拓扑更新事件，通知模拟系统重新计算电力
                }
            }
        }
    }
}