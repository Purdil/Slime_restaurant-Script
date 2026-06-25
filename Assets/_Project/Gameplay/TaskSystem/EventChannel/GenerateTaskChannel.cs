using _Project.Core.Systems.EventChannel;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.EventChannel
{
    [CreateAssetMenu(fileName = "TaskChannel", menuName = "EventChannel/TaskChannel/GenerateTaskChannel")]
    public class GenerateTaskChannel : EventChannel<TaskAssignment>
    {
    }
}
