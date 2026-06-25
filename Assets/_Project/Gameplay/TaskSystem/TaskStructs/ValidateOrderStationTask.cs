using System;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.TaskSystem.OrderSystem;
using _Project.Gameplay.TaskSystem.TaskObject;

namespace _Project.Gameplay.TaskSystem.TaskStructs
{
    public struct ValidateOrderStationTask : ITask
    {
        public event Action OnEndTask;
        private readonly OrderTicket _ticket;
        private readonly CookingStationObject _station;

        public ValidateOrderStationTask(OrderTicket ticket, CookingStationObject station)
        {
            OnEndTask = null;
            _ticket = ticket;
            _station = station;
        }

        public void Execute(Agent agent)
        {
            if (_ticket == null || _ticket.IsCanceled)
            {
                agent.GetModule<TaskModule>()?.ClearTasks();
                OnEndTask?.Invoke();
                return;
            }

            if (_station == null || _station.HasReachableInteractPosition() == false)
            {
                if (RestaurantOrderManager.IsNullInstance == false)
                {
                    RestaurantOrderManager.Instance.CancelOrder(_ticket);
                }

                agent.GetModule<TaskModule>()?.ClearTasks();
            }

            OnEndTask?.Invoke();
        }

        public void Cancel(Agent agent)
        {
        }

        public void HandleOnEndTask()
        {
            OnEndTask?.Invoke();
        }
    }
}
