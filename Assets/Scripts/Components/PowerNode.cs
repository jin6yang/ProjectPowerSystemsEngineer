using UnityEngine;
using ProjectPowerSystemsEngineer.Data;
using ProjectPowerSystemsEngineer.Grid;

namespace ProjectPowerSystemsEngineer.Components
{
    /// <summary>
    /// 所有电力网格组件的绝对基类。
    /// 无论是发电机、电线还是 AI 核心，都必须挂载此脚本（或其子类）。
    /// </summary>
    public class PowerNode : MonoBehaviour
    {
        [Header("Data Profile")]
        [Tooltip("此组件的配置数据源")]
        public ComponentData data;

        // --- 运行时状态 (Runtime State) ---
        public Vector2Int GridPosition { get; private set; }

        // 动态电力数值
        public float CurrentPowerInput { get; private set; }
        public float CurrentStability { get; private set; }

        public bool IsOverloaded { get; private set; }
        public bool IsOnFire { get; private set; }

        /// <summary>
        /// 当 BuilderController 将其放置到网格上时，调用此方法进行初始化
        /// </summary>
        public virtual void Initialize(Vector2Int pos)
        {
            GridPosition = pos;
            CurrentPowerInput = 0f;
            CurrentStability = 100f; // 默认 100% 纯净度
            IsOverloaded = false;

            Debug.Log($"[PowerNode] {data?.componentName} 已在 {pos} 初始化。");
        }

        /// <summary>
        /// 接收电力输入（由未来的 PowerSimulationSystem 每帧或每次拓扑改变时调用）
        /// </summary>
        public virtual void ReceivePower(float power, float stability)
        {
            CurrentPowerInput = power;
            CurrentStability = stability;

            CheckOverload();
        }

        /// <summary>
        /// 过载检测逻辑
        /// </summary>
        protected virtual void CheckOverload()
        {
            if (data == null) return;

            // GDD 核心逻辑：输入功率 > 承载上限，导致过载爆炸
            if (CurrentPowerInput > data.maxPowerCapacity && !IsOverloaded)
            {
                TriggerOverload();
            }
        }

        protected virtual void TriggerOverload()
        {
            IsOverloaded = true;
            Debug.LogWarning($"[警告] {data.componentName} 发生过载！输入:{CurrentPowerInput}MW / 上限:{data.maxPowerCapacity}MW");

            // TODO: 播放爆炸特效，修改网格状态为着火，摧毁此 GameObject
        }

        // ==========================================
        // 供外部获取该节点“输出能力”的接口
        // ==========================================

        /// <summary>
        /// 获取当前节点的实际输出功率
        /// </summary>
        public virtual float GetPowerOutput()
        {
            if (IsOverloaded || IsOnFire) return 0f;

            // 如果是发电机，输出等于自身发电量
            if (data.category == ComponentCategory.Generation)
            {
                return data.powerGeneration;
            }

            // 如果是传输线缆，输出等于 (输入 - 自身消耗)
            return Mathf.Max(0f, CurrentPowerInput - data.powerConsumption);
        }

        /// <summary>
        /// 获取当前节点输出的电力稳定性
        /// </summary>
        public virtual float GetStabilityOutput()
        {
            if (IsOverloaded || IsOnFire) return 0f;

            // 加上自身对稳定性的影响修饰符，并限制在 0-100 之间
            return Mathf.Clamp(CurrentStability + data.stabilityModifier, 0f, 100f);
        }
    }
}