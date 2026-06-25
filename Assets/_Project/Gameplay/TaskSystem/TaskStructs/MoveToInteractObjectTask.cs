using System;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.TaskSystem.TaskObject;

namespace _Project.Gameplay.TaskSystem.TaskStructs
{
    public struct MoveToInteractObjectTask : ITask
    {
        public event Action OnEndTask;

        private readonly InteractTaskObject _targetObject;
        private readonly TaskTypeEnum _interactType;
        private readonly Action<Agent> _onMoveFailed;
        private MoveTask _moveTask;
        private Agent _agent;
        private PathAgentModule _pathModule;

        public MoveToInteractObjectTask(
            InteractTaskObject targetObject,
            TaskTypeEnum interactType)
            : this(targetObject, interactType, null)
        {
        }

        public MoveToInteractObjectTask(
            InteractTaskObject targetObject,
            TaskTypeEnum interactType,
            Action<Agent> onMoveFailed)
        {
            OnEndTask = null;
            _targetObject = targetObject;
            _interactType = interactType;
            _onMoveFailed = onMoveFailed;
            _moveTask = default;
            _agent = null;
            _pathModule = null;
        }

        public void Execute(Agent agent)
        {
            _agent = agent;
            _pathModule = agent.GetModule<PathAgentModule>();

            if (_targetObject == null)
            {
                agent.GetModule<TaskModule>()?.ClearTasks();
                OnEndTask?.Invoke();
                return;
            }

            _moveTask = new MoveTask(_targetObject.GetInteractPosition(_interactType), true, _onMoveFailed);
            _moveTask.OnEndTask += HandleOnEndTask;
            _moveTask.Execute(agent);
        }

        public void Cancel(Agent agent)
        {
            _moveTask.OnEndTask -= HandleOnEndTask;
            _moveTask.Cancel(agent);
        }

        public void HandleOnEndTask()
        {
            _moveTask.OnEndTask -= HandleOnEndTask;

            if (_pathModule != null && _pathModule.LastMoveSucceeded == false)
            {
                ServerGuidanceModule guidanceModule = _agent == null ? null : _agent.GetModule<ServerGuidanceModule>();
                guidanceModule?.CancelGuide();
            }

            OnEndTask?.Invoke();
        }
    }
}
