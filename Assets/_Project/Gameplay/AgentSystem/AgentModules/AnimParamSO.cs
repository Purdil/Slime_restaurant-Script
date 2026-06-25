using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules
{
    [CreateAssetMenu(fileName = "AnimParam", menuName = "AnimParamSO", order = 0)]
    public class AnimParamSO : ScriptableObject
    {
        [field: SerializeField] public string ParamName { get; private set; }
        [field: SerializeField] public int ParamHash { get; private set; }

        private void OnValidate()
        {
            ParamHash = Animator.StringToHash(ParamName);
        }
    }
}