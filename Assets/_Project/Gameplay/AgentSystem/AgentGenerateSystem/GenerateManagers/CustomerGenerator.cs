using _Project.Core.PoolManaging;
using _Project.Gameplay.AgentSystem._Agent.TaskAgent;
using _Project.Gameplay.AgentSystem.AgentGenerateSystem.GenerateRequestStructs;

namespace _Project.Gameplay.AgentSystem.AgentGenerateSystem.GenerateManagers
{
    public class CustomerGenerator : AgentGenerateManager
    {
        protected override PoolItemSO ResolvePoolItem(GenerateAgentRequest request)
        {
            if (request.profile is CustomerAgentProfileSO customerProfile
                && customerProfile.PoolItem != null)
            {
                return customerProfile.PoolItem;
            }

            return base.ResolvePoolItem(request);
        }
    }
}
