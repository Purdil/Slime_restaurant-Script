using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules.AvoidanceModule
{
    public interface IAvoidanceBody
    {
        int AvoidanceId { get; }
        Vector2 Position { get; }
        Vector2 AvoidancePosition { get; }
        Vector2 Velocity { get; }
        Vector2 DesiredVelocity { get; }
        Vector2 HalfSize { get; }
        float PersonalSpace { get; }
        bool HasMoveIntent { get; }
        bool CanReceivePush { get; }
        bool IsAvoidanceEnabled { get; }
        void AddAvoidanceImpulse(Vector2 impulse, int priority);
    }
}
