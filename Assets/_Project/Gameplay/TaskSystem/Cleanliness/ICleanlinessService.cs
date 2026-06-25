using System;
using _Project.Gameplay.TaskSystem.TaskObject;

namespace _Project.Gameplay.TaskSystem.Cleanliness
{
    public interface ICleanlinessService
    {
        public event Action<float> OnCleanlinessChanged;
        public void AddTrash(TrashObject trash);
        public void RemoveTrash(TrashObject trash);
        public bool HasThisTrash(TrashObject trash);
        public float GetCalcCleanliness();
        public void ClearAllTrash();

    }
}