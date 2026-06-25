using System;
using _Project.Gameplay.AgentSystem._Agent;

namespace _Project.Gameplay.TaskSystem
{
    public interface ITask
    {
        public event Action OnEndTask;
        public void Execute(Agent agent);
        public void Cancel(Agent agent);
        public void HandleOnEndTask();
    }
}