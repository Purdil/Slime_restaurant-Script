using System;
using _Project.Core.CustomLogging;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.AgentSystem.AgentModules.CustomerModule;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.TaskStructs
{
    public struct MoveTask : ITask
    {
        public Vector2 Position;
        public event Action OnEndTask;
        private PathAgentModule _pathModule;
        private IAgentMoveModule _moveModule;
        private Agent _agent;
        private readonly bool _shouldClearTasksOnFailure;
        private readonly Action<Agent> _onMoveFailed;

        public MoveTask(Vector3 position, bool shouldClearTasksOnFailure = true)
            : this(position, shouldClearTasksOnFailure, null)
        {
        }

        public MoveTask(Vector3 position, bool shouldClearTasksOnFailure, Action<Agent> onMoveFailed)
        {
            Position = position;
            OnEndTask =  null; 
            _pathModule = null;
            _moveModule = null;
            _agent = null;
            _shouldClearTasksOnFailure = shouldClearTasksOnFailure;
            _onMoveFailed = onMoveFailed;
        }


        public void Execute(Agent agent)
        {
            _agent = agent;
            PathAgentModule moveModule = agent.GetModule<PathAgentModule>();
            if (moveModule != null)
            {
                moveModule.OnMoveComplete += HandleOnEndTask;
                _pathModule = moveModule;
                moveModule.RequestPath(Position);
            }
            else
            {
                _moveModule =  agent.GetModule<IAgentMoveModule>();
                _moveModule.SetDestination(Position);
                CLog.Log($"{agent.Name} move to {Position}, Path모듈 찾을 수 없음");
            }
            
        }

        public void Cancel(Agent agent)
        {
            if (_pathModule != null)
            {
                _pathModule.OnMoveComplete -= HandleOnEndTask;
                _pathModule.ResetPath();
            }
            else
            {
                agent.GetModule<PathAgentModule>()?.ResetPath();
            }

            agent.GetModule<IAgentMoveModule>()?.StopImmediate();
        }

        public void HandleOnEndTask()
        {
            if (_shouldClearTasksOnFailure &&
                _pathModule != null &&
                _pathModule.LastMoveSucceeded == false)
            {
                HandleMoveFailed();
            }

            OnEndTask?.Invoke();
            if (_pathModule != null)
            {
                _pathModule.OnMoveComplete -= HandleOnEndTask; 
            }
        }

        private void HandleMoveFailed()
        {
            if (_onMoveFailed != null)
            {
                _onMoveFailed.Invoke(_agent);
                return;
            }

            TaskModule taskModule = _agent?.GetModule<TaskModule>();
            if (taskModule == null)
            {
                return;
            }

            if (taskModule.TaskType == TaskTypeEnum.Customer)
            {
                CustomerDiningModule diningModule = _agent.GetModule<CustomerDiningModule>();
                if (diningModule != null && diningModule.IsLeavingRestaurant == false)
                {
                    diningModule.LeaveRestaurant(TaskCancelReason.PathFailed);
                    return;
                }
            }

            taskModule.CancelAllTasks(TaskCancelReason.PathFailed);
        }
    }
}
