using _Project.Core.ModuleSystem;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.FSM;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules.CommonModule
{
    public class AgentAIFSMModule : MonoBehaviour, IModule
    {
        [SerializeField] private StateListSO stateList;

        public StateMachine StateMachine { get; private set; }

        private Agent _owner;

        private void Update()
        {
            StateMachine?.UpdateMachine();
        }

        public void Initialize(ModuleOwner moduleOwner)
        {
            _owner = moduleOwner as Agent;
            Debug.Assert(_owner != null, "AgentAIFSMModule은 Agent 타입의 ModuleOwner에서만 사용할 수 있습니다.");

            if (_owner == null || stateList == null)
            {
                return;
            }

            StateMachine = new StateMachine(_owner, stateList);
        }

        public void ChangeState(StateSO state)
        {
            StateMachine?.ChangeState(state);
        }

        public void ChangeState(int stateIndex)
        {
            StateMachine?.ChangeState(stateIndex);
        }
    }
}
