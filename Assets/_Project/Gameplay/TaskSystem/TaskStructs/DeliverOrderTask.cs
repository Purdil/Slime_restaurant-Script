using System;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.TaskSystem.OrderSystem;

namespace _Project.Gameplay.TaskSystem.TaskStructs
{
    public struct DeliverOrderTask : ITask
    {
        public event Action OnEndTask;
        private readonly OrderTicket _ticket;

        public DeliverOrderTask(OrderTicket ticket)
        {
            OnEndTask = null;
            _ticket = ticket;
        }

        public void Execute(Agent agent)
        {
            if (RestaurantOrderManager.IsNullInstance == false)
            {
                RestaurantOrderManager.Instance.DeliverOrder(_ticket, agent);
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
