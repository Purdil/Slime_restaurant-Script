using System;
using System.Collections.Generic;
using _Project.Core.CustomLogging;
using _Project.Core.ModuleSystem;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem._Agent.TaskAgent;
using _Project.Gameplay.StatSystem;
using _Project.Gameplay.TaskSystem;
using _Project.Gameplay.TaskSystem.EventChannel;
using _Project.Gameplay.TaskSystem.Managers;
using _Project.Gameplay.TaskSystem.TaskObject;
using _Project.Gameplay.TaskSystem.TaskStructs;
using _Project.UI.Scripts.MVP._Main.Main;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules.CommonModule
{
    public class TaskModule : MonoBehaviour, IModule , IApplyProfileModule, IAgentStatConsumer
    {
        private const float ASSIGNMENT_DISTANCE_TIE_THRESHOLD = 1f;

        private static readonly List<TaskModule> ActiveModules = new();
        private static readonly List<TaskModule> CandidateModules = new();
        private static int _managerAssignmentOrder;

        [SerializeField] private SendingTaskEventChannel taskChannel;
        [SerializeField] private OpenCloseEventChannel openCloseEventChannel;
        [SerializeField] private List<StatDataSO> stats;
        public TaskTypeEnum TaskType { get; private set; }
        public event Action OnEndTask;

        private Dictionary<StatTypeSO,float> _statValueDict = new();
        public bool IsTasking { get; private set; } = false;
        private Queue<ITask> _taskQueue = new();
        private Agent _owner;
        private ITask _currentTask;

        private float _taskElapsed;
        private float _currentWorkAmount;
        private InteractTaskObject _currentInteractObj;
        private float _multiplyTaskSpeed = 1f;
        private int _lastManagerAssignmentOrder;

        public void Initialize(ModuleOwner moduleOwner)
        {
            _owner = moduleOwner as Agent;
            Debug.Assert(_owner != null, "캐스팅에 실패 했잖아. 당연히 Task 모듈 쓰는데 부모가 TaskAgent가 아니면 어떡하냐? 이 멍청아");
            TaskType = _owner == null ? TaskTypeEnum.None : _owner.DefaultTaskType;
        }

        private void Update()
        {
            if (IsTasking && _currentInteractObj != null)
            {
                _taskElapsed += Time.deltaTime * _multiplyTaskSpeed;
                if (_currentWorkAmount <= _taskElapsed)
                {
                    EndWork();
                }
            }
        }

        private void OnEnable()
        {
            if (ActiveModules.Contains(this) == false)
            {
                ActiveModules.Add(this);
            }

            Debug.Assert(taskChannel != null, $"{gameObject.name}에 TaskChannel이 연결되지 않았습니다.");
            if (taskChannel != null)
            {
                taskChannel.OnEvent += HandleTaskChannel;
            }

            if (RestaurantRuntimeCloseService.IsClosed ||
                openCloseEventChannel != null && openCloseEventChannel.IsOpen == false)
            {
                CancelAllTasks(TaskCancelReason.RestaurantClosed);
            }
        }
        private void OnDisable()
        {
            ActiveModules.Remove(this);

            if (taskChannel != null)
            {
                taskChannel.OnEvent -= HandleTaskChannel;
            }

            CancelAllTasks(TaskCancelReason.Manual);
            OnEndTask = null;
        }

        private void HandleTaskChannel(TaskAssignment assignment)
        {
            TryReceiveManagerTask(assignment);
        }

        public void ApplyProfile(AgentProfileSO taskAgentDataSo)
        {
            TaskType = taskAgentDataSo.TaskType;
            ClearTasks();
            ResetWorkState();
            OnEndTask = null;
        }

        public ITask GetTask()
        {
            return _taskQueue.Count > 0 ? _taskQueue.Dequeue() : null;
        }

        public bool HasTask()
        {
            return _taskQueue.Count > 0;
        }

        //작업을 실행.
        public void ExecuteTask()
        {
            if (_taskQueue.Count == 0)
            {
                return;
            }

            _currentTask = _taskQueue.Dequeue();
            _currentTask.OnEndTask += HandleEndTask;
            IsTasking = true;
            _currentTask.Execute(_owner);
        }

        private void HandleEndTask()
        {
            IsTasking = false;
            if (_currentTask != null)
            {
                _currentTask.OnEndTask -= HandleEndTask;
                _currentTask = null;
            }
        }

        public static void CancelAllActiveStaffTasks(TaskCancelReason reason)
        {
            for (int i = ActiveModules.Count - 1; i >= 0; i--)
            {
                TaskModule module = ActiveModules[i];
                if (module == null)
                {
                    ActiveModules.RemoveAt(i);
                    continue;
                }

                if (module.TaskType == TaskTypeEnum.Customer)
                {
                    continue;
                }

                module.CancelAllTasks(reason);
            }
        }

        public static bool TryAssignManagerTask(TaskAssignment assignment)
        {
            if (assignment == null || assignment.CanStayQueued() == false)
            {
                return false;
            }

            FillCandidateModules(assignment);
            if (CandidateModules.Count == 0)
            {
                return false;
            }

            bool hasReferencePosition = TryGetAssignmentReferencePosition(assignment, out Vector3 referencePosition);
            while (CandidateModules.Count > 0 && assignment.CanStayQueued())
            {
                int candidateIndex = GetBestCandidateIndex(referencePosition, hasReferencePosition);
                TaskModule candidate = CandidateModules[candidateIndex];
                CandidateModules.RemoveAt(candidateIndex);

                if (candidate.TryReceiveManagerTask(assignment))
                {
                    CandidateModules.Clear();
                    return true;
                }
            }

            CandidateModules.Clear();
            return false;
        }

        public void CancelCurrentTask(TaskCancelReason reason = TaskCancelReason.Manual)
        {
            if (_currentTask == null)
            {
                return;
            }

            ITask cancelTask = _currentTask;
            cancelTask.OnEndTask -= HandleEndTask;
            _currentTask = null;
            cancelTask.Cancel(_owner);
            _owner.GetModule<ServerGuidanceModule>()?.CancelGuide(reason);
            ResetWorkState();
        }

        public void CancelAllTasks(TaskCancelReason reason)
        {
            CancelCurrentTask(reason);
            ClearTasks();

            if (_owner == null)
            {
                ResetWorkState();
                return;
            }

            _owner.GetModule<ServerGuidanceModule>()?.CancelGuide(reason);
            _owner.GetModule<PathAgentModule>()?.ResetPath();
            IAgentMoveModule moveModule = _owner.GetModule<IAgentMoveModule>();
            moveModule?.StopFollow();
            moveModule?.StopImmediate();
            _owner.Renderer?.ControlManualAnimation(true);
            ResetWorkState();
        }

        public void ClearTasks()
        {
            while (_taskQueue.Count > 0)
            {
                ITask task = _taskQueue.Dequeue();
                task?.Cancel(_owner);
            }
        }


        //작업이 실행
        public void StartWork(InteractTaskObject obj, bool complete, float workAmount = -1f)
        {
            obj.Interact(_owner);
            _currentInteractObj = obj;
            IsTasking = true;
            _currentWorkAmount = ResolveWorkAmount(obj, workAmount);
            if (obj.EffectStatType && _statValueDict.TryGetValue(obj.EffectStatType, out float multiplyValue))
            {
                _multiplyTaskSpeed = multiplyValue;
            }

            if (complete || _currentWorkAmount <= 0f)
            {
                EndWork();
            }
        }

        private void EndWork()
        {
            if (IsTasking == false && _currentInteractObj == null && _currentTask == null)
            {
                return;
            }

            ResetWorkState();
            OnEndTask?.Invoke();
        }

        public void AddTask(ITask obj)
        {
            _taskQueue.Enqueue(obj);
        }

        public bool TryReceiveManagerTask(TaskAssignment assignment)
        {
            if (CanReceiveManagerTask(assignment) == false)
            {
                return false;
            }

            if (assignment.TryAssign(_owner, TaskType) == false)
            {
                return false;
            }

            foreach (ITask task in assignment.Tasks)
            {
                AddTask(task);
            }

            _lastManagerAssignmentOrder = ++_managerAssignmentOrder;
            return true;
        }

        private bool CanReceiveManagerTask(TaskAssignment assignment)
        {
            if (RestaurantRuntimeCloseService.IsClosed ||
                openCloseEventChannel != null && openCloseEventChannel.IsOpen == false)
            {
                return false;
            }

            if (assignment == null || IsTasking || _taskQueue.Count > 0)
            {
                return false;
            }

            return assignment.CanAssignTo(_owner, TaskType);
        }

       

        private void ResetWorkState()
        {
            if (_currentTask != null)
            {
                _currentTask.OnEndTask -= HandleEndTask;
            }
            
            IsTasking = false;
            _currentInteractObj = null;
            _taskElapsed = 0f;
            _currentWorkAmount = 0f;
            _currentTask = null;
            _multiplyTaskSpeed = 1f;
        }

        private float ResolveWorkAmount(InteractTaskObject obj, float workAmount)
        {
            if (workAmount >= 0f)
            {
                return workAmount;
            }

            return obj.DefinitionSO == null ? 0f : obj.DefinitionSO.baseWorkAmount;
        }

        public void RefreshStats(IAgentStatProvider statProvider)
        {
            foreach (StatDataSO dataSo in stats)
            {
                if (statProvider.TryGetStatData(dataSo.StatType, out float statValue))
                {
                    _statValueDict[dataSo.StatType] = statValue;
                }
            }
        }

        public void UpdateStats(StatTypeSO updateType, float updateValue)
        {
            if (_statValueDict.ContainsKey(updateType))
            {
                _statValueDict[updateType] = updateValue;
            }
        }

        private static void FillCandidateModules(TaskAssignment assignment)
        {
            CandidateModules.Clear();
            for (int i = ActiveModules.Count - 1; i >= 0; i--)
            {
                TaskModule module = ActiveModules[i];
                if (module == null)
                {
                    ActiveModules.RemoveAt(i);
                    continue;
                }

                if (module.CanReceiveManagerTask(assignment))
                {
                    CandidateModules.Add(module);
                }
            }
        }

        private static int GetBestCandidateIndex(Vector3 referencePosition, bool hasReferencePosition)
        {
            int bestIndex = 0;
            for (int i = 1; i < CandidateModules.Count; i++)
            {
                if (IsBetterCandidate(
                        CandidateModules[i],
                        CandidateModules[bestIndex],
                        referencePosition,
                        hasReferencePosition))
                {
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static bool IsBetterCandidate(
            TaskModule candidate,
            TaskModule currentBest,
            Vector3 referencePosition,
            bool hasReferencePosition)
        {
            if (hasReferencePosition)
            {
                float candidateDistance = candidate.GetAssignmentDistance(referencePosition);
                float currentBestDistance = currentBest.GetAssignmentDistance(referencePosition);
                if (Mathf.Abs(candidateDistance - currentBestDistance) > ASSIGNMENT_DISTANCE_TIE_THRESHOLD)
                {
                    return candidateDistance < currentBestDistance;
                }
            }

            return candidate._lastManagerAssignmentOrder < currentBest._lastManagerAssignmentOrder;
        }

        private static bool TryGetAssignmentReferencePosition(
            TaskAssignment assignment,
            out Vector3 referencePosition)
        {
            foreach (ITask task in assignment.Tasks)
            {
                if (task is MoveTask moveTask)
                {
                    referencePosition = moveTask.Position;
                    return true;
                }

                if (task is WorkTask workTask && workTask.targetObject != null)
                {
                    referencePosition = workTask.targetObject.transform.position;
                    return true;
                }
            }

            referencePosition = default;
            return false;
        }

        private float GetAssignmentDistance(Vector3 referencePosition)
        {
            if (_owner == null)
            {
                return float.MaxValue;
            }

            return Vector3.SqrMagnitude(_owner.transform.position - referencePosition);
        }
    }
}
