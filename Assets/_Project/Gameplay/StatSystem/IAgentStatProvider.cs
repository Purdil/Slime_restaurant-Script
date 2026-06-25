namespace _Project.Gameplay.StatSystem
{
    public interface IAgentStatProvider
    {
        bool TryGetStatData(StatTypeSO statType, out float statValue);
    }
}