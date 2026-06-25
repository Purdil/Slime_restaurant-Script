using _Project.Core.PoolManaging;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem._Agent.TaskAgent;
using _Project.Gameplay.AgentSystem.AgentGenerateSystem;
using _Project.Gameplay.AgentSystem.AgentGenerateSystem.GenerateRequestStructs;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project.Gameplay.AgentSystem.TestCode
{
    public class TestCodeByYDY : MonoBehaviour
    {
        [SerializeField] private AgentProfileSO testProfile;
        [SerializeField] private GenerateAgentChannel channel;

        private void Update()
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector3 spawnPosition = GetMouseWorldPosition();
                Collider2D hit = Physics2D.OverlapPoint(spawnPosition);
                if (hit != null && hit.TryGetComponent(out Agent agent))
                {
                    PoolManager.Instance.Push(agent);
                }
                else
                {
                    channel.Raise(new GenerateAgentRequest
                    {
                        profile = testProfile,
                        spawnPosition = spawnPosition,
                    });
                }
            }
        }

        private Vector3 GetMouseWorldPosition()
        {
            Vector2 mousePosition = Mouse.current.position.value;
            float distanceFromCamera = -Camera.main.transform.position.z;
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, distanceFromCamera));
            worldPosition.z = 0f;
            return worldPosition;
        }
    }
}
