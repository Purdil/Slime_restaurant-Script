using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules.CommonModule
{
    public interface IRenderer
    {
        public void PlayClip(int clipHash);
        public void ControlManualAnimation(bool value, int animHash = 0);
        public void FlipThere(Vector3 position);
    }
}