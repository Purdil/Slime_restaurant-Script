using System;
using _Project.UI.Scripts.MVP.Shared.Resource;

namespace _Project.Core.Systems.Reward.OfflineReward
{
    public class OfflineRewardService
    {
        private readonly OfflineRewardConfigSO _config;

        public OfflineRewardService(OfflineRewardConfigSO config)
        {
            _config = config;
        }

        public ResourceType RewardResourceType => GetRewardResourceType();
        public int MinimumRewardMinutes => GetMinimumRewardMinutes();
        public int MaximumRewardHours => GetMaximumRewardHours();
        public float RewardMultiplier => GetRewardMultiplier();

        public OfflineRewardResult Calculate(DateTime lastClaimUtc, DateTime currentUtc, long coinPerMinute)
        {
            return Calculate(
                lastClaimUtc,
                currentUtc,
                coinPerMinute,
                GetRewardResourceType(),
                GetMinimumRewardMinutes(),
                GetMaximumRewardHours(),
                GetRewardMultiplier());
        }

        public static OfflineRewardResult Calculate(
            DateTime lastClaimUtc,
            DateTime currentUtc,
            long coinPerMinute,
            ResourceType rewardResourceType,
            int minimumRewardMinutes,
            int maximumRewardHours,
            float rewardMultiplier)
        {
            lastClaimUtc = EnsureUtc(lastClaimUtc);
            currentUtc = EnsureUtc(currentUtc);
            coinPerMinute = Math.Max(0, coinPerMinute);
            rewardMultiplier = Math.Max(0f, rewardMultiplier);

            if (currentUtc <= lastClaimUtc)
            {
                return OfflineRewardResult.None(TimeSpan.Zero);
            }

            TimeSpan offlineTime = currentUtc - lastClaimUtc;
            TimeSpan minimumRewardTime = TimeSpan.FromMinutes(Math.Max(0, minimumRewardMinutes));

            if (offlineTime < minimumRewardTime)
            {
                return OfflineRewardResult.None(offlineTime);
            }

            TimeSpan maximumRewardTime = TimeSpan.FromHours(Math.Max(1, maximumRewardHours));
            TimeSpan rewardedTime = offlineTime > maximumRewardTime ? maximumRewardTime : offlineTime;
            double rewardAmountValue = Math.Floor(rewardedTime.TotalMinutes * coinPerMinute * rewardMultiplier);
            long rewardAmount = rewardAmountValue >= long.MaxValue
                ? long.MaxValue
                : (long)rewardAmountValue;

            if (rewardAmount <= 0)
            {
                return OfflineRewardResult.None(offlineTime);
            }

            return OfflineRewardResult.Available(
                rewardResourceType,
                rewardAmount,
                offlineTime,
                rewardedTime);
        }

        private ResourceType GetRewardResourceType()
        {
            if (_config == null)
            {
                return OfflineRewardConfigSO.DEFAULT_REWARD_RESOURCE_TYPE;
            }

            return _config.RewardResourceType;
        }

        private int GetMinimumRewardMinutes()
        {
            if (_config == null)
            {
                return OfflineRewardConfigSO.DEFAULT_MINIMUM_REWARD_MINUTES;
            }

            return Math.Max(0, _config.MinimumRewardMinutes);
        }

        private int GetMaximumRewardHours()
        {
            if (_config == null)
            {
                return OfflineRewardConfigSO.DEFAULT_MAXIMUM_REWARD_HOURS;
            }

            return Math.Max(1, _config.MaximumRewardHours);
        }

        private float GetRewardMultiplier()
        {
            if (_config == null)
            {
                return OfflineRewardConfigSO.DEFAULT_REWARD_MULTIPLIER;
            }

            return Math.Max(0f, _config.RewardMultiplier);
        }

        private static DateTime EnsureUtc(DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
            {
                return dateTime;
            }

            return dateTime.ToUniversalTime();
        }
    }
}
