using _Project.Gameplay.AgentSystem.AgentModules;
using _Project.Gameplay.AgentSystem.FSM.Transitions;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.FSM
{
    [CreateAssetMenu(fileName = "State data", menuName = "Agent/State data", order = 0)]
    public class StateSO : ScriptableObject
    {
        public string stateName;
        public string className;
        public int assetIndex;
        [SerializeField] private AIStateTransition[] transitions;

        public AIStateTransition[] Transitions => transitions;
    }
}
