using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.FSM.Conditions
{
    [CreateAssetMenu(fileName = "CurTaskEndCondition", menuName = "Agent/FSM/Conditions/CurTaskEnd", order = 0)]
    public class CurTaskEndConditionSO : StateConditionSO
    {
        public override bool CheckCondition(AIStateContext context)
        {
            return !context.OwnerAgent.GetModule<TaskModule>().IsTasking;
        }
    }
}