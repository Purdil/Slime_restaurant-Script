using System.Collections.Generic;
using UnityEngine;

namespace _Project.Gameplay.BuildSystem.Scripts.SaveData
{
    public class FloorTileSaveCollection
    {
        private readonly List<FloorTileSaveData> _items = new();

        public List<FloorTileSaveData> Items => _items;

        public void Clear()
        {
            _items.Clear();
        }

        public void LoadFrom(List<FloorTileSaveData> source)
        {
            _items.Clear();

            if (source == null)
            {
                return;
            }

            _items.AddRange(source);
        }

        public bool Save(int floorBuildingID, Vector2Int gridPos)
        {
            int index = FindIndex(gridPos);

            if (index >= 0)
            {
                if (_items[index].BuildingID == floorBuildingID)
                {
                    return false;
                }

                _items[index].BuildingID = floorBuildingID;
                return true;
            }

            FloorTileSaveData data = new FloorTileSaveData
            {
                BuildingID = floorBuildingID,
                X = gridPos.x,
                Y = gridPos.y
            };

            _items.Add(data);
            return true;
        }

        private int FindIndex(Vector2Int gridPos)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                FloorTileSaveData data = _items[i];

                if (data.X != gridPos.x || data.Y != gridPos.y)
                {
                    continue;
                }

                return i;
            }

            return -1;
        }
    }
}
