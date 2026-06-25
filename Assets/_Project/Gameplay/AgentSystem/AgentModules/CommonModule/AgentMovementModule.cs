using _Project.Core.ModuleSystem;
using _Project.Gameplay.AgentSystem.AgentModules.AvoidanceModule;
using _Project.Gameplay.AgentSystem.Astar;
using _Project.Gameplay.StatSystem;
using System.Collections.Generic;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules.CommonModule
{
    public class AgentMovementModule : MonoBehaviour, IModule, IAgentMoveModule, IAgentStatConsumer
    {
        private const float MIN_VECTOR_SQR = 0.0001f;
        private const int VELOCITY_CANDIDATE_COUNT = 5;

        private static readonly List<AgentMovementModule> ActiveModules = new();
        private static readonly Vector2Int[] RelocateDirections =
        {
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.up,
            Vector2Int.down
        };

        [SerializeField] private StatTypeSO speedStatType;
        [SerializeField] private float defaultMoveSpeed = 5f;
        [SerializeField] private float followStopDistance = 0.08f;
        [SerializeField] private float followResumeDistance = 0.18f;
        [SerializeField] private float followUpdateDistance = 0.05f;
        [SerializeField] private float followResponsiveness = 6f;
        [SerializeField] private float followCatchUpSpeedMultiplier = 1.25f;
        [SerializeField] private float maxMoveDeltaTime = 0.05f;

        public Vector2 CurVelocity { get; private set; }
        public float MagCurVelocity { get; private set; }
        public bool CanMove { get; private set; } = true;
        public bool IsFollowing { get; private set; }
        public bool IsMovingForAnimation { get; private set; }

        private readonly Vector2[] _velocityCandidates = new Vector2[VELOCITY_CANDIDATE_COUNT];
        private ModuleOwner _owner;
        private IAgentAvoidanceModule _avoidanceModule;
        private Vector3 _destination;
        private AstarManager _astarManager;
        private float _moveSpeed;
        private Vector2 _currentVelocity;
        private Vector2 _desiredVelocity;
        private Transform _followTarget;
        private Vector2 _followOffset;
        private Vector2 _followTargetVelocity;
        private Vector2 _previousFollowTargetPosition;
        private bool _hasFollowTargetPosition;

        private void OnValidate()
        {
            defaultMoveSpeed = Mathf.Max(0f, defaultMoveSpeed);
            followStopDistance = Mathf.Max(0f, followStopDistance);
            followResumeDistance = Mathf.Max(followStopDistance, followResumeDistance);
            followUpdateDistance = Mathf.Max(0f, followUpdateDistance);
            followResponsiveness = Mathf.Max(0f, followResponsiveness);
            followCatchUpSpeedMultiplier = Mathf.Max(1f, followCatchUpSpeedMultiplier);
            maxMoveDeltaTime = Mathf.Max(0.01f, maxMoveDeltaTime);
        }

        private void OnEnable()
        {
            if (ActiveModules.Contains(this) == false)
            {
                ActiveModules.Add(this);
            }
        }

        private void Update()
        {
            if (CanMove == false)
            {
                return;
            }

            float deltaTime = Mathf.Min(Time.deltaTime, maxMoveDeltaTime);
            UpdateFollowDestination(deltaTime);
            Move(deltaTime);
            CurVelocity = _currentVelocity;
            MagCurVelocity = _currentVelocity.magnitude;
            
            UpdateAnimationMotionState();
        }

        private void OnDisable()
        {
            ActiveModules.Remove(this);
        }

        public static void RelocateAgentsFromBlockedCells(Vector2Int[] blockedCells)
        {
            if (blockedCells == null || blockedCells.Length == 0 || AstarManager.IsNullInstance)
            {
                return;
            }

            AstarManager astarManager = AstarManager.Instance;
            for (int i = ActiveModules.Count - 1; i >= 0; i--)
            {
                AgentMovementModule module = ActiveModules[i];
                if (module == null)
                {
                    ActiveModules.RemoveAt(i);
                    continue;
                }

                Transform targetTransform = module._owner == null ? module.transform : module._owner.transform;
                Vector2Int currentCell = astarManager.WorldToCellPosition(targetTransform.position);
                if (ContainsCell(blockedCells, currentCell) == false)
                {
                    continue;
                }

                if (TryFindRelocationPosition(astarManager, currentCell, blockedCells, out Vector3 position))
                {
                    module.SetPosition(position);
                    PathAgentModule pathModule = module._owner == null
                        ? null
                        : module._owner.GetModule<PathAgentModule>();

                    if (pathModule != null)
                    {
                        pathModule.RepathCurrentDestination();
                    }
                    continue;
                }

                module.StopImmediate();
            }
        }

        public void Initialize(ModuleOwner moduleOwner)
        {
            _owner = moduleOwner;
            _avoidanceModule = moduleOwner.GetModule<IAgentAvoidanceModule>();
            CanMove = true;
            _moveSpeed = defaultMoveSpeed;
            _destination = _owner.transform.position;

            if (!AstarManager.IsNullInstance)
            {
                _astarManager = AstarManager.Instance;
            }

            UpdateAvoidanceMotionState();
        }

        public void RefreshStats(IAgentStatProvider statProvider)
        {
            if (statProvider.TryGetStatData(speedStatType, out float value))
            {
                _moveSpeed = value;
            }
        }

        public void UpdateStats(StatTypeSO updateType, float updateValue)
        {
            if (updateType == speedStatType)
            {
                _moveSpeed = updateValue;
            }
        }


        public void SetDestination(Vector3 destination)
        {
            StopFollow(false);
            _destination = destination;
        }

        public void SetCanMove(bool canMove)
        {
            _destination = _owner.transform.position;
            _currentVelocity = Vector2.zero;
            _desiredVelocity = Vector2.zero;
            CanMove = canMove;
            IsMovingForAnimation = false;
            UpdateAvoidanceMotionState();
        }

        public void StopImmediate()
        {
            StopFollow(false);
            _destination = _owner.transform.position;
            _currentVelocity = Vector2.zero;
            _desiredVelocity = Vector2.zero;
            IsMovingForAnimation = false;
            UpdateAvoidanceMotionState();
        }

        public void SetPosition(Vector3 position)
        {
            StopFollow(false);

            if (_owner != null)
            {
                _owner.transform.position = position;
            }
            else
            {
                transform.position = position;
            }

            _destination = position;
            _currentVelocity = Vector2.zero;
            _desiredVelocity = Vector2.zero;
            CurVelocity = Vector2.zero;
            MagCurVelocity = 0f;
            IsMovingForAnimation = false;
            _avoidanceModule?.RefreshRegistration();
            UpdateAvoidanceMotionState();
        }

        public void StartFollow(Transform target, Vector2 offset)
        {
            if (target == null)
            {
                StopFollow();
                return;
            }

            _followTarget = target;
            _followOffset = offset;
            IsFollowing = true;
            _previousFollowTargetPosition = target.position;
            _followTargetVelocity = Vector2.zero;
            _hasFollowTargetPosition = true;
            UpdateFollowDestination(0f, true);
        }

        public void StopFollow(bool stopImmediate = true)
        {
            IsFollowing = false;
            _followTarget = null;
            _followTargetVelocity = Vector2.zero;
            _hasFollowTargetPosition = false;

            if (stopImmediate)
            {
                StopImmediate();
            }
        }

        public bool TryGetFollowDestination(out Vector3 followDestination)
        {
            if (IsFollowing == false || _followTarget == null)
            {
                followDestination = default;
                return false;
            }

            followDestination = GetFollowPosition();
            return true;
        }

        private void Move(float deltaTime)
        {
            Vector2 currentPosition = _owner.transform.position;
            Vector2 destination = _destination;
            Vector2 toDestination = destination - currentPosition;
            float maxDistance = _moveSpeed * deltaTime;
            Vector2 avoidanceImpulse = ConsumeAvoidanceImpulse();

            if (TryGetFollowVelocity(currentPosition, out Vector2 followVelocity))
            {
                _desiredVelocity = followVelocity;
                Vector2 followBaseVelocity = _desiredVelocity + avoidanceImpulse;
                if (followBaseVelocity.sqrMagnitude <= MIN_VECTOR_SQR)
                {
                    _currentVelocity = Vector2.zero;
                    UpdateAvoidanceMotionState();
                    return;
                }

                if (TryMoveWithVelocity(currentPosition, followBaseVelocity, deltaTime))
                {
                    return;
                }

                _currentVelocity = Vector2.zero;
                UpdateAvoidanceMotionState();
                return;
            }

            if (toDestination.sqrMagnitude <= maxDistance * maxDistance)
            {
                _desiredVelocity = Vector2.zero;

                if (avoidanceImpulse.sqrMagnitude > MIN_VECTOR_SQR &&
                    TryMoveWithVelocity(currentPosition, avoidanceImpulse, deltaTime))
                {
                    return;
                }

                _currentVelocity = Vector2.zero;

                if (TryGetAstarManager(out AstarManager astarManager) == false ||
                    astarManager.CanMovePosition(destination))
                {
                    _owner.transform.position = destination;
                    _avoidanceModule?.RefreshRegistration();
                }

                UpdateAvoidanceMotionState();
                return;
            }

            _desiredVelocity = toDestination.normalized * _moveSpeed;
            Vector2 baseVelocity = _desiredVelocity + avoidanceImpulse;

            if (TryMoveWithVelocity(currentPosition, baseVelocity, deltaTime))
            {
                return;
            }

            _currentVelocity = Vector2.zero;
            UpdateAvoidanceMotionState();
        }

        private Vector2 ConsumeAvoidanceImpulse()
        {
            if (_avoidanceModule == null)
            {
                return Vector2.zero;
            }

            return _avoidanceModule.ConsumeAvoidanceImpulse();
        }

        private bool TryMoveWithVelocity(Vector2 currentPosition, Vector2 baseVelocity, float deltaTime)
        {
            int candidateCount = FillVelocityCandidates(baseVelocity);

            for (int i = 0; i < candidateCount; i++)
            {
                Vector2 velocity = _velocityCandidates[i];
                Vector2 nextPosition = currentPosition + velocity * deltaTime;

                if (TryGetAstarManager(out AstarManager astarManager) &&
                    astarManager.CanMovePosition(nextPosition) == false)
                {
                    continue;
                }

                _owner.transform.position = nextPosition;
                _currentVelocity = velocity;
                _avoidanceModule?.RefreshRegistration();
                UpdateAvoidanceMotionState();
                return true;
            }

            return false;
        }

        private int FillVelocityCandidates(Vector2 baseVelocity)
        {
            _velocityCandidates[0] = baseVelocity;

            if (_avoidanceModule == null || _avoidanceModule.IsAvoidanceEnabled == false)
            {
                return 1;
            }

            int candidateCount = _avoidanceModule.FillVelocityCandidates(baseVelocity, _velocityCandidates);

            if (candidateCount <= 0)
            {
                _velocityCandidates[0] = baseVelocity;
                return 1;
            }

            return candidateCount;
        }

        private void UpdateFollowDestination(float deltaTime, bool forceUpdate = false)
        {
            if (IsFollowing == false)
            {
                return;
            }

            if (_followTarget == null)
            {
                StopFollow();
                return;
            }

            UpdateFollowTargetVelocity(deltaTime);
            Vector2 followPosition = GetFollowPosition();
            if (forceUpdate ||
                ((Vector2)_destination - followPosition).sqrMagnitude >= followUpdateDistance * followUpdateDistance)
            {
                _destination = followPosition;
            }
        }

        private bool TryGetFollowVelocity(Vector2 currentPosition, out Vector2 velocity)
        {
            velocity = Vector2.zero;
            if (IsFollowing == false || _followTarget == null)
            {
                return false;
            }

            Vector2 followPosition = GetFollowPosition();
            Vector2 toFollowPosition = followPosition - currentPosition;
            Vector2 correctionVelocity = toFollowPosition * followResponsiveness;
            float maxFollowSpeed = _moveSpeed * followCatchUpSpeedMultiplier;

            if (toFollowPosition.sqrMagnitude <= followStopDistance * followStopDistance)
            {
                correctionVelocity = Vector2.ClampMagnitude(correctionVelocity, _moveSpeed);
            }
            else
            {
                correctionVelocity = Vector2.ClampMagnitude(correctionVelocity, maxFollowSpeed);
            }

            velocity = Vector2.ClampMagnitude(_followTargetVelocity + correctionVelocity, maxFollowSpeed);
            if (velocity.sqrMagnitude <= MIN_VECTOR_SQR &&
                toFollowPosition.sqrMagnitude > followResumeDistance * followResumeDistance)
            {
                velocity = Vector2.ClampMagnitude(toFollowPosition.normalized * _moveSpeed, maxFollowSpeed);
            }

            return true;
        }

        private void UpdateFollowTargetVelocity(float deltaTime)
        {
            if (_followTarget == null)
            {
                _followTargetVelocity = Vector2.zero;
                return;
            }

            Vector2 targetPosition = _followTarget.position;
            if (_hasFollowTargetPosition == false || deltaTime <= 0f)
            {
                _previousFollowTargetPosition = targetPosition;
                _followTargetVelocity = Vector2.zero;
                _hasFollowTargetPosition = true;
                return;
            }

            _followTargetVelocity = (targetPosition - _previousFollowTargetPosition) / deltaTime;
            _previousFollowTargetPosition = targetPosition;
        }

        private Vector2 GetFollowPosition()
        {
            return (Vector2)_followTarget.position + _followOffset;
        }

        private void UpdateAnimationMotionState()
        {
            if (IsFollowing)
            {
                Vector2 currentPosition = _owner.transform.position;
                Vector2 toFollowPosition = GetFollowPosition() - currentPosition;
                IsMovingForAnimation =
                    _currentVelocity.sqrMagnitude > MIN_VECTOR_SQR ||
                    _followTargetVelocity.sqrMagnitude > MIN_VECTOR_SQR ||
                    toFollowPosition.sqrMagnitude > followStopDistance * followStopDistance;
                return;
            }

            IsMovingForAnimation = _currentVelocity.sqrMagnitude > MIN_VECTOR_SQR;
        }

        private void UpdateAvoidanceMotionState()
        {
            _avoidanceModule?.SetMotionState(_desiredVelocity, _currentVelocity, CanMove);
        }

        private bool TryGetAstarManager(out AstarManager astarManager)
        {
            if (_astarManager != null)
            {
                astarManager = _astarManager;
                return true;
            }

            if (AstarManager.IsNullInstance)
            {
                astarManager = null;
                return false;
            }

            _astarManager = AstarManager.Instance;
            astarManager = _astarManager;
            return true;
        }

        private static bool TryFindRelocationPosition(
            AstarManager astarManager,
            Vector2Int currentCell,
            Vector2Int[] blockedCells,
            out Vector3 position)
        {
            for (int i = 0; i < RelocateDirections.Length; i++)
            {
                Vector2Int candidateCell = currentCell + RelocateDirections[i];
                if (ContainsCell(blockedCells, candidateCell))
                {
                    continue;
                }

                Vector3 candidatePosition = astarManager.CellToWorldPosition(candidateCell);
                if (astarManager.CanMovePosition(candidatePosition) == false)
                {
                    continue;
                }

                position = candidatePosition;
                return true;
            }

            position = default;
            return false;
        }

        private static bool ContainsCell(Vector2Int[] cells, Vector2Int targetCell)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == targetCell)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
