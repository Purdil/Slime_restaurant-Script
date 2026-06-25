using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.Astar
{
    [CreateAssetMenu(fileName = "BakedData", menuName = "Agent/Path/BakedData", order = 0)]
    public class BakedDataSO : ScriptableObject
    {
        public List<NodeData> points = new();
        private Dictionary<Vector2Int, NodeData> _pointDict; 
        private void OnEnable()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_pointDict == null || _pointDict.Count != points.Count)
            {
                _pointDict = points.ToDictionary(node => node.cellPosition);
            }
        }

        public void ClearPoints() => points?.Clear();

        public void AddPoint(Vector2 worldPosition, Vector2Int cellPosition)
        {
            points.Add(new NodeData(worldPosition, cellPosition));
        }

        public bool HasNode(Vector2Int cellPosition)
            => _pointDict != null && _pointDict.ContainsKey(cellPosition);

        public bool TryGetNode(Vector2Int cellPosition, out NodeData nodeData)
        {
            if (HasNode(cellPosition))
            {
                nodeData = _pointDict[cellPosition];
                return true;
            }

            nodeData = default;
            return false;
        }
    }
}
