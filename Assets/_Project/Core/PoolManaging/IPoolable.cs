using UnityEngine;

namespace _Project.Core.PoolManaging
{
    public interface IPoolable
    {
        public int PoolId { get; set; }
        public string Name { get; }
        public GameObject SelfObject { get; }
        public void OnPop();
        public void OnPush();
    }
}