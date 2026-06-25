using System.Collections.Generic;
using _Project.Core.ModuleSystem;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _Project.Gameplay.AgentSystem.AgentModules.AvoidanceModule
{
    public sealed class AgentAvoidanceModule : MonoBehaviour, IModule, IAgentAvoidanceModule, IAvoidanceBody
    {
        private const float MIN_VECTOR_SQR = 0.0001f;
        private const float STOP_SPEED_RATE = 0.35f;
        private const float PUSH_DISTANCE_RATE = 0.6f;

        [Header("Activation")]
        [SerializeField] private bool isAvoidanceEnabled = true;

        [Header("Body")]
        [SerializeField] private Vector2 bodySize = new(0.5f, 0.35f);
        [SerializeField] private Vector2 centerOffset;
        [SerializeField] private float personalSpace = 0.05f;

        [Header("Steering")]
        [SerializeField] private Vector2 avoidanceRangeOffset;
        [SerializeField] private float avoidanceRange = 0.35f;
        [SerializeField] private float avoidancePower = 1.2f;

        [Header("Debug")]
        [SerializeField] private bool isDrawGizmos = true;
        [SerializeField] private bool isDrawGizmosOnlySelected = true;

#if UNITY_EDITOR
        [Header("Editor Size Fit")]
        [SerializeField] private Sprite sizeSourceSprite;
        [SerializeField] private bool isAutoFitBodySizeFromSprite;
        [SerializeField] private bool isUseSpriteRendererWhenSourceEmpty = true;
        [SerializeField] private bool isIncludeTransformScale = true;
        [SerializeField] private Vector2 fitPadding;
        [SerializeField] private float fitScale = 1f;
#endif

        public int AvoidanceId { get; private set; }
        public Vector2 Position => (Vector2)transform.position + centerOffset;
        public Vector2 AvoidancePosition => Position + avoidanceRangeOffset;
        public Vector2 Velocity => _currentVelocity;
        public Vector2 DesiredVelocity => _desiredVelocity;
        public Vector2 HalfSize => bodySize * 0.5f;
        public float PersonalSpace => personalSpace;
        public bool HasMoveIntent => _desiredVelocity.sqrMagnitude > MIN_VECTOR_SQR;
        public bool CanReceivePush => _canReceivePush && IsAvoidanceEnabled;
        public bool IsAvoidanceEnabled => isAvoidanceEnabled && isActiveAndEnabled;
        public bool HasPendingAvoidanceImpulse => _avoidanceImpulse.sqrMagnitude > MIN_VECTOR_SQR;

        private readonly Vector2[] _fallbackCandidates = new Vector2[5];
        private readonly List<IAvoidanceBody> _nearBodies = new(16);
        private AgentAvoidanceManager _avoidanceManager;
        private bool _isInitialized;
        private bool _isRegistered;
        private bool _canReceivePush = true;
        private int _avoidanceImpulsePriority = int.MinValue;
        private Vector2 _currentVelocity;
        private Vector2 _desiredVelocity;
        private Vector2 _avoidanceImpulse;

        private void OnEnable()
        {
            RegisterSelf();
        }

        private void OnDisable()
        {
            UnregisterSelf();
        }

        public void Initialize(ModuleOwner moduleOwner)
        {
            _avoidanceManager = AgentAvoidanceManager.Instance;
            AvoidanceId = _avoidanceManager.GetNextAvoidanceId();
            _isInitialized = true;
            RegisterSelf();
        }

        public void SetEnable(bool enable)
        {
            isAvoidanceEnabled = enable;
        }

        public Vector2 ApplyAvoidance(Vector2 desiredVelocity)
        {
            int candidateCount = FillVelocityCandidates(desiredVelocity, _fallbackCandidates);

            if (candidateCount <= 0)
            {
                return desiredVelocity;
            }

            return _fallbackCandidates[0];
        }

        public Vector2 ConsumeAvoidanceImpulse()
        {
            Vector2 impulse = _avoidanceImpulse;
            _avoidanceImpulse = Vector2.zero;
            _avoidanceImpulsePriority = int.MinValue;

            return impulse;
        }

        public int FillVelocityCandidates(Vector2 desiredVelocity, Vector2[] results)
        {
            if (results == null || results.Length == 0)
            {
                return 0;
            }

            _desiredVelocity = desiredVelocity;

            if (IsAvoidanceEnabled == false || _avoidanceManager == null)
            {
                results[0] = desiredVelocity;
                return 1;
            }

            RegisterSelf();
            _avoidanceManager.Refresh(this);

            float speed = desiredVelocity.magnitude;

            if (speed <= 0f)
            {
                results[0] = Vector2.zero;
                return 1;
            }

            float queryRange = GetQueryRange();
            _avoidanceManager.Query(AvoidancePosition, queryRange, _nearBodies);

            Vector2 separation = Vector2.zero;
            Vector2 sideStep = Vector2.zero;

            for (int i = 0; i < _nearBodies.Count; i++)
            {
                IAvoidanceBody other = _nearBodies[i];

                if (ShouldSkipBody(other))
                {
                    continue;
                }

                if (TryGetPushVector(other, out Vector2 pushVector))
                {
                    separation += pushVector;
                }

                if (desiredVelocity.sqrMagnitude > MIN_VECTOR_SQR)
                {
                    sideStep += GetSideStep(other, desiredVelocity);
                    TryRequestPush(other, desiredVelocity);
                }
            }

            int count = 0;
            count = TryAddCandidate(results, count, desiredVelocity + (sideStep + separation) * avoidancePower, speed);
            count = TryAddCandidate(results, count, desiredVelocity + (-sideStep + separation) * avoidancePower, speed);
            count = TryAddCandidate(results, count, desiredVelocity + separation * avoidancePower, speed);
            count = TryAddCandidate(results, count, desiredVelocity.normalized * speed * STOP_SPEED_RATE, speed);
            count = TryAddCandidate(results, count, Vector2.zero, speed);

            return count;
        }

        public void RefreshRegistration()
        {
            if (_isRegistered == false)
            {
                RegisterSelf();
                return;
            }

            _avoidanceManager?.Refresh(this);
        }

        public void SetMotionState(Vector2 desiredVelocity, Vector2 currentVelocity, bool canReceivePush)
        {
            _desiredVelocity = desiredVelocity;
            _currentVelocity = currentVelocity;
            _canReceivePush = canReceivePush;
        }

        public void AddAvoidanceImpulse(Vector2 impulse, int priority)
        {
            if (impulse.sqrMagnitude <= MIN_VECTOR_SQR)
            {
                return;
            }

            if (priority < _avoidanceImpulsePriority)
            {
                return;
            }

            if (priority > _avoidanceImpulsePriority)
            {
                _avoidanceImpulse = Vector2.zero;
                _avoidanceImpulsePriority = priority;
            }

            _avoidanceImpulse += impulse;
        }

        public void SetAvoidanceEnabled(bool isEnabled)
        {
            if (isAvoidanceEnabled == isEnabled)
            {
                return;
            }

            isAvoidanceEnabled = isEnabled;

            if (isAvoidanceEnabled)
            {
                RegisterSelf();
                return;
            }

            UnregisterSelf();
        }

        public void SetBodySize(Vector2 size)
        {
            bodySize = new Vector2(
                Mathf.Max(0.05f, size.x),
                Mathf.Max(0.05f, size.y));
        }

        public void SetCenterOffset(Vector2 offset)
        {
            centerOffset = offset;
        }

        public void SetPersonalSpace(float space)
        {
            personalSpace = Mathf.Max(0f, space);
        }

        public void SetAvoidanceRangeOffset(Vector2 offset)
        {
            avoidanceRangeOffset = offset;
        }

        private bool ShouldSkipBody(IAvoidanceBody other)
        {
            return ReferenceEquals(other, this) || other.IsAvoidanceEnabled == false;
        }

        private int TryAddCandidate(Vector2[] results, int count, Vector2 velocity, float maxSpeed)
        {
            if (count >= results.Length)
            {
                return count;
            }

            results[count] = Vector2.ClampMagnitude(velocity, maxSpeed);
            return count + 1;
        }

        private bool TryRequestPush(IAvoidanceBody other, Vector2 desiredVelocity)
        {
            if (other.CanReceivePush == false || other.HasMoveIntent)
            {
                return false;
            }

            if (desiredVelocity.sqrMagnitude <= MIN_VECTOR_SQR)
            {
                return false;
            }

            Vector2 toOther = other.Position - Position;
            Vector2 forward = desiredVelocity.normalized;
            float forwardDistance = Vector2.Dot(toOther, forward);

            if (forwardDistance <= 0f)
            {
                return false;
            }

            float pushDistance = GetPushDistance(other);

            if (toOther.sqrMagnitude > pushDistance * pushDistance)
            {
                return false;
            }

            Vector2 sideStep = GetSideStep(other, desiredVelocity);
            Vector2 impulseDirection = (forward + sideStep * 0.5f).normalized;
            Vector2 impulse = impulseDirection * Mathf.Min(desiredVelocity.magnitude, avoidancePower);
            other.AddAvoidanceImpulse(impulse, AvoidanceId);

            return true;
        }

        private Vector2 GetSideStep(IAvoidanceBody other, Vector2 desiredVelocity)
        {
            Vector2 forward = desiredVelocity.normalized;
            Vector2 right = new Vector2(forward.y, -forward.x);
            Vector2 otherMotion = GetOtherMotion(other);

            if (otherMotion.sqrMagnitude > MIN_VECTOR_SQR)
            {
                Vector2 otherForward = otherMotion.normalized;
                float directionDot = Vector2.Dot(forward, otherForward);

                if (directionDot < -0.5f)
                {
                    return right;
                }

                if (directionDot > 0.5f)
                {
                    float forwardDistance = Vector2.Dot(other.Position - Position, forward);

                    if (forwardDistance > 0f)
                    {
                        return Vector2.zero;
                    }
                }
            }

            float side = Vector2.Dot(other.Position - Position, right);

            return side >= 0f ? -right : right;
        }

        private Vector2 GetOtherMotion(IAvoidanceBody other)
        {
            if (other.DesiredVelocity.sqrMagnitude > MIN_VECTOR_SQR)
            {
                return other.DesiredVelocity;
            }

            return other.Velocity;
        }

        private float GetPushDistance(IAvoidanceBody other)
        {
            float width = HalfSize.x + other.HalfSize.x;
            float height = HalfSize.y + other.HalfSize.y;
            float bodyDistance = Mathf.Max(width, height);
            return bodyDistance + PersonalSpace + other.PersonalSpace + avoidanceRange * PUSH_DISTANCE_RATE;
        }

        private bool TryGetPushVector(IAvoidanceBody other, out Vector2 pushVector)
        {
            Vector2 bodyDiff = Position - other.Position;
            Vector2 rangeDiff = AvoidancePosition - other.AvoidancePosition;

            if (bodyDiff.sqrMagnitude <= MIN_VECTOR_SQR)
            {
                bodyDiff = AvoidanceId < other.AvoidanceId ? Vector2.left : Vector2.right;
            }

            float combinedX = HalfSize.x + other.HalfSize.x + PersonalSpace + other.PersonalSpace + avoidanceRange;
            float combinedY = HalfSize.y + other.HalfSize.y + PersonalSpace + other.PersonalSpace + avoidanceRange;
            float normalizedX = rangeDiff.x / combinedX;
            float normalizedY = rangeDiff.y / combinedY;
            float normalizedDistanceSqr = normalizedX * normalizedX + normalizedY * normalizedY;

            if (normalizedDistanceSqr >= 1f)
            {
                pushVector = Vector2.zero;
                return false;
            }

            float normalizedDistance = Mathf.Sqrt(normalizedDistanceSqr);
            float weight = 1f - normalizedDistance;

            pushVector = bodyDiff.normalized * weight;
            return true;
        }

        private void RegisterSelf()
        {
            if (_isInitialized == false || IsAvoidanceEnabled == false || _isRegistered)
            {
                return;
            }

            if (_avoidanceManager == null)
            {
                _avoidanceManager = AgentAvoidanceManager.Instance;
            }

            _avoidanceManager.Register(this);
            _isRegistered = true;
        }

        private void UnregisterSelf()
        {
            if (_isRegistered == false)
            {
                return;
            }

            if (_avoidanceManager != null)
            {
                _avoidanceManager.Unregister(this);
            }

            _isRegistered = false;
        }

        private float GetQueryRange()
        {
            return Mathf.Max(bodySize.x, bodySize.y) + personalSpace + avoidanceRange;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            bodySize.x = Mathf.Max(0.05f, bodySize.x);
            bodySize.y = Mathf.Max(0.05f, bodySize.y);
            personalSpace = Mathf.Max(0f, personalSpace);
            avoidanceRange = Mathf.Max(0f, avoidanceRange);
            avoidancePower = Mathf.Max(0f, avoidancePower);
            fitPadding.x = Mathf.Max(0f, fitPadding.x);
            fitPadding.y = Mathf.Max(0f, fitPadding.y);
            fitScale = Mathf.Max(0.01f, fitScale);

            if (isAutoFitBodySizeFromSprite)
            {
                FitBodySizeFromSprite(false);
            }
        }

        [ContextMenu("Fit Body Size From Sprite")]
        private void FitBodySizeFromSpriteContext()
        {
            FitBodySizeFromSprite(true);
        }

        [ContextMenu("Clear Size Source Sprite")]
        private void ClearSizeSourceSprite()
        {
            Undo.RecordObject(this, "Clear Size Source Sprite");
            sizeSourceSprite = null;
            EditorUtility.SetDirty(this);
        }

        private void FitBodySizeFromSprite(bool isRecordUndo)
        {
            Sprite sourceSprite = GetSizeSourceSprite();

            if (sourceSprite == null)
            {
                return;
            }

            Vector2 nextSize = sourceSprite.bounds.size;

            if (isIncludeTransformScale)
            {
                Vector3 scale = transform.lossyScale;
                nextSize.x *= Mathf.Abs(scale.x);
                nextSize.y *= Mathf.Abs(scale.y);
            }

            nextSize *= fitScale;
            nextSize += fitPadding;

            if (isRecordUndo)
            {
                Undo.RecordObject(this, "Fit Agent Avoidance Body Size");
            }

            SetBodySize(nextSize);

            if (isRecordUndo)
            {
                EditorUtility.SetDirty(this);
            }
        }

        private Sprite GetSizeSourceSprite()
        {
            if (sizeSourceSprite != null)
            {
                return sizeSourceSprite;
            }

            if (isUseSpriteRendererWhenSourceEmpty == false)
            {
                return null;
            }

            if (TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                return spriteRenderer.sprite;
            }

            return null;
        }

        private void OnDrawGizmos()
        {
            if (isDrawGizmosOnlySelected)
            {
                return;
            }

            DrawAvoidanceGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            DrawAvoidanceGizmos();
        }

        private void DrawAvoidanceGizmos()
        {
            if (isDrawGizmos == false)
            {
                return;
            }

            Vector2 center = Position;
            Vector2 rangeCenter = AvoidancePosition;
            Vector2 personalSpaceSize = bodySize + Vector2.one * (personalSpace * 2f);
            Vector2 rangeSize = bodySize + Vector2.one * ((personalSpace + avoidanceRange) * 2f);

            Gizmos.color = IsAvoidanceEnabled ? Color.cyan : Color.gray;
            Gizmos.DrawWireCube(center, bodySize);

            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(center, personalSpaceSize);

            Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.65f);
            Gizmos.DrawWireCube(rangeCenter, rangeSize);

            Gizmos.color = new Color(1f, 1f, 1f, 0.9f);
            Gizmos.DrawLine(transform.position, center);
            Gizmos.DrawWireSphere(center, 0.05f);

            Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.9f);
            Gizmos.DrawLine(center, rangeCenter);
            Gizmos.DrawWireSphere(rangeCenter, 0.05f);
        }
#endif
    }
}
