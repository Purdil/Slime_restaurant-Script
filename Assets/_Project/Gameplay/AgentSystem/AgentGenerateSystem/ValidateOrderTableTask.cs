using System;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.TaskSystem;
using _Project.Gameplay.TaskSystem.OrderSystem;
using _Project.Gameplay.TaskSystem.TaskObject;

namespace _Project.Gameplay.AgentSystem.AgentGenerateSystem
{
    public struct ValidateOrderTableTask : ITask
    {
        public event Action OnEndTask;
        private readonly OrderTicket _ticket;
        private readonly CustomerTable _table;
        private readonly TaskTypeEnum _interactType;

        public ValidateOrderTableTask(OrderTicket ticket, CustomerTable table, TaskTypeEnum interactType)
        {
            OnEndTask = null;
            _ticket = ticket;
            _table = table;
            _interactType = interactType;
        }

        public void Execute(Agent agent)
        {
            if (_ticket == null || _ticket.IsCanceled)
            {
                agent.GetModule<TaskModule>()?.ClearTasks();
                OnEndTask?.Invoke();
                return;
            }

            if (_table == null || _table.HasReachableInteractPosition(_interactType) == false)
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