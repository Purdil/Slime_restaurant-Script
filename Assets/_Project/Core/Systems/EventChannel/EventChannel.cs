using System;
using UnityEngine;

namespace _Project.Core.Systems.EventChannel
{
    public struct Empty
    {
        
    }
   
    public class EventChannel<T> : ScriptableObject
    {
        public event Action<T> OnEvent;
        protected bool HasListeners => OnEvent != null;

        public virtual void Raise(T value)
        {
            OnEvent?.Invoke(value);
        }
    }
}
