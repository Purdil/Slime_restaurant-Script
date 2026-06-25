using _Project.Core.Systems.EventChannel;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.EventChannel
{
    [CreateAssetMenu(fileName = "InteractObjectRegisterChannel", menuName = "EventChannel/TaskChannel/InteractObjectRegisterEventChannel", order = 0)]
    public class InteractObjectRegisterChannel : EventChannel<(MonoBehaviour obj,bool regist)>
    {
        
    }
}