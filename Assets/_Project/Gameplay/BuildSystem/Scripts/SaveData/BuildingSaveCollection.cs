using System.Collections.Generic;
using UnityEngine;

namespace _Project.Gameplay.BuildSystem.Scripts.SaveData
{
    public class BuildingSaveCollection
    {
        private readonly List<BuildingSaveData> _items = new();

        public List<BuildingSaveData> Items => _items;
        public int Count => _items.Count;

        public void Clear()
        {
            _items.Clear();
        }

        
        
        public void LoadFrom(List<BuildingSaveData> source)
        {
            _items.Clear();

            if (source == null)
            {
                return;
            }

            _items.AddRange(source);
        }

        public void Add(BuildingPlacedInfo info)
        {
            BuildingSaveData data = new BuildingSaveData
            {
                BuildingID = info.BuildingId,
                X = info.GridPos.x,
                Y = info.GridPos.y,
                Rotation = info.Rotation
            };

            data.SetConstructorID(info.ConstructorID);
            _items.Add(data);
        }

        public void Remove(int buildingId, Vector2Int gridPos)
        {
            int index = FindIndex(buildingId, gridPos);

            if (index < 0)
            {
                return;
            }

            _items.RemoveAt(index);
        }

        private int FindIndex(int buildingId, Vector2Int gridPos)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                BuildingSaveData data = _items[i];

                if (data.BuildingID != buildingId)
                {
                    continue;
                }

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
