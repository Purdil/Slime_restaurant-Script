using _Project.Core.Systems.EventChannel;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.EventChannel
{
    [CreateAssetMenu(fileName = "SendingTaskChannel", menuName = "EventChannel/TaskChannel/SendingTaskEventChannel", order = 0)]
    public class SendingTaskEventChannel : EventChannel<TaskAssignment>
    {
    }
}
