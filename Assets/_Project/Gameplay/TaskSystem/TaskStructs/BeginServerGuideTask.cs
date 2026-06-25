using System;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.TaskSystem.TaskObject;

namespace _Project.Gameplay.TaskSystem.TaskStructs
{
    public struct BeginServerGuideTask : ITask
    {
        public event Action OnEndTask;

        private readonly EnterDoor _door;
        private readonly Agent _customer;
        private readonly CustomerTable _table;

        public BeginServerGuideTask(
            EnterDoor door,
            Agent customer,
            CustomerTable table)
        {
            OnEndTask = null;
            _door = door;
            _customer = customer;
            _table = table;
        }

        public void Execute(Agent agent)
        {
            if (_door == null ||
                _door.TryStartReservedGuide(agent, _customer, _table) == false)
            {
                agent.GetModule<TaskModule>()?.ClearTasks();
            }

            OnEndTask?.Invoke();
        }

        public void Cancel(Agent agent)
        {
            if (_door != null)
            {
                _door.CancelGuideReservation(agent, _customer, _table);
            }
        }

        public void HandleOnEndTask()
        {
            OnEndTask?.Invoke();
        }
    }
}
