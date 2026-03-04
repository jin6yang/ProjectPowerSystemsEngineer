using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // 引入泛型集合
using ProjectPowerSystemsEngineer.Grid;
using ProjectPowerSystemsEngineer.Data;
using ProjectPowerSystemsEngineer.Components;

namespace ProjectPowerSystemsEngineer.Systems
{
    public class BuilderController : MonoBehaviour
    {
        [Header("Build Data (Hotbar)")]
        public ComponentData[] availableComponents;

        [Header("Ghost Materials")]
        public Material validGhostMaterial;
        public Material invalidGhostMaterial;

        [Header("Interaction Settings")]
        public float dragThreshold = 10f;

        // --- 核心交互状态 ---
        public PowerNode SelectedNode { get; private set; } // 全局当前选中的节点
        private bool isBuildingMode = false;
        private int currentSelectedIndex = 0;

        private GameObject ghostInstance;
        private MeshRenderer[] ghostRenderers;

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
            // 1. Esc 键逻辑 (分层级退出)
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (isBuildingMode)
                {
                    if (cableStartNode != null) CancelCablePlacement();
                    else ToggleBuildMode();
                }
                else if (SelectedNode != null)
                {
                    SelectedNode = null;
                    Debug.Log("[系统] 取消选中目标");
                }
            }

            // 2. Delete/Backspace 拆除逻辑 (【核心修改】：仅在 标准模式 且有选中物体时生效)
            if (!isBuildingMode && SelectedNode != null)
            {
                if (Keyboard.current.deleteKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame)
                {
                    DeleteSelectedNode();
                }
            }

            // 3. 建造模式开关 (B键)
            if (Keyboard.current.bKey.wasPressedThisFrame) ToggleBuildMode();

            // 4. 快捷栏切换 (1, 2, 3, 4)
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

        // ==================== 统一的核心点击处理 ====================
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

            // 【核心修改】：彻底隔离 标准模式 与 建造模式
            if (!isBuildingMode)
            {
                // ---- 标准模式：只处理选中逻辑 ----
                if (clickedNode != null)
                {
                    SelectedNode = clickedNode;
                    Debug.Log($"<color=cyan>[交互] 选中设施: {SelectedNode.data.componentName} | 当前输入功率: {SelectedNode.CurrentPowerInput}MW</color>");
                }
                else
                {
                    SelectedNode = null;
                    Debug.Log("[系统] 取消选中目标");
                }
            }
            else
            {
                // ---- 建造模式：只处理放置和连线逻辑，禁用选中 ----
                SelectedNode = null; // 确保在建造模式中点击不会触发任何选中状态

                if (SelectedComponent != null)
                {
                    if (SelectedComponent.isPointToPointCable)
                    {
                        // 连线模式：只有点在"不是电线"的建筑上才有效
                        if (clickedNode != null && !clickedNode.data.isPointToPointCable)
                        {
                            if (cableStartNode == null)
                            {
                                cableStartNode = clickedNode;
                                Debug.Log($"[建造] 起点已选择: {clickedNode.data.componentName}");
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
                        // 方块放置模式：必须点在空地上
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
                SelectedNode = null; // 【核心修改】：进入建造模式时，强制清空之前的选中状态
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
            }
        }

        // ==================== 智能级联拆除 ====================
        private void DeleteSelectedNode()
        {
            if (SelectedNode == null) return;

            Debug.Log($"<color=red>[系统] 拆除了设施: {SelectedNode.data.componentName}</color>");

            // 收集所有需要被一并销毁的节点（建筑本体 + 依附的电线）
            List<PowerNode> nodesToDestroy = new List<PowerNode>();
            nodesToDestroy.Add(SelectedNode);

            PowerNode[] allNodesInScene = FindObjectsByType<PowerNode>(FindObjectsSortMode.None);

            // 如果拆除的是一栋实体建筑，我们需要找出连在它身上的电线
            if (!SelectedNode.data.isPointToPointCable)
            {
                // 1. 寻找以它为【起点】的电线 (它们存在于 SelectedNode 的连线列表中)
                foreach (var connectedNode in SelectedNode.OutgoingConnections)
                {
                    if (connectedNode != null && connectedNode.data.isPointToPointCable)
                    {
                        if (!nodesToDestroy.Contains(connectedNode)) nodesToDestroy.Add(connectedNode);
                    }
                }

                // 2. 寻找以它为【终点】的电线 (遍历全图，看看谁的连线列表里包含了 SelectedNode)
                foreach (var node in allNodesInScene)
                {
                    if (node != null && node.data.isPointToPointCable && node.OutgoingConnections.Contains(SelectedNode))
                    {
                        if (!nodesToDestroy.Contains(node)) nodesToDestroy.Add(node);
                    }
                }
            }

            // 统一执行清理与销毁
            foreach (var nodeToDestroy in nodesToDestroy)
            {
                // a. 释放网格占用（仅限实体建筑）
                if (!nodeToDestroy.data.isPointToPointCable)
                {
                    GridCell cell = GridManager.Instance.GetCell(nodeToDestroy.GridPosition);
                    // 确保我们清空的是自己，而不是后来造在同一个格子的别人（双重保险）
                    if (cell != null && cell.PlacedObject == nodeToDestroy.gameObject)
                    {
                        cell.PlacedObject = null;
                    }
                }

                // b. 清除全图中其他建筑对它的"连接记忆" (打断拓扑关系)
                foreach (var node in allNodesInScene)
                {
                    if (node != null && node.OutgoingConnections.Contains(nodeToDestroy))
                    {
                        node.OutgoingConnections.Remove(nodeToDestroy);
                    }
                }

                // c. 物理销毁
                if (nodeToDestroy != null && nodeToDestroy.gameObject != null)
                {
                    Destroy(nodeToDestroy.gameObject);
                }
            }

            SelectedNode = null;

            // TODO: 调用 PowerSimulationSystem 重新计算全网电力分布
        }
    }
}