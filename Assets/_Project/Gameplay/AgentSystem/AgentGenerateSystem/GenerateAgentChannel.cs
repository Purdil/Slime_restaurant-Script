using System;
using System.Collections.Generic;
using _Project.Core.Systems.EventChannel;
using _Project.Gameplay.AgentSystem.AgentGenerateSystem.GenerateRequestStructs;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentGenerateSystem
{
    [CreateAssetMenu(fileName = "GenerateAgentChannel", menuName = "EventChannel/GenerateAgentChannel", order = 0)]
    public class GenerateAgentChannel : EventChannel<GenerateAgentRequest>
    {
        private readonly List<GenerateAgentRequest> _pendingRequests = new();

        public void Register(Action<GenerateAgentRequest> handler)
        {
            if (handler == null)
            {
                return;
            }

            OnEvent += handler;
            FlushPendingRequests();
        }

        public void Unregister(Action<GenerateAgentRequest> handler)
        {
            if (handler == null)
            {
                return;
            }

            OnEvent -= handler;
        }

        public override void Raise(GenerateAgentRequest value)
        {
            if (HasListeners == false)
            {
                _pendingRequests.Add(value);
                return;
            }

            base.Raise(value);
        }

        private void FlushPendingRequests()
        {
            if (HasListeners == false || _pendingRequests.Count == 0)
            {
                return;
            }

            List<GenerateAgentRequest> pendingRequests = new(_pendingRequests);
            _pendingRequests.Clear();

            for (int i = 0; i < pendingRequests.Count; i++)
            {
                base.Raise(pendingRequests[i]);
            }
        }
    }
}
