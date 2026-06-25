using _Project.Core.CustomLogging;
using UnityEngine;

namespace _Project.Core.PoolManaging
{
    [CreateAssetMenu(fileName = "PoolItemSO", menuName = "Pool/PoolItem", order = 0)]
    public class PoolItemSO : ScriptableObject
    {
        [field:SerializeField] public string Name {get; private set;}
        [field: SerializeField] public int InitCount {get; private set;}
        [field: SerializeField] public GameObject Prefab { get; private set; }

        private void OnValidate()
        {
            if(Prefab == null) 
                return;
           
            IPoolable poolable = Prefab.GetComponentInChildren<IPoolable>();
            if (poolable == null)
            {
                Prefab = null;
                CLog.LogError("야 이건 풀이 가능한 객체가 아니잖아 멍청아");
            }
            else
            {
                Name = poolable.Name;
            }
        }
    }
}