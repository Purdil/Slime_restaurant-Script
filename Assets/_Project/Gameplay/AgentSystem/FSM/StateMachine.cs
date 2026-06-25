using System;
using System.Collections.Generic;
using System.Reflection;
using _Project.Core.CustomLogging;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.FSM.Transitions;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.FSM
{
    public class StateMachine
    {
        public AgentState CurrentState { get; private set; }
        public StateSO CurrentStateData { get; private set; }
        public AIStateContext Context { get; private set; }

        private readonly Dictionary<int, AgentState> _stateDict;
        private readonly Dictionary<int, StateSO> _stateDataDict;
        private readonly Dictionary<StateSO, int> _stateIndexDict;

        public StateMachine(Agent agent, StateListSO stateList) : this(agent, stateList.states)
        {
            StateSO initialState = stateList.initialState;
            if (initialState == null && stateList.states is { Length: > 0 })
            {
                initialState = stateList.states[0];
            }

            if (initialState != null)
            {
                ChangeState(initialState);
            }
        }

        public StateMachine(Agent agent, StateSO[] stateList)
        {
            _stateDict = new Dictionary<int, AgentState>();
            _stateDataDict = new Dictionary<int, StateSO>();
            _stateIndexDict = new Dictionary<StateSO, int>();
            Context = new AIStateContext(agent, this);

            if (stateList == null)
            {
                return;
            }

            foreach (StateSO stateData in stateList)
            {
                if (stateData == null)
                {
                    continue;
                }

                Type type = Type.GetType(stateData.className);

                if (type == null)
                {
                    CLog.LogError($"타입을 찾는 데 실패했습니다. : {stateData.className}");
                    continue;
                }

                if (typeof(AgentState).IsAssignableFrom(type) == false)
                {
                    CLog.LogError($"State class must inherit AgentState. : {stateData.className}");
                    continue;
                }

                ConstructorInfo constructor = type.GetConstructor(new[] { typeof(Agent) });
                if (constructor == null)
                {
                    CLog.LogError($"State class must have public constructor (Agent agent). : {stateData.className}");
                    continue;
                }

                AgentState agentState = constructor.Invoke(new object[] { agent }) as AgentState;
                if (agentState == null)
                {
                    CLog.LogError($"State instance creation failed. : {stateData.className}");
                    continue;
                }

                _stateDict[stateData.assetIndex] = agentState;
                _stateDataDict[stateData.assetIndex] = stateData;
                _stateIndexDict[stateData] = stateData.assetIndex;
            }
        }

        public void ChangeState(StateSO newState)
        {
            if (newState == null || _stateIndexDict.TryGetValue(newState, out int stateIndex) == false)
            {
                return;
            }
            ChangeState(stateIndex);
        }

        public void ChangeState(int newStateIndex)
        {
            if (CurrentStateData != null && CurrentStateData.assetIndex == newStateIndex)
            {
                return;
            }

            AgentState newState = _stateDict.GetValueOrDefault(newStateIndex);
            
            if (newState == null)
            {
                return;
            }

            CurrentState?.Exit();
            CurrentState = newState;
            CurrentStateData = _stateDataDict.GetValueOrDefault(newStateIndex);
            Context.SetCurrentState(CurrentState, CurrentStateData);
            CurrentState.Enter();
        }

        public void UpdateMachine()
        {
            if (CurrentState == null)
            {
                return;
            }

            Context.Tick(Time.deltaTime);
            CurrentState.Update();
            EvaluateTransitions();
        }

        private void EvaluateTransitions()
        {
            AIStateTransition[] transitions = CurrentStateData?.Transitions;
            if (transitions == null || transitions.Length == 0)
            {
                return;
            }

            foreach (AIStateTransition transition in transitions)
            {
                if (transition != null && transition.CanTransit(Context))
                {
                    ChangeState(transition.NextState);
                    return;
                }
            }
        }
    }
}
