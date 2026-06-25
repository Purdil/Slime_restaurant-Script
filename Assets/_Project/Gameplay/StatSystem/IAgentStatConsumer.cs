using System.Collections.Generic;

namespace _Project.Gameplay.StatSystem
{
    public interface IAgentStatConsumer
    {
        public void RefreshStats(IAgentStatProvider statProvider);
        
        public void UpdateStats(StatTypeSO updateType, float updateValue);
    }
}