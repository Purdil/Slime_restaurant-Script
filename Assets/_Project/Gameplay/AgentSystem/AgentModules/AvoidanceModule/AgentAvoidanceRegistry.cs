using System.Collections.Generic;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules.AvoidanceModule
{
    public sealed class AgentAvoidanceRegistry
    {
        private readonly Dictionary<Vector2Int, List<IAvoidanceBody>> _cellBodies = new();
        private readonly Dictionary<IAvoidanceBody, Vector2Int> _cellByBody = new();
        private readonly float _cellSize;

        public AgentAvoidanceRegistry(float cellSize)
        {
            _cellSize = Mathf.Max(0.1f, cellSize);
        }

        public void Register(IAvoidanceBody body)
        {
            if (body == null || _cellByBody.ContainsKey(body))
            {
                return;
            }

            Vector2Int cell = WorldToCell(body.AvoidancePosition);
            _cellByBody.Add(body, cell);
            GetBodies(cell).Add(body);
        }

        public void Unregister(IAvoidanceBody body)
        {
            if (body == null || _cellByBody.TryGetValue(body, out Vector2Int cell) == false)
            {
                return;
            }

            if (_cellBodies.TryGetValue(cell, out List<IAvoidanceBody> bodies))
            {
                bodies.Remove(body);
            }

            _cellByBody.Remove(body);
        }

        public void Refresh(IAvoidanceBody body)
        {
            if (body == null || _cellByBody.TryGetValue(body, out Vector2Int previousCell) == false)
            {
                return;
            }

            Vector2Int currentCell = WorldToCell(body.AvoidancePosition);

            if (previousCell == currentCell)
            {
                return;
            }

            if (_cellBodies.TryGetValue(previousCell, out List<IAvoidanceBody> previousBodies))
            {
                previousBodies.Remove(body);
            }

            _cellByBody[body] = currentCell;
            GetBodies(currentCell).Add(body);
        }

        public void Query(Vector2 position, float range, List<IAvoidanceBody> results)
        {
            results.Clear();

            if (range <= 0f)
            {
                return;
            }

            Vector2Int centerCell = WorldToCell(position);
            int cellRange = Mathf.CeilToInt(range / _cellSize);

            for (int x = -cellRange; x <= cellRange; x++)
            {
                for (int y = -cellRange; y <= cellRange; y++)
                {
                    Vector2Int cell = new Vector2Int(centerCell.x + x, centerCell.y + y);

                    if (_cellBodies.TryGetValue(cell, out List<IAvoidanceBody> bodies) == false)
                    {
                        continue;
                    }

                    for (int i = 0; i < bodies.Count; i++)
                    {
                        results.Add(bodies[i]);
                    }
                }
            }
        }

        private List<IAvoidanceBody> GetBodies(Vector2Int cell)
        {
            if (_cellBodies.TryGetValue(cell, out List<IAvoidanceBody> bodies) == false)
            {
                bodies = new List<IAvoidanceBody>(8);
                _cellBodies.Add(cell, bodies);
            }

            return bodies;
        }

        private Vector2Int WorldToCell(Vector2 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / _cellSize),
                Mathf.FloorToInt(position.y / _cellSize));
        }
    }
}
