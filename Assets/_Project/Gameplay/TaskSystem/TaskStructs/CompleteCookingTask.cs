using System;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.TaskSystem.OrderSystem;

namespace _Project.Gameplay.TaskSystem.TaskStructs
{
    public struct CompleteCookingTask : ITask
    {
        public event Action OnEndTask;
        private readonly OrderTicket _ticket;

        public CompleteCookingTask(OrderTicket ticket)
        {
            OnEndTask = null;
            _ticket = ticket;
        }

        public void Execute(Agent agent)
        {
            if (RestaurantOrderManager.IsNullInstance == false)
            {
                RestaurantOrderManager.Instance.MarkCooked(_ticket);
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
