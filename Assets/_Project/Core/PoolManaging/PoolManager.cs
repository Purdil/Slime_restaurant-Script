using System.Collections.Generic;
using _Project.Core.CustomLogging;
using _Project.Core.Manager;
using UnityEngine;

namespace _Project.Core.PoolManaging
{
    public class PoolManager : MonoSingleton<PoolManager>
    {
        [SerializeField] private List<PoolItemListSO> poolItemListSos = new();
        private readonly Dictionary<int,PoolFactory> _poolFactoryDictionary = new();
        private readonly Dictionary<string,Transform> _poolParentDictionary = new();

        protected override void Awake()
        {
            base.Awake();
            InitFactory();
        }

        private void InitFactory()
        {
            foreach (PoolItemListSO itemListSo in poolItemListSos)
            {
                foreach (PoolItemSO poolItemSo in itemListSo.PoolItemSos)
                {
                    if (!_poolParentDictionary.ContainsKey(poolItemSo.Name))
                    {
                        _poolParentDictionary[poolItemSo.Name] = new GameObject(poolItemSo.Name + "Pool").transform;
                    }
                    int poolId = poolItemSo.GetInstanceID();
                    _poolFactoryDictionary[poolId] = new PoolFactory(poolItemSo,_poolParentDictionary[poolItemSo.Name],poolId);
                        
                }
            }
        }

        public IPoolable Pop(PoolItemSO poolItemSO)
        {
            if (poolItemSO == null)
            {
                CLog.LogWarning("PoolItemSO가 비어 있어 Pop할 수 없습니다.");
                return null;
            }

            int id = poolItemSO.GetInstanceID();
            if (_poolFactoryDictionary.TryGetValue(id, out PoolFactory poolFactory) == false)
            {
                poolFactory = CreateFactory(poolItemSO, id);
            }

            if (poolFactory == null)
            {
                return null;
            }

            IPoolable obj = poolFactory.Pop();
            (obj as MonoBehaviour)?.transform.SetParent(null);
            return obj;
        }
        

        public void Push(IPoolable poolable)
        {
            if (_poolFactoryDictionary.TryGetValue(poolable.PoolId, out PoolFactory poolFactory))
            {
                poolFactory.Push(poolable);
                return;
            }
            CLog.LogWarning($"{poolable.Name} 풀이 존재하지 않습니다.");
        }

        private PoolFactory CreateFactory(PoolItemSO poolItemSO, int poolId)
        {
            if (poolItemSO.Prefab == null)
            {
                CLog.LogWarning($"{poolItemSO.Name} 풀 프리팹이 비어 있습니다.");
                return null;
            }

            if (!_poolParentDictionary.ContainsKey(poolItemSO.Name))
            {
                _poolParentDictionary[poolItemSO.Name] = new GameObject(poolItemSO.Name + "Pool").transform;
            }

            PoolFactory poolFactory = new PoolFactory(
                poolItemSO,
                _poolParentDictionary[poolItemSO.Name],
                poolId);
            _poolFactoryDictionary[poolId] = poolFactory;
            return poolFactory;
        }
    }
}
                    
                    
