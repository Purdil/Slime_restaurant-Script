using System;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.Astar
{
    public struct AstarNode : IComparable<AstarNode>
    {
        public static AstarNode NoneNode => new() { isValid = false };

        public Vector2 worldPosition;
        public Vector2Int cellPosition;
        public NodeData nodeData;
        public bool isValid;

        public int parentIndex;
        
        public float gCost;
        public float fCost; // fCost = gCost + hCost
        
        public int CompareTo(AstarNode other)
        {
            if (Mathf.Approximately(other.fCost, fCost))
            {
                return 0;
            }

            return other.fCost < fCost ? -1 : 1;
        }
        
        public override bool Equals(object obj)
        {
            if (obj is AstarNode astarNode)
            {
                return astarNode.cellPosition == cellPosition;
            }
            return false;
        }

        public override int GetHashCode() => cellPosition.GetHashCode();

        public static bool operator ==(AstarNode lhs, AstarNode rhs) 
            => lhs.Equals(rhs);
        

        public static bool operator !=(AstarNode lhs, AstarNode rhs)
            => !(lhs == rhs);
    }
}
