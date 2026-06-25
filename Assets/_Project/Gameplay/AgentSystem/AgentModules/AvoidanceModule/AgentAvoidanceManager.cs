using System.Collections.Generic;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules.AvoidanceModule
{
    [DefaultExecutionOrder(-100)]
    public sealed class AgentAvoidanceManager : MonoBehaviour
    {
        private static AgentAvoidanceManager _instance;

        [SerializeField] private float cellSize = 1f;
#if UNITY_EDITOR
        [SerializeField] private bool isDrawCellGrid;
        [SerializeField] private int drawCellGridRange = 10;
        [SerializeField] private Color cellGridColor = new Color(0.2f, 0.8f, 1f, 0.35f);
#endif

        public static AgentAvoidanceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    CreateDefaultInstance();
                }

                return _instance;
            }
        }

        public static bool HasInstance => _instance != null;

        private AgentAvoidanceRegistry _registry;
        private int _nextAvoidanceId = 1;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            InitializeRegistry();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public int GetNextAvoidanceId()
        {
            int nextId = _nextAvoidanceId;
            _nextAvoidanceId++;

            return nextId;
        }

        public void Register(IAvoidanceBody body)
        {
            _registry.Register(body);
        }

        public void Unregister(IAvoidanceBody body)
        {
            _registry.Unregister(body);
        }

        public void Refresh(IAvoidanceBody body)
        {
            _registry.Refresh(body);
        }

        public void Query(Vector2 position, float range, List<IAvoidanceBody> results)
        {
            _registry.Query(position, range, results);
        }

        private static void CreateDefaultInstance()
        {
            GameObject managerObject = new GameObject(nameof(AgentAvoidanceManager));
            _instance = managerObject.AddComponent<AgentAvoidanceManager>();
        }

        private void InitializeRegistry()
        {
            cellSize = Mathf.Max(0.1f, cellSize);
            _registry = new AgentAvoidanceRegistry(cellSize);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (isDrawCellGrid == false)
            {
                return;
            }

            DrawCellGridGizmos();
        }

        private void OnValidate()
        {
            cellSize = Mathf.Max(0.1f, cellSize);
            drawCellGridRange = Mathf.Max(1, drawCellGridRange);
        }

        private void DrawCellGridGizmos()
        {
            float validatedCellSize = Mathf.Max(0.1f, cellSize);
            Vector2Int centerCell = WorldToCell(transform.position, validatedCellSize);
            int minX = centerCell.x - drawCellGridRange;
            int maxX = centerCell.x + drawCellGridRange;
            int minY = centerCell.y - drawCellGridRange;
            int maxY = centerCell.y + drawCellGridRange;
            float z = transform.position.z;

            Gizmos.color = cellGridColor;

            for (int x = minX; x <= maxX + 1; x++)
            {
                float worldX = x * validatedCellSize;
                Vector3 start = new Vector3(worldX, minY * validatedCellSize, z);
                Vector3 end = new Vector3(worldX, (maxY + 1) * validatedCellSize, z);
                Gizmos.DrawLine(start, end);
            }

            for (int y = minY; y <= maxY + 1; y++)
            {
                float worldY = y * validatedCellSize;
                Vector3 start = new Vector3(minX * validatedCellSize, worldY, z);
                Vector3 end = new Vector3((maxX + 1) * validatedCellSize, worldY, z);
                Gizmos.DrawLine(start, end);
            }
        }

        private static Vector2Int WorldToCell(Vector2 position, float validatedCellSize)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / validatedCellSize),
                Mathf.FloorToInt(position.y / validatedCellSize));
        }
#endif
    }
}
