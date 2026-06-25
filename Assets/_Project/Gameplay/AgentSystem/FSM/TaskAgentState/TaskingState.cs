using _Project.Core.CustomLogging;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;

namespace _Project.Gameplay.AgentSystem.FSM.TaskAgentState
{
    public class TaskingState : AgentState
    {
        private TaskModule _taskModule;
        public TaskingState(Agent agent) : base(agent)
        {
            _taskModule = agent.GetModule<TaskModule>();
        }

        public override void Enter()
        {
            base.Enter();
            _taskModule.ExecuteTask();
            //끝나는 걸 구독할 이유가 없음. 컨디션 기반의 자동 Idle 전환이 진행됨.
        }
    }
}
