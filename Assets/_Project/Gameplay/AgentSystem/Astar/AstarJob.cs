using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.Astar
{
    public struct AstarJob : IJob
    {
        [ReadOnly] private NativeArray<NodeData> _maps;
        [ReadOnly] private NativeHashMap<Vector2Int, int> _posToIndexMap;
        private NativeList<int> _result;
        private NativeList<int> _allResultMaps;
        [ReadOnly] private NativeArray<bool> _isBlockedMap;
        private int _startIndex;
        private int _endIndex;

        public AstarJob(int startIndex, int endIndex, NativeArray<NodeData> maps, NativeHashMap<Vector2Int, int> posToIndexMap, NativeList<int> result,
            NativeArray<bool> isBlockedMap, NativeList<int> allResultMaps)
        {
            _startIndex = startIndex;
            _endIndex = endIndex;
            _maps = maps;
            _posToIndexMap = posToIndexMap;
            _result = result;
            _isBlockedMap = isBlockedMap;
            _allResultMaps = allResultMaps;
        }

        public void Execute()
        {
            PriorityQueueOfAstarNode openList = new PriorityQueueOfAstarNode(Allocator.Temp);
            NativeArray<int> parentMap = new NativeArray<int>(_maps.Length, Allocator.Temp);
            NativeArray<bool> isVisited = new NativeArray<bool>(_maps.Length, Allocator.Temp);
            for (int i = 0; i < _maps.Length; i++)
            {
                parentMap[i] = -1;
            }

            if (CalculatePath(openList, parentMap, isVisited))
            {
                NativeList<int> deleteCornerList =  new NativeList<int>(_maps.Length, Allocator.Temp);
               
                
                deleteCornerList.Add(_result[0]);

                for (int i = 1; i < _result.Length - 1; i++)
                {
                    
                    Vector2Int beforeDirection = _maps[_result[i]].cellPosition - _maps[_result[i - 1]].cellPosition;
                    Vector2Int nextDirection = _maps[_result[i + 1]].cellPosition - _maps[_result[i]].cellPosition;

                    if (beforeDirection != nextDirection)
                    {
                        deleteCornerList.Add(_result[i]);
                    }
                    
                }
                deleteCornerList.Add(_result[^1]);
                
                _result.Clear();

                foreach (int index in deleteCornerList)
                {
                    _result.Add(index);
                }

                deleteCornerList.Dispose();
            }

            openList.Dispose();
            parentMap.Dispose();
            isVisited.Dispose();
        }

        private bool CalculatePath(PriorityQueueOfAstarNode openList, NativeArray<int> parentMap, NativeArray<bool> isVisited)
        {
            bool hasPath = false;
            if (_posToIndexMap.TryGetValue(_maps[_startIndex].cellPosition, out int startIndex) == false)
            {
                return false;
            }

            if (_posToIndexMap.TryGetValue(_maps[_endIndex].cellPosition, out int endIndex) == false)
            {
                return false;
            }

            openList.Push(new AstarNode
            {
                nodeData = _maps[startIndex],
                worldPosition = _maps[startIndex].worldPosition,
                cellPosition = _maps[startIndex].cellPosition,
                parentIndex = -1,
                gCost = 0,
                fCost = CalcH(_maps[startIndex].cellPosition, _maps[endIndex].cellPosition)
            });

            while (openList.Count > 0)
            {
                AstarNode currentNode = openList.Pop();
                int currentIndex = _posToIndexMap[currentNode.cellPosition];
                if (isVisited[currentIndex])
                {
                    continue;
                }

                isVisited[currentIndex] = true;
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        if (x == 0 && y == 0)
                        {
                            continue;
                        }

                        Vector2Int neighborNode = new Vector2Int(x, y) + currentNode.cellPosition;

                        if (x != 0 && y != 0)
                        {
                            Vector2Int sideA = new Vector2Int(currentNode.cellPosition.x + x, currentNode.cellPosition.y);
                            Vector2Int sideB = new Vector2Int(currentNode.cellPosition.x, currentNode.cellPosition.y + y);
                            if (_posToIndexMap.TryGetValue(sideA, out int indexA) == false
                                || _posToIndexMap.TryGetValue(sideB, out int indexB) == false)
                            {
                                continue;
                            }

                            if (_isBlockedMap[indexA] || _isBlockedMap[indexB])
                            {
                                continue;
                            }
                        }

                        if (_posToIndexMap.TryGetValue(neighborNode, out int neighborIndex) == false)
                        {
                            continue;
                        }

                        if (_isBlockedMap[neighborIndex])
                        {
                            continue;
                        }

                        if (isVisited[neighborIndex])
                        {
                            continue;
                        }
                        
                        float newG = Vector2Int.Distance(currentNode.cellPosition, neighborNode) + currentNode.gCost;

                        AstarNode nextAstarNode = new AstarNode
                        {
                            nodeData = _maps[neighborIndex],
                            worldPosition = _maps[neighborIndex].worldPosition,
                            cellPosition = _maps[neighborIndex].cellPosition,
                            parentIndex = currentIndex,
                            gCost = newG,
                            fCost = newG + CalcH(_maps[neighborIndex].cellPosition, _maps[endIndex].cellPosition)
                        };

                        if (openList.TryGetIndex(nextAstarNode.cellPosition, out int heapIdx))
                        {
                            if (newG < openList.heap[heapIdx].gCost)
                            {
                                openList.heap.RemoveAt(heapIdx);
                                parentMap[neighborIndex] = currentIndex;
                                openList.Push(nextAstarNode);
                            }
                        }
                        else
                        {
                            parentMap[neighborIndex] = currentIndex;
                            openList.Push(nextAstarNode);
                        }
                    }
                }

                if (currentNode.cellPosition == _maps[endIndex].cellPosition)
                {
                    hasPath = true;
                    break;
                }
            }

            if (hasPath)
            {
                int current = endIndex;
                while (current != -1)
                {
                    _result.Add(current);
                    _allResultMaps.Add(current);
                    current = parentMap[current];
                }

                for (int i = 0, j = _result.Length - 1; i < j; i++, j--)
                {
                    (_result[i], _result[j]) = (_result[j], _result[i]);
                }
            }

            return hasPath;
        }

        private float CalcH(Vector2Int startPosition, Vector2Int endPosition)
        {
            return Vector2Int.Distance(startPosition, endPosition);
        }
    }
}
