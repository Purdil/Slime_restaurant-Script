using _Project.Core.ModuleSystem;
using _Project.Gameplay.AgentSystem._Agent.TaskAgent;
using _Project.Gameplay.AgentSystem.AgentModules.Generated;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules.CommonModule
{
    public class AgentRenderModule : MonoBehaviour, IModule, IApplyProfileModule, IRenderer
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float walkVelocityThreshold = 0.03f;
        [SerializeField] private float idleVelocityThreshold = 0.01f;
        [SerializeField] private float idleDelay = 0.18f;

        private ModuleOwner _moduleOwner;
        private IAgentMoveModule _moveModule;
        private bool _useAutoAnimation = true;
        private int _curAnimHash;
        private float _idleElapsed;
        private float _lastPositionX;
        private void OnValidate()
        {
            walkVelocityThreshold = Mathf.Max(0f, walkVelocityThreshold);
            idleVelocityThreshold = Mathf.Clamp(idleVelocityThreshold, 0f, walkVelocityThreshold);
            idleDelay = Mathf.Max(0f, idleDelay);
        }

        private void Update()
        {
            
            FlipVisual();
            
            if (_useAutoAnimation == false || _moveModule == null)
            {
                return;
            }

            bool shouldWalk = _moveModule.IsMovingForAnimation ||
                              _moveModule.MagCurVelocity > walkVelocityThreshold;
            if (shouldWalk)
            {
                _idleElapsed = 0f;
                if (_curAnimHash != AnimParamRefs.WALK)
                {
                    _curAnimHash = AnimParamRefs.WALK;
                    PlayClip(_curAnimHash);
                }

                return;
            }

            if (_moveModule.MagCurVelocity > idleVelocityThreshold)
            {
                _idleElapsed = 0f;
                return;
            }

            _idleElapsed += Time.deltaTime;
            if (_curAnimHash != AnimParamRefs.IDLE && _idleElapsed >= idleDelay)
            {
                _curAnimHash = AnimParamRefs.IDLE;
                PlayClip(_curAnimHash);
            }
        }

        public void FlipThere(Vector3 position)
        {
            float deltaX = position.x - transform.position.x;
            Vector3 scale = animator.transform.localScale;

            if (deltaX < 0 && scale.x < 0)
            {
                scale.x *= -1;
                animator.gameObject.transform.localScale = scale;
            }
            else if (deltaX > 0 && scale.x > 0)
            {
                scale.x *= -1;
                animator.gameObject.transform.localScale = scale;
            }
        }
        
        private void FlipVisual()
        {
            float deltaX = transform.position.x - _lastPositionX;
            Vector3 scale = animator.transform.localScale;
            
            if (Mathf.Abs(deltaX) > 0.001f) 
            {
                if (deltaX < 0 && scale.x < 0)
                {
                    scale.x *= -1;
                    animator.gameObject.transform.localScale = scale;
                }
                else if (deltaX > 0 && scale.x > 0)
                {
                    scale.x *= -1;
                    animator.gameObject.transform.localScale = scale;
                }
                    
            }
    
            _lastPositionX = transform.position.x;
        }

        public void Initialize(ModuleOwner moduleOwner)
        {
            _moduleOwner = moduleOwner;
            _moveModule = _moduleOwner.GetModule<IAgentMoveModule>();
        }

        public void ControlManualAnimation(bool value, int animHash = 0)
        {
            _useAutoAnimation = value;
            if (!value)
            {
                _idleElapsed = 0f;
                PlayClip(animHash);
            }
        }

        public void ApplyProfile(AgentProfileSO profileSo)
        {
            animator.runtimeAnimatorController = profileSo.AnimationController;
        }

        public void PlayClip(int clipHash)
        {
            animator.Play(clipHash);
        }
    }
}
