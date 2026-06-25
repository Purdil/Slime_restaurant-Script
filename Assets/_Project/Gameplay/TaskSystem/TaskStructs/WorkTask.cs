using System;
using _Project.Core.CustomLogging;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.TaskStructs
{
    public struct WorkTask : ITask
    {
        public InteractTaskObject targetObject;
        public event Action OnEndTask;
        private TaskModule _taskModule;
        private bool _completeImmediately;
        private float _workAmount;
        private int _animParamHash;
        private IRenderer _renderer;

        public WorkTask(InteractTaskObject targetObject, bool completeImmediately)
        {
            this.targetObject = targetObject;
            OnEndTask = null;
            _taskModule = null;
            _completeImmediately = completeImmediately;
            _workAmount = -1f;
            _animParamHash = 0;
            _renderer = null;
        }

        public WorkTask(
            InteractTaskObject targetObject,
            bool completeImmediately,
            float workAmount,
            AnimParamSO animParam)
        {
            this.targetObject = targetObject;
            OnEndTask = null;
            _taskModule = null;
            _completeImmediately = completeImmediately;
            _workAmount = Mathf.Max(0f, workAmount);
            _animParamHash = animParam == null ? 0 : animParam.ParamHash;
            _renderer = null;
        }

        public void Execute(Agent agent)
        {
            if (targetObject == null)
            {
                CLog.LogError("WorkTask targetObject가 비어 있습니다.");
                OnEndTask?.Invoke();
                return;
            }

            TaskModule taskModule = agent.GetModule<TaskModule>();
            if (taskModule == null)
            {
                CLog.LogError($"TaskModule이 없는 Agent는 일 할 수 없습니다. : {agent.AgentName}");
                OnEndTask?.Invoke();
                return;
            }

            _renderer = agent.Renderer;
            _taskModule = taskModule;
            _taskModule.OnEndTask += HandleOnEndTask;

            int animParamHash = ResolveAnimParamHash();
            if (animParamHash != 0)
            {
                _renderer?.ControlManualAnimation(false, animParamHash);
            }

            _taskModule.StartWork(targetObject, _completeImmediately, _workAmount);
        }

        public void Cancel(Agent agent)
        {
            if (targetObject != null)
            {
                targetObject.HandleTaskCanceled(agent);
            }

            if (_taskModule != null)
            {
                _taskModule.OnEndTask -= HandleOnEndTask;
            }

            _renderer?.ControlManualAnimation(true);
        }

        public void HandleOnEndTask()
        {
            OnEndTask?.Invoke();
            _taskModule.OnEndTask -= HandleOnEndTask;
            _renderer?.ControlManualAnimation(true);
        }

        private int ResolveAnimParamHash()
        {
            if (_animParamHash != 0)
            {
                return _animParamHash;
            }

            if (targetObject.DefinitionSO == null || targetObject.DefinitionSO.playAnimParam == null)
            {
                return 0;
            }

            return targetObject.DefinitionSO.playAnimParam.ParamHash;
        }
    }
}
