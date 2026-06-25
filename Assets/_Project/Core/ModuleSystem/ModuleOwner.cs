using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _Project.Core.ModuleSystem
{
    public abstract class ModuleOwner : MonoBehaviour
    {
        protected Dictionary<Type,IModule> _moduleDict;
        protected readonly Dictionary<Type, IModule> _resolvedCache = new();
        protected virtual void Awake()
        {
            _moduleDict = GetComponentsInChildren<IModule>().ToDictionary(module => module.GetType());

            InitializeModule();
            AfterInitializeModule();
        }


        protected virtual void InitializeModule()
        {
            foreach (IModule module in _moduleDict.Values)
            {
                module.Initialize(this);
            }
        }
        protected virtual void AfterInitializeModule()
        {
            foreach (IAfterInitModule module in _moduleDict.Values.OfType<IAfterInitModule>())
            {
                module.AfterInit();
            }
        }

        public T GetModule<T>()
        {
            Type requestType = typeof(T);
            
            if(_moduleDict.TryGetValue(requestType, out IModule module))
                return (T) module;
            if(_resolvedCache.TryGetValue(requestType, out IModule moduleInstance))
                return (T) moduleInstance;
            
            foreach (IModule valueModule in _moduleDict.Values)
            {
                if (valueModule is T castedModule)
                {
                    _resolvedCache[requestType] = valueModule;
                    return castedModule;
                }
            }
            return default;
        }
    }
}