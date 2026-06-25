using System;
using _Project.Core.ModuleSystem;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.Astar;
using _Project.Gameplay.AgentSystem.Astar.EventChannel;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules.CommonModule
{
    public class PathAgentModule : MonoBehaviour, IModule
    {
        [SerializeField] private RequestAstarPathChannel requestAstarPathChannel;
        [SerializeField] private int maxRetryCount = 2;
        [SerializeField] private float stuckFailTime = 3f;
        [SerializeField] private float progressDistance = 0.02f;
        [SerializeField] private float maxPathMoveTime = 20f;
        [SerializeField] private float pathRequestTimeout = 5f;
        [SerializeField] private float maxWatchDeltaTime = 0.1f;


        public event Action OnMoveComplete;
        public bool LastMoveSucceeded { get; private set; } = true;
        private Vector2[] _path = Array.Empty<Vector2>();
        private Vector2Int[] _allPath = Array.Empty<Vector2Int>();
        private IAgentMoveModule _moveModule;
        private Agent _owner;
        private Vector3 _absoluteDestination;
        private Vector3 _destination;
        private Vector3 _moveDirection;
        private int _pathIndex;
        private int _callBackId = 0;
        private bool _hasDestination;
        private Vector3 _lastProgressPosition;
        private float _stuckElapsed;
        private float _moveElapsed;
        private float _pathRequestElapsed;
        private int _retryCount;
        private bool _isSubscribedToAstar;
        private bool _isWaitingPath;

        private void OnValidate()
        {
            maxRetryCount = Mathf.Max(0, maxRetryCount);
            stuckFailTime = Mathf.Max(0f, stuckFailTime);
            progressDistance = Mathf.Max(0.001f, progressDistance);
            maxPathMoveTime = Mathf.Max(0f, maxPathMoveTime);
            pathRequestTimeout = Mathf.Max(0f, pathRequestTimeout);
            maxWatchDeltaTime = Mathf.Max(0.01f, maxWatchDeltaTime);
        }

        private void Update()
        {
            if (_hasDestination == false)
            {
                return;
            }

            if (_isWaitingPath)
            {
                UpdatePathRequestWatch();
                return;
            }

            if (_path.Length == 0)
            {
                return;
            }

            UpdatePathFailureWatch();
            if (_path.Length > 0 && CheckArrive() && _pathIndex < _path.Length)
            {
                _moveModule.SetDestination(_destination);
            }
        }

        private void OnEnable()
        {
            TrySubscribeChangeMap();
        }

        private void OnDisable()
        {
            UnsubscribeChangeMap();
            _callBackId++;
            _path = Array.Empty<Vector2>();
            _allPath = Array.Empty<Vector2Int>();
            _destination = Vector2.zero;
            _absoluteDestination = Vector2.zero;
            _moveDirection =  Vector2.zero;
            _pathIndex = 0;
            _hasDestination = false;
            _isWaitingPath = false;
            LastMoveSucceeded = false;
            ResetPathWatch();
        }

        public void Initialize(ModuleOwner moduleOwner)
        {
            _owner = moduleOwner as Agent;
            _moveModule = moduleOwner.GetModule<IAgentMoveModule>();
        }

        private void HandleChangeMap(Vector2Int[] obj)
        {
            foreach (Vector2Int point in obj)
            {
                if (_hasDestination && ContainsPathCell(point))
                {
                    _moveModule?.StopImmediate();
                    ResetPath(false);
                    RequestPath(_absoluteDestination, false);
                    return;
                }
            }
        }

        public void ResetPath(bool restAbsolute = true)
        {
            _path = Array.Empty<Vector2>();
            _allPath = Array.Empty<Vector2Int>();
            _pathIndex = 0;
            _moveDirection = Vector2.zero;
            _destination = Vector2.zero;
            _isWaitingPath = false;
            if (restAbsolute)
            {
                _hasDestination = false;
                _absoluteDestination = Vector2.zero;
            }

            ResetPathWatch();
        }

        //경로 요청
        public void RequestPath(Vector3 destination)
        {
            RequestPath(destination, true);
        }

        public void RepathCurrentDestination()
        {
            if (_hasDestination == false)
            {
                return;
            }

            _moveModule?.StopImmediate();
            ResetPath(false);
            RequestPath(_absoluteDestination, false);
        }

        private void RequestPath(Vector3 destination, bool shouldResetRetry)
        {
            _absoluteDestination =  destination;
            _hasDestination = true;
            LastMoveSucceeded = false;
            _path = Array.Empty<Vector2>();
            _allPath = Array.Empty<Vector2Int>();
            _pathIndex = 0;
            _moveDirection = Vector2.zero;
            _destination = Vector2.zero;
            _isWaitingPath = true;
            if (shouldResetRetry)
            {
                _retryCount = 0;
            }

            ResetPathWatch();
            TrySubscribeChangeMap();
            if (requestAstarPathChannel == null)
            {
                FailMove();
                return;
            }

            _callBackId++;
            requestAstarPathChannel.Raise(new PathRequest
            {
                start = _owner.transform.position,
                end = destination,
                callBack = HandlePathCallback,
                CallBackId =  _callBackId,
            });
        }

        private void HandlePathCallback(Vector2[] path,Vector2Int[] allPathMap, int callBackId)
        {

            if (callBackId != _callBackId)
            {
                return;
            }

            _isWaitingPath = false;
           
            if (path == null || path.Length == 0)
            {
                HandlePathFailed();
                return;
            }
            
            _pathIndex = 0;
            _path = path;
            _allPath = allPathMap;
            LastMoveSucceeded = true;
            _destination = _path[_pathIndex];
            _moveDirection = _destination - _owner.transform.position;
            ResetPathWatch();
            _moveModule.SetDestination(_destination);
            
                
        }

        private bool CheckArrive()
        {
            if (Vector3.Distance(_owner.transform.position, _destination) < 0.1f ||
                Vector3.Dot(_destination - _owner.transform.position, _moveDirection) < 0)
            {
                ++_pathIndex;
                if (_pathIndex < _path.Length)
                {
                    _destination = _path[_pathIndex];
                    _moveDirection = _destination - _owner.transform.position;
                    return true;
                }
                ArriveAbsoluteDestination();
                OnMoveComplete?.Invoke();
               
            }

            return false;
        }

        private void ArriveAbsoluteDestination()
        {
            _path = Array.Empty<Vector2>();
            _allPath = Array.Empty<Vector2Int>();
            _hasDestination = false;
            _isWaitingPath = false;
            _absoluteDestination = Vector2.zero;
            LastMoveSucceeded = true;
        }

        private void UpdatePathFailureWatch()
        {
            if (_owner == null)
            {
                return;
            }

            float deltaTime = GetWatchDeltaTime();
            _moveElapsed += deltaTime;
            Vector3 currentPosition = _owner.transform.position;
            if ((currentPosition - _lastProgressPosition).sqrMagnitude >= progressDistance * progressDistance)
            {
                _lastProgressPosition = currentPosition;
                _stuckElapsed = 0f;
                return;
            }

            _stuckElapsed += deltaTime;
            bool isStuck = stuckFailTime > 0f && _stuckElapsed >= stuckFailTime;
            bool isOverTime = maxPathMoveTime > 0f && _moveElapsed >= maxPathMoveTime;
            if (isStuck == false && isOverTime == false)
            {
                return;
            }

            if (TryRetryPath())
            {
                return;
            }

            FailMove();
        }

        private void UpdatePathRequestWatch()
        {
            _pathRequestElapsed += GetWatchDeltaTime();
            if (pathRequestTimeout <= 0f || _pathRequestElapsed < pathRequestTimeout)
            {
                return;
            }

            if (TryRetryPath())
            {
                return;
            }

            FailMove();
        }

        private void HandlePathFailed()
        {
            if (TryRetryPath())
            {
                return;
            }

            FailMove();
        }

        private bool TryRetryPath()
        {
            if (_hasDestination == false || _retryCount >= maxRetryCount)
            {
                return false;
            }

            _retryCount++;
            _moveModule?.StopImmediate();
            RequestPath(_absoluteDestination, false);
            return true;
        }

        private void FailMove()
        {
            ResetPath();
            _moveModule?.StopImmediate();
            LastMoveSucceeded = false;
            OnMoveComplete?.Invoke();
        }

        private void ResetPathWatch()
        {
            _lastProgressPosition = _owner == null ? transform.position : _owner.transform.position;
            _stuckElapsed = 0f;
            _moveElapsed = 0f;
            _pathRequestElapsed = 0f;
        }

        private float GetWatchDeltaTime()
        {
            return Mathf.Min(Time.deltaTime, maxWatchDeltaTime);
        }

        private bool ContainsPathCell(Vector2Int point)
        {
            for (int i = 0; i < _allPath.Length; i++)
            {
                if (_allPath[i] == point)
                {
                    return true;
                }
            }

            return false;
        }

        private void TrySubscribeChangeMap()
        {
            if (_isSubscribedToAstar || AstarManager.IsNullInstance)
            {
                return;
            }

            AstarManager.Instance.changeMapEvent += HandleChangeMap;
            _isSubscribedToAstar = true;
        }

        private void UnsubscribeChangeMap()
        {
            if (_isSubscribedToAstar == false)
            {
                return;
            }

            if (AstarManager.IsNullInstance)
            {
                _isSubscribedToAstar = false;
                return;
            }

            AstarManager.Instance.changeMapEvent -= HandleChangeMap;
            _isSubscribedToAstar = false;
        }

        private void OnDrawGizmos()
        {
            if (_path == null)
            {
                return;
            }

            Gizmos.color = Color.green;
            foreach (Vector2 point in _path)
            {
                Gizmos.DrawSphere(point, 0.1f);
            }
        }
    }
}
