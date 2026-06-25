using System.Collections.Generic;
using _Project.Core.CustomLogging;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.AgentSystem.AgentModules.CustomerModule;
using _Project.Gameplay.AgentSystem.Astar;
using _Project.Gameplay.TaskSystem.EventChannel;
using _Project.Gameplay.TaskSystem.Managers;
using _Project.Gameplay.TaskSystem.TaskStructs;
using _Project.UI.Scripts.MVP._Main.Main;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.TaskObject
{
    public class EnterDoor : InteractTaskObject
    {
        private const int REACHABLE_POSITION_SEARCH_RADIUS = 3;
        private const float MIN_LINE_POSITION_DISTANCE = 0.5f;

        [SerializeField] private GenerateTaskChannel generateTaskChannel;
        [SerializeField] private OpenCloseEventChannel openCloseEventChannel;
        [SerializeField] private Transform interactionPosition;
        [SerializeField] private float lineDistanceOffset;
        
        private readonly List<Agent> _waitingAgents = new();
        private readonly HashSet<Agent> _readyAgents = new();
        private readonly HashSet<Agent> _reservedAgents = new();
        private readonly List<Vector3> _linePositionCache = new();

        private InteractObjectManager _interactObjectManager;
        private bool _hasPendingGuideRequest;
        
        protected override void OnEnable()
        {
            base.OnEnable();
            CacheInteractObjectManager();
        }

        public override void Interact(Agent agent)
        {
            TaskModule agentTaskModule = agent.GetModule<TaskModule>();
            Debug.Assert(agentTaskModule != null,
                $"Interact는 TaskModule가진 Agent만 할 수 있습니다. : {agent.AgentId}");

            if (agentTaskModule.TaskType == TaskTypeEnum.Customer)
            {
                EnqueueCustomer(agent);
                _readyAgents.Add(agent);
                TryRequestServerGuideTask();
                return;
            }

            if (agentTaskModule.TaskType == TaskTypeEnum.Server)
            {
                HandleServerInteract(agent, agentTaskModule);
            }
        }
        
        public override void HandleTaskCanceled(Agent agent)
        {
            base.HandleTaskCanceled(agent);

            if (agent == null || _waitingAgents.Contains(agent) == false)
            {
                return;
            }

            DequeueCustomer(agent);
            CustomerDiningModule diningModule = agent.GetModule<CustomerDiningModule>();
            if (diningModule != null && diningModule.IsLeavingRestaurant == false)
            {
                diningModule.LeaveRestaurant(TaskCancelReason.PathFailed);
            }

            TryRequestServerGuideTask();
        }

        public Vector3 ReserveCustomerLinePosition(Agent agent)
        {
            EnqueueCustomer(agent);
            return GetLinePosition(GetLineIndex(agent));
        }

        public bool TryRequestServerGuideTask()
        {
            if (RestaurantRuntimeCloseService.IsClosed ||
                openCloseEventChannel != null && openCloseEventChannel.IsOpen == false)
            {
                return false;
            }

            CleanupMissingAgents();

            if (_hasPendingGuideRequest ||
                generateTaskChannel == null)
            {
                return false;
            }

            if (!TryGetFirstReadyCustomer(out Agent readyCustomer))
            {
                return false;
            }

            if (!TryFindTable(out CustomerTable table))
            {
                BeginTableWaitForReadyCustomers();
                return false;
            }

            List<ITask> taskList = new()
            {
                new MoveTask(GetInteractPosition(TaskTypeEnum.Server)),
                new BeginServerGuideTask(this, readyCustomer, table),
                new MoveToInteractObjectTask(table, TaskTypeEnum.Server),
                new WorkTask(table, true)
            };

            TaskAssignment assignment = new(
                taskList,
                TaskTypeEnum.Server,
                server => TryAssignGuide(server, readyCustomer, table),
                () => HandleGuideAssignmentCanceled(readyCustomer),
                () => CanKeepGuideAssignmentQueued(readyCustomer, table));

            _hasPendingGuideRequest = true;
            generateTaskChannel.Raise(assignment);

            if (assignment.IsAcceptedByManager == false)
            {
                _hasPendingGuideRequest = false;
                return false;
            }

            return true;
        }

        public override Vector3 GetInteractPosition(TaskTypeEnum interactType = TaskTypeEnum.None)
        {
            if (interactType == TaskTypeEnum.Customer)
            {
                return GetLinePosition(_waitingAgents.Count);
            }

            return ResolveReachablePosition(GetInteractionBasePosition(), Vector3.up);
        }

        public override Vector3 GetNearestInteractPosition(
            Vector3 fromPosition,
            TaskTypeEnum interactType = TaskTypeEnum.None)
        {
            if (interactType == TaskTypeEnum.Customer)
            {
                return GetLinePosition(_waitingAgents.Count);
            }

            Vector3 basePosition = GetInteractionBasePosition();
            return ResolveReachablePosition(basePosition, fromPosition - basePosition);
        }

        private void HandleServerInteract(Agent server, TaskModule serverTaskModule)
        {
            _hasPendingGuideRequest = false;

            if (!TryGetFirstReadyCustomer(out Agent customer) ||
                !TryFindTable(out CustomerTable table) ||
                !table.TryReserve())
            {
                TryRequestServerGuideTask();
                return;
            }

            ServerGuidanceModule guidanceModule = server.GetModule<ServerGuidanceModule>();
            if (guidanceModule == null)
            {
                CLog.LogError($"{server.AgentName}에 ServerGuidanceModule이 없습니다.");
                table.CancelReservation();
                TryRequestServerGuideTask();
                return;
            }

            if (!guidanceModule.TryBeginGuide(customer, table, this))
            {
                table.CancelReservation();
                TryRequestServerGuideTask();
                return;
            }

            customer.GetModule<CustomerDiningModule>()?.EndTableWaiting();
            DequeueCustomer(customer);
            guidanceModule.MarkGuideStarted();
            PrepareCustomerFollow(customer, server);
            serverTaskModule.AddTask(new MoveToInteractObjectTask(table, TaskTypeEnum.Server));
            serverTaskModule.AddTask(new WorkTask(table, true));
            TryRequestServerGuideTask();
        }

        private void EnqueueCustomer(Agent agent)
        {
            if (agent == null || _waitingAgents.Contains(agent))
            {
                return;
            }

            _waitingAgents.Add(agent);
        }

        private void DequeueCustomer(Agent agent)
        {
            int index = _waitingAgents.IndexOf(agent);
            if (index < 0)
            {
                return;
            }

            _waitingAgents.RemoveAt(index);
            _readyAgents.Remove(agent);
            _reservedAgents.Remove(agent);
             
            for (int i = index; i < _waitingAgents.Count; i++)
            {
                UpdateCustomerPosition(_waitingAgents[i], i);
            }
        }

        private int GetLineIndex(Agent agent) => _waitingAgents.IndexOf(agent);

        private bool TryGetFirstReadyCustomer(out Agent customer)
        {
            CleanupMissingAgents();

            foreach (Agent agent in _waitingAgents)
            {
                if (!_readyAgents.Contains(agent))
                {
                    continue;
                }

                if (_reservedAgents.Contains(agent))
                {
                    continue;
                }

                customer = agent;
                return true;
            }

            customer = null;
            return false;
        }
        
        private void UpdateCustomerPosition(Agent agent, int lineIndex)
        {
            if (_readyAgents.Contains(agent) == false)
            {
                return;
            }

            TaskModule taskModule = agent.GetModule<TaskModule>();
            if (taskModule == null)
            {
                return;
            }

            Vector3 newPosition = GetLinePosition(lineIndex);
            taskModule.CancelCurrentTask();
            taskModule.ClearTasks();
            taskModule.AddTask(new MoveTask(newPosition));
        }

        private Vector3 GetLinePosition(int lineIndex)
        {
            if (lineIndex < 0)
            {
                return GetInteractionBasePosition();
            }

            RebuildLinePositionCache(lineIndex + 1);
            return _linePositionCache[lineIndex];
        }

        private Vector3 GetInteractionBasePosition()
        {
            return interactionPosition == null ? transform.position : interactionPosition.position;
        }

        private Vector3 ResolveReachablePosition(Vector3 preferredPosition, Vector3 preferredDirection)
        {
            if (AstarManager.IsNullInstance)
            {
                return preferredPosition;
            }

            AstarManager astarManager = AstarManager.Instance;
            if (IsReachableLineCandidate(astarManager, preferredPosition, null))
            {
                return preferredPosition;
            }

            Vector2Int baseCell = astarManager.WorldToCellPosition(preferredPosition);
            Vector2Int primaryDirection = ToCellDirection(preferredDirection);
            if (primaryDirection != Vector2Int.zero &&
                TryFindReachableCellInDirection(
                    astarManager,
                    baseCell,
                    primaryDirection,
                    null,
                    out Vector3 position))
            {
                return position;
            }

            if (TryFindNearestReachableCell(astarManager, baseCell, preferredPosition, null, out position))
            {
                return position;
            }

            return preferredPosition;
        }

        private bool TryFindReachableCellInDirection(
            AstarManager astarManager,
            Vector2Int baseCell,
            Vector2Int direction,
            List<Vector3> excludedPositions,
            out Vector3 position)
        {
            for (int i = 1; i <= REACHABLE_POSITION_SEARCH_RADIUS; i++)
            {
                Vector3 candidate = astarManager.CellToWorldPosition(baseCell + direction * i);
                if (IsReachableLineCandidate(astarManager, candidate, excludedPositions))
                {
                    position = candidate;
                    return true;
                }
            }

            position = default;
            return false;
        }

        private bool TryFindNearestReachableCell(
            AstarManager astarManager,
            Vector2Int baseCell,
            Vector3 preferredPosition,
            List<Vector3> excludedPositions,
            out Vector3 position)
        {
            float minDistance = float.MaxValue;
            position = default;

            for (int x = -REACHABLE_POSITION_SEARCH_RADIUS; x <= REACHABLE_POSITION_SEARCH_RADIUS; x++)
            {
                for (int y = -REACHABLE_POSITION_SEARCH_RADIUS; y <= REACHABLE_POSITION_SEARCH_RADIUS; y++)
                {
                    Vector3 candidate = astarManager.CellToWorldPosition(baseCell + new Vector2Int(x, y));
                    if (IsReachableLineCandidate(astarManager, candidate, excludedPositions) == false)
                    {
                        continue;
                    }

                    float distance = Vector3.SqrMagnitude(candidate - preferredPosition);
                    if (distance >= minDistance)
                    {
                        continue;
                    }

                    minDistance = distance;
                    position = candidate;
                }
            }

            return minDistance < float.MaxValue;
        }

        private void RebuildLinePositionCache(int count)
        {
            _linePositionCache.Clear();
            for (int i = 0; i < count; i++)
            {
                Vector3 linePosition = GetRawLinePosition(i);
                _linePositionCache.Add(ResolveReachableLinePosition(linePosition, Vector3.down, _linePositionCache));
            }
        }

        private Vector3 GetRawLinePosition(int lineIndex)
        {
            return GetInteractionBasePosition() +
                   Vector3.down * (Mathf.Abs(lineDistanceOffset) * (lineIndex + 1));
        }

        private Vector3 ResolveReachableLinePosition(
            Vector3 preferredPosition,
            Vector3 preferredDirection,
            List<Vector3> excludedPositions)
        {
            if (AstarManager.IsNullInstance)
            {
                return preferredPosition;
            }

            AstarManager astarManager = AstarManager.Instance;
            if (IsReachableLineCandidate(astarManager, preferredPosition, excludedPositions))
            {
                return preferredPosition;
            }

            Vector2Int baseCell = astarManager.WorldToCellPosition(preferredPosition);
            Vector2Int primaryDirection = ToCellDirection(preferredDirection);
            if (primaryDirection != Vector2Int.zero &&
                TryFindReachableCellInDirection(
                    astarManager,
                    baseCell,
                    primaryDirection,
                    excludedPositions,
                    out Vector3 position))
            {
                return position;
            }

            if (TryFindNearestReachableCell(
                    astarManager,
                    baseCell,
                    preferredPosition,
                    excludedPositions,
                    out position))
            {
                return position;
            }

            return preferredPosition;
        }

        private bool IsReachableLineCandidate(
            AstarManager astarManager,
            Vector3 candidate,
            List<Vector3> excludedPositions)
        {
            if (astarManager.CanMovePosition(candidate) == false)
            {
                return false;
            }

            if (excludedPositions == null)
            {
                return true;
            }

            Vector2Int candidateCell = astarManager.WorldToCellPosition(candidate);
            for (int i = 0; i < excludedPositions.Count; i++)
            {
                if (astarManager.WorldToCellPosition(excludedPositions[i]) == candidateCell ||
                    Vector3.SqrMagnitude(excludedPositions[i] - candidate) <
                    MIN_LINE_POSITION_DISTANCE * MIN_LINE_POSITION_DISTANCE)
                {
                    return false;
                }
            }

            return true;
        }

        private Vector2Int ToCellDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector2Int.zero;
            }

            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                return new Vector2Int(direction.x > 0f ? 1 : -1, 0);
            }

            return new Vector2Int(0, direction.y > 0f ? 1 : -1);
        }
        
        private void BeginTableWaitForReadyCustomers()
        {
            for (int i = 0; i < _waitingAgents.Count; i++)
            {
                Agent agent = _waitingAgents[i];
                if (_readyAgents.Contains(agent) == false)
                {
                    continue;
                }

                if (_reservedAgents.Contains(agent))
                {
                    continue;
                }

                agent.GetModule<CustomerDiningModule>()?.BeginTableWaiting();
            }
        }

        public void CancelWaitingCustomers(
            TaskCancelReason reason,
            bool shouldLeaveCustomers = true)
        {
            _hasPendingGuideRequest = false;

            if (shouldLeaveCustomers)
            {
                for (int i = 0; i < _waitingAgents.Count; i++)
                {
                    _waitingAgents[i]?.GetModule<CustomerDiningModule>()?.LeaveRestaurant(reason);
                }
            }

            _waitingAgents.Clear();
            _readyAgents.Clear();
            _reservedAgents.Clear();
        }

        public bool TryStartReservedGuide(
            Agent server,
            Agent customer,
            CustomerTable table)
        {
            ServerGuidanceModule guidanceModule = server?.GetModule<ServerGuidanceModule>();
            if (guidanceModule == null ||
                guidanceModule.GuidingCustomer != customer ||
                guidanceModule.TargetTable != table ||
                IsGuideCandidateValid(customer, table) == false ||
                _reservedAgents.Contains(customer) == false)
            {
                CancelGuideReservation(server, customer, table);
                return false;
            }

            customer.GetModule<CustomerDiningModule>()?.EndTableWaiting();
            DequeueCustomer(customer);
            guidanceModule.MarkGuideStarted();
            PrepareCustomerFollow(customer, server);
            return true;
        }

        public void CancelGuideReservation(
            Agent server,
            Agent customer,
            CustomerTable table)
        {
            _reservedAgents.Remove(customer);

            ServerGuidanceModule guidanceModule = server?.GetModule<ServerGuidanceModule>();
            if (guidanceModule != null &&
                guidanceModule.GuidingCustomer == customer &&
                guidanceModule.TargetTable == table)
            {
                guidanceModule.CancelGuide();
            }
            else
            {
                table?.CancelReservation();
            }

            if (customer != null && customer.IsActivate && _waitingAgents.Contains(customer))
            {
                UpdateCustomerPosition(customer, GetLineIndex(customer));
            }

            TryRequestServerGuideTask();
        }

        public void RestoreGuideCustomer(Agent customer)
        {
            if (customer == null || customer.IsActivate == false)
            {
                return;
            }

            _reservedAgents.Remove(customer);
            EnqueueCustomer(customer);
            _readyAgents.Add(customer);
            UpdateCustomerPosition(customer, GetLineIndex(customer));
            TryRequestServerGuideTask();
        }

        private void PrepareCustomerFollow(Agent customer, Agent server)
        {
            TaskModule customerTaskModule = customer.GetModule<TaskModule>();
            customerTaskModule?.CancelCurrentTask();
            customerTaskModule?.ClearTasks();
            customer.GetModule<PathAgentModule>()?.ResetPath();

            IAgentMoveModule moveModule = customer.GetModule<IAgentMoveModule>();
            moveModule?.StopImmediate();
            moveModule?.StartFollow(server.transform, new Vector2(0, -1f));
        }

        private bool TryAssignGuide(
            Agent server,
            Agent customer,
            CustomerTable table)
        {
            _hasPendingGuideRequest = false;

            if (IsGuideCandidateValid(customer, table) == false ||
                table.TryReserve() == false)
            {
                return false;
            }

            ServerGuidanceModule guidanceModule = server.GetModule<ServerGuidanceModule>();
            if (guidanceModule == null)
            {
                CLog.LogError($"{server.AgentName}에 ServerGuidanceModule이 없습니다.");
                table.CancelReservation();
                return false;
            }

            if (guidanceModule.TryBeginGuide(customer, table, this) == false)
            {
                table.CancelReservation();
                return false;
            }

            _reservedAgents.Add(customer);
            customer.GetModule<CustomerDiningModule>()?.EndTableWaiting();
            TryRequestServerGuideTask();
            return true;
        }

        private void HandleGuideAssignmentCanceled(Agent customer)
        {
            _hasPendingGuideRequest = false;
            _reservedAgents.Remove(customer);
            TryRequestServerGuideTask();
        }

        private bool CanKeepGuideAssignmentQueued(Agent customer, CustomerTable table)
        {
            if (RestaurantRuntimeCloseService.IsClosed ||
                openCloseEventChannel != null && openCloseEventChannel.IsOpen == false)
            {
                return false;
            }

            return IsGuideCandidateValid(customer, table) && table.CanReserve;
        }

        private bool IsGuideCandidateValid(Agent customer, CustomerTable table)
        {
            if (customer == null ||
                customer.IsActivate == false ||
                table == null ||
                _waitingAgents.Contains(customer) == false ||
                _readyAgents.Contains(customer) == false)
            {
                return false;
            }

            CustomerDiningModule diningModule = customer.GetModule<CustomerDiningModule>();
            return diningModule == null || diningModule.IsLeavingRestaurant == false;
        }

        private void CleanupMissingAgents()
        {
            bool changed = false;
            for (int i = _waitingAgents.Count - 1; i >= 0; i--)
            {
                if (_waitingAgents[i] != null &&
                    _waitingAgents[i].IsActivate)
                {
                    continue;
                }

                _waitingAgents.RemoveAt(i);
                changed = true;
            }

            _readyAgents.RemoveWhere(a => a == null || a.IsActivate == false || !_waitingAgents.Contains(a));
            _reservedAgents.RemoveWhere(a => a == null || a.IsActivate == false || !_waitingAgents.Contains(a));
            
            if (changed)
            {
                for (int i = 0; i < _waitingAgents.Count; i++)
                {
                    UpdateCustomerPosition(_waitingAgents[i], i);
                }
            }
        }

        private void CacheInteractObjectManager()
        {
            if (_interactObjectManager == null && !InteractObjectManager.IsNullInstance)
            {
                _interactObjectManager = InteractObjectManager.Instance;
            }
        }

        private bool TryFindTable(out CustomerTable table)
        {
            CacheInteractObjectManager();
            table = _interactObjectManager?.FindNearInteractableTaskObject<CustomerTable>(
                transform.position, TaskTypeEnum.Server);
            return table != null;
        }
    }
}
