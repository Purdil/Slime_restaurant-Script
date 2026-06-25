using System;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem._Agent.TaskAgent;
using _Project.Gameplay.TaskSystem;
using JetBrains.Annotations;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentGenerateSystem.GenerateRequestStructs
{
    public struct GenerateAgentRequest
    {
        public AgentProfileSO profile;
        public Vector3 spawnPosition;
        [CanBeNull] public ITask initTask;
        [CanBeNull] public Action<Agent> callback;
        public bool dontUseSpawnPosition;

        public GenerateAgentRequest(AgentProfileSO profile,bool dontUseSpawnPosition, Vector3 spawnPosition = default,
            ITask initTask = null, Action<Agent> callback = null)
        {
            this.profile = profile;
            this.spawnPosition = spawnPosition;
            this.initTask = initTask;
            this.callback = callback;
            this.dontUseSpawnPosition = dontUseSpawnPosition;
        }

    }
}