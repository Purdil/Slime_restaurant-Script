using System.Collections.Generic;
using _Project.Core.PoolManaging;
using _Project.Core.ModuleSystem;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem._Agent.TaskAgent;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.StatSystem;
using _Project.Gameplay.TaskSystem;
using _Project.Gameplay.TaskSystem.OrderSystem;
using _Project.Gameplay.TaskSystem.TaskObject;
using _Project.Gameplay.TaskSystem.TaskStructs;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules.CustomerModule
{
    public class CustomerDiningModule : MonoBehaviour, IModule, IApplyProfileModule, IAgentStatConsumer
    {
        private static readonly List<CustomerDiningModule> ActiveCustomerModules = new();

        [SerializeField] private StatTypeSO waitTimeStatType;
        [SerializeField] private StatTypeSO eatingSpeedStatType;
        [SerializeField, HideInInspector] private float defaultWaitTime = CustomerDiningConfigSO.DEFAULT_WAIT_TIME;
        [SerializeField, HideInInspector] private float defaultEatingSpeed = CustomerDiningConfigSO.DEFAULT_EATING_SPEED;
        [SerializeField, HideInInspector] private float defaultTableWaitPenaltyTime = CustomerDiningConfigSO.DEFAULT_TABLE_WAIT_PENALTY_TIME;
        [SerializeField, HideInInspector] private float defaultTableWaitRatingPenalty = CustomerDiningConfigSO.DEFAULT_TABLE_WAIT_RATING_PENALTY;
        [SerializeField, HideInInspector] private float trashRatingPenalty = CustomerDiningConfigSO.DEFAULT_TRASH_RATING_PENALTY;
        [SerializeField, HideInInspector] private float maxWaitRatingPenalty = CustomerDiningConfigSO.DEFAULT_MAX_FOOD_WAIT_RATING_PENALTY;
        [SerializeField, HideInInspector] private AnimParamSO eatAnimParam;

        private Agent _owner;
        private TaskModule _taskModule;
        private CustomerTable _table;
        private OrderTicket _ticket;
        private Vector3 _homePosition;
        private float _waitTime;
        private float _eatingSpeed;
        private float _waitingElapsedTime;
        private float _eatingElapsedTime;
        private float _tableWaitingElapsedTime;
        private float _tableWaitPenaltyTime;
        private float _tableWaitRatingPenalty;
        private float _trashRatingPenalty;
        private float _maxFoodWaitRatingPenalty;
        private int _trashEncounterCount;
        private bool _isWaitingTable;
        private bool _isWaitingFood;
        private bool _isEating;
        private bool _hasTableWaitPenalty;
        private bool _isLeavingRestaurant;
        private AnimParamSO _eatAnimParam;

        public static IReadOnlyList<CustomerDiningModule> ActiveCustomers => ActiveCustomerModules;
        public bool CanEncounterTrash => _owner != null && _owner.IsActivate && _isLeavingRestaurant == false;
        public bool IsLeavingRestaurant => _isLeavingRestaurant;
        public Vector3 EncounterPosition => _owner == null ? transform.position : _owner.transform.position;

        private void OnEnable()
        {
            if (ActiveCustomerModules.Contains(this) == false)
            {
                ActiveCustomerModules.Add(this);
            }
        }

        private void Update()
        {
            if (_isWaitingTable)
            {
                UpdateTableWaiting();
            }

            if (_isWaitingFood)
            {
                UpdateWaitingFood();
            }

            if (_isEating)
            {
                UpdateEating();
            }
        }

        private void OnDisable()
        {
            ActiveCustomerModules.Remove(this);
        }

        public void Initialize(ModuleOwner moduleOwner)
        {
            _owner = moduleOwner as Agent;
            _taskModule = _owner?.GetModule<TaskModule>();
            ApplyLegacyDiningValues();
        }

        public void ApplyProfile(AgentProfileSO profileSo)
        {
            ApplyLegacyDiningValues();

            if (profileSo is not CustomerAgentProfileSO customerProfile)
            {
                return;
            }

            ApplyCustomerProfileDiningValues(customerProfile);
        }

        public void SetHomePosition(Vector3 homePosition)
        {
            _homePosition = homePosition;
            _isLeavingRestaurant = false;
            _waitingElapsedTime = 0f;
            _eatingElapsedTime = 0f;
            _tableWaitingElapsedTime = 0f;
            _trashEncounterCount = 0;
            _isWaitingTable = false;
            _hasTableWaitPenalty = false;
        }

        public void BeginTableWaiting()
        {
            if (_isWaitingTable)
            {
                return;
            }

            _isWaitingTable = true;
            if (_hasTableWaitPenalty == false)
            {
                _tableWaitingElapsedTime = 0f;
            }
        }

        public void EndTableWaiting()
        {
            _isWaitingTable = false;
        }

        public void BeginDining(CustomerTable table)
        {
            if (_isLeavingRestaurant)
            {
                return;
            }

            if (table == null || RestaurantOrderManager.IsNullInstance)
            {
                LeaveRestaurant();
                return;
            }

            _table = table;
            _waitingElapsedTime = 0f;
            _eatingElapsedTime = 0f;

            if (RestaurantOrderManager.Instance.TryCreateOrder(_owner, table, out _ticket) == false)
            {
                LeaveRestaurant();
                return;
            }

            _isWaitingFood = true;
            _isEating = false;
        }

        public void ReceiveOrder(OrderTicket ticket)
        {
            if (_isLeavingRestaurant || _ticket != ticket || ticket == null || ticket.IsCanceled)
            {
                return;
            }

            _isWaitingFood = false;
            _isEating = true;
            _eatingElapsedTime = 0f;

            if (_eatAnimParam != null)
            {
                _owner.Renderer?.ControlManualAnimation(false, _eatAnimParam.ParamHash);
            }
        }

        public void CancelOrder(OrderTicket ticket, TaskCancelReason reason = TaskCancelReason.OrderCanceled)
        {
            if (_ticket != ticket)
            {
                return;
            }

            _ticket = null;
            if (ShouldSubmitRatingOnCancel(reason))
            {
                SubmitRating(false);
            }

            LeaveRestaurant(reason);
        }

        public void RegisterTrashEncounter()
        {
            _trashEncounterCount++;
        }

        public void RefreshStats(IAgentStatProvider statProvider)
        {
            if (waitTimeStatType != null && statProvider.TryGetStatData(waitTimeStatType, out float waitTime))
            {
                _waitTime = Mathf.Max(0f, waitTime);
            }

            if (eatingSpeedStatType != null && statProvider.TryGetStatData(eatingSpeedStatType, out float eatingSpeed))
            {
                _eatingSpeed = Mathf.Max(0.01f, eatingSpeed);
            }
        }

        public void UpdateStats(StatTypeSO updateType, float updateValue)
        {
            if (updateType == waitTimeStatType)
            {
                _waitTime = Mathf.Max(0f, updateValue);
            }

            if (updateType == eatingSpeedStatType)
            {
                _eatingSpeed = Mathf.Max(0.01f, updateValue);
            }
        }

        private void UpdateWaitingFood()
        {
            _waitingElapsedTime += Time.deltaTime;
            if (_waitingElapsedTime < _waitTime)
            {
                return;
            }

            if (RestaurantOrderManager.IsNullInstance == false)
            {
                RestaurantOrderManager.Instance.CancelOrder(_ticket);
                return;
            }

            SubmitRating(false);
            LeaveRestaurant();
        }

        private void UpdateTableWaiting()
        {
            if (_hasTableWaitPenalty)
            {
                return;
            }

            _tableWaitingElapsedTime += Time.deltaTime;
            if (_tableWaitingElapsedTime < _tableWaitPenaltyTime)
            {
                return;
            }

            _hasTableWaitPenalty = true;
        }

        private void UpdateEating()
        {
            _eatingElapsedTime += Time.deltaTime * _eatingSpeed;
            if (_ticket == null || _eatingElapsedTime < _ticket.Menu.EatingTime)
            {
                return;
            }

            CompleteEating();
        }

        private void CompleteEating()
        {
            _isEating = false;
            _owner.Renderer?.ControlManualAnimation(true);

            if (RestaurantOrderManager.IsNullInstance)
            {
                LeaveRestaurant();
                return;
            }

            RestaurantOrderManager orderManager = RestaurantOrderManager.Instance;
            orderManager.AddCustomerPayment(_ticket.Menu.Price);
            SubmitRating(true);
            orderManager.CompleteOrder(_ticket);
            LeaveRestaurant();
        }
    
        public static void LeaveAllActiveCustomers(TaskCancelReason reason)
        {
            for (int i = ActiveCustomerModules.Count - 1; i >= 0; i--)
            {
                CustomerDiningModule module = ActiveCustomerModules[i];
                if (module == null)
                {
                    ActiveCustomerModules.RemoveAt(i);
                    continue;
                }

                module.LeaveRestaurant(reason);
            }
        }

        public static void ReturnAllActiveCustomersToPoolImmediately(TaskCancelReason reason)
        {
            for (int i = ActiveCustomerModules.Count - 1; i >= 0; i--)
            {
                CustomerDiningModule module = ActiveCustomerModules[i];
                if (module == null)
                {
                    ActiveCustomerModules.RemoveAt(i);
                    continue;
                }

                module.ReturnToPoolImmediately(reason);
            }
        }

        public void LeaveRestaurant(TaskCancelReason reason = TaskCancelReason.CustomerLeave)
        {
            if (_isLeavingRestaurant)
            {
                return;
            }

            _isLeavingRestaurant = true;
            _isWaitingTable = false;
            _isWaitingFood = false;
            _isEating = false;
            OrderTicket ticket = _ticket;
            _ticket = null;

            if (ticket != null && RestaurantOrderManager.IsNullInstance == false)
            {
                RestaurantOrderManager.Instance.CancelOrder(ticket, reason, false);
            }

            _owner.Renderer?.ControlManualAnimation(true);
            bool shouldNotifyDoor = reason != TaskCancelReason.RestaurantClosed &&
                                    reason != TaskCancelReason.AccountSwitch;
            _table?.ClearTable(shouldNotifyDoor);
            _table = null;

            if (_taskModule == null)
            {
                return;
            }

            _taskModule.CancelAllTasks(reason);
            _taskModule.AddTask(new MoveTask(_homePosition, false));
            _taskModule.AddTask(new ReturnAgentToPoolTask());
        }

        public void ReturnToPoolImmediately(TaskCancelReason reason)
        {
            if (_owner == null)
            {
                ActiveCustomerModules.Remove(this);
                return;
            }

            ClearRuntimeStateForImmediateReturn(reason);

            if (PoolManager.IsNullInstance == false)
            {
                PoolManager.Instance.Push(_owner);
                return;
            }

            _owner.gameObject.SetActive(false);
        }

        private void SubmitRating(bool completed)
        {
            if (RestaurantOrderManager.IsNullInstance)
            {
                return;
            }

            float rating = completed ? 5f : 2f;
            rating -= GetFoodWaitRatingPenalty();
            rating -= GetTableWaitRatingPenalty();
            rating -= _trashEncounterCount * _trashRatingPenalty;
            RestaurantOrderManager.Instance.SubmitCustomerRating(rating);
        }

        private float GetFoodWaitRatingPenalty()
        {
            float waitRatio = _waitTime <= 0f ? 1f : Mathf.Clamp01(_waitingElapsedTime / _waitTime);
            return waitRatio * _maxFoodWaitRatingPenalty;
        }

        private float GetTableWaitRatingPenalty()
        {
            return _hasTableWaitPenalty ? _tableWaitRatingPenalty : 0f;
        }

        private static bool ShouldSubmitRatingOnCancel(TaskCancelReason reason)
        {
            return reason != TaskCancelReason.RestaurantClosed &&
                   reason != TaskCancelReason.AccountSwitch;
        }

        private void ClearRuntimeStateForImmediateReturn(TaskCancelReason reason)
        {
            _isLeavingRestaurant = true;
            _isWaitingTable = false;
            _isWaitingFood = false;
            _isEating = false;
            _hasTableWaitPenalty = false;
            _waitingElapsedTime = 0f;
            _eatingElapsedTime = 0f;
            _tableWaitingElapsedTime = 0f;
            _trashEncounterCount = 0;

            OrderTicket ticket = _ticket;
            _ticket = null;

            if (ticket != null && RestaurantOrderManager.IsNullInstance == false)
            {
                RestaurantOrderManager.Instance.CancelOrder(ticket, reason, false);
            }

            _table?.ClearTable(false);
            _table = null;

            _owner.Renderer?.ControlManualAnimation(true);
            _taskModule?.CancelAllTasks(reason);
        }

        private void ApplyLegacyDiningValues()
        {
            _waitTime = Mathf.Max(0f, defaultWaitTime);
            _eatingSpeed = Mathf.Max(0.01f, defaultEatingSpeed);
            _tableWaitPenaltyTime = Mathf.Max(0f, defaultTableWaitPenaltyTime);
            _tableWaitRatingPenalty = Mathf.Max(0f, defaultTableWaitRatingPenalty);
            _trashRatingPenalty = Mathf.Max(0f, trashRatingPenalty);
            _maxFoodWaitRatingPenalty = Mathf.Max(0f, maxWaitRatingPenalty);
            _eatAnimParam = eatAnimParam;
        }

        private void ApplyCustomerProfileDiningValues(CustomerAgentProfileSO customerProfile)
        {
            _waitTime = customerProfile.WaitTime;
            _eatingSpeed = customerProfile.EatingSpeed;
            _tableWaitPenaltyTime = customerProfile.TableWaitPenaltyTime;
            _tableWaitRatingPenalty = customerProfile.TableWaitRatingPenalty;
            _trashRatingPenalty = customerProfile.TrashRatingPenalty;
            _maxFoodWaitRatingPenalty = customerProfile.MaxFoodWaitRatingPenalty;
            _eatAnimParam = customerProfile.EatAnimParam == null ? _eatAnimParam : customerProfile.EatAnimParam;
        }
    }
}
