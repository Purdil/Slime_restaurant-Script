using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using _Project.Core.Systems.Firebase.Save;
using _Project.Core.Systems.Reward.OfflineReward;
using _Project.Gameplay.BuildSystem.Scripts.SaveData;
using _Project.UI.Scripts.MVP.Shared.Resource;
using Firebase.Auth;

namespace _Project.Core.Systems.Firebase.Repository
{
    public interface IFirebaseSaveRepository
    {
        Task<UserSaveData> LoadOrCreateUserAsync(
            FirebaseUser user,
            Dictionary<string, long> defaultResources,
            float currentVersion);

        Task<ResourceSaveResult> ApplyResourceDeltaAsync(
            FirebaseUser user,
            ResourceType resourceType,
            long delta);

        Task<RatingSaveResult> ApplyRatingDeltaAsync(
            FirebaseUser user,
            float totalRatingDelta,
            int ratingCountDelta);

        Task<OfflineRewardResult> GetOfflineRewardPreviewAsync(
            FirebaseUser user,
            ResourceType resourceType,
            DateTime currentUtc,
            int minimumRewardMinutes,
            int maximumRewardHours,
            float rewardMultiplier);

        Task<OfflineRewardSaveResult> ApplyOfflineRewardAsync(
            FirebaseUser user,
            ResourceType resourceType,
            DateTime currentUtc,
            int minimumRewardMinutes,
            int maximumRewardHours,
            float rewardMultiplier);

        Task SaveOfflineIncomeSnapshotAsync(
            FirebaseUser user,
            long coinPerMinute);
        
        Task ApplyBuildingDataAsync(FirebaseUser user, BuildSaveWrapper wrapper);
        Task ApplySaveStaffAsync(FirebaseUser user, int agentId, bool isAdd);

        Task DeleteUserAccountAsync(FirebaseUser user);
        Task ApplySaveQuestAsync(FirebaseUser user, int questId);
    }

    public readonly struct ResourceSaveResult
    {
        public bool IsAccepted { get; }
        public bool ShouldReconcile { get; }
        public long ServerAmount { get; }

        public ResourceSaveResult(bool isAccepted, bool shouldReconcile, long serverAmount)
        {
            IsAccepted = isAccepted;
            ShouldReconcile = shouldReconcile;
            ServerAmount = serverAmount;
        }
    }

    public readonly struct OfflineRewardSaveResult
    {
        public bool IsAccepted { get; }
        public bool ShouldReconcile { get; }
        public long ServerAmount { get; }
        public long RewardAmount { get; }

        public OfflineRewardSaveResult(
            bool isAccepted,
            bool shouldReconcile,
            long serverAmount,
            long rewardAmount)
        {
            IsAccepted = isAccepted;
            ShouldReconcile = shouldReconcile;
            ServerAmount = serverAmount;
            RewardAmount = rewardAmount;
        }
    }

    public readonly struct RatingSaveResult
    {
        public bool IsAccepted { get; }
        public bool ShouldReconcile { get; }
        public float TotalRating { get; }
        public int RatingCount { get; }

        public RatingSaveResult(
            bool isAccepted,
            bool shouldReconcile,
            float totalRating,
            int ratingCount)
        {
            IsAccepted = isAccepted;
            ShouldReconcile = shouldReconcile;
            TotalRating = totalRating;
            RatingCount = ratingCount;
        }
    }
}
