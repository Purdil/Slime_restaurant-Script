namespace _Project.Core.ModuleSystem
{
    public interface IModule
    {
        void Initialize(ModuleOwner moduleOwner);
    }
}