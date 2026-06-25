using System;
using System.Collections.Generic;
using _Project.Core.Manager;
using _Project.Core.PoolManaging;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem._Agent.TaskAgent;
using _Project.Gameplay.AgentSystem.AgentGenerateSystem;
using _Project.Gameplay.AgentSystem.AgentGenerateSystem.GenerateRequestStructs;
using _Project.Gameplay.TaskSystem;
using Alchemy.Serialization;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Project.Gameplay.AgentSystem
{
    [AlchemySerialize]
    public partial class AgentHireManager : MonoSingleton<AgentHireManager>
    {
        [SerializeField] private Transform[] generatePositions;
        [AlchemySerializeField,NonSerialized] private Dictionary<TaskTypeEnum,GenerateAgentChannel> _generators = new();
        
        private Dictionary<int,Agent> _hiredAgents = new();
        //골드 체크와 지불은 UI쪽에서
        public void HireAgent(AgentProfileSO hireAgent)
        {
            if (_generators.TryGetValue(hireAgent.TaskType, out GenerateAgentChannel generator) && _hiredAgents.ContainsKey(hireAgent.AgentId))
            {
                generator.Raise(new GenerateAgentRequest
                {
                    profile = hireAgent,
                    spawnPosition = generatePositions[Random.Range(0, generatePositions.Length)].position,
                    callback = RegistHiredAgent
                });
            }
        }

        private void RegistHiredAgent(Agent obj)
        {
            _hiredAgents.Add(obj.AgentId,obj);
        }

        // 지불은 UI에서
        public void FireAgent(AgentProfileSO hireAgent)
        {
            if (_hiredAgents.TryGetValue(hireAgent.AgentId, out Agent agent))
            {
                PoolManager.Instance.Push(agent);
            }
        }
        
    }
}