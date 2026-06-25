using System;
using _Project.UI.Scripts.MVP.Shared.Resource;

namespace _Project.Core.Systems.Reward.OfflineReward
{
    public readonly struct OfflineRewardResult
    {
        public bool IsAvailable { get; }
        public ResourceType ResourceType { get; }
        public long RewardAmount { get; }
        public TimeSpan OfflineTime { get; }
        public TimeSpan RewardedTime { get; }

        private OfflineRewardResult(
            bool isAvailable,
            ResourceType resourceType,
            long rewardAmount,
            TimeSpan offlineTime,
            TimeSpan rewardedTime)
        {
            IsAvailable = isAvailable;
            ResourceType = resourceType;
            RewardAmount = rewardAmount;
            OfflineTime = offlineTime;
            RewardedTime = rewardedTime;
        }

        public static OfflineRewardResult None(TimeSpan offlineTime)
        {
            return new OfflineRewardResult(
                false,
                OfflineRewardConfigSO.DEFAULT_REWARD_RESOURCE_TYPE,
                0,
                offlineTime,
                TimeSpan.Zero);
        }

        public static OfflineRewardResult Available(
            ResourceType resourceType,
            long rewardAmount,
            TimeSpan offlineTime,
            TimeSpan rewardedTime)
        {
            return new OfflineRewardResult(
                true,
                resourceType,
                rewardAmount,
                offlineTime,
                rewardedTime);
        }
    }
}
