using UnityEngine;

namespace _Project.Gameplay.AgentSystem.FSM.Conditions
{
    [CreateAssetMenu(fileName = "StateElapsedTimeCondition", menuName = "Agent/FSM/Conditions/State Elapsed Time", order = 1)]
    public class StateElapsedTimeConditionSO : StateConditionSO
    {
        [SerializeField] private float requiredSeconds = 1f;

        public override bool CheckCondition(AIStateContext context)
        {
            return context != null && context.StateElapsedTime >= requiredSeconds;
        }
    }
}
