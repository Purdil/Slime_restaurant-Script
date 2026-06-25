using _Project.Core.Systems.EventChannel;
using UnityEngine;

namespace _Project.Core.Systems.Firebase
{
    public readonly struct SaveStaffRequest
    {
        public int AgentId { get; }
        public bool IsAdd { get; }

        public SaveStaffRequest(int agentId, bool isAdd)
        {
            AgentId = agentId;
            IsAdd = isAdd;
        }
    }

    [CreateAssetMenu(fileName = "SaveStaffChannel", menuName = "Task/SaveStaffChannel", order = 0)]
    public class SaveStaffChannel : EventChannel<SaveStaffRequest>
    {
        
    }
}
