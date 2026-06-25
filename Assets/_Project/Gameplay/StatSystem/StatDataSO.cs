using UnityEngine;

namespace _Project.Gameplay.StatSystem
{
    [CreateAssetMenu(fileName = "StatData", menuName = "Stat/StatSO", order = 0)]
    public class StatDataSO : ScriptableObject
    {
        [field: SerializeField] public StatTypeSO StatType { get; private set; }
        [field: SerializeField] public float Value { get; private set; }
    }
}
