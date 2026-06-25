using System.Collections.Generic;
using _Project.Gameplay.AgentSystem.AgentModules.AvoidanceModule;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project.Gameplay.AgentSystem.TestCode
{
    public sealed class AgentAvoidanceTestSceneController : MonoBehaviour
    {
        [SerializeField] private int pairCount = 4;
        [SerializeField] private float scenarioRadius = 3f;
        [SerializeField] private float verticalSpacing = 0.7f;
        [SerializeField] private float agentSpeed = 1.4f;
        [SerializeField] private float stopDistance = 0.08f;
        [SerializeField] private float personalSpace = 0.08f;
        [SerializeField] private bool isAvoidanceEnabledOnStart = true;

        private readonly List<GameObject> _spawnedAgents = new();
        private readonly List<AgentAvoidanceTestMover> _movers = new();
        private readonly List<Object> _createdAssets = new();

        private Sprite _circleSprite;
        private bool _isAvoidanceEnabled;

        private void Start()
        {
            _isAvoidanceEnabled = isAvoidanceEnabledOnStart;
            _ = AgentAvoidanceManager.Instance;

            SpawnScenario();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                SetAvoidanceEnabled(_isAvoidanceEnabled == false);
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                ResetScenario();
            }
        }

        private void OnDestroy()
        {
            ClearScenario();

            for (int i = 0; i < _createdAssets.Count; i++)
            {
                Destroy(_createdAssets[i]);
            }

            _createdAssets.Clear();
        }

        public void SetAvoidanceEnabled(bool isEnabled)
        {
            _isAvoidanceEnabled = isEnabled;

            for (int i = 0; i < _movers.Count; i++)
            {
                _movers[i].SetAvoidanceEnabled(_isAvoidanceEnabled);
            }
        }

        private void ResetScenario()
        {
            ClearScenario();
            SpawnScenario();
        }

        private void SpawnScenario()
        {
            int count = Mathf.Max(1, pairCount);
            float centerOffset = (count - 1) * verticalSpacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float y = i * verticalSpacing - centerOffset;
                Vector2 bodySize = i % 2 == 0 ? new Vector2(0.5f, 0.35f) : new Vector2(0.72f, 0.45f);

                CreateAgent($"Avoidance Test Agent L{i + 1}", new Vector2(-scenarioRadius, y), new Vector2(scenarioRadius, y), bodySize, Color.cyan);
                CreateAgent($"Avoidance Test Agent R{i + 1}", new Vector2(scenarioRadius, y + 0.18f), new Vector2(-scenarioRadius, y + 0.18f), bodySize, Color.magenta);
            }

            CreateAgent("Avoidance Test Big Agent", new Vector2(0f, -scenarioRadius), new Vector2(0f, scenarioRadius), new Vector2(0.95f, 0.55f), new Color(1f, 0.75f, 0.25f));
        }

        private void CreateAgent(string agentName, Vector2 start, Vector2 target, Vector2 bodySize, Color color)
        {
            GameObject agentObject = new GameObject(agentName);
            agentObject.transform.SetParent(transform);
            agentObject.transform.position = start;
            agentObject.transform.localScale = new Vector3(bodySize.x, bodySize.y, 1f);

            SpriteRenderer spriteRenderer = agentObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = GetCircleSprite();
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 10;

            AgentAvoidanceModule avoidanceModule = agentObject.AddComponent<AgentAvoidanceModule>();
            avoidanceModule.SetBodySize(bodySize);
            avoidanceModule.SetPersonalSpace(personalSpace);
            avoidanceModule.SetAvoidanceEnabled(_isAvoidanceEnabled);

            agentObject.AddComponent<AgentAvoidanceTestAgent>();

            AgentAvoidanceTestMover mover = agentObject.AddComponent<AgentAvoidanceTestMover>();
            mover.Initialize(target, agentSpeed, stopDistance);

            _spawnedAgents.Add(agentObject);
            _movers.Add(mover);
        }

        private Sprite GetCircleSprite()
        {
            if (_circleSprite == null)
            {
                _circleSprite = CreateCircleSprite();
            }

            return _circleSprite;
        }

        private Sprite CreateCircleSprite()
        {
            const int TEXTURE_SIZE = 64;
            const float RADIUS = 30f;

            Texture2D texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((TEXTURE_SIZE - 1) * 0.5f, (TEXTURE_SIZE - 1) * 0.5f);

            for (int y = 0; y < TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < TEXTURE_SIZE; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = distance <= RADIUS ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, TEXTURE_SIZE, TEXTURE_SIZE), new Vector2(0.5f, 0.5f), TEXTURE_SIZE);
            _createdAssets.Add(texture);
            _createdAssets.Add(sprite);

            return sprite;
        }

        private void ClearScenario()
        {
            for (int i = 0; i < _spawnedAgents.Count; i++)
            {
                Destroy(_spawnedAgents[i]);
            }

            _spawnedAgents.Clear();
            _movers.Clear();
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(16f, 16f, 420f, 24f), $"Agent Avoidance Test / Space: Toggle ({_isAvoidanceEnabled}) / R: Reset");
        }
    }
}
