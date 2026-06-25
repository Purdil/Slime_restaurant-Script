using _Project.Gameplay.AgentSystem.AgentModules;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.Menu
{
    [CreateAssetMenu(fileName = "FoodCookingStep", menuName = "Task/Menu/FoodCookingStep", order = 1)]
    public class FoodCookingStepSO : ScriptableObject
    {
        [SerializeField] private string stepName;
        [SerializeField] private CookingStationType stationType;
        [SerializeField] private float workAmount;
        [SerializeField] private AnimParamSO playAnimParam;

        public string StepName => string.IsNullOrWhiteSpace(stepName) ? name : stepName;
        public CookingStationType StationType => stationType;
        public float WorkAmount => Mathf.Max(0f, workAmount);
        public AnimParamSO PlayAnimParam => playAnimParam;
    }
}
