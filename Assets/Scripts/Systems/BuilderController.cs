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
        public PowerNode SelectedNode { get; private set; }
        public Vector2Int? SelectedGridPosition { get; private set; }

        private bool isBuildingMode = false;
        private int currentSelectedIndex = 0;

        private GameObject ghostInstance;
        private MeshRenderer[] ghostRenderers;
        private GameObject indicatorInstance;

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
            GameObject lineObj = new GameObject("CablePreviewLine");
            lineObj.transform.SetParent(this.transform);
            previewLine = lineObj.AddComponent<LineRenderer>();
            previewLine.startWidth = 0.2f;
            previewLine.endWidth = 0.2f;
            previewLine.material = validGhostMaterial;
            previewLine.positionCount = 2;
            previewLine.enabled = false;

            if (selectionIndicatorPrefab != null)
            {
                indicatorInstance = Instantiate(selectionIndicatorPrefab, transform);
                indicatorInstance.SetActive(false);
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

            // 【拆除与清理逻辑】仅在标准模式下生效
            if (!isBuildingMode && SelectedNode != null)
            {
                // 1. 完全拆除节点 (Delete / Backspace)
                if (Keyboard.current.deleteKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame)
                {
                    DeleteSelectedNode();
                }
                // 2. 【新增】仅拆除依附的电线 (- 减号键)
                else if (Keyboard.current.minusKey.wasPressedThisFrame || Keyboard.current.numpadMinusKey.wasPressedThisFrame)
                {
                    DeleteAttachedCablesOnly();
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

            if (SelectedNode != null && SelectedNode.data.isPointToPointCable)
            {
                indicatorInstance.SetActive(false);
                return;
            }

            if (SelectedGridPosition.HasValue)
            {
                indicatorInstance.SetActive(true);
                Vector3 worldPos = GridManager.Instance.GridToWorldPosition(SelectedGridPosition.Value);
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
                ClearSelection();
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

        // ==================== 智能拆除系统 ====================

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

            ExecuteDestruction(nodesToDestroy, allNodesInScene);
        }

        // 【新增】单独一键拆除关联的所有电线
        private void DeleteAttachedCablesOnly()
        {
            // 如果没选中东西，或者选中的本身就是电线，则不生效
            if (SelectedNode == null || SelectedNode.data.isPointToPointCable) return;

            Debug.Log($"<color=yellow>[系统] 快速剥离了连接至 {SelectedNode.data.componentName} 的所有电线</color>");

            List<PowerNode> cablesToDestroy = new List<PowerNode>();
            PowerNode[] allNodesInScene = FindObjectsByType<PowerNode>(FindObjectsSortMode.None);

            // 1. 寻找从该建筑连出去的电线
            foreach (var connectedNode in SelectedNode.OutgoingConnections)
            {
                if (connectedNode != null && connectedNode.data.isPointToPointCable)
                {
                    if (!cablesToDestroy.Contains(connectedNode)) cablesToDestroy.Add(connectedNode);
                }
            }

            // 2. 寻找连入该建筑的电线
            foreach (var node in allNodesInScene)
            {
                if (node != null && node.data.isPointToPointCable && node.OutgoingConnections.Contains(SelectedNode))
                {
                    if (!cablesToDestroy.Contains(node)) cablesToDestroy.Add(node);
                }
            }

            ExecuteDestruction(cablesToDestroy, allNodesInScene, keepSelected: true);
        }

        // 将共用的销毁逻辑提取出来
        private void ExecuteDestruction(List<PowerNode> nodesToDestroy, PowerNode[] allNodesInScene, bool keepSelected = false)
        {
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

            if (!keepSelected)
            {
                ClearSelection();
            }

            PowerSimulationSystem.Instance?.RecalculatePowerGrid();
        }
    }
}