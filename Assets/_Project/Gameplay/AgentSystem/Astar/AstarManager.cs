using System;
using System.Collections.Generic;
using System.Linq;
using _Project.Core.CustomLogging;
using _Project.Core.Manager;
using _Project.Gameplay.AgentSystem.Astar.EventChannel;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Project.Gameplay.AgentSystem.Astar
{
    public class AstarManager : MonoSingleton<AstarManager>
    {
        [SerializeField] private Tilemap baseTilemap;
        [SerializeField] private Tilemap wallTilemap;
        [SerializeField] private GridUpdatedChannel  gridUpdatedChannel;
        [SerializeField] private RequestAstarPathChannel requestAstarPathChannel;
        [SerializeField] private BakedDataSO bakedDataSO;
        [SerializeField] private GridSystem gridSystem;
        public event Action<Vector2Int[]> changeMapEvent; 
        private readonly List<RunningJob> _runningJobs = new();
        private readonly HashSet<Vector2Int> _blockedCells = new();
        private readonly List<Vector2Int> _changedCells = new();
        private NativeArray<NodeData> _bakedMap;
        private NativeHashMap<Vector2Int, int> _posToIndexMap;
        private NativeArray<bool> _isBlockedMap;
        protected override void Awake()
        {
            base.Awake();
            _posToIndexMap = new NativeHashMap<Vector2Int, int>(
                bakedDataSO.points.Count, Allocator.Persistent);
            requestAstarPathChannel.OnEvent += HandleRequestPath;
            gridUpdatedChannel.OnEvent += HandleGridUpdate;
            gridSystem.OnPurchaseStateChanged += RefreshBlockedMap;
            UpdateNativeMap();
            RefreshBlockedMap();
        }

        

        private void FixedUpdate()
        {
            if (_runningJobs.Count > 0)
            {
                CollectFinishedJobs();
            }
        }

        private void OnDisable()
        {
            StopAllRunningJobs();
            if (_bakedMap.IsCreated)
            {
                _bakedMap.Dispose();
            }

            if (_posToIndexMap.IsCreated)
            {
                _posToIndexMap.Dispose();
            }

            if (_isBlockedMap.IsCreated)
            {
                _isBlockedMap.Dispose();
            }
            gridUpdatedChannel.OnEvent -= HandleGridUpdate;
            requestAstarPathChannel.OnEvent -= HandleRequestPath;
            gridSystem.OnPurchaseStateChanged -= RefreshBlockedMap;
        }
        //TODO : 맵 리베이크 추후 다시 작성할 예정. (수복,삭제 분리)
        private void HandleGridUpdate(BuildingPlacedInfo obj)
        {
            StopAllJobs(true);
            RefreshBlockedMap();
            changeMapEvent?.Invoke(obj.Points);
        }

        public void RefreshBlockedMap()
        {
            RefreshBlockedMap(false);
        }

        public void RefreshBlockedMapAndNotifyChanges()
        {
            RefreshBlockedMap(true);
        }

        private void RefreshBlockedMap(bool shouldNotifyChangedCells)
        {
            if (_isBlockedMap.IsCreated == false || _bakedMap.IsCreated == false)
            {
                return;
            }

            _changedCells.Clear();
            CollectBlockedCells();

            for (int i = 0; i < _bakedMap.Length; i++)
            {
                bool isBlocked = _blockedCells.Contains(_bakedMap[i].cellPosition);
                if (shouldNotifyChangedCells && _isBlockedMap[i] != isBlocked)
                {
                    _changedCells.Add(_bakedMap[i].cellPosition);
                }

                _isBlockedMap[i] = isBlocked;
            }

            if (shouldNotifyChangedCells && _changedCells.Count > 0)
            {
                changeMapEvent?.Invoke(_changedCells.ToArray());
            }
        }

        private void CollectBlockedCells()
        {
            _blockedCells.Clear();
            AddOccupiedGridBlocks();
            AddWallBlocks();
        }

        private void AddOccupiedGridBlocks()
        {
            if (gridSystem == null)
            {
                return;
            }

            RectInt bounds = gridSystem.BuildBounds;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    Vector2Int gridPosition = new Vector2Int(x, y);
                    if (gridSystem.IsInsideGrid(gridPosition) == false ||
                        gridSystem.IsOccupied(gridPosition) == false)
                    {
                        continue;
                    }

                    _blockedCells.Add(gridPosition);
                }
            }
        }

        private void AddWallBlocks()
        {
            if (baseTilemap == null || wallTilemap == null)
            {
                return;
            }

            wallTilemap.CompressBounds();
            foreach (var pos in wallTilemap.cellBounds.allPositionsWithin)
            {
                Vector3Int tilePos = new Vector3Int(pos.x, pos.y, pos.z);
                if (wallTilemap.HasTile(tilePos) == false)
                {
                    continue;
                }

                Vector3 wallWorldPosition = wallTilemap.GetCellCenterWorld(tilePos);
                _blockedCells.Add(WorldToCellPosition(wallWorldPosition));
            }
        }

        private void UpdateNativeMap()
        {
            int index = 0;
            if (_bakedMap.IsCreated)
            {
                _bakedMap.Dispose();
            }

            if (_posToIndexMap.IsCreated)
            {
                _posToIndexMap.Dispose();
            }

            _posToIndexMap = new NativeHashMap<Vector2Int, int>(
                bakedDataSO.points.Count, Allocator.Persistent);
            _bakedMap = new NativeArray<NodeData>(bakedDataSO.points.Count, Allocator.Persistent);
            _isBlockedMap = new NativeArray<bool>(bakedDataSO.points.Count, Allocator.Persistent);
            foreach (NodeData node in bakedDataSO.points)
            {
                _bakedMap[index] = node;
                _posToIndexMap.Add(node.cellPosition, index);
                ++index;
            }
        }

        public Vector2Int WorldToCellPosition(Vector2 worldPosition)
        {
            return (Vector2Int)baseTilemap.WorldToCell(worldPosition);
        }

        public Vector2 CellToWorldPosition(Vector2Int cellPosition)
        {
            return baseTilemap.GetCellCenterWorld((Vector3Int)cellPosition);
        }

        public bool CanMovePosition(Vector3 position)
        {
            if (_posToIndexMap.TryGetValue(WorldToCellPosition(position), out int index) == false)
            {
                return false;
            }

            if (_isBlockedMap[index])
            {
                return false;
            }

            return true;
        }

        public bool TryGetRandomMovePosition(out Vector3 position)
        {
            position = default;

            if (bakedDataSO == null ||
                bakedDataSO.points == null ||
                bakedDataSO.points.Count == 0 ||
                _isBlockedMap.IsCreated == false)
            {
                return false;
            }

            int startIndex = UnityEngine.Random.Range(0, bakedDataSO.points.Count);
            for (int i = 0; i < bakedDataSO.points.Count; i++)
            {
                int index = (startIndex + i) % bakedDataSO.points.Count;
                if (index >= _isBlockedMap.Length || _isBlockedMap[index])
                {
                    continue;
                }

                position = bakedDataSO.points[index].worldPosition;
                return true;
            }

            return false;
        }

        private void HandleRequestPath(PathRequest data)
        {
            ScheduleJob(data);
        }

        private void ScheduleJob(PathRequest req)
        {
            Vector2Int startCellPosition = WorldToCellPosition(req.start);
            Vector2Int endCellPosition = WorldToCellPosition(req.end);

            if (_posToIndexMap.TryGetValue(startCellPosition, out int startIndex) == false ||
                _posToIndexMap.TryGetValue(endCellPosition, out int endIndex) == false)
            {
                CLog.LogError("얌마 길을 찾을수가 없잖아. 위치 똑바로 안넣어?");
                req.callBack?.Invoke(Array.Empty<Vector2>(),Array.Empty<Vector2Int>(),req.CallBackId);
                return;
            }
            
            NativeList<int> cornerResult = new NativeList<int>(Allocator.Persistent);
            NativeList<int> allResultMaps = new NativeList<int>(Allocator.Persistent);
            AstarJob job = new AstarJob(
                startIndex,
                endIndex,
                _bakedMap,
                _posToIndexMap,
                cornerResult,
                _isBlockedMap,
                allResultMaps);
            JobHandle handle = job.Schedule();

            _runningJobs.Add(new RunningJob
            {
                handle = handle,
                result = cornerResult,
                callback = req.callBack,
                CallBackId = req.CallBackId,
                allresult = allResultMaps
            });
        }

        private void StopAllRunningJobs()
        {
            for (int i = _runningJobs.Count - 1; i >= 0; i--)
            {
                CompleteJob(i,false);
            }
        }
        private void CollectFinishedJobs()
        {
            for (int i = _runningJobs.Count - 1; i >= 0; i--)
            {
                var job = _runningJobs[i];
                if (job.handle.IsCompleted == false)
                {
                    continue;
                }

                CompleteJob(i, true);
            }
        }

        private void StopAllJobs(bool shouldInvokeCallback)
        {
            for (int i = _runningJobs.Count - 1; i >= 0; i--)
            {
                CompleteJob(i, shouldInvokeCallback);
            }
        }

        private void CompleteJob(int index, bool shouldInvokeCallback)
        {
            RunningJob job = _runningJobs[index];
            job.handle.Complete();
            
            if (shouldInvokeCallback)
            {
                Vector2[] path = ConvertIndexArrWorld(job.result);
                Vector2Int[] allPath = ConvertIndexArrCell(job.allresult);
                job.callback?.Invoke(path,allPath,job.CallBackId);
            }

            if (job.result.IsCreated)
            {
                job.result.Dispose();
            }

            if (job.allresult.IsCreated)
            {
                job.allresult.Dispose();
            }
                
            _runningJobs.RemoveAt(index);
        }

        private Vector2[] ConvertIndexArrWorld(NativeList<int> jobResult)
        {
            List<Vector2> convertList = new();
            foreach (int index in jobResult)
            {
                convertList.Add(bakedDataSO.points[index].worldPosition);
            }
            
            return convertList.ToArray();
        }

        private Vector2Int[] ConvertIndexArrCell(NativeList<int> jobResult)
        {
            List<Vector2Int> convertList = new();
            foreach (int index in jobResult)
            {
                convertList.Add(bakedDataSO.points[index].cellPosition);
            }
            
            return convertList.ToArray();
        }
    }
}
