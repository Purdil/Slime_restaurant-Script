using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using _Project.Core.CustomLogging;
using _Project.Core.Systems.Firebase.Save;
using _Project.Core.Systems.Reward.OfflineReward;
using _Project.Gameplay.BuildSystem.Scripts.SaveData;
using _Project.UI.Scripts.MVP.Shared.Resource;
using Firebase.Auth;
using Firebase.Firestore;

namespace _Project.Core.Systems.Firebase.Repository
{
    public class FirebaseSaveRepository : IFirebaseSaveRepository
    {
        private const string USERS_COLLECTION = "users";
        private const string CURRENCIES_FIELD = "Currencies";
        private const string BUILDING_DATA_FIELD = "SaveWrapper";
        private const string HIRED_AGENT_IDS_FIELD = "HiredAgentIds";
        private const string LAST_LOGIN_DATE_FIELD = "LastLoginDate";
        private const string USER_NAME_FIELD = "UserName";
        private const string CREATE_TIME_FIELD = "CreateTime";
        private const string VERSION_FIELD = "Version";
        private const string TOTAL_RATING_FIELD = "TotalRating";
        private const string RATING_COUNT_FIELD = "RatingCount";
        private const string LAST_OFFLINE_REWARD_CLAIM_DATE_FIELD = "LastOfflineRewardClaimDate";
        private const string LAST_KNOWN_COIN_PER_MINUTE_FIELD = "LastKnownCoinPerMinute";
        private const string COMPLETED_QUEST_IDS_FIELD = "CompletedQuestIds";
        private readonly FirebaseFirestore _firestore;

        public FirebaseSaveRepository(FirebaseFirestore firestore)
        {
            _firestore = firestore;
        }

        public async Task<UserSaveData> LoadOrCreateUserAsync(
            FirebaseUser user,
            Dictionary<string, long> defaultResources,
            float currentVersion)
        {
            if (user == null)
            {
                CLog.LogError("Firebase user is null.");
                return null;
            }

            DocumentReference docRef = GetUserDocument(user);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                UserSaveData saveData = snapshot.ConvertTo<UserSaveData>();
                Dictionary<string, object> updateData = new()
                {
                    { LAST_LOGIN_DATE_FIELD, FieldValue.ServerTimestamp }
                };

                if (HasUsableTimestamp(saveData.LastOfflineRewardClaimDate) == false)
                {
                    updateData[LAST_OFFLINE_REWARD_CLAIM_DATE_FIELD] = FieldValue.ServerTimestamp;
                }

                await docRef.UpdateAsync(updateData);

                return saveData;
            }

            return await CreateNewUserAsync(user, defaultResources, currentVersion);
        }

        public async Task<ResourceSaveResult> ApplyResourceDeltaAsync(
            FirebaseUser user,
            ResourceType resourceType,
            long delta)
        {
            if (user == null)
            {
                CLog.LogWarning("ApplyResourceDeltaAsync called without user.");
                return new ResourceSaveResult(false, false, 0);
            }

            DocumentReference docRef = GetUserDocument(user);
            string resourceKey = resourceType.ToString();
            string fieldPath = $"{CURRENCIES_FIELD}.{resourceKey}";

            try
            {
                return await _firestore.RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot snapshot = await transaction.GetSnapshotAsync(docRef);
                    if (snapshot.Exists == false)
                    {
                        CLog.LogWarning("Resource transaction failed because user save document does not exist.");
                        return new ResourceSaveResult(false, false, 0);
                    }

                    UserSaveData saveData = snapshot.ConvertTo<UserSaveData>();
                    long serverAmount = 0;
                    if (saveData?.Currencies != null)
                    {
                        saveData.Currencies.TryGetValue(resourceKey, out serverAmount);
                    }

                    long nextAmount = serverAmount + delta;
                    if (nextAmount < 0)
                    {
                        return new ResourceSaveResult(false, true, serverAmount);
                    }

                    transaction.Update(docRef, new Dictionary<string, object>
                    {
                        { fieldPath, nextAmount },
                        { LAST_LOGIN_DATE_FIELD, FieldValue.ServerTimestamp }
                    });

                    return new ResourceSaveResult(true, true, nextAmount);
                });
            }
            catch (Exception exception)
            {
                CLog.LogError(exception.ToString());
                return new ResourceSaveResult(false, false, 0);
            }
        }

        public async Task<RatingSaveResult> ApplyRatingDeltaAsync(
            FirebaseUser user,
            float totalRatingDelta,
            int ratingCountDelta)
        {
            if (user == null)
            {
                CLog.LogWarning("ApplyRatingDeltaAsync called without user.");
                return new RatingSaveResult(false, false, 0f, 0);
            }

            DocumentReference docRef = GetUserDocument(user);

            try
            {
                return await _firestore.RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot snapshot = await transaction.GetSnapshotAsync(docRef);
                    if (snapshot.Exists == false)
                    {
                        CLog.LogWarning("Rating transaction failed because user save document does not exist.");
                        return new RatingSaveResult(false, false, 0f, 0);
                    }

                    UserSaveData saveData = snapshot.ConvertTo<UserSaveData>();
                    float serverTotalRating = saveData?.TotalRating ?? UserSaveData.DEFAULT_TOTAL_RATING;
                    int serverRatingCount = saveData?.RatingCount ?? UserSaveData.DEFAULT_RATING_COUNT;
                    float nextTotalRating = serverTotalRating + totalRatingDelta;
                    int nextRatingCount = serverRatingCount + ratingCountDelta;

                    if (nextRatingCount < 0 || nextTotalRating < 0f)
                    {
                        return new RatingSaveResult(false, true, serverTotalRating, serverRatingCount);
                    }

                    if (nextRatingCount == 0)
                    {
                        nextTotalRating = 0f;
                    }
                    else if (nextTotalRating > nextRatingCount * 5f)
                    {
                        return new RatingSaveResult(false, true, serverTotalRating, serverRatingCount);
                    }

                    transaction.Update(docRef, new Dictionary<string, object>
                    {
                        { TOTAL_RATING_FIELD, nextTotalRating },
                        { RATING_COUNT_FIELD, nextRatingCount },
                        { LAST_LOGIN_DATE_FIELD, FieldValue.ServerTimestamp }
                    });

                    return new RatingSaveResult(true, true, nextTotalRating, nextRatingCount);
                });
            }
            catch (Exception exception)
            {
                CLog.LogError(exception.ToString());
                return new RatingSaveResult(false, false, 0f, 0);
            }
        }

        public async Task<OfflineRewardResult> GetOfflineRewardPreviewAsync(
            FirebaseUser user,
            ResourceType resourceType,
            DateTime currentUtc,
            int minimumRewardMinutes,
            int maximumRewardHours,
            float rewardMultiplier)
        {
            if (user == null)
            {
                CLog.LogWarning("GetOfflineRewardPreviewAsync called without user.");
                return OfflineRewardResult.None(TimeSpan.Zero);
            }

            DocumentReference docRef = GetUserDocument(user);

            try
            {
                return await _firestore.RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot snapshot = await transaction.GetSnapshotAsync(docRef);
                    if (snapshot.Exists == false)
                    {
                        CLog.LogWarning("Offline reward preview failed because user save document does not exist.");
                        return OfflineRewardResult.None(TimeSpan.Zero);
                    }

                    UserSaveData saveData = snapshot.ConvertTo<UserSaveData>();
                    return CalculateOfflineReward(
                        saveData,
                        resourceType,
                        currentUtc,
                        minimumRewardMinutes,
                        maximumRewardHours,
                        rewardMultiplier);
                });
            }
            catch (Exception exception)
            {
                CLog.LogError(exception.ToString());
                return OfflineRewardResult.None(TimeSpan.Zero);
            }
        }

        public async Task<OfflineRewardSaveResult> ApplyOfflineRewardAsync(
            FirebaseUser user,
            ResourceType resourceType,
            DateTime currentUtc,
            int minimumRewardMinutes,
            int maximumRewardHours,
            float rewardMultiplier)
        {
            if (user == null)
            {
                CLog.LogWarning("ApplyOfflineRewardAsync called without user.");
                return new OfflineRewardSaveResult(false, false, 0, 0);
            }

            DocumentReference docRef = GetUserDocument(user);
            string resourceKey = resourceType.ToString();
            string fieldPath = $"{CURRENCIES_FIELD}.{resourceKey}";

            try
            {
                return await _firestore.RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot snapshot = await transaction.GetSnapshotAsync(docRef);
                    if (snapshot.Exists == false)
                    {
                        CLog.LogWarning("Offline reward transaction failed because user save document does not exist.");
                        return new OfflineRewardSaveResult(false, false, 0, 0);
                    }

                    UserSaveData saveData = snapshot.ConvertTo<UserSaveData>();
                    long serverAmount = 0;
                    if (saveData?.Currencies != null)
                    {
                        saveData.Currencies.TryGetValue(resourceKey, out serverAmount);
                    }

                    OfflineRewardResult rewardResult = CalculateOfflineReward(
                        saveData,
                        resourceType,
                        currentUtc,
                        minimumRewardMinutes,
                        maximumRewardHours,
                        rewardMultiplier);

                    if (rewardResult.IsAvailable == false)
                    {
                        return new OfflineRewardSaveResult(false, true, serverAmount, 0);
                    }

                    if (long.MaxValue - serverAmount < rewardResult.RewardAmount)
                    {
                        return new OfflineRewardSaveResult(false, true, serverAmount, 0);
                    }

                    long nextAmount = serverAmount + rewardResult.RewardAmount;

                    transaction.Update(docRef, new Dictionary<string, object>
                    {
                        { fieldPath, nextAmount },
                        { LAST_OFFLINE_REWARD_CLAIM_DATE_FIELD, FieldValue.ServerTimestamp },
                        { LAST_LOGIN_DATE_FIELD, FieldValue.ServerTimestamp }
                    });

                    return new OfflineRewardSaveResult(
                        true,
                        true,
                        nextAmount,
                        rewardResult.RewardAmount);
                });
            }
            catch (Exception exception)
            {
                CLog.LogError(exception.ToString());
                return new OfflineRewardSaveResult(false, false, 0, 0);
            }
        }

        public async Task SaveOfflineIncomeSnapshotAsync(FirebaseUser user, long coinPerMinute)
        {
            if (user == null)
            {
                CLog.LogWarning("SaveOfflineIncomeSnapshotAsync called without user.");
                return;
            }

            if (coinPerMinute < 0)
            {
                coinPerMinute = 0;
            }

            DocumentReference docRef = GetUserDocument(user);

            try
            {
                await docRef.UpdateAsync(new Dictionary<string, object>
                {
                    { LAST_KNOWN_COIN_PER_MINUTE_FIELD, coinPerMinute },
                    { LAST_LOGIN_DATE_FIELD, FieldValue.ServerTimestamp }
                });
            }
            catch (Exception exception)
            {
                CLog.LogError(exception.ToString());
            }
        }

        public async Task ApplyBuildingDataAsync(FirebaseUser user, BuildSaveWrapper wrapper)
        {
             if (user == null) 
             {
                CLog.LogWarning("ApplyBuildingDataAsync called without user.");
                return;
             }

             DocumentReference docRef = GetUserDocument(user);

            try
            {
                await _firestore.RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot snapshot = await transaction.GetSnapshotAsync(docRef);
                    if (snapshot.Exists == false)
                    {
                        CLog.LogWarning("Building transaction failed because user save document does not exist.");
                        return;
                    }
                    
                    transaction.Update(docRef, new Dictionary<string, object>
                    {
                        {BUILDING_DATA_FIELD, wrapper },
                    });

                    
                });
            }
            catch (Exception exception)
            {
                CLog.LogError(exception.ToString());
            }
        }
        public async Task ApplySaveQuestAsync(FirebaseUser user, int questId)
        {
            if (user == null)
            {
                CLog.LogWarning("ApplySaveQuestAsync called without user.");
                return;
            }

            DocumentReference docRef = GetUserDocument(user);

            try
            {
                await docRef.UpdateAsync(new Dictionary<string, object>
                {
                    { COMPLETED_QUEST_IDS_FIELD, FieldValue.ArrayUnion(questId) },
                    { LAST_LOGIN_DATE_FIELD, FieldValue.ServerTimestamp }
                });
            }
            catch (Exception exception)
            {
                CLog.LogError(exception.ToString());
            }
        }
        public async Task ApplySaveStaffAsync(FirebaseUser user, int agentId, bool isAdd)
        {
            if (user == null)
            {
                CLog.LogWarning("ApplySaveStaffAsync called without user.");
                return;
            }

            DocumentReference docRef = GetUserDocument(user);
            object updateValue = isAdd
                ? FieldValue.ArrayUnion(agentId)
                : FieldValue.ArrayRemove(agentId);

            try
            {
                await docRef.UpdateAsync(new Dictionary<string, object>
                {
                    { HIRED_AGENT_IDS_FIELD, updateValue },
                    { LAST_LOGIN_DATE_FIELD, FieldValue.ServerTimestamp }
                });
            }
            catch (Exception exception)
            {
                CLog.LogError(exception.ToString());
            }
        }

        public async Task DeleteUserAccountAsync(FirebaseUser user)
        {
            if (user == null)
            {
                CLog.LogWarning("DeleteUserAccountAsync called without user.");
                return;
            }

            await GetUserDocument(user).DeleteAsync();
            await user.DeleteAsync();
        }

        private DocumentReference GetUserDocument(FirebaseUser user)
        {
            return _firestore.Collection(USERS_COLLECTION).Document(user.UserId);
        }

        private async Task<UserSaveData> CreateNewUserAsync(
            FirebaseUser user,
            Dictionary<string, long> defaultResources,
            float currentVersion)
        {
            Dictionary<string, long> currencies = defaultResources ?? new Dictionary<string, long>();
            BuildSaveWrapper saveWrapper = new();
            List<int> hiredAgentIds = new();
            List<int> completedQuestIds = new();
            Timestamp currentTime = Timestamp.FromDateTime(DateTime.UtcNow);
            Dictionary<string, object> document = new()
            {
                { USER_NAME_FIELD, user.UserId },
                { CURRENCIES_FIELD, currencies },
                { BUILDING_DATA_FIELD, saveWrapper },
                { HIRED_AGENT_IDS_FIELD, hiredAgentIds },
                { COMPLETED_QUEST_IDS_FIELD, completedQuestIds },
                { TOTAL_RATING_FIELD, UserSaveData.DEFAULT_TOTAL_RATING },
                { RATING_COUNT_FIELD, UserSaveData.DEFAULT_RATING_COUNT },
                { CREATE_TIME_FIELD, FieldValue.ServerTimestamp },
                { LAST_LOGIN_DATE_FIELD, FieldValue.ServerTimestamp },
                { LAST_OFFLINE_REWARD_CLAIM_DATE_FIELD, FieldValue.ServerTimestamp },
                { LAST_KNOWN_COIN_PER_MINUTE_FIELD, 0L },
                { VERSION_FIELD, currentVersion }
            };

            await GetUserDocument(user).SetAsync(document);

            return new UserSaveData(
                user.UserId,
                currencies,
                saveWrapper,
                hiredAgentIds,
                completedQuestIds,
                currentVersion,
                UserSaveData.DEFAULT_TOTAL_RATING,
                UserSaveData.DEFAULT_RATING_COUNT,
                currentTime,
                0L);
        }

        private static bool HasUsableTimestamp(Timestamp timestamp)
        {
            return TryGetTimestampUtc(timestamp, out DateTime dateTime) && dateTime.Year > 2020;
        }

        private static OfflineRewardResult CalculateOfflineReward(
            UserSaveData saveData,
            ResourceType resourceType,
            DateTime currentUtc,
            int minimumRewardMinutes,
            int maximumRewardHours,
            float rewardMultiplier)
        {
            if (saveData == null ||
                TryGetTimestampUtc(saveData.LastOfflineRewardClaimDate, out DateTime lastClaimUtc) == false ||
                lastClaimUtc.Year <= 2020)
            {
                return OfflineRewardResult.None(TimeSpan.Zero);
            }

            return OfflineRewardService.Calculate(
                lastClaimUtc,
                currentUtc,
                saveData.LastKnownCoinPerMinute,
                resourceType,
                minimumRewardMinutes,
                maximumRewardHours,
                rewardMultiplier);
        }

        private static bool TryGetTimestampUtc(Timestamp timestamp, out DateTime dateTime)
        {
            dateTime = default;

            try
            {
                dateTime = timestamp.ToDateTime();
                if (dateTime.Kind != DateTimeKind.Utc)
                {
                    dateTime = dateTime.ToUniversalTime();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
