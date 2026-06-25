using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules.AvoidanceModule
{
    public interface IAgentAvoidanceModule
    {
        bool IsAvoidanceEnabled { get; }
        bool HasPendingAvoidanceImpulse { get; }
        Vector2 ApplyAvoidance(Vector2 desiredVelocity);
        Vector2 ConsumeAvoidanceImpulse();
        int FillVelocityCandidates(Vector2 desiredVelocity, Vector2[] results);
        void RefreshRegistration();
        void SetMotionState(Vector2 desiredVelocity, Vector2 currentVelocity, bool canReceivePush);
        void SetAvoidanceEnabled(bool isEnabled);
    }
}
