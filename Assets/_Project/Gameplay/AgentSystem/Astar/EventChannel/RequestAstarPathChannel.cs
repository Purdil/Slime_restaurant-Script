using System;
using _Project.Core.Systems.EventChannel;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.Astar.EventChannel
{
    [CreateAssetMenu(fileName = "AstarPath", menuName = "Astar/RequestPathChannel", order = 0)]
    public class RequestAstarPathChannel : EventChannel<PathRequest>
    {
        
    }
}