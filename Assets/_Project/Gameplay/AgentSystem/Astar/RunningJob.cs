using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.Astar
{
    public struct RunningJob
    {
        public JobHandle handle;
        public NativeList<int> result;
        public NativeList<int> allresult;
        public Action<Vector2[],Vector2Int[],int> callback;
        public int CallBackId;
    }
}