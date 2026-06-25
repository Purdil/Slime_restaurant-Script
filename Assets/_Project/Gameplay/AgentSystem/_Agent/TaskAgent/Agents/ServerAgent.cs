using _Project.Gameplay.AgentSystem.AgentModules;
using _Project.Gameplay.TaskSystem;

namespace _Project.Gameplay.AgentSystem._Agent.TaskAgent.Agents
{
    public class ServerAgent : Agent
    {
        public override TaskTypeEnum DefaultTaskType => TaskTypeEnum.Server;

        protected override void Awake()
        {
            if (GetComponentInChildren<ServerGuidanceModule>() == null)
            {
                gameObject.AddComponent<ServerGuidanceModule>();
            }

            if (GetComponentInChildren<ServerFoodCarryModule>() == null)
            {
                gameObject.AddComponent<ServerFoodCarryModule>();
            }

            base.Awake();
        }
    }
}
