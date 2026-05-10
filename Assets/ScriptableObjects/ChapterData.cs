using UnityEngine;

namespace ProjectPowerSystemsEngineer.Data
{
    [System.Serializable]
    public class LevelInfo
    {
        [Tooltip("鼠标悬停时，在下方显示的关卡中文名称")]
        public string levelName;

#if UNITY_EDITOR
        [Tooltip("直接从 Project 窗口拖入关卡场景文件 (.unity)")]
        public UnityEditor.SceneAsset sceneFile;
#endif

        [HideInInspector]
        public string scenePath;
    }

    /// <summary>
    /// 章节关卡数据集 (ScriptableObject)
    /// 你可以创建多份此资产，例如“第一章数据”、“第二章数据”
    /// </summary>
    [CreateAssetMenu(fileName = "New Chapter Data", menuName = "ProjectPowerSystemsEngineer/Chapter Data")]
    public class ChapterData : ScriptableObject
    {
        [Header("章节基本信息")]
        public string chapterName = "新章节";

        [Header("关卡列表")]
        [Tooltip("在这里添加本章节包含的所有关卡")]
        public LevelInfo[] levels;

#if UNITY_EDITOR
        // 当你在 Inspector 中修改数据时，自动提取场景的真实路径
        private void OnValidate()
        {
            if (levels != null)
            {
                for (int i = 0; i < levels.Length; i++)
                {
                    if (levels[i] != null && levels[i].sceneFile != null)
                    {
                        levels[i].scenePath = UnityEditor.AssetDatabase.GetAssetPath(levels[i].sceneFile);
                    }
                    else if (levels[i] != null)
                    {
                        levels[i].scenePath = "";
                    }
                }
            }
        }
#endif
    }
}