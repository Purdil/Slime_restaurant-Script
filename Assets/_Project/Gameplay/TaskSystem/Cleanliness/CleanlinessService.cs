using System;
using System.Collections.Generic;
using _Project.Core.PoolManaging;
using _Project.Gameplay.TaskSystem.TaskObject;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.Cleanliness
{
    public class CleanlinessService : ICleanlinessService
    {
        public event Action<float> OnCleanlinessChanged;
        private readonly List<TrashObject> _activeTrashObjects = new();
        private readonly int _maxTrashCount;

        public CleanlinessService(int maxTrashCount)
        {
            _maxTrashCount = Mathf.Max(1,maxTrashCount);
        }

        public void ClearAllTrash()
        {
            foreach (TrashObject trashObject in _activeTrashObjects)
            {
                PoolManager.Instance.Push(trashObject);
            }
            _activeTrashObjects.Clear();
        }
        public void AddTrash(TrashObject trash)
        {
            if (_activeTrashObjects.Contains(trash))
                return;
            _activeTrashObjects.Add(trash);
            OnCleanlinessChanged?.Invoke(GetCalcCleanliness());
        }
        public void RemoveTrash(TrashObject trash)
        {
            _activeTrashObjects.Remove(trash);
            OnCleanlinessChanged?.Invoke(GetCalcCleanliness());
        }

        public bool HasThisTrash(TrashObject trash)
        {
            return  _activeTrashObjects.Contains(trash) ;
        }
        
        public float GetCalcCleanliness()
        {
            float dirtyRatio = _activeTrashObjects.Count / (float)_maxTrashCount;
            return Mathf.Clamp01(1f - dirtyRatio) * 100f;
        }
    }
}