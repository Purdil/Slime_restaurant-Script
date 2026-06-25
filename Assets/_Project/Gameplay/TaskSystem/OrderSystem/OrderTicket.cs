using System.Collections.Generic;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules;
using _Project.Gameplay.TaskSystem.Menu;
using _Project.Gameplay.TaskSystem.TaskObject;

namespace _Project.Gameplay.TaskSystem.OrderSystem
{
    public sealed class OrderTicket
    {
        public int OrderId { get; }
        public Agent Customer { get; }
        public CustomerTable Table { get; }
        public FoodMenuSO Menu { get; }
        public OrderStatus Status { get; private set; }
        public float WaitStartedTime { get; }
        public CookingStationObject FinishStation { get; private set; }
        public FoodServingObject PreparedFood { get; private set; }
        public ServerFoodCarryModule FoodCarrier { get; private set; }
        public IReadOnlyList<CookingStationObject> ReservedStations => _reservedStations;
        public bool IsCanceled => Status == OrderStatus.Canceled;
        public bool IsDelivered => Status == OrderStatus.Delivered || Status == OrderStatus.Completed;

        private readonly List<CookingStationObject> _reservedStations = new();

        public OrderTicket(int orderId, Agent customer, CustomerTable table, FoodMenuSO menu, float waitStartedTime)
        {
            OrderId = orderId;
            Customer = customer;
            Table = table;
            Menu = menu;
            WaitStartedTime = waitStartedTime;
            Status = OrderStatus.Created;
        }

        public void SetStatus(OrderStatus status)
        {
            Status = status;
        }

        public void SetFinishStation(CookingStationObject station)
        {
            FinishStation = station;
        }

        public void SetPreparedFood(FoodServingObject food)
        {
            PreparedFood = food;
            food?.Bind(this);
        }

        public void ClearPreparedFood(FoodServingObject food)
        {
            if (PreparedFood != food)
            {
                return;
            }

            PreparedFood = null;
            FoodCarrier = null;
        }

        public void SetFoodCarrier(ServerFoodCarryModule carrier)
        {
            FoodCarrier = carrier;
        }

        public void ClearFoodCarrier(ServerFoodCarryModule carrier)
        {
            if (FoodCarrier == carrier)
            {
                FoodCarrier = null;
            }
        }

        public void AddReservedStation(CookingStationObject station)
        {
            if (station != null && _reservedStations.Contains(station) == false)
            {
                _reservedStations.Add(station);
            }
        }

        public void RemoveReservedStation(CookingStationObject station)
        {
            _reservedStations.Remove(station);
        }

        public void ReleaseReservedStations()
        {
            for (int i = _reservedStations.Count - 1; i >= 0; i--)
            {
                _reservedStations[i]?.ReleaseForOrder(this);
            }

            _reservedStations.Clear();
        }
    }
}
