using System.Collections.Generic;
using _Project.Core.CustomLogging;
using UnityEngine;

namespace _Project.Core.PoolManaging
{
    public class PoolFactory
    {
        private readonly Stack<IPoolable> _poolObjects;
        private readonly Transform _parent;
        private readonly PoolItemSO _poolItem;
        private readonly int _poolId;
        public PoolFactory(PoolItemSO poolItem,Transform parent,int poolId)
        {
            _poolObjects = new Stack<IPoolable>(poolItem.InitCount);
            _poolItem = poolItem;
            _parent = parent;
            _poolId = poolId;
            AddItem(poolItem.InitCount);
        }

        private void AddItem(int count)
        {
            for (int i = 0; i < count; i++)
            {
                IPoolable obj = Object.Instantiate(_poolItem.Prefab, _parent).GetComponent<IPoolable>();
                obj.PoolId = _poolId;
                MonoBehaviour castObj = obj as MonoBehaviour;
                if (castObj != null)
                {
                    castObj.gameObject.name = obj.Name;
                    Push(obj);
                }
                    
            }
        }

        public IPoolable Pop()
        {
            if(_poolObjects.Count <= 0)
                AddItem(1);
            IPoolable obj = _poolObjects.Pop();
            MonoBehaviour castObj = obj as MonoBehaviour;
            if (castObj != null)
            {
                castObj.gameObject.SetActive(true);
                obj.OnPop();
            }
            return obj;
        }

        public void Push(IPoolable obj)
        {
            MonoBehaviour castObj = obj as MonoBehaviour;
            if (castObj != null)
            {
                castObj.gameObject.SetActive(false);
                obj.OnPush();
                castObj.gameObject.name = _poolItem.Name;
                castObj.gameObject.transform.SetParent(_parent);
                _poolObjects.Push(obj);
            }
            else
            {
                CLog.Log("모든 IPoolable 객체는 Mono를 상속받아야 합니다^^.");
            }
           
        }
    }
}
