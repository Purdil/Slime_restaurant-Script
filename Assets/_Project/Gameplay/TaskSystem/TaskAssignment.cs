using System;
using System.Collections.Generic;
using _Project.Gameplay.AgentSystem._Agent;

namespace _Project.Gameplay.TaskSystem
{
    public sealed class TaskAssignment
    {
        private readonly List<ITask> _tasks;
        private readonly bool _allowDeferredTasks;

        public IReadOnlyList<ITask> Tasks => _tasks;
        public TaskTypeEnum TargetTaskType { get; }
        public bool IsAssigned { get; private set; }
        public bool IsAcceptedByManager { get;  private set; }
        public bool IsCanceled { get; private set; }
        public Agent AssignedAgent { get; private set; }

        private readonly Func<Agent, bool> _tryPrepareAssignment;
        private readonly Func<bool> _canStayQueued;
        private readonly Action _onCanceled;

        public TaskAssignment(List<ITask> tasks, TaskTypeEnum targetTaskType)
            : this(tasks, targetTaskType, null, null, null)
        {
        }

        public TaskAssignment(
            List<ITask> tasks,
            TaskTypeEnum targetTaskType,
            Func<Agent, bool> tryPrepareAssignment,
            Action onCanceled,
            Func<bool> canStayQueued = null,
            bool allowDeferredTasks = false)
        {
            _tasks = tasks == null ? new List<ITask>() : new List<ITask>(tasks);
            _allowDeferredTasks = allowDeferredTasks;
            TargetTaskType = targetTaskType;
            _tryPrepareAssignment = tryPrepareAssignment;
            _canStayQueued = canStayQueued;
            _onCanceled = onCanceled;
        }

        public void InQueueByManager()
        {
            IsAcceptedByManager = true;
        }

        public bool CanAssignTo(Agent agent, TaskTypeEnum agentTaskType)
        {
            if (agent == null || CanStayQueued() == false)
            {
                return false;
            }

            return TargetTaskType == TaskTypeEnum.None || TargetTaskType == agentTaskType;
        }

        public bool TryAssign(Agent agent, TaskTypeEnum agentTaskType)
        {
            if (CanAssignTo(agent, agentTaskType) == false)
            {
                return false;
            }

            if (_tryPrepareAssignment != null && _tryPrepareAssignment.Invoke(agent) == false)
            {
                CanStayQueued();
                return false;
            }

            if (_tasks.Count == 0)
            {
                Cancel();
                return false;
            }

            AssignedAgent = agent;
            IsAssigned = true;
            return true;
        }

        public void ReplaceTasks(List<ITask> tasks)
        {
            if (IsAssigned || IsCanceled)
            {
                return;
            }

            _tasks.Clear();
            if (tasks == null)
            {
                return;
            }

            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i] != null)
                {
                    _tasks.Add(tasks[i]);
                }
            }
        }

        public bool CanStayQueued()
        {
            if (IsAssigned || IsCanceled)
            {
                return false;
            }

            if (_tasks.Count == 0 && _allowDeferredTasks == false)
            {
                Cancel();
                return false;
            }

            if (_canStayQueued != null && _canStayQueued.Invoke() == false)
            {
                Cancel();
                return false;
            }

            return true;
        }

        public void Cancel()
        {
            if (IsCanceled || IsAssigned)
            {
                return;
            }

            IsCanceled = true;
            _onCanceled?.Invoke();
        }
    }
}
