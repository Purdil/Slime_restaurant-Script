using System;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.TaskSystem.OrderSystem;
using _Project.Gameplay.TaskSystem.TaskObject;

namespace _Project.Gameplay.TaskSystem.TaskStructs
{
    public struct ReleaseOrderStationTask : ITask
    {
        public event Action OnEndTask;
        private readonly OrderTicket _ticket;
        private readonly CookingStationObject _station;

        public ReleaseOrderStationTask(OrderTicket ticket, CookingStationObject station)
        {
            OnEndTask = null;
            _ticket = ticket;
            _station = station;
        }

        public void Execute(Agent agent)
        {
            _station?.ReleaseForOrder(_ticket);
            OnEndTask?.Invoke();
        }

        public void Cancel(Agent agent)
        {
            _station?.ReleaseForOrder(_ticket);
        }

        public void HandleOnEndTask()
        {
            OnEndTask?.Invoke();
        }
    }
}
