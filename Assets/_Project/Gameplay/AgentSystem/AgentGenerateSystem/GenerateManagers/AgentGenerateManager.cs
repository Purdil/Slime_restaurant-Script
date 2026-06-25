using _Project.Core.CustomLogging;
using _Project.Core.PoolManaging;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules.AvoidanceModule;
using _Project.Gameplay.AgentSystem.AgentGenerateSystem.GenerateRequestStructs;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Project.Gameplay.AgentSystem.AgentGenerateSystem.GenerateManagers
{
    public abstract class AgentGenerateManager : MonoBehaviour
    {
        [SerializeField] private Transform[] spawnPositions;
        [SerializeField] private GenerateAgentChannel generateChannel;
        [SerializeField] private PoolItemSO slime;

        private void OnEnable()
        {
            if (generateChannel == null)
            {
                CLog.LogError($"AgentGenerator에 GenerateAgentChannel이 지정되지 않았습니다 : {gameObject.name}");
                return;
            }

            generateChannel.Register(GenerateAgent);
        }

        private void OnDisable()
        {
            if (generateChannel == null)
            {
                return;
            }

            generateChannel.Unregister(GenerateAgent);
        }

        protected virtual void GenerateAgent(GenerateAgentRequest request)
        {
            PoolItemSO poolItem = ResolvePoolItem(request);
            if (poolItem == null)
            {
                CLog.LogError($"AgentGenerator에 PoolItem이 지정되지 않았잖어 : {gameObject.name}");
                return;
            }

            IPoolable poolable = PoolManager.Instance.Pop(poolItem);

            if (poolable is Agent agent)
            {
                agent.ApplyProfile(request.profile);
                SetAgentSpawnPosition(agent, ResolveSpawnPosition(request));
                request.initTask?.Execute(agent);
                request.callback?.Invoke(agent);
            }
            else
            {
                if (poolable != null)
                {
                    PoolManager.Instance.Push(poolable);
                }

                CLog.LogError($"AgentGenerator에 PoolItem이 Agent가 아니잖어 : {gameObject.name}");
            }
        }

        protected virtual PoolItemSO ResolvePoolItem(GenerateAgentRequest request)
        {
            return slime;
        }

        private void SetAgentSpawnPosition(Agent agent, Vector3 spawnPosition)
        {
            IAgentMoveModule moveModule = agent.GetModule<IAgentMoveModule>();
            if (moveModule != null)
            {
                moveModule.SetPosition(spawnPosition);
                return;
            }

            agent.transform.position = spawnPosition;
            agent.GetModule<IAgentAvoidanceModule>()?.RefreshRegistration();
        }

        private Vector3 ResolveSpawnPosition(GenerateAgentRequest request)
        {
            if (request.dontUseSpawnPosition == false)
            {
                return request.spawnPosition;
            }

            if (TryGetRandomSpawnPosition(out Vector3 spawnPosition))
            {
                return spawnPosition;
            }

            CLog.LogError($"AgentGenerator에 사용할 수 있는 SpawnPosition이 없습니다 : {gameObject.name}");
            return transform.position;
        }

        private bool TryGetRandomSpawnPosition(out Vector3 result)
        {
            result = default;

            if (spawnPositions == null || spawnPositions.Length == 0)
            {
                return false;
            }

            int startIndex = Random.Range(0, spawnPositions.Length);
            for (int i = 0; i < spawnPositions.Length; i++)
            {
                int index = (startIndex + i) % spawnPositions.Length;
                Transform spawnPosition = spawnPositions[index];
                if (spawnPosition == null)
                {
                    continue;
                }

                result = spawnPosition.position;
                return true;
            }

            return false;
        }
    }
}
