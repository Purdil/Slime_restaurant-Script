using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;

namespace _Project.Gameplay.AgentSystem.FSM
{
    public abstract class AgentState
    {
        protected readonly Agent _agent;
        protected readonly IRenderer _renderer;

        public AgentState(Agent agent)
        {
            _agent = agent;
          
            _renderer = agent.GetModule<IRenderer>();
        }

        public virtual void Enter()
        {
        }

        public virtual void Update() {}
        public virtual void Exit() {}
    }
}