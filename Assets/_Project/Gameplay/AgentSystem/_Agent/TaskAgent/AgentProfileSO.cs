using System;
using System.Collections.Generic;
using _Project.Gameplay.AgentSystem.AgentGenerateSystem.GenerateRequestStructs;
using _Project.Gameplay.AgentSystem.Astar;
using _Project.Gameplay.StatSystem;
using _Project.Gameplay.TaskSystem;
using Alchemy.Serialization;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem._Agent.TaskAgent
{
    [AlchemySerialize]
    [CreateAssetMenu(fileName = "AgentProfile", menuName = "Agent/AgentProfileSO", order = 0)]
    public partial class AgentProfileSO : ScriptableObject
    {
        [field: SerializeField] public string AgentName { get; set; }
        [field: SerializeField] public TaskTypeEnum TaskType { get; set; }
        [field: SerializeField] public int AgentId { get; set; }
        [field: SerializeField] public RuntimeAnimatorController AnimationController { get; set; }
        [field: SerializeField] public StatDataSO[] StatData { get; set; }
        public IReadOnlyDictionary<StatTypeSO, float> ModifyInitStats
        {
            get
            {
                EnsureModifyInitStats();
                return modifyInitStats;
            }
        }

        [AlchemySerializeField, NonSerialized] private Dictionary<StatTypeSO, float> modifyInitStats = new();

        public bool GetModifyInitStat(StatTypeSO statType, out float returnValue)
        {
            if (statType == null || modifyInitStats == null)
            {
                returnValue = 0;
                return false;
            }
            
            modifyInitStats.TryGetValue(statType, out float value);
            returnValue = value;
            return true;
        }

        public void SetModifyInitStat(StatTypeSO statType, float value)
        {
            if (statType == null)
            {
                return;
            }

            EnsureModifyInitStats();
            modifyInitStats[statType] = value;
        }

        public bool RemoveModifyInitStat(StatTypeSO statType)
        {
            if (statType == null || modifyInitStats == null)
            {
                return false;
            }

            return modifyInitStats.Remove(statType);
        }

        public void ClearModifyInitStats()
        {
            EnsureModifyInitStats();
            modifyInitStats.Clear();
        }

        public bool ContainsModifyInitStat(StatTypeSO statType)
        {
            return statType != null && modifyInitStats != null && modifyInitStats.ContainsKey(statType);
        }

        private void EnsureModifyInitStats()
        {
            modifyInitStats ??= new Dictionary<StatTypeSO, float>();
        }
    }
}
