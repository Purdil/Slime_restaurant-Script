using _Project.Gameplay.TaskSystem;

namespace _Project.Gameplay.AgentSystem._Agent.TaskAgent.Agents
{
    public class CleanerAgent : Agent
    {
        public override TaskTypeEnum DefaultTaskType => TaskTypeEnum.Cleaner;
    }
}
