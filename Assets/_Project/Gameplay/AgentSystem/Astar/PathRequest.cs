using System;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.Astar
{
    public struct PathRequest
    {
        public Vector2 start;
        public Vector2 end;
        public Action<Vector2[],Vector2Int[],int> callBack;
        public int CallBackId;
    }
}