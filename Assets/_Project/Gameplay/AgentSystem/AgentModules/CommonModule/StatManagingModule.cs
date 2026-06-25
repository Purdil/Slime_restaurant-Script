using System;
using System.Collections.Generic;
using System.Threading;
using _Project.Core.CustomLogging;
using _Project.Core.ModuleSystem;
using _Project.Gameplay.AgentSystem._Agent.TaskAgent;
using _Project.Gameplay.StatSystem;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules.CommonModule
{
    public class StatManagingModule : MonoBehaviour, IModule , IApplyProfileModule ,IAgentStatProvider
    {
        private StatDataSO[] _statData;
        private ModuleOwner _owner;
        private Dictionary<StatTypeSO, float> _statDataDict = new();
        private readonly Dictionary<StatTypeSO,CancellationTokenSource> _isModifyStatDict = new();
        private readonly List<IAgentStatConsumer> _statUserList = new();

        private void OnDisable()
        {
            StopAndDisposeAllTokens();
        }

        public void Initialize(ModuleOwner moduleOwner)
        {
            _owner = moduleOwner;
        }
        public void ApplyProfile(AgentProfileSO profileSo)
        {
            _statData = profileSo.StatData;
            foreach (StatDataSO dataSo in _statData)
            {
                if (profileSo.GetModifyInitStat(dataSo.StatType, out float statValue))
                {
                    _statDataDict[dataSo.StatType] = statValue;
                }
                else
                {
                    _statDataDict[dataSo.StatType] = dataSo.Value;
                }
            }
        }

        public void AddManagingStat(StatDataSO statData)
        {
            Debug.Assert(statData != null, $"전달된 스테이트 데이터가 존재하지 않습니다. \n ObjectName : {gameObject.name}");
            if(_statDataDict != null && _statDataDict.ContainsKey(statData.StatType))
                return;
            if(_statDataDict == null)
                _statDataDict = new Dictionary<StatTypeSO, float>();
            _statDataDict[statData.StatType] = statData.Value;
        }

        //스탯을 사용하는 Owner에서 호출.
        public void SetStatUsers(List<IAgentStatConsumer> statConsumers)
        {
            _statUserList.AddRange(statConsumers);
        }
        
        public bool TryGetStatData(StatTypeSO statType, out float statValue)
        {
            if (_statDataDict.TryGetValue(statType, out float data))
            {
                statValue = data;
                return true;
            }

            CLog.LogError($"없는 스탯을 가져오려 하였습니다. : {_owner.name}");
            statValue = 0f;
            return false;
        }

        public void ModifyStat(StatTypeSO statType, float newValue)
        {
            if (_statDataDict.ContainsKey(statType))
            {
                _statDataDict[statType] = newValue;
                UpdateStats(statType,newValue);
            }
        }

        public async UniTaskVoid ModifyStat(StatTypeSO statType, float newValue, int backTime, Action callback = null)
        {
            if (_isModifyStatDict.TryGetValue(statType, out CancellationTokenSource cancellationToken))
            {
                cancellationToken.Cancel();
                cancellationToken.Dispose();
                _isModifyStatDict.Remove(statType);
            }
            if (_statDataDict.ContainsKey(statType))
            {
                float temp = _statDataDict[statType];
                _statDataDict[statType] = newValue;
                UpdateStats(statType, newValue);
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                _isModifyStatDict.Add(statType, tokenSource);
                bool isComplete = await WaitModifyStat(statType,temp, backTime, tokenSource.Token);
                
                if(isComplete)
                    callback?.Invoke();
            }
        }

        private async UniTask<bool> WaitModifyStat(StatTypeSO statType, float beforeValue, float backTime, CancellationToken token)
        {
            bool isComplete = false;
            try
            {
                await UniTask.Delay((int)backTime * 1000, cancellationToken: token);
            }
            catch (Exception)
            {
                //CLog.LogError("WaitModifyStat에서 오류 난다 임마! : " + exception);
            }
            finally
            {
                if (_isModifyStatDict.TryGetValue(statType, out CancellationTokenSource cancellationToken)
                    && token ==  cancellationToken.Token)
                {
                    _statDataDict[statType] = beforeValue;
                    UpdateStats(statType, beforeValue);
                    
                    cancellationToken.Cancel();
                    cancellationToken.Dispose();
                    _isModifyStatDict.Remove(statType);
                    isComplete = true;
                }
            }

            return isComplete;
        }

        public void RefreshStat()
        {
            foreach (IAgentStatConsumer consumer in _statUserList)
            {
                consumer.RefreshStats(this);
            }
        }

        private void UpdateStats(StatTypeSO statType, float updateValue)
        {
            foreach (IAgentStatConsumer statConsumer in _statUserList)
            {
                statConsumer.UpdateStats(statType, updateValue);
            }
        }

        private void StopAndDisposeAllTokens()
        {
            foreach (CancellationTokenSource value in _isModifyStatDict.Values)
            {
                value.Cancel();
                value.Dispose();
            }
            _isModifyStatDict.Clear();
        }


    }
}
