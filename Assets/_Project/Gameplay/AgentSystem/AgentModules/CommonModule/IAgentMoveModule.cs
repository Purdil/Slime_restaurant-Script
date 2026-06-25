using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules.CommonModule
{
    public interface IAgentMoveModule
    {
        public Vector2 CurVelocity { get;}
        public float MagCurVelocity { get; }
        public bool IsFollowing { get; }
        public bool IsMovingForAnimation { get; }
        void SetDestination(Vector3 destination);
        void StopImmediate();
        void SetPosition(Vector3 position);
        void StartFollow(Transform target, Vector2 offset);
        void StopFollow(bool stopImmediate = true);
        bool TryGetFollowDestination(out Vector3 followDestination);
    }
}
