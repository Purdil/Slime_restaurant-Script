using UnityEngine;

namespace _Project.Gameplay.AgentSystem.FSM.Conditions
{
    public abstract class StateConditionSO : ScriptableObject
    {
        public abstract bool CheckCondition(AIStateContext context);
    }
}
