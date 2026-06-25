using System;
using _Project.Gameplay.AgentSystem.FSM.Conditions;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.FSM.Transitions
{
    [Serializable]
    public class AIStateTransition
    {
        [SerializeField] private string transitionName;
        [SerializeField] private StateSO nextState;
        [SerializeField] private AIConditionMatchMode conditionMatchMode = AIConditionMatchMode.All;
        [SerializeField] private StateConditionSO[] conditions = Array.Empty<StateConditionSO>();

        public string TransitionName => transitionName;
        public StateSO NextState => nextState;
        public StateConditionSO[] Conditions => conditions;

        public bool CanTransit(AIStateContext context)
        {
            if (nextState == null)
            {
                return false;
            }

            if (conditions == null || conditions.Length == 0)
            {
                return true;
            }

            if (conditionMatchMode == AIConditionMatchMode.Any)
            {
                return HasAnyValidCondition(context);
            }

            return HasAllValidConditions(context);
        }

        private bool HasAllValidConditions(AIStateContext context)
        {
            foreach (StateConditionSO condition in conditions)
            {
                if (condition == null || condition.CheckCondition(context) == false)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasAnyValidCondition(AIStateContext context)
        {
            foreach (StateConditionSO condition in conditions)
            {
                if (condition != null && condition.CheckCondition(context))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
