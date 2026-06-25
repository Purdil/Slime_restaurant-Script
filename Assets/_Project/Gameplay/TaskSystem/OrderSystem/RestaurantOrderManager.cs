using System;
using System.Collections.Generic;
using _Project.Core.CustomLogging;
using _Project.Core.Manager;
using _Project.Core.PoolManaging;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem._Agent.TaskAgent;
using _Project.Gameplay.AgentSystem.AgentGenerateSystem;
using _Project.Gameplay.AgentSystem.AgentModules;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.AgentSystem.AgentModules.CustomerModule;
using _Project.Gameplay.TaskSystem.EventChannel;
using _Project.Gameplay.TaskSystem.Managers;
using _Project.Gameplay.TaskSystem.Menu;
using _Project.Gameplay.TaskSystem.TaskObject;
using _Project.Gameplay.TaskSystem.TaskStructs;
using _Project.UI.Scripts.MVP._Main.Main;
using _Project.UI.Scripts.MVP.Shared;
using _Project.UI.Scripts.MVP.Shared.Rating;
using _Project.UI.Scripts.MVP.Shared.Resource;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.OrderSystem
{
    public class RestaurantOrderManager : MonoSingleton<RestaurantOrderManager>
    {
        [SerializeField] private FoodMenuCatalogSO menuCatalog;
        [SerializeField] private GenerateTaskChannel generateTaskChannel;
        [SerializeField] private OpenCloseEventChannel openCloseEventChannel;
        [SerializeField] private GameEntryPoint gameEntryPoint;
        [SerializeField] private RatingEventChannel ratingEventChannel;
        [SerializeField] private float waitingRetryInterval = 0.25f;

        public event Action<OrderTicket> OnOrderCreated;
        public event Action<OrderTicket> OnOrderCooked;
        public event Action<OrderTicket> OnOrderDelivered;
        public event Action<OrderTicket> OnOrderCompleted;
        public event Action<OrderTicket> OnOrderCanceled;

        private readonly List<OrderTicket> _orders = new();
        private readonly List<OrderTicket> _waitingCookOrders = new();
        private readonly List<OrderTicket> _waitingDeliveryOrders = new();
        private InteractObjectManager _interactObjectManager;
        private float _waitingRetryElapsed;
        private int _nextOrderId;

        protected override void Awake()
        {
            base.Awake();
            CacheInteractObjectManager();
        }

        private void Update()
        {
            if (_waitingCookOrders.Count == 0 && _waitingDeliveryOrders.Count == 0)
            {
                return;
            }

            _waitingRetryElapsed += Time.deltaTime;
            if (_waitingRetryElapsed < waitingRetryInterval)
            {
                return;
            }

            _waitingRetryElapsed = 0f;
            RetryWaitingCookOrders();
            RetryWaitingDeliveryOrders();
        }

        public bool TryCreateOrder(Agent customer, CustomerTable table, out OrderTicket ticket)
        {
            if (RestaurantRuntimeCloseService.IsClosed ||
                openCloseEventChannel != null && openCloseEventChannel.IsOpen == false)
            {
                ticket = null;
                return false;
            }

            IReadOnlyList<FoodMenuSO> orderableMenus = null;
            if (customer != null && customer.CurrentProfile is CustomerAgentProfileSO customerProfile)
            {
                orderableMenus = customerProfile.OrderableMenus;
            }

            return TryCreateOrder(customer, table, orderableMenus, out ticket);
        }

        public bool TryCreateOrder(
            Agent customer,
            CustomerTable table,
            IReadOnlyList<FoodMenuSO> orderableMenus,
            out OrderTicket ticket)
        {
            ticket = null;
            if (RestaurantRuntimeCloseService.IsClosed ||
                openCloseEventChannel != null && openCloseEventChannel.IsOpen == false)
            {
                return false;
            }

            if (customer == null || table == null)
            {
                return false;
            }

            FoodMenuSO menu;
            bool hasOrderableMenus = HasMenuReference(orderableMenus);
            bool hasMenu;
            if (hasOrderableMenus)
            {
                hasMenu = TryGetRandomCookableMenu(orderableMenus, out menu);
            }
            else
            {
                hasMenu = TryGetDefaultMenu(out menu);
            }

            if (hasMenu == false)
            {
                return false;
            }

            return TryCreateOrder(customer, table, menu, out ticket);
        }

        public void MarkCooked(OrderTicket ticket)
        {
            if (CanUseTicket(ticket) == false)
            {
                return;
            }

            SpawnPreparedFood(ticket);
            ticket.SetStatus(OrderStatus.Cooked);
            OnOrderCooked?.Invoke(ticket);
            OrderQueueResult queueResult = TryQueueDelivery(ticket);
            if (queueResult == OrderQueueResult.Wait)
            {
                AddWaitingOrder(_waitingDeliveryOrders, ticket);
            }
            else if (queueResult == OrderQueueResult.Cancel)
            {
                CancelOrder(ticket);
            }
        }

        public void DeliverOrder(OrderTicket ticket)
        {
            DeliverOrder(ticket, null);
        }

        public void DeliverOrder(OrderTicket ticket, Agent server)
        {
            if (CanUseTicket(ticket) == false)
            {
                return;
            }

            PlaceDeliveredFood(ticket, server);
            ticket.SetStatus(OrderStatus.Delivered);
            OnOrderDelivered?.Invoke(ticket);
            ticket.Customer.GetModule<CustomerDiningModule>()?.ReceiveOrder(ticket);
        }

        public void CompleteOrder(OrderTicket ticket)
        {
            if (CanUseTicket(ticket) == false)
            {
                return;
            }

            ticket.SetStatus(OrderStatus.Completed);
            ReturnPreparedFood(ticket);
            _orders.Remove(ticket);
            OnOrderCompleted?.Invoke(ticket);
        }

        public void AddCustomerPayment(int amount)
        {
            Debug.Assert(gameEntryPoint != null, $"gameEntyPoint가 없습니다. {gameObject.name}");
            if (amount <= 0 || gameEntryPoint?.ResourceService == null)
            {
                return;
            }

            gameEntryPoint.ResourceService.Add(ResourceType.Coin, amount, ResourceChangeSource.Agent);
        }

        public void SubmitCustomerRating(float rating)
        {
            RatingEventChannel targetChannel = ratingEventChannel != null
                ? ratingEventChannel
                : gameEntryPoint?.RatingEventChannel;
            if (targetChannel == null)
            {
                return;
            }

            targetChannel.Raise(Mathf.Clamp(rating, 0f, 5f));
        }

        public void CancelAllOrders(
            TaskCancelReason reason,
            bool shouldNotifyCustomer = true)
        {
            List<OrderTicket> tickets = new();
            for (int i = 0; i < _orders.Count; i++)
            {
                tickets.Add(_orders[i]);
            }

            for (int i = 0; i < tickets.Count; i++)
            {
                CancelOrder(tickets[i], reason, shouldNotifyCustomer);
            }

            _waitingCookOrders.Clear();
            _waitingDeliveryOrders.Clear();
        }

        public void CancelOrder(
            OrderTicket ticket,
            TaskCancelReason reason = TaskCancelReason.OrderCanceled,
            bool shouldNotifyCustomer = true)
        {
            if (ticket == null || ticket.Status == OrderStatus.Completed || ticket.Status == OrderStatus.Canceled)
            {
                return;
            }

            ticket.SetStatus(OrderStatus.Canceled);
            ReturnPreparedFood(ticket);
            ticket.ReleaseReservedStations();
            _waitingCookOrders.Remove(ticket);
            _waitingDeliveryOrders.Remove(ticket);
            _orders.Remove(ticket);
            OnOrderCanceled?.Invoke(ticket);
            if (shouldNotifyCustomer)
            {
                ticket.Customer.GetModule<CustomerDiningModule>()?.CancelOrder(ticket, reason);
            }
        }

        public bool TryPickupPreparedFood(OrderTicket ticket, Agent server)
        {
            if (CanUseTicket(ticket) == false || ticket.PreparedFood == null || server == null)
            {
                return false;
            }

            ServerFoodCarryModule carryModule = server.GetModule<ServerFoodCarryModule>();
            return carryModule != null && carryModule.TryCarry(ticket);
        }

        private bool TryCreateOrder(Agent customer, CustomerTable table, FoodMenuSO menu, out OrderTicket ticket)
        {
            ticket = new OrderTicket(++_nextOrderId, customer, table, menu, Time.time);
            ticket.SetStatus(OrderStatus.WaitingCook);
            _orders.Add(ticket);
            OnOrderCreated?.Invoke(ticket);

            OrderQueueResult queueResult = TryQueueCooking(ticket);
            if (queueResult == OrderQueueResult.Cancel)
            {
                CancelOrder(ticket);
                return false;
            }

            if (queueResult == OrderQueueResult.Wait)
            {
                AddWaitingOrder(_waitingCookOrders, ticket);
            }

            return true;
        }

        private bool TryGetDefaultMenu(out FoodMenuSO menu)
        {
            menu = null;
            return menuCatalog != null && TryGetRandomCookableMenu(menuCatalog.Menus, out menu);
        }

        private bool TryGetRandomCookableMenu(IReadOnlyList<FoodMenuSO> menus, out FoodMenuSO menu)
        {
            menu = null;
            if (menus == null || menus.Count == 0)
            {
                return false;
            }

            int startIndex = UnityEngine.Random.Range(0, menus.Count);
            for (int i = 0; i < menus.Count; i++)
            {
                int index = (startIndex + i) % menus.Count;
                if (IsMenuCookable(menus[index]))
                {
                    menu = menus[index];
                    return true;
                }
            }

            return false;
        }

        private bool HasMenuReference(IReadOnlyList<FoodMenuSO> menus)
        {
            if (menus == null || menus.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < menus.Count; i++)
            {
                if (menus[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsMenuCookable(FoodMenuSO menu)
        {
            if (menu == null || menu.CookingSteps == null || menu.CookingSteps.Count == 0)
            {
                return false;
            }

            CacheInteractObjectManager();
            if (_interactObjectManager == null)
            {
                return false;
            }

            for (int i = 0; i < menu.CookingSteps.Count; i++)
            {
                FoodCookingStepSO step = menu.CookingSteps[i];
                if (step == null || HasReachableCookingStation(step.StationType) == false)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasReachableCookingStation(CookingStationType stationType)
        {
            return _interactObjectManager.HasTaskObject<CookingStationObject>(
                candidate => candidate.Matches(stationType) && candidate.HasReachableInteractPosition());
        }

        private OrderQueueResult TryQueueCooking(OrderTicket ticket)
        {
            if (generateTaskChannel == null || ticket.Menu.CookingSteps.Count == 0)
            {
                return OrderQueueResult.Cancel;
            }

            TaskAssignment assignment = null;
            assignment = new TaskAssignment(
                null,
                TaskTypeEnum.Chef,
                chef => TryPrepareCookingAssignment(ticket, chef, assignment),
                () => HandleQueuedOrderAssignmentCanceled(ticket),
                () => CanKeepCookingAssignmentQueued(ticket),
                true);
            generateTaskChannel.Raise(assignment);
            return assignment.IsAcceptedByManager ? OrderQueueResult.Queued : OrderQueueResult.Wait;
        }

        private bool TryPrepareCookingAssignment(
            OrderTicket ticket,
            Agent chef,
            TaskAssignment assignment)
        {
            if (CanUseTicket(ticket) == false || chef == null || assignment == null)
            {
                return false;
            }

            List<ITask> tasks = new();
            List<FoodCookingStepSO> reservedSteps = new();
            List<CookingStationObject> reservedStations = new();
            Vector3 searchPosition = chef.transform.position;
            ticket.ReleaseReservedStations();

            foreach (FoodCookingStepSO step in ticket.Menu.CookingSteps)
            {
                if (step == null)
                {
                    ticket.ReleaseReservedStations();
                    return false;
                }

                CookingStationSearchResult searchResult = TryGetCookingStation(
                    step.StationType,
                    searchPosition,
                    ticket,
                    out CookingStationObject station);
                if (searchResult == CookingStationSearchResult.Busy)
                {
                    ticket.ReleaseReservedStations();
                    return false;
                }

                if (searchResult != CookingStationSearchResult.Available)
                {
                    ticket.ReleaseReservedStations();
                    CancelOrder(ticket);
                    return false;
                }

                if (station.TryReserveForOrder(ticket, TaskTypeEnum.Chef) == false)
                {
                    ticket.ReleaseReservedStations();
                    return false;
                }

                if (step.StationType == CookingStationType.FoodReadyTable)
                {
                    ticket.SetFinishStation(station);
                }

                reservedSteps.Add(step);
                reservedStations.Add(station);
                searchPosition = station.GetInteractPosition(TaskTypeEnum.Chef);
            }

            for (int i = 0; i < reservedSteps.Count; i++)
            {
                FoodCookingStepSO step = reservedSteps[i];
                CookingStationObject station = reservedStations[i];
                tasks.Add(new ValidateOrderStationTask(ticket, station));
                tasks.Add(new MoveTask(station.GetInteractPosition(TaskTypeEnum.Chef)));
                tasks.Add(new WorkTask(station, false, step.WorkAmount, step.PlayAnimParam));

                if (HasLaterStation(reservedStations, station, i) == false)
                {
                    tasks.Add(new ReleaseOrderStationTask(ticket, station));
                }
            }

            tasks.Add(new CompleteCookingTask(ticket));
            assignment.ReplaceTasks(tasks);
            ticket.SetStatus(OrderStatus.Cooking);
            return true;
        }

        private OrderQueueResult TryQueueDelivery(OrderTicket ticket)
        {
            if (generateTaskChannel == null)
            {
                return OrderQueueResult.Cancel;
            }

            List<ITask> tasks = new();
            if (ticket.Table == null || ticket.Table.HasReachableInteractPosition(TaskTypeEnum.Server) == false)
            {
                return OrderQueueResult.Cancel;
            }

            if (ticket.FinishStation != null)
            {
                if (ticket.FinishStation.HasReachableInteractPosition() == false)
                {
                    return OrderQueueResult.Cancel;
                }

                if (ticket.FinishStation.TryReserveForOrder(ticket, TaskTypeEnum.Server) == false)
                {
                    return OrderQueueResult.Wait;
                }

                tasks.Add(new ValidateOrderStationTask(ticket, ticket.FinishStation));
                tasks.Add(new MoveToInteractObjectTask(
                    ticket.FinishStation,
                    TaskTypeEnum.Server,
                    server => HandleDeliveryMoveFailed(ticket, server)));
                tasks.Add(new WorkTask(ticket.FinishStation, true));
                tasks.Add(new ReleaseOrderStationTask(ticket, ticket.FinishStation));
            }

            tasks.Add(new ValidateOrderTableTask(ticket, ticket.Table, TaskTypeEnum.Server));
            tasks.Add(new MoveToInteractObjectTask(
                ticket.Table,
                TaskTypeEnum.Server,
                server => HandleDeliveryMoveFailed(ticket, server)));
            tasks.Add(new DeliverOrderTask(ticket));
            if (TryRaiseTaskAssignment(
                    tasks,
                    TaskTypeEnum.Server,
                    () => CanKeepDeliveryAssignmentQueued(ticket),
                    () => HandleQueuedOrderAssignmentCanceled(ticket)) == false)
            {
                if (ticket.IsCanceled)
                {
                    return OrderQueueResult.Cancel;
                }

                if (ticket.PreparedFood == null)
                {
                    ticket.FinishStation?.ReleaseForOrder(ticket);
                }

                return OrderQueueResult.Wait;
            }

            ticket.SetStatus(OrderStatus.WaitingDelivery);
            return OrderQueueResult.Queued;
        }

        private CookingStationSearchResult TryGetCookingStation(
            CookingStationType stationType,
            Vector3 searchPosition,
            OrderTicket ticket,
            out CookingStationObject station)
        {
            CacheInteractObjectManager();
            station = null;
            if (_interactObjectManager == null)
            {
                return CookingStationSearchResult.Missing;
            }

            if (_interactObjectManager.TryFindNearTaskObject<CookingStationObject>(
                    searchPosition,
                    out station,
                    candidate => candidate.Matches(stationType) &&
                                 candidate.GetReservationState(ticket, TaskTypeEnum.Chef) ==
                                 CookingStationSearchResult.Available))
            {
                return CookingStationSearchResult.Available;
            }

            if (HasCookingStationState(stationType, ticket, CookingStationSearchResult.Busy))
            {
                return CookingStationSearchResult.Busy;
            }

            if (HasCookingStationState(stationType, ticket, CookingStationSearchResult.Blocked))
            {
                return CookingStationSearchResult.Blocked;
            }

            return CookingStationSearchResult.Missing;
        }

        private bool HasCookingStationState(
            CookingStationType stationType,
            OrderTicket ticket,
            CookingStationSearchResult state)
        {
            return _interactObjectManager.HasTaskObject<CookingStationObject>(
                candidate => candidate.Matches(stationType) &&
                             candidate.GetReservationState(ticket, TaskTypeEnum.Chef) == state);
        }

        private bool CanUseTicket(OrderTicket ticket)
        {
            return ticket != null && ticket.IsCanceled == false;
        }

        private void SpawnPreparedFood(OrderTicket ticket)
        {
            if (ticket == null || ticket.PreparedFood != null || ticket.FinishStation == null)
            {
                return;
            }

            PoolItemSO poolItem = ticket.Menu.FoodPoolItem;
            if (poolItem == null || PoolManager.IsNullInstance)
            {
                return;
            }

            FoodServingObject food = PoolManager.Instance.Pop(poolItem) as FoodServingObject;
            if (food == null)
            {
                CLog.LogWarning($"{ticket.Menu.MenuName} 음식 풀 오브젝트를 생성하지 못했습니다.");
                return;
            }

            ticket.SetPreparedFood(food);
            ticket.FinishStation.TryReserveForOrder(ticket, TaskTypeEnum.Server);
            ticket.FinishStation.PlacePreparedFood(food);
        }

        private void PlaceDeliveredFood(OrderTicket ticket, Agent server)
        {
            if (ticket == null || ticket.PreparedFood == null || ticket.Table == null)
            {
                return;
            }

            FoodServingObject food = ticket.PreparedFood;
            ServerFoodCarryModule carryModule = null;
            if (server != null)
            {
                carryModule = server.GetModule<ServerFoodCarryModule>();
            }

            if (carryModule == null)
            {
                carryModule = ticket.FoodCarrier;
            }

            if (carryModule != null)
            {
                FoodServingObject carriedFood = carryModule.ReleaseForTable(ticket);
                if (carriedFood != null)
                {
                    food = carriedFood;
                }
            }

            ticket.Table.PlaceDeliveredFood(food);
        }

        private void ReturnPreparedFood(OrderTicket ticket)
        {
            if (ticket == null || ticket.PreparedFood == null)
            {
                return;
            }

            FoodServingObject food = ticket.PreparedFood;
            ServerFoodCarryModule foodCarrier = ticket.FoodCarrier;
            if (foodCarrier != null)
            {
                foodCarrier.ClearCarriedFood(false);
            }

            ticket.ClearPreparedFood(food);

            if (PoolManager.IsNullInstance == false)
            {
                PoolManager.Instance.Push(food);
                return;
            }

            food.Clear();
            food.gameObject.SetActive(false);
        }

        private bool TryRaiseTaskAssignment(
            List<ITask> tasks,
            TaskTypeEnum taskType,
            Func<bool> canStayQueued,
            Action onCanceled)
        {
            TaskAssignment assignment = new TaskAssignment(
                tasks,
                taskType,
                null,
                onCanceled,
                canStayQueued);
            generateTaskChannel.Raise(assignment);
            return assignment.IsAcceptedByManager;
        }

        private void HandleDeliveryMoveFailed(OrderTicket ticket, Agent server)
        {
            if (CanUseTicket(ticket) == false)
            {
                return;
            }

            TaskModule taskModule = server == null ? null : server.GetModule<TaskModule>();
            taskModule?.ClearTasks();
            RestorePreparedFoodAfterDeliveryFailed(ticket, server);
            if (CanUseTicket(ticket) == false)
            {
                return;
            }

            ticket.SetStatus(OrderStatus.Cooked);
            AddWaitingOrder(_waitingDeliveryOrders, ticket);
        }

        private void RestorePreparedFoodAfterDeliveryFailed(OrderTicket ticket, Agent server)
        {
            if (ticket == null || ticket.PreparedFood == null)
            {
                return;
            }

            ServerFoodCarryModule carryModule = null;
            if (server != null)
            {
                carryModule = server.GetModule<ServerFoodCarryModule>();
            }

            if (carryModule == null)
            {
                carryModule = ticket.FoodCarrier;
            }

            FoodServingObject food = null;
            if (carryModule != null)
            {
                food = carryModule.ReleaseForTable(ticket);
            }

            if (food == null)
            {
                food = ticket.PreparedFood;
            }

            CookingStationObject finishStation = ticket.FinishStation;
            if (finishStation == null)
            {
                CancelOrder(ticket, TaskCancelReason.PathFailed);
                return;
            }

            if (finishStation.TryReserveForOrder(ticket, TaskTypeEnum.Server) == false)
            {
                CancelOrder(ticket, TaskCancelReason.PathFailed);
                return;
            }

            ticket.SetPreparedFood(food);
            finishStation.PlacePreparedFood(food);
        }

        private bool CanKeepCookingAssignmentQueued(OrderTicket ticket)
        {
            if (CanUseTicket(ticket) == false)
            {
                return false;
            }

            IReadOnlyList<CookingStationObject> stations = ticket.ReservedStations;
            if (stations == null || stations.Count == 0)
            {
                return IsMenuCookable(ticket.Menu);
            }

            for (int i = 0; i < stations.Count; i++)
            {
                CookingStationObject station = stations[i];
                if (station == null ||
                    station.GetReservationState(ticket, TaskTypeEnum.Chef) != CookingStationSearchResult.Available)
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanKeepDeliveryAssignmentQueued(OrderTicket ticket)
        {
            if (CanUseTicket(ticket) == false ||
                ticket.Table == null ||
                ticket.Table.HasReachableInteractPosition(TaskTypeEnum.Server) == false)
            {
                return false;
            }

            if (ticket.FinishStation == null)
            {
                return true;
            }

            return ticket.FinishStation.GetReservationState(ticket, TaskTypeEnum.Server) ==
                   CookingStationSearchResult.Available;
        }

        private void HandleQueuedOrderAssignmentCanceled(OrderTicket ticket)
        {
            if (CanUseTicket(ticket) == false)
            {
                return;
            }

            CancelOrder(ticket);
        }

        private void CacheInteractObjectManager()
        {
            if (_interactObjectManager == null && InteractObjectManager.IsNullInstance == false)
            {
                _interactObjectManager = InteractObjectManager.Instance;
            }
        }

        private void RetryWaitingCookOrders()
        {
            for (int i = _waitingCookOrders.Count - 1; i >= 0; i--)
            {
                OrderTicket ticket = _waitingCookOrders[i];
                if (CanUseTicket(ticket) == false)
                {
                    _waitingCookOrders.RemoveAt(i);
                    continue;
                }

                OrderQueueResult result = TryQueueCooking(ticket);
                if (result == OrderQueueResult.Queued)
                {
                    _waitingCookOrders.RemoveAt(i);
                }
                else if (result == OrderQueueResult.Cancel)
                {
                    _waitingCookOrders.RemoveAt(i);
                    CancelOrder(ticket);
                }
            }
        }

        private void RetryWaitingDeliveryOrders()
        {
            for (int i = _waitingDeliveryOrders.Count - 1; i >= 0; i--)
            {
                OrderTicket ticket = _waitingDeliveryOrders[i];
                if (CanUseTicket(ticket) == false)
                {
                    _waitingDeliveryOrders.RemoveAt(i);
                    continue;
                }

                OrderQueueResult result = TryQueueDelivery(ticket);
                if (result == OrderQueueResult.Queued)
                {
                    _waitingDeliveryOrders.RemoveAt(i);
                }
                else if (result == OrderQueueResult.Cancel)
                {
                    _waitingDeliveryOrders.RemoveAt(i);
                    CancelOrder(ticket);
                }
            }
        }

        private void AddWaitingOrder(List<OrderTicket> waitingOrders, OrderTicket ticket)
        {
            if (waitingOrders.Contains(ticket) == false)
            {
                waitingOrders.Add(ticket);
            }
        }

        private bool HasLaterStation(List<CookingStationObject> stations, CookingStationObject station, int currentIndex)
        {
            for (int i = currentIndex + 1; i < stations.Count; i++)
            {
                if (stations[i] == station)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
