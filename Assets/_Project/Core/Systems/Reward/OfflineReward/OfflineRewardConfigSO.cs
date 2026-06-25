using _Project.UI.Scripts.MVP.Shared.Resource;
using UnityEngine;

namespace _Project.Core.Systems.Reward.OfflineReward
{
    [CreateAssetMenu(fileName = "OfflineRewardConfig", menuName = "_Project/Reward/Offline Reward Config", order = 0)]
    public class OfflineRewardConfigSO : ScriptableObject
    {
        public const ResourceType DEFAULT_REWARD_RESOURCE_TYPE = ResourceType.Coin;
        public const int DEFAULT_MINIMUM_REWARD_MINUTES = 5;
        public const int DEFAULT_MAXIMUM_REWARD_HOURS = 8;
        public const float DEFAULT_REWARD_MULTIPLIER = 0.15f;

        [SerializeField] private ResourceType rewardResourceType = DEFAULT_REWARD_RESOURCE_TYPE;
        [SerializeField] private int minimumRewardMinutes = DEFAULT_MINIMUM_REWARD_MINUTES;
        [SerializeField] private int maximumRewardHours = DEFAULT_MAXIMUM_REWARD_HOURS;
        [SerializeField] private float rewardMultiplier = DEFAULT_REWARD_MULTIPLIER;

        public ResourceType RewardResourceType => rewardResourceType;
        public int MinimumRewardMinutes => minimumRewardMinutes;
        public int MaximumRewardHours => maximumRewardHours;
        public float RewardMultiplier => rewardMultiplier;

        private void OnValidate()
        {
            if (minimumRewardMinutes < 0)
            {
                minimumRewardMinutes = 0;
            }

            if (maximumRewardHours < 1)
            {
                maximumRewardHours = 1;
            }

            if (rewardMultiplier < 0f)
            {
                rewardMultiplier = 0f;
            }
        }
    }
}
