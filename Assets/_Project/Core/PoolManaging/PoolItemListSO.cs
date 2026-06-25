using System.Collections.Generic;
using UnityEngine;

namespace _Project.Core.PoolManaging
{
    [CreateAssetMenu(fileName = "PoolItemListSO", menuName = "Pool/PoolItemListSO", order = 0)]
    public class PoolItemListSO : ScriptableObject
    {
        [field: SerializeField] public List<PoolItemSO> PoolItemSos { get; private set; }
        
    }
}