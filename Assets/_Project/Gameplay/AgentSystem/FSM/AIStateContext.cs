using _Project.Gameplay.AgentSystem._Agent;

namespace _Project.Gameplay.AgentSystem.FSM
{
    public sealed class AIStateContext
    {
        public Agent OwnerAgent { get; }
        public StateMachine StateMachine { get; }
        public AgentState CurrentState { get; private set; }
        public StateSO CurrentStateData { get; private set; }
        public float StateElapsedTime { get; private set; }

        public AIStateContext(Agent ownerAgent, StateMachine stateMachine)
        {
            OwnerAgent = ownerAgent;
            StateMachine = stateMachine;
        }

        public void SetCurrentState(AgentState currentState, StateSO currentStateData)
        {
            CurrentState = currentState;
            CurrentStateData = currentStateData;
            StateElapsedTime = 0f;
        }

        public void Tick(float deltaTime)
        {
            StateElapsedTime += deltaTime;
        }
    }
}
