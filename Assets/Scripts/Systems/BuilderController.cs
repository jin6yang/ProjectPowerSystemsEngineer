using ProjectPowerSystemsEngineer.Components;
using ProjectPowerSystemsEngineer.Data;
using ProjectPowerSystemsEngineer.Grid;
using ProjectPowerSystemsEngineer.Simulation;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectPowerSystemsEngineer.Systems
{
    public class BuilderController : MonoBehaviour
    {
        [HideInInspector]
        public ComponentData[] availableComponents;

        [Header("Ghost Materials")]
        public Material validGhostMaterial;
        public Material invalidGhostMaterial;

        [Header("UI & Feedback")]
        public GameObject selectionIndicatorPrefab;

        [Header("Interaction Settings")]
        public float dragThreshold = 10f;

        // 【新增】定义哪些 Layer 会阻挡电线穿透（比如你的高墙 Layer）
        [Tooltip("设置会阻挡电线的物理层级 (例如 Obstacle)")]
        public LayerMask cableObstacleLayer;

        public PowerNode SelectedNode { get; private set; }
        public Vector2Int? SelectedGridPosition { get; private set; }

        public bool IsBuildingMode => isBuildingMode;
        private bool isBuildingMode = false;
        private int currentSelectedIndex = 0;

        private GameObject ghostInstance;
        private MeshRenderer[] ghostRenderers;
        private GameObject indicatorInstance;

        private Vector2 mouseDownPosition;
        private bool isDragging = false;

        private PowerNode cableStartNode = null;
        private LineRenderer previewLine;

        // 记录当前电线路径是否畅通
        private bool isCablePathValid = true;

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
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
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
            bool isCancelPressed = false;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) isCancelPressed = true;
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) isCancelPressed = true;

            if (isCancelPressed)
            {
                if (isBuildingMode)
                {
                    if (cableStartNode != null) CancelCablePlacement();
                    else ToggleBuildMode();
                }
                else if (SelectedGridPosition.HasValue || SelectedNode != null)
                {
                    ClearSelection();
                }
            }

            if (!isBuildingMode && SelectedNode != null)
            {
                if (Keyboard.current.deleteKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame) DeleteSelectedNode();
                else if (Keyboard.current.minusKey.wasPressedThisFrame || Keyboard.current.numpadMinusKey.wasPressedThisFrame) DeleteAttachedCablesOnly();
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

        // 【新增】核心射线检测逻辑，判断两点之间的半空中是否有物理高墙遮挡
        private bool CheckCablePathClear(Vector3 start, Vector3 end)
        {
            Vector3 direction = end - start;
            float distance = direction.magnitude;

            // 发射一条从起点到终点的射线，只检测 cableObstacleLayer 指定的层级
            if (Physics.Raycast(start, direction.normalized, distance, cableObstacleLayer))
            {
                return false; // 撞到高墙障碍物，路径被阻挡！
            }
            return true; // 畅通无阻
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
                if (cell != null && cell.IsOccupied && cell.PlacedObject != null)
                {
                    clickedNode = cell.PlacedObject.GetComponent<PowerNode>();
                }
            }

            if (!isBuildingMode)
            {
                SelectedNode = clickedNode;
                SelectedGridPosition = gridPos;
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
                                // 【拦截】只有当路径畅通时，才允许建立连接！
                                if (isCablePathValid)
                                {
                                    CreateCableConnection(cableStartNode, clickedNode);
                                    CancelCablePlacement();
                                }
                                else
                                {
                                    Debug.LogWarning("[系统] 电线被障碍物阻挡，无法连接！请绕道！");
                                }
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

            if (SelectedNode != null && SelectedNode.data != null && SelectedNode.data.isPointToPointCable)
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
                        endPos = gPos.HasValue ? GridManager.Instance.GridToWorldPosition(gPos.Value) + Vector3.up * 1f : startPos;
                    }

                    previewLine.SetPosition(0, startPos);
                    previewLine.SetPosition(1, endPos);

                    // 【视线检测】实时更新预览线的颜色
                    isCablePathValid = CheckCablePathClear(startPos, endPos);
                    previewLine.material = isCablePathValid ? validGhostMaterial : invalidGhostMaterial;
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
                if (cell != null && cell.IsOccupied && cell.PlacedObject != null) return cell.PlacedObject.GetComponent<PowerNode>();
            }
            return null;
        }

        public void EnterBuildModeFromUI(int index)
        {
            if (availableComponents == null || index < 0 || index >= availableComponents.Length) return;
            currentSelectedIndex = index;
            if (!isBuildingMode) isBuildingMode = true;
            ClearSelection();
            RefreshGhost();
            CancelCablePlacement();
        }

        private void ToggleBuildMode()
        {
            if (SelectedComponent == null) return;
            isBuildingMode = !isBuildingMode;
            if (isBuildingMode)
            {
                ClearSelection();
                RefreshGhost();
            }
            else
            {
                if (ghostInstance != null) Destroy(ghostInstance);
                CancelCablePlacement();
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
            List<PowerNode> nodesToDestroy = new List<PowerNode>();
            nodesToDestroy.Add(SelectedNode);
            PowerNode[] allNodesInScene = FindObjectsByType<PowerNode>(FindObjectsSortMode.None);

            if (!SelectedNode.data.isPointToPointCable)
            {
                foreach (var connectedNode in SelectedNode.OutgoingConnections)
                {
                    if (connectedNode != null && connectedNode.data != null && connectedNode.data.isPointToPointCable)
                    {
                        if (!nodesToDestroy.Contains(connectedNode)) nodesToDestroy.Add(connectedNode);
                    }
                }
                foreach (var node in allNodesInScene)
                {
                    if (node != null && node.data != null && node.data.isPointToPointCable && node.OutgoingConnections.Contains(SelectedNode))
                    {
                        if (!nodesToDestroy.Contains(node)) nodesToDestroy.Add(node);
                    }
                }
            }
            ExecuteDestruction(nodesToDestroy, allNodesInScene);
        }

        private void DeleteAttachedCablesOnly()
        {
            if (SelectedNode == null || SelectedNode.data.isPointToPointCable) return;
            List<PowerNode> cablesToDestroy = new List<PowerNode>();
            PowerNode[] allNodesInScene = FindObjectsByType<PowerNode>(FindObjectsSortMode.None);

            foreach (var connectedNode in SelectedNode.OutgoingConnections)
            {
                if (connectedNode != null && connectedNode.data != null && connectedNode.data.isPointToPointCable)
                {
                    if (!cablesToDestroy.Contains(connectedNode)) cablesToDestroy.Add(connectedNode);
                }
            }
            foreach (var node in allNodesInScene)
            {
                if (node != null && node.data != null && node.data.isPointToPointCable && node.OutgoingConnections.Contains(SelectedNode))
                {
                    if (!cablesToDestroy.Contains(node)) cablesToDestroy.Add(node);
                }
            }
            ExecuteDestruction(cablesToDestroy, allNodesInScene, keepSelected: true);
        }

        private void ExecuteDestruction(List<PowerNode> nodesToDestroy, PowerNode[] allNodesInScene, bool keepSelected = false)
        {
            foreach (var nodeToDestroy in nodesToDestroy)
            {
                if (nodeToDestroy.data != null && !nodeToDestroy.data.isPointToPointCable)
                {
                    GridCell cell = GridManager.Instance.GetCell(nodeToDestroy.GridPosition);
                    if (cell != null && cell.PlacedObject == nodeToDestroy.gameObject) cell.PlacedObject = null;
                }

                foreach (var node in allNodesInScene)
                {
                    if (node != null && node.OutgoingConnections.Contains(nodeToDestroy)) node.OutgoingConnections.Remove(nodeToDestroy);
                }
                if (nodeToDestroy != null && nodeToDestroy.gameObject != null) Destroy(nodeToDestroy.gameObject);
            }
            if (!keepSelected) ClearSelection();
            PowerSimulationSystem.Instance?.RecalculatePowerGrid();
        }
    }
}