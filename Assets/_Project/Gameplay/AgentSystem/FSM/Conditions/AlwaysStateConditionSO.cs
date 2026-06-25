using UnityEngine;

namespace _Project.Gameplay.AgentSystem.FSM.Conditions
{
    [CreateAssetMenu(fileName = "AlwaysCondition", menuName = "Agent/FSM/Conditions/Always", order = 0)]
    public class AlwaysStateConditionSO : StateConditionSO
    {
        public override bool CheckCondition(AIStateContext context)
        {
            return true;
        }
    }
}
