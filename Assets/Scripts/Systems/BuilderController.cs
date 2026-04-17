using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using ProjectPowerSystemsEngineer.Grid;
using ProjectPowerSystemsEngineer.Data;
using ProjectPowerSystemsEngineer.Components;
using ProjectPowerSystemsEngineer.Simulation;

namespace ProjectPowerSystemsEngineer.Systems
{
    public class BuilderController : MonoBehaviour
    {
        [Header("Build Data (Hotbar)")]
        public ComponentData[] availableComponents;

        [Header("Ghost Materials")]
        public Material validGhostMaterial;
        public Material invalidGhostMaterial;

        [Header("UI & Feedback")]
        [Tooltip("地块选中时的四角指示器预制体")]
        public GameObject selectionIndicatorPrefab;

        [Header("Interaction Settings")]
        public float dragThreshold = 10f;

        // --- 核心交互状态 ---
        public PowerNode SelectedNode { get; private set; } // 全局当前选中的节点（如果有）
        public Vector2Int? SelectedGridPosition { get; private set; } // 全局当前选中的地块坐标

        private bool isBuildingMode = false;
        private int currentSelectedIndex = 0;

        private GameObject ghostInstance;
        private MeshRenderer[] ghostRenderers;
        private GameObject indicatorInstance; // 指示器的实例

        private Vector2 mouseDownPosition;
        private bool isDragging = false;

        // --- 电缆连线专用状态 ---
        private PowerNode cableStartNode = null;
        private LineRenderer previewLine;

        public ComponentData SelectedComponent =>
            (availableComponents != null && availableComponents.Length > 0 && currentSelectedIndex < availableComponents.Length)
            ? availableComponents[currentSelectedIndex] : null;

        void Start()
        {
            // 初始化连线预览
            GameObject lineObj = new GameObject("CablePreviewLine");
            lineObj.transform.SetParent(this.transform);
            previewLine = lineObj.AddComponent<LineRenderer>();
            previewLine.startWidth = 0.2f;
            previewLine.endWidth = 0.2f;
            previewLine.material = validGhostMaterial;
            previewLine.positionCount = 2;
            previewLine.enabled = false;

            // 初始化地块指示器
            if (selectionIndicatorPrefab != null)
            {
                indicatorInstance = Instantiate(selectionIndicatorPrefab, transform);
                indicatorInstance.SetActive(false); // 默认隐藏
            }
        }

        void Update()
        {
            if (Keyboard.current == null || Mouse.current == null) return;

            HandleKeyboardInput();
            HandleMouseState();

            if (isBuildingMode)
            {
                UpdateGhostPosition();
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame && !isDragging)
            {
                ProcessLeftClick();
            }
        }

        private void HandleKeyboardInput()
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (isBuildingMode)
                {
                    if (cableStartNode != null) CancelCablePlacement();
                    else ToggleBuildMode();
                }
                else if (SelectedGridPosition.HasValue || SelectedNode != null)
                {
                    ClearSelection();
                    Debug.Log("[系统] 取消选中目标/地块");
                }
            }

            if (!isBuildingMode && SelectedNode != null)
            {
                if (Keyboard.current.deleteKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame)
                {
                    DeleteSelectedNode();
                }
            }

            if (Keyboard.current.bKey.wasPressedThisFrame) ToggleBuildMode();

            if (availableComponents == null || availableComponents.Length == 0) return;
            int newIndex = currentSelectedIndex;
            if (Keyboard.current.digit1Key.wasPressedThisFrame) newIndex = 0;
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) newIndex = 1;
            else if (Keyboard.current.digit3Key.wasPressedThisFrame) newIndex = 2;
            else if (Keyboard.current.digit4Key.wasPressedThisFrame) newIndex = 3;

            if (newIndex < availableComponents.Length && newIndex != currentSelectedIndex)
            {
                currentSelectedIndex = newIndex;
                Debug.Log($"[Builder] 切换至工具: {SelectedComponent.componentName}");
                CancelCablePlacement();
                if (isBuildingMode) RefreshGhost();
            }
        }

        private void HandleMouseState()
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                mouseDownPosition = Mouse.current.position.ReadValue();
                isDragging = false;
            }

            if (Mouse.current.leftButton.isPressed)
            {
                if (Vector2.Distance(mouseDownPosition, Mouse.current.position.ReadValue()) > dragThreshold)
                    isDragging = true;
            }
        }

        private void ProcessLeftClick()
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            PowerNode clickedNode = null;

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                clickedNode = hit.collider.GetComponentInParent<PowerNode>();
            }

            Vector2Int? gridPos = GridManager.Instance.GetMouseGridPosition();
            if (clickedNode == null && gridPos.HasValue)
            {
                GridCell cell = GridManager.Instance.GetCell(gridPos.Value);
                if (cell != null && cell.IsOccupied)
                {
                    clickedNode = cell.PlacedObject.GetComponent<PowerNode>();
                }
            }

            if (!isBuildingMode)
            {
                // ---- 标准模式 ----
                SelectedNode = clickedNode;
                SelectedGridPosition = gridPos;

                if (SelectedNode != null)
                {
                    Debug.Log($"<color=cyan>[交互] 选中设施: {SelectedNode.data.componentName}</color>");
                }
                else if (SelectedGridPosition.HasValue)
                {
                    Debug.Log($"<color=cyan>[交互] 选中空地块: {SelectedGridPosition.Value}</color>");
                }

                UpdateSelectionIndicator();
            }
            else
            {
                // ---- 建造模式 ----
                ClearSelection();

                if (SelectedComponent != null)
                {
                    if (SelectedComponent.isPointToPointCable)
                    {
                        if (clickedNode != null && !clickedNode.data.isPointToPointCable)
                        {
                            if (cableStartNode == null)
                            {
                                cableStartNode = clickedNode;
                            }
                            else if (cableStartNode != clickedNode)
                            {
                                CreateCableConnection(cableStartNode, clickedNode);
                                CancelCablePlacement();
                            }
                        }
                    }
                    else
                    {
                        if (gridPos.HasValue && clickedNode == null)
                        {
                            bool canPlace = GridManager.Instance.IsCellAvailable(gridPos.Value);
                            if (canPlace)
                            {
                                Vector3 worldPos = GridManager.Instance.GridToWorldPosition(gridPos.Value);
                                PlaceStandardObject(gridPos.Value, worldPos);
                            }
                        }
                    }
                }
            }
        }

        private void ClearSelection()
        {
            SelectedNode = null;
            SelectedGridPosition = null;
            UpdateSelectionIndicator();
        }

        private void UpdateSelectionIndicator()
        {
            if (indicatorInstance == null) return;

            // 1. 如果选中了电线（非网格占用的实体），隐藏地块指示器，只依赖UI显示
            if (SelectedNode != null && SelectedNode.data.isPointToPointCable)
            {
                indicatorInstance.SetActive(false);
                return;
            }

            // 2. 如果选中了网格（不论是空地还是有建筑），显示地块指示器
            if (SelectedGridPosition.HasValue)
            {
                indicatorInstance.SetActive(true);
                Vector3 worldPos = GridManager.Instance.GridToWorldPosition(SelectedGridPosition.Value);
                // 将指示器微微抬高，防止与地面产生 Z-Fighting 穿模
                indicatorInstance.transform.position = worldPos + Vector3.up * 0.02f;
            }
            else
            {
                indicatorInstance.SetActive(false);
            }
        }

        private void UpdateGhostPosition()
        {
            if (SelectedComponent == null) return;

            if (SelectedComponent.isPointToPointCable)
            {
                if (ghostInstance != null && ghostInstance.activeSelf) ghostInstance.SetActive(false);

                if (cableStartNode != null)
                {
                    previewLine.enabled = true;
                    Vector3 startPos = cableStartNode.transform.position + Vector3.up * 1f;

                    Vector3 endPos;
                    PowerNode hoveredNode = GetHoveredNode();
                    if (hoveredNode != null) endPos = hoveredNode.transform.position + Vector3.up * 1f;
                    else
                    {
                        Vector2Int? gPos = GridManager.Instance.GetMouseGridPosition();
                        endPos = gPos.HasValue ? GridManager.Instance.GridToWorldPosition(gPos.Value) : startPos;
                    }
                    previewLine.SetPosition(0, startPos);
                    previewLine.SetPosition(1, endPos);
                }
                else previewLine.enabled = false;
            }
            else
            {
                previewLine.enabled = false;
                Vector2Int? gridPos = GridManager.Instance.GetMouseGridPosition();

                if (gridPos.HasValue)
                {
                    if (ghostInstance != null && !ghostInstance.activeSelf) ghostInstance.SetActive(true);

                    Vector3 worldPos = GridManager.Instance.GridToWorldPosition(gridPos.Value);
                    if (ghostInstance != null) ghostInstance.transform.position = worldPos;

                    bool canPlace = GridManager.Instance.IsCellAvailable(gridPos.Value);
                    if (ghostRenderers != null)
                    {
                        foreach (var renderer in ghostRenderers)
                            renderer.material = canPlace ? validGhostMaterial : invalidGhostMaterial;
                    }
                }
                else
                {
                    if (ghostInstance != null && ghostInstance.activeSelf) ghostInstance.SetActive(false);
                }
            }
        }

        private PowerNode GetHoveredNode()
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                PowerNode node = hit.collider.GetComponentInParent<PowerNode>();
                if (node != null) return node;
            }
            Vector2Int? gridPos = GridManager.Instance.GetMouseGridPosition();
            if (gridPos.HasValue)
            {
                GridCell cell = GridManager.Instance.GetCell(gridPos.Value);
                if (cell != null && cell.IsOccupied) return cell.PlacedObject.GetComponent<PowerNode>();
            }
            return null;
        }

        private void ToggleBuildMode()
        {
            if (SelectedComponent == null) return;
            isBuildingMode = !isBuildingMode;

            if (isBuildingMode)
            {
                ClearSelection(); // 进入建造模式强制清空选中
                RefreshGhost();
                Debug.Log($">>> 进入建造模式！(工具:{SelectedComponent.componentName})");
            }
            else
            {
                if (ghostInstance != null) Destroy(ghostInstance);
                CancelCablePlacement();
                Debug.Log("<<< 退出建造模式");
            }
        }

        private void RefreshGhost()
        {
            if (ghostInstance != null) Destroy(ghostInstance);
            if (SelectedComponent.isPointToPointCable) return;

            if (SelectedComponent.ghostPrefab != null)
            {
                ghostInstance = Instantiate(SelectedComponent.ghostPrefab);
                ghostRenderers = ghostInstance.GetComponentsInChildren<MeshRenderer>();
                ghostInstance.SetActive(true);
            }
        }

        private void CancelCablePlacement()
        {
            cableStartNode = null;
            previewLine.enabled = false;
        }

        private void PlaceStandardObject(Vector2Int pos, Vector3 worldPos)
        {
            if (SelectedComponent.prefab == null) return;
            GameObject newObj = Instantiate(SelectedComponent.prefab, worldPos, Quaternion.identity);
            PowerNode node = newObj.GetComponent<PowerNode>();
            if (node != null)
            {
                node.data = SelectedComponent;
                node.Initialize(pos);
            }
            GridCell cell = GridManager.Instance.GetCell(pos);
            if (cell != null) cell.PlacedObject = newObj;

            PowerSimulationSystem.Instance?.RecalculatePowerGrid();
        }

        private void CreateCableConnection(PowerNode start, PowerNode end)
        {
            if (SelectedComponent.prefab == null) return;

            GameObject cableObj = Instantiate(SelectedComponent.prefab, Vector3.zero, Quaternion.identity);
            PowerNode cableNode = cableObj.GetComponent<PowerNode>();

            if (cableNode != null)
            {
                cableNode.data = SelectedComponent;
                cableNode.InitializeAsCable(start, end);

                LineRenderer lr = cableObj.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    MeshCollider mc = cableObj.AddComponent<MeshCollider>();
                    Mesh mesh = new Mesh();
                    lr.BakeMesh(mesh, Camera.main, true);
                    mc.sharedMesh = mesh;
                }

                PowerSimulationSystem.Instance?.RecalculatePowerGrid();
            }
        }

        private void DeleteSelectedNode()
        {
            if (SelectedNode == null) return;

            Debug.Log($"<color=red>[系统] 拆除了设施: {SelectedNode.data.componentName}</color>");

            List<PowerNode> nodesToDestroy = new List<PowerNode>();
            nodesToDestroy.Add(SelectedNode);

            PowerNode[] allNodesInScene = FindObjectsByType<PowerNode>(FindObjectsSortMode.None);

            if (!SelectedNode.data.isPointToPointCable)
            {
                foreach (var connectedNode in SelectedNode.OutgoingConnections)
                {
                    if (connectedNode != null && connectedNode.data.isPointToPointCable)
                    {
                        if (!nodesToDestroy.Contains(connectedNode)) nodesToDestroy.Add(connectedNode);
                    }
                }

                foreach (var node in allNodesInScene)
                {
                    if (node != null && node.data.isPointToPointCable && node.OutgoingConnections.Contains(SelectedNode))
                    {
                        if (!nodesToDestroy.Contains(node)) nodesToDestroy.Add(node);
                    }
                }
            }

            foreach (var nodeToDestroy in nodesToDestroy)
            {
                if (!nodeToDestroy.data.isPointToPointCable)
                {
                    GridCell cell = GridManager.Instance.GetCell(nodeToDestroy.GridPosition);
                    if (cell != null && cell.PlacedObject == nodeToDestroy.gameObject)
                    {
                        cell.PlacedObject = null;
                    }
                }

                foreach (var node in allNodesInScene)
                {
                    if (node != null && node.OutgoingConnections.Contains(nodeToDestroy))
                    {
                        node.OutgoingConnections.Remove(nodeToDestroy);
                    }
                }

                if (nodeToDestroy != null && nodeToDestroy.gameObject != null)
                {
                    Destroy(nodeToDestroy.gameObject);
                }
            }

            ClearSelection(); // 拆除后自动清空选中状态
            PowerSimulationSystem.Instance?.RecalculatePowerGrid();
        }
    }
}