using _Project.Core.PoolManaging;
using _Project.Gameplay.TaskSystem.Menu;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.OrderSystem
{
    public class FoodServingObject : MonoBehaviour, IPoolable
    {
        [SerializeField] private string poolName = "Food";

        public int PoolId { get; set; }
        public string Name => string.IsNullOrWhiteSpace(poolName) ? gameObject.name : poolName;
        public GameObject SelfObject => gameObject;
        public FoodMenuSO Menu { get; private set; }
        public OrderTicket Ticket { get; private set; }

        private Vector3 _defaultLocalScale;
        private bool _hasDefaultLocalScale;

        private void Awake()
        {
            CacheDefaultScale();
        }

        public void Bind(OrderTicket ticket)
        {
            Ticket = ticket;
            Menu = ticket?.Menu;
        }

        public void AttachTo(Transform parent, Vector3 localPosition)
        {
            if (parent == null)
            {
                return;
            }

            transform.SetParent(parent, false);
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.identity;
            RestoreDefaultScale();
        }

        public void PlaceAt(Transform parent, Vector3 worldPosition)
        {
            transform.SetParent(parent, true);
            transform.position = worldPosition;
            transform.rotation = Quaternion.identity;
            RestoreDefaultScale();
        }

        public void Clear()
        {
            Ticket = null;
            Menu = null;
        }

        public void OnPop()
        {
            CacheDefaultScale();
            RestoreDefaultScale();
        }

        public void OnPush()
        {
            Clear();
            RestoreDefaultScale();
        }

        private void CacheDefaultScale()
        {
            if (_hasDefaultLocalScale)
            {
                return;
            }

            _defaultLocalScale = transform.localScale;
            _hasDefaultLocalScale = true;
        }

        private void RestoreDefaultScale()
        {
            if (_hasDefaultLocalScale == false)
            {
                CacheDefaultScale();
            }

            transform.localScale = _defaultLocalScale;
        }
    }
}
