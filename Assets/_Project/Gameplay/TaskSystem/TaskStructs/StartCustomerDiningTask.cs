using System;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.AgentSystem.AgentModules.CustomerModule;
using _Project.Gameplay.TaskSystem.TaskObject;

namespace _Project.Gameplay.TaskSystem.TaskStructs
{
    public struct StartCustomerDiningTask : ITask
    {
        public event Action OnEndTask;
        private readonly CustomerTable _table;

        public StartCustomerDiningTask(CustomerTable table)
        {
            OnEndTask = null;
            _table = table;
        }

        public void Execute(Agent agent)
        {
            agent.GetModule<CustomerDiningModule>()?.BeginDining(_table);
            agent.GetModule<IRenderer>().FlipThere(_table.GetInteractPosition(TaskTypeEnum.Customer));
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
