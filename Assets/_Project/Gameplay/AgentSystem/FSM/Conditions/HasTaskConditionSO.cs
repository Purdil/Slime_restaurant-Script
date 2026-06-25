using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.TaskSystem;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.FSM.Conditions
{
    [CreateAssetMenu(fileName = "HasTaskConditionSO", menuName = "Agent/FSM/Conditions/HasTask", order = 0)]
    public class HasTaskConditionSO : StateConditionSO
    {
        public override bool CheckCondition(AIStateContext context)
        {
            TaskModule taskModule = context.OwnerAgent.GetModule<TaskModule>();
            return  taskModule.HasTask();
        }
    }
}