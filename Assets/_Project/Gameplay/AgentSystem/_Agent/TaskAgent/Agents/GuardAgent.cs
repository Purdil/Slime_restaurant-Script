using _Project.Gameplay.TaskSystem;

namespace _Project.Gameplay.AgentSystem._Agent.TaskAgent.Agents
{
    public class GuardAgent : Agent
    {
        public override TaskTypeEnum DefaultTaskType => TaskTypeEnum.Guard;
    }
}
