using System;
using _Project.Gameplay.AgentSystem._Agent;

namespace _Project.Gameplay.TaskSystem.TaskStructs
{
    public struct ReleaseInteractObjectTask : ITask
    {
        public event Action OnEndTask;
        private readonly InteractTaskObject _targetObject;

        public ReleaseInteractObjectTask(InteractTaskObject targetObject)
        {
            OnEndTask = null;
            _targetObject = targetObject;
        }

        public void Execute(Agent agent)
        {
            _targetObject?.ReleaseDesignated();
            OnEndTask?.Invoke();
        }

        public void Cancel(Agent agent)
        {
            _targetObject?.ReleaseDesignated();
        }

        public void HandleOnEndTask()
        {
            OnEndTask?.Invoke();
        }
    }
}
