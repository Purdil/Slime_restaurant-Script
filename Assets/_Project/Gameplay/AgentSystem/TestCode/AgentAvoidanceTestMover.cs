using _Project.Gameplay.AgentSystem.AgentModules.AvoidanceModule;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.TestCode
{
    public sealed class AgentAvoidanceTestMover : MonoBehaviour
    {
        private AgentAvoidanceModule _avoidanceModule;
        private Vector2 _origin;
        private Vector2 _target;
        private float _speed;
        private float _stopDistance;

        private void Awake()
        {
            _avoidanceModule = GetComponent<AgentAvoidanceModule>();
        }

        private void Update()
        {
            Vector2 position = transform.position;
            Vector2 toTarget = _target - position;

            if (toTarget.sqrMagnitude <= _stopDistance * _stopDistance)
            {
                SwapTarget();
                return;
            }

            Vector2 desiredVelocity = toTarget.normalized * _speed;

            if (_avoidanceModule != null)
            {
                desiredVelocity = _avoidanceModule.ApplyAvoidance(desiredVelocity);
            }

            transform.position = position + desiredVelocity * Time.deltaTime;
        }

        public void Initialize(Vector2 target, float speed, float stopDistance)
        {
            _origin = transform.position;
            _target = target;
            _speed = Mathf.Max(0f, speed);
            _stopDistance = Mathf.Max(0.01f, stopDistance);
        }

        public void SetAvoidanceEnabled(bool isEnabled)
        {
            if (_avoidanceModule == null)
            {
                return;
            }

            _avoidanceModule.SetAvoidanceEnabled(isEnabled);
        }

        private void SwapTarget()
        {
            Vector2 previousTarget = _target;
            _target = _origin;
            _origin = previousTarget;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _target);
            Gizmos.DrawSphere(_target, 0.08f);
        }
#endif
    }
}
