using System.Linq;
using _Project.Core.ModuleSystem;
using _Project.Core.PoolManaging;
using _Project.Gameplay.AgentSystem._Agent.TaskAgent;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.StatSystem;
using _Project.Gameplay.TaskSystem;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem._Agent
{
    public abstract class Agent : ModuleOwner , IPoolable
    {
        public int PoolId { get; set; }
        public string Name => gameObject.name;
        public string AgentName { get; private set; }
        public int AgentId { get; private set; }
        public AgentProfileSO CurrentProfile { get; private set; }
        public bool IsActivate { get; private set; }
        public GameObject SelfObject => gameObject;
        public IRenderer Renderer { get; private set; }
        public virtual TaskTypeEnum DefaultTaskType => TaskTypeEnum.None;

        protected override void InitializeModule()
        {
            base.InitializeModule();
            GetModule<StatManagingModule>()?.SetStatUsers(_moduleDict.Values.OfType<IAgentStatConsumer>().ToList());
            Renderer = GetModule<IRenderer>();
        }

        public void ApplyProfile(AgentProfileSO profile)
        {
            CurrentProfile = profile;
            AgentName = profile.AgentName;
            AgentId = profile.AgentId;
            gameObject.name = profile.AgentName;
            foreach (IApplyProfileModule module in _moduleDict.Values.OfType<IApplyProfileModule>())
            {
                module.ApplyProfile(profile);
            }
            GetModule<StatManagingModule>()?.RefreshStat();
        }

        public void OnPop()
        {
            IsActivate = true;
        }

        public void OnPush()
        {
            IsActivate = false;
        }
    }
}
