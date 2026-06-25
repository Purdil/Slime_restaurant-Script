using System;
using _Project.Core.PoolManaging;
using _Project.Gameplay.AgentSystem._Agent;

namespace _Project.Gameplay.TaskSystem.TaskStructs
{
    public struct ReturnAgentToPoolTask : ITask
    {
        public event Action OnEndTask;

        public void Execute(Agent agent)
        {
            PoolManager.Instance.Push(agent);
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
