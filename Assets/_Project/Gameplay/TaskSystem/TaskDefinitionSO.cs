using _Project.Gameplay.AgentSystem.AgentModules;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem
{
    [CreateAssetMenu(fileName = "TaskDefinitionSO", menuName = "Task/TaskDefinition", order = 0)]
    public class TaskDefinitionSO : ScriptableObject
    {
        [field: SerializeField] public float baseWorkAmount;
        [field: SerializeField] public AnimParamSO playAnimParam;
    }
}