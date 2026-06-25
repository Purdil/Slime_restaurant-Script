using System.Collections.Generic;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.TaskSystem.EventChannel;
using _Project.Gameplay.TaskSystem.Managers;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.Managers.TaskManagers
{
    public abstract class AbstractTaskManager : MonoBehaviour
    {
        [SerializeField] private SendingTaskEventChannel sendingTaskChannel;
        [SerializeField] private GenerateTaskChannel generateTaskChannel;
        [SerializeField] private TaskTypeEnum managingTaskType;
        protected readonly Queue<TaskAssignment> TaskQueue = new();

        protected virtual void OnEnable()
        {
            Debug.Assert(generateTaskChannel != null, $"{gameObject.name}에 GenerateTaskChannel이 연결되지 않았습니다.");
            Debug.Assert(sendingTaskChannel != null, $"{gameObject.name}에 SendingTaskChannel이 연결되지 않았습니다.");
            if (generateTaskChannel == null)
            {
                return;
            }

            generateTaskChannel.OnEvent += HandleGenerateTask;
        }

        protected virtual void OnDisable()
        {
            if (generateTaskChannel != null)
            {
                generateTaskChannel.OnEvent -= HandleGenerateTask;
            }

            CancelQueuedAssignments();
        }

        private void FixedUpdate()
        {
            if (TaskQueue.Count == 0)
            {
                return;
            }

            if (RestaurantRuntimeCloseService.IsClosed)
            {
                CancelQueuedAssignments();
                return;
            }

            RaiseTask();
        }

        protected virtual void RaiseTask()
        {
            int checkCount = TaskQueue.Count;
            for (int i = 0; i < checkCount && TaskQueue.Count > 0; i++)
            {
                TaskAssignment assignment = TaskQueue.Dequeue();
                if (ShouldDropAssignment(assignment))
                {
                    continue;
                }

                if (TaskModule.TryAssignManagerTask(assignment))
                {
                    continue;
                }

                if (ShouldDropAssignment(assignment) == false)
                {
                    TaskQueue.Enqueue(assignment);
                }
            }
        }

        protected virtual void HandleGenerateTask(TaskAssignment assignment)
        {
            if (CanHandleAssignment(assignment) == false)
            {
                return;
            }

            if (assignment.CanStayQueued() == false)
            {
                return;
            }

            assignment.InQueueByManager();
            TaskQueue.Enqueue(assignment);
        }

        private bool CanHandleAssignment(TaskAssignment assignment)
        {
            if (assignment == null || assignment.CanStayQueued() == false)
            {
                return false;
            }

            return assignment.TargetTaskType == managingTaskType;
        }

        private bool ShouldDropAssignment(TaskAssignment assignment)
        {
            return assignment == null ||
                   assignment.IsAssigned ||
                   assignment.IsCanceled ||
                   assignment.CanStayQueued() == false;
        }

        private void CancelQueuedAssignments()
        {
            while (TaskQueue.Count > 0)
            {
                TaskQueue.Dequeue()?.Cancel();
            }
        }
    }
}
