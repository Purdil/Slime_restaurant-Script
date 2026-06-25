using _Project.Core.ModuleSystem;
using _Project.Core.PoolManaging;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.TaskSystem.OrderSystem;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules
{
    public class ServerFoodCarryModule : MonoBehaviour, IModule
    {
        [Header("Food Carry Position")]
        [SerializeField] private Transform carryAnchor;
        [SerializeField] private Vector3 carryLocalOffset = new(0f, 1.25f, 0f);
#if UNITY_EDITOR
        [SerializeField] private bool isDrawCarryPositionGizmo = true;
        [SerializeField] private Color carryPositionGizmoColor = new(1f, 0.6f, 0.15f, 0.9f);
        [SerializeField] private float carryPositionGizmoRadius = 0.12f;
#endif

        public bool HasFood => _carriedFood != null;
        public FoodServingObject CarriedFood => _carriedFood;

        private Agent _owner;
        private FoodServingObject _carriedFood;

        private void OnDisable()
        {
            ClearCarriedFood(true);
        }

        public void Initialize(ModuleOwner moduleOwner)
        {
            _owner = moduleOwner as Agent;
            Debug.Assert(_owner != null, "ServerFoodCarryModule은 Agent 타입의 ModuleOwner에서만 사용할 수 있습니다.");
        }

        public bool TryCarry(OrderTicket ticket)
        {
            if (_owner == null || ticket == null || ticket.PreparedFood == null || _carriedFood != null)
            {
                return false;
            }

            _carriedFood = ticket.PreparedFood;
            ticket.SetFoodCarrier(this);
            _carriedFood.AttachTo(GetCarryAnchor(), carryLocalOffset);
            return true;
        }

        public FoodServingObject ReleaseForTable(OrderTicket ticket)
        {
            if (_carriedFood == null || ticket != null && ticket.FoodCarrier != this)
            {
                return null;
            }

            FoodServingObject food = _carriedFood;
            _carriedFood = null;
            ticket?.ClearFoodCarrier(this);
            return food;
        }

        public void ClearCarriedFood(bool shouldReturnToPool)
        {
            if (_carriedFood == null)
            {
                return;
            }

            FoodServingObject food = _carriedFood;
            OrderTicket ticket = food.Ticket;
            _carriedFood = null;
            if (shouldReturnToPool)
            {
                ticket?.ClearPreparedFood(food);
            }
            else
            {
                ticket?.ClearFoodCarrier(this);
            }

            if (shouldReturnToPool && PoolManager.IsNullInstance == false)
            {
                PoolManager.Instance.Push(food);
                return;
            }

            if (shouldReturnToPool)
            {
                food.Clear();
                food.gameObject.SetActive(false);
            }
        }

        private Transform GetCarryAnchor()
        {
            return carryAnchor == null ? transform : carryAnchor;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            carryPositionGizmoRadius = Mathf.Max(0.01f, carryPositionGizmoRadius);
        }

        private void OnDrawGizmosSelected()
        {
            if (isDrawCarryPositionGizmo == false)
            {
                return;
            }

            Transform targetAnchor = GetCarryAnchor();
            Vector3 carryPosition = targetAnchor.TransformPoint(carryLocalOffset);
            Gizmos.color = carryPositionGizmoColor;
            Gizmos.DrawLine(targetAnchor.position, carryPosition);
            Gizmos.DrawWireSphere(carryPosition, carryPositionGizmoRadius);
        }
#endif
    }
}
