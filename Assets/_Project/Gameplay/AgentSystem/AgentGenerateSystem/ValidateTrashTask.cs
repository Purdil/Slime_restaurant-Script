using System;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.TaskSystem;
using _Project.Gameplay.TaskSystem.Cleanliness;
using _Project.Gameplay.TaskSystem.TaskObject;

namespace _Project.Gameplay.AgentSystem.AgentGenerateSystem
{
    public struct ValidateTrashTask : ITask
    {
        public event Action OnEndTask;

        private readonly TrashObject _trashObject;

        public ValidateTrashTask(TrashObject trashObject)
        {
            _trashObject = trashObject;
            OnEndTask = null;
        }
        
        public void Execute(Agent agent)
        {
            if (_trashObject == null || !_trashObject.CanInteract())
            {
                agent.GetModule<TaskModule>()?.ClearTasks();
                OnEndTask?.Invoke();
                return;
            }

            if (CleanlinessManager.IsNullInstance ||
                !CleanlinessManager.Instance.CleanlinessService.HasThisTrash(_trashObject))
            {
                agent.GetModule<TaskModule>()?.ClearTasks();
                OnEndTask?.Invoke();
                return;
            }
            if (!_trashObject.TryDesignate())
            {
                agent.GetModule<TaskModule>()?.ClearTasks();
            }
            OnEndTask?.Invoke();
        }

        public void Cancel(Agent agent)
        {
        }

        public void HandleOnEndTask()
        {
        }
    }
}