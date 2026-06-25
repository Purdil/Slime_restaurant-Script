using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using _Project.Core.CustomLogging;
using _Project.Core.Systems.Firebase.Auth;
using _Project.Core.Systems.Firebase.Repository;
using _Project.Core.Systems.Firebase.Save;
using _Project.Core.Systems.Reward.OfflineReward;
using _Project.Gameplay.BuildSystem.Scripts.SaveData;
using _Project.Gameplay.TaskSystem.Cleanliness;
using _Project.Gameplay.TaskSystem.Managers;
using _Project.Gameplay.TaskSystem.Managers.TaskManagers;
using _Project.UI.Scripts.MVP._Quest.Quest;
using _Project.UI.Scripts.MVP.Shared;
using _Project.UI.Scripts.MVP.Shared.Resource;
using _Project.UI.Scripts.PopUp;
using _Project.UI.Scripts.PopUp.BigPopUp;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Google;
using UnityEngine;

namespace _Project.Core.Systems.Firebase.Login
{
    public class FirebaseLoginFlowManager : MonoBehaviour
    {
        private const float CURRENT_SAVE_VERSION = 1f;
        [SerializeField] private BigPopUpEventChannel popUpEventChannel;
        [SerializeField] private GameEntryPoint gameEntryPoint;
        [SerializeField] private BuildSaveSystem buildSaveSystem;
        [SerializeField] private SaveStaffChannel saveStaffChannel;
        [SerializeField] private SaveQuestChannel saveQuestChannel;
        [SerializeField] private OfflineRewardConfigSO offlineRewardConfig;
        #if UNITY_EDITOR
        [SerializeField] private bool shouldDebugLoginState;
        [SerializeField] private bool shouldDebugLoginInfo;
        #endif
        
        private readonly string _webClientId = "650550820258-t507ph7bnmv77mhh1ac0pvb7c006c005.apps.googleusercontent.com";

        public LoginFlowState CurrentState { get; private set; }
        public event Action<LoginFlowState> OnStateChanged;

        private IFirebaseAuthClient _authClient;
        private IFirebaseSaveRepository _saveRepository;
        public FirebaseUser CurrentUser { get; private set; }
        private bool _isWorking;
        private bool _hasSaveLoaded;
        private readonly List<SaveStaffRequest> _pendingSaveStaffRequests = new();
        private readonly List<SaveQuestRequest> _pendingSaveQuestRequests = new();
        public LoginProvider CurrentLoginProvider { get; private set; }
        private bool _hasLoginProvider;
        private bool _isRatingSubscribed;
        private OfflineRewardService _offlineRewardService;

        private void Awake()
        {
            _authClient = new FirebaseAuthClient(_webClientId);
            _saveRepository = new FirebaseSaveRepository(FirebaseFirestore.DefaultInstance);
            _offlineRewardService = new OfflineRewardService(offlineRewardConfig);
            ChangeState(LoginFlowState.None);
        }

        private void OnEnable()
        {
            if (gameEntryPoint != null && gameEntryPoint.ResourceService != null)
            {
                gameEntryPoint.ResourceService.OnResourceChanged += HandleResourceChanged;
            }

            if (buildSaveSystem != null)
            {
                buildSaveSystem.OnBuildSave += HandleBuildingChanged;
            }

            if (saveStaffChannel != null)
            {
                saveStaffChannel.OnEvent += HandleSaveStaff;
            }
            if (saveQuestChannel != null)
            {
                saveQuestChannel.OnEvent += HandleSaveQuest;
            }
            else
            {
                CLog.LogError($"{gameObject.name}에 SaveStaffChannel이 연결되지 않았습니다.");
            }

            TrySubscribeRatingService();
        }

        private async void Start()
        {
            await RunLoginFlowAsync(null);
        }

        private void OnDisable()
        {
            if (gameEntryPoint != null && gameEntryPoint.ResourceService != null)
            {
                gameEntryPoint.ResourceService.OnResourceChanged -= HandleResourceChanged;
            }

            if (buildSaveSystem != null)
            {
                buildSaveSystem.OnBuildSave -= HandleBuildingChanged;
            }

            if (saveStaffChannel != null)
            {
                saveStaffChannel.OnEvent -= HandleSaveStaff;
            }
            if (saveQuestChannel != null)
            {
                saveQuestChannel.OnEvent -= HandleSaveQuest;
            }
            UnsubscribeRatingService();
        }

        private async void OnApplicationPause(bool isPaused)
        {
            if (isPaused == false)
            {
                return;
            }

            await SaveOfflineIncomeSnapshotAsync();
        }

        private async void OnApplicationQuit()
        {
            await SaveOfflineIncomeSnapshotAsync();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnValidate()
        {
            Debug.Assert(popUpEventChannel != null, $"popUpEventChannel is null {gameObject.name}");
            Debug.Assert(saveStaffChannel != null, $"saveStaffChannel is null {gameObject.name}");
            Debug.Assert(offlineRewardConfig != null, $"offlineRewardConfig is null {gameObject.name}");
        }
#endif

        public async void RequestGuestLogin()
        {
            await RunLoginFlowAsync(LoginProvider.Guest);
        }

        public async void RequestGoogleLogin()
        {
            await RunLoginFlowAsync(LoginProvider.Google);
        }

        public async void RequestLogout()
        {
            FirebaseUser user = CurrentUser ?? _authClient.CurrentUser;

            if (user == null)
            {
                ClearLoginState();

                ChangeState(LoginFlowState.WaitingLogin);
                LogLoginInfo();
                return;
            }

            if (user.IsAnonymous)
            {
                await DeleteAccountAsync(user);
                return;
            }

            ChangeState(LoginFlowState.LoggingOut);

            await SaveOfflineIncomeSnapshotAsync();
            _authClient.SignOut();
            ClearLoginState();

            ChangeState(LoginFlowState.WaitingLogin);
            LogLoginInfo();
        }

        public async void RequestDeleteAccount()
        {
            FirebaseUser user = CurrentUser ?? _authClient.CurrentUser;

            if (user == null)
            {
                return;
            }

            await DeleteAccountAsync(user);
        }

        private async Task RunLoginFlowAsync(LoginProvider? requestedProvider)
        {
            if (_isWorking)
            {
                CLog.LogWarning("Login flow is already running.");
                return;
            }

            if (gameEntryPoint == null || gameEntryPoint.ResourceService == null)
            {
                CLog.LogError("GameEntryPoint or ResourceService is missing.");
                return;
            }

            TrySubscribeRatingService();

            _isWorking = true;

            try
            {
                ChangeState(LoginFlowState.InitializingFirebase);

                bool initialized = await _authClient.InitializeAsync();

                if (initialized == false)
                {
                    popUpEventChannel.Raise(new ConfirmBigPopUpRequest(
                        new BigPopUpData(PopUpMessageType.Error,"로그인 준비 실패","서버 연결을 준비하지 못했습니다. 인터넷 연결을 확인한 뒤 다시 시도해주세요.")));
                    ChangeState(LoginFlowState.Failed);
                    return;
                }

                if (_authClient.HasCachedUser)
                {
                    if (ShouldUpgradeGuestToGoogle(requestedProvider))
                    {
                        await UpgradeGuestToGoogleAsync();
                        return;
                    }

                    await ApplyAuthenticatedUserAsync(
                        _authClient.CurrentUser,
                        ResolveLoginProvider(_authClient.CurrentUser));
                    return;
                }

                if (requestedProvider.HasValue == false)
                {
                    ChangeState(LoginFlowState.WaitingLogin);
                    LogLoginInfo();
                    return;
                }

                ChangeState(LoginFlowState.SigningIn);

                FirebaseUser user = await SignInAsync(requestedProvider.Value);

                if (user == null)
                {
                    popUpEventChannel.Raise(new ConfirmBigPopUpRequest(
                        new BigPopUpData(PopUpMessageType.Error,"\uB85C\uADF8\uC778 \uC2E4\uD328","\uB85C\uADF8\uC778\uC744 \uC644\uB8CC\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uC7A0\uC2DC \uD6C4 \uB2E4\uC2DC \uC2DC\uB3C4\uD574\uC8FC\uC138\uC694.")));
                    LogLoginInfo(requestedProvider.Value, false);
                    ChangeState(LoginFlowState.Failed);
                    return;
                }
                CLog.Log(user.UserId);
                await ApplyAuthenticatedUserAsync(user, requestedProvider.Value);
            }
            catch (GoogleSignIn.SignInException exception)
            {
                CLog.LogWarning(exception.Message);
                ShowGoogleLoginFailedPopup(exception.Status.ToString());
                ChangeState(LoginFlowState.Failed);
            }
            catch (Exception exception)
            {
                CLog.LogError(exception.ToString());
                popUpEventChannel.Raise(new ConfirmBigPopUpRequest(
                    new BigPopUpData(PopUpMessageType.Error,"\uB85C\uADF8\uC778 \uC624\uB958","\uC54C \uC218 \uC5C6\uB294 \uC624\uB958\uAC00 \uBC1C\uC0DD\uD588\uC2B5\uB2C8\uB2E4. \uC7A0\uC2DC \uD6C4 \uB2E4\uC2DC \uC2DC\uB3C4\uD574\uC8FC\uC138\uC694.")));
                ChangeState(LoginFlowState.Failed);
            }
            finally
            {
                _isWorking = false;
            }
        }

        private async Task UpgradeGuestToGoogleAsync()
        {
            ChangeState(LoginFlowState.SigningIn);

            try
            {
                FirebaseUser linkedUser = await _authClient.LinkGuestWithGoogleAsync();

                if (linkedUser == null)
                {
                    ShowGoogleLinkCanceledPopup();
                    ChangeState(LoginFlowState.Ready);
                    LogLoginInfo();
                    return;
                }

                await ApplyAuthenticatedUserAsync(linkedUser, LoginProvider.Google);
            }
            catch (FirebaseAccountLinkException exception)
            {
                ShowGoogleAlreadyLinkedPopup();
                CLog.LogWarning(exception.Message);
                ChangeState(LoginFlowState.Ready);
                LogLoginInfo();
            }
            catch (FirebaseException exception) when (IsAlreadyLinkedAuthError(exception))
            {
                ShowGoogleAlreadyLinkedPopup();
                CLog.LogWarning(exception.Message);
                ChangeState(LoginFlowState.Ready);
                LogLoginInfo();
            }
            catch (GoogleSignIn.SignInException exception)
            {
                CLog.LogWarning(exception.Message);
                ShowGoogleLoginFailedPopup(exception.Status.ToString());
                ChangeState(LoginFlowState.Ready);
                LogLoginInfo();
            }
        }

        private async Task ApplyAuthenticatedUserAsync(FirebaseUser user, LoginProvider provider)
        {
            if (user == null)
            {
                ChangeState(LoginFlowState.Failed);
                return;
            }

            if (CurrentUser != null && CurrentUser.UserId != user.UserId)
            {
                _hasSaveLoaded = false;
                RestaurantRuntimeCloseService.ClearAccountRuntime();
            }
            CleanlinessManager.Instance.ClearTrash();
            CurrentUser = user;
            CurrentLoginProvider = provider;
            _hasLoginProvider = true;
            LogLoginInfo();

            ChangeState(LoginFlowState.LoadingSave);
            
            
            
            UserSaveData saveData = await _saveRepository.LoadOrCreateUserAsync(
                CurrentUser,
                gameEntryPoint.ResourceService.CreateSaveSnapshot(),
                CURRENT_SAVE_VERSION);

            if (saveData == null)
            {
                popUpEventChannel.Raise(new ConfirmBigPopUpRequest(
                    new BigPopUpData(PopUpMessageType.Error,"\uB370\uC774\uD130 \uBD88\uB7EC\uC624\uAE30 \uC2E4\uD328","\uACC4\uC815 \uB370\uC774\uD130 \uBD88\uB7EC\uC624\uAE30\uC5D0 \uC2E4\uD328\uD588\uC2B5\uB2C8\uB2E4. \uB124\uD2B8\uC6CC\uD06C \uC0C1\uD0DC\uB97C \uD655\uC778\uD55C \uB4A4 \uB2E4\uC2DC \uC2DC\uB3C4\uD574\uC8FC\uC138\uC694.")));
                ChangeState(LoginFlowState.Failed);
                return;
            }

            ChangeState(LoginFlowState.ApplyingSave);

            gameEntryPoint.RatingService.InitializeRating(saveData.TotalRating, saveData.RatingCount);
            gameEntryPoint.ResourceService.ApplySaveSnapshot(saveData.Currencies);
            buildSaveSystem.Load(saveData.SaveWrapper);
            gameEntryPoint.ApplyHiredAgentIds(saveData.HiredAgentIds);
            gameEntryPoint.ApplyCompletedQuestIds(saveData.CompletedQuestIds);
            _hasSaveLoaded = true;
            await FlushPendingSaveStaffRequestsAsync();
            await FlushPendingSaveQuestRequestsAsync();
            await TryPresentOfflineRewardAsync(saveData);

            ChangeState(LoginFlowState.Ready);
        }

        private async Task DeleteAccountAsync(FirebaseUser user)
        {
            ChangeState(LoginFlowState.DeletingAccount);

            await _saveRepository.DeleteUserAccountAsync(user);
            _authClient.SignOut();

            ClearLoginState();

            ChangeState(LoginFlowState.WaitingLogin);
            LogLoginInfo();
        }

        private void ClearLoginState()
        {
            _hasSaveLoaded = false;
            _pendingSaveStaffRequests.Clear();
            _pendingSaveQuestRequests.Clear();
            CurrentUser = null;
            _hasLoginProvider = false;
            RestaurantRuntimeCloseService.ClearAccountRuntime();
            gameEntryPoint?.ClearHiredAgentIds();
            gameEntryPoint?.ClearCompletedQuestIds();
        }

        private Task<FirebaseUser> SignInAsync(LoginProvider provider)
        {
            return provider switch
            {
                LoginProvider.Guest => _authClient.SignInGuestAsync(),
                LoginProvider.Google => _authClient.SignInGoogleAsync(),
                _ => Task.FromResult<FirebaseUser>(null)
            };
        }

        private bool ShouldUpgradeGuestToGoogle(LoginProvider? requestedProvider)
        {
            if (CurrentLoginProvider == requestedProvider)
            {
                popUpEventChannel.Raise(new ConfirmBigPopUpRequest(
                    new BigPopUpData(PopUpMessageType.Error,"구글 연동 실패","해당 계정은 이미 구글에 연동된 상태입니다.")));
                return false;
            }
            return requestedProvider == LoginProvider.Google &&
                   _authClient.CurrentUser != null &&
                   _authClient.CurrentUser.IsAnonymous &&
                   CurrentLoginProvider != LoginProvider.Google;
        }

        private LoginProvider ResolveLoginProvider(FirebaseUser user)
        {
            if (user != null && user.IsAnonymous)
            {
                return LoginProvider.Guest;
            }

            return LoginProvider.Google;
        }

        private static bool IsAlreadyLinkedAuthError(FirebaseException exception)
        {
            AuthError authError = (AuthError)exception.ErrorCode;
            return authError == AuthError.CredentialAlreadyInUse ||
                   authError == AuthError.AccountExistsWithDifferentCredentials ||
                   authError == AuthError.ProviderAlreadyLinked ||
                   authError == AuthError.EmailAlreadyInUse;
        }

        private void ShowGoogleAlreadyLinkedPopup()
        {
            popUpEventChannel.Raise(new ConfirmBigPopUpRequest(
                new BigPopUpData(
                    PopUpMessageType.Error,
                    "\uC774\uBBF8 \uC5F0\uB3D9\uB41C \uACC4\uC815",
                    "\uC120\uD0DD\uD55C Google \uACC4\uC815\uC740 \uC774\uBBF8 \uB2E4\uB978 \uACC4\uC815\uC5D0 \uC5F0\uB3D9\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4. \uB2E4\uB978 Google \uACC4\uC815\uC744 \uC120\uD0DD\uD558\uAC70\uB098 \uAE30\uC874 \uACC4\uC815\uC73C\uB85C \uB85C\uADF8\uC778\uD574 \uC8FC\uC138\uC694.")));
        }

        private void ShowGoogleLinkCanceledPopup()
        {
            popUpEventChannel.Raise(new ConfirmBigPopUpRequest(
                new BigPopUpData(
                    PopUpMessageType.Warning,
                    "Google \uC5F0\uB3D9 \uCDE8\uC18C",
                    "Google \uACC4\uC815 \uC120\uD0DD\uC774 \uCDE8\uC18C\uB418\uC5B4 \uAC8C\uC2A4\uD2B8 \uACC4\uC815 \uC5F0\uB3D9\uC744 \uC644\uB8CC\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.")));
        }

        private void ShowGoogleLoginFailedPopup(string reason)
        {
            popUpEventChannel.Raise(new ConfirmBigPopUpRequest(
                new BigPopUpData(
                    PopUpMessageType.Error,
                    "Google \uB85C\uADF8\uC778 \uC2E4\uD328",
                    $"Google \uB85C\uADF8\uC778 \uCC98\uB9AC \uC911 \uBB38\uC81C\uAC00 \uBC1C\uC0DD\uD588\uC2B5\uB2C8\uB2E4. \uC7A0\uC2DC \uD6C4 \uB2E4\uC2DC \uC2DC\uB3C4\uD574 \uC8FC\uC138\uC694.\n\uC6D0\uC778: {reason}")));
        }

        private async void HandleResourceChanged(
            ResourceType resourceType,
            ResourceChangeSource source,
            long amount)
        {
            if (_hasSaveLoaded == false)
            {
                return;
            }

            if (CurrentUser == null)
            {
                return;
            }

            if (source == ResourceChangeSource.Save)
            {
                return;
            }

            if (amount == 0)
            {
                return;
            }

            ResourceSaveResult saveResult = await _saveRepository.ApplyResourceDeltaAsync(
                CurrentUser,
                resourceType,
                amount);

            if (gameEntryPoint == null || gameEntryPoint.ResourceService == null)
            {
                return;
            }

            if (saveResult.ShouldReconcile == false)
            {
                return;
            }

            if (saveResult.IsAccepted &&
                gameEntryPoint.ResourceService.GetCurrentResource(resourceType) == saveResult.ServerAmount)
            {
                return;
            }

            gameEntryPoint.ResourceService.ApplySaveSnapshot(new Dictionary<string, long>
            {
                { resourceType.ToString(), saveResult.ServerAmount }
            });
        }

        private async void HandleRatingAccumulationChanged(float totalRatingDelta, int ratingCountDelta)
        {
            if (_hasSaveLoaded == false)
            {
                return;
            }

            if (CurrentUser == null)
            {
                return;
            }

            if (gameEntryPoint == null || gameEntryPoint.RatingService == null)
            {
                return;
            }

            if (totalRatingDelta == 0f && ratingCountDelta == 0)
            {
                return;
            }

            RatingSaveResult saveResult = await _saveRepository.ApplyRatingDeltaAsync(
                CurrentUser,
                totalRatingDelta,
                ratingCountDelta);

            if (gameEntryPoint == null || gameEntryPoint.RatingService == null)
            {
                return;
            }

            if (saveResult.ShouldReconcile == false)
            {
                return;
            }

            if (saveResult.IsAccepted &&
                Mathf.Approximately(gameEntryPoint.RatingService.TotalRating, saveResult.TotalRating) &&
                gameEntryPoint.RatingService.RatingCount == saveResult.RatingCount)
            {
                return;
            }

            gameEntryPoint.RatingService.InitializeRating(saveResult.TotalRating, saveResult.RatingCount);
        }
        
        private async void HandleBuildingChanged(BuildSaveWrapper obj)
        {
            if (_hasSaveLoaded == false)
            {
                return;
            }

            if (CurrentUser == null)
            {
                return;
            }
            await _saveRepository.ApplyBuildingDataAsync(CurrentUser,obj);
        }
        
        private async void HandleSaveStaff(SaveStaffRequest request)
        {
            if (_hasSaveLoaded == false)
            {
                if (CurrentUser != null)
                {
                    _pendingSaveStaffRequests.Add(request);
                }
                else
                {
                    CLog.LogWarning("Staff save request ignored because current user is empty.");
                }

                return;
            }

            if (CurrentUser == null)
            {
                return;
            }

            await _saveRepository.ApplySaveStaffAsync(
                CurrentUser,
                request.AgentId,
                request.IsAdd);
        }
        private async void HandleSaveQuest(SaveQuestRequest request)
        {
            if (_hasSaveLoaded == false)
            {
                if (CurrentUser != null)
                {
                    _pendingSaveQuestRequests.Add(request);
                }

                return;
            }

            if (CurrentUser == null)
            {
                return;
            }

            await _saveRepository.ApplySaveQuestAsync(CurrentUser, request.QuestId);
        }
        private async Task FlushPendingSaveStaffRequestsAsync()
        {
            if (_pendingSaveStaffRequests.Count == 0)
            {
                return;
            }

            if (CurrentUser == null)
            {
                _pendingSaveStaffRequests.Clear();
                return;
            }

            List<SaveStaffRequest> requests = new(_pendingSaveStaffRequests);
            _pendingSaveStaffRequests.Clear();

            for (int i = 0; i < requests.Count; i++)
            {
                SaveStaffRequest request = requests[i];
                await _saveRepository.ApplySaveStaffAsync(
                    CurrentUser,
                    request.AgentId,
                    request.IsAdd);
            }
        }
        private async Task FlushPendingSaveQuestRequestsAsync()
        {
            if (_pendingSaveQuestRequests.Count == 0)
            {
                return;
            }

            if (CurrentUser == null)
            {
                _pendingSaveQuestRequests.Clear();
                return;
            }

            List<SaveQuestRequest> requests = new(_pendingSaveQuestRequests);
            _pendingSaveQuestRequests.Clear();

            for (int i = 0; i < requests.Count; i++)
            {
                SaveQuestRequest request = requests[i];
                await _saveRepository.ApplySaveQuestAsync(CurrentUser, request.QuestId);
            }
        }
        private async Task TryPresentOfflineRewardAsync(UserSaveData saveData)
        {
            if (_offlineRewardService == null)
            {
                return;
            }

            if (popUpEventChannel == null)
            {
                return;
            }

            if (TryGetLastOfflineRewardClaimUtc(saveData, out _) == false)
            {
                return;
            }

            if (CurrentUser == null)
            {
                return;
            }

            FirebaseUser rewardUser = CurrentUser;
            OfflineRewardResult rewardResult = await _saveRepository.GetOfflineRewardPreviewAsync(
                rewardUser,
                _offlineRewardService.RewardResourceType,
                DateTime.UtcNow,
                _offlineRewardService.MinimumRewardMinutes,
                _offlineRewardService.MaximumRewardHours,
                _offlineRewardService.RewardMultiplier);

            if (CurrentUser == null || CurrentUser.UserId != rewardUser.UserId)
            {
                return;
            }

            if (rewardResult.IsAvailable == false)
            {
                return;
            }

            popUpEventChannel.Raise(new ConfirmBigPopUpRequest(
                new BigPopUpData(
                    PopUpMessageType.Success,
                    "\uBC29\uCE58 \uBCF4\uC0C1",
                    CreateOfflineRewardMessage(rewardResult)),
                onConfirm: () => _ = ClaimOfflineRewardAsync(rewardResult)));
        }

        private bool TryGetLastOfflineRewardClaimUtc(UserSaveData saveData, out DateTime lastClaimUtc)
        {
            lastClaimUtc = default;

            if (saveData == null)
            {
                return false;
            }

            try
            {
                lastClaimUtc = saveData.LastOfflineRewardClaimDate.ToDateTime();
            }
            catch (Exception exception)
            {
                CLog.LogWarning(exception.Message);
                return false;
            }

            if (lastClaimUtc.Year <= 2020)
            {
                return false;
            }

            return true;
        }

        private async Task ClaimOfflineRewardAsync(OfflineRewardResult rewardResult)
        {
            if (rewardResult.IsAvailable == false)
            {
                return;
            }

            if (CurrentUser == null)
            {
                return;
            }

            if (gameEntryPoint == null || gameEntryPoint.ResourceService == null)
            {
                return;
            }

            FirebaseUser claimUser = CurrentUser;
            OfflineRewardSaveResult saveResult = await _saveRepository.ApplyOfflineRewardAsync(
                claimUser,
                rewardResult.ResourceType,
                DateTime.UtcNow,
                _offlineRewardService.MinimumRewardMinutes,
                _offlineRewardService.MaximumRewardHours,
                _offlineRewardService.RewardMultiplier);

            if (CurrentUser == null || CurrentUser.UserId != claimUser.UserId)
            {
                return;
            }

            if (saveResult.IsAccepted == false)
            {
                CLog.LogWarning("Offline reward save failed.");
                return;
            }

            if (saveResult.ShouldReconcile == false)
            {
                return;
            }

            gameEntryPoint.ResourceService.ApplySaveSnapshot(new Dictionary<string, long>
            {
                { rewardResult.ResourceType.ToString(), saveResult.ServerAmount }
            });
        }

        private string CreateOfflineRewardMessage(OfflineRewardResult rewardResult)
        {
            return $"{FormatOfflineTime(rewardResult.OfflineTime)} \uB3D9\uC548 \uB808\uC2A4\uD1A0\uB791\uC774 \uC6B4\uC601\uB418\uC5B4 {rewardResult.RewardAmount:N0} {rewardResult.ResourceType}\uC744 \uD68D\uB4DD\uD588\uC2B5\uB2C8\uB2E4.";
        }

        private string FormatOfflineTime(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}\uC2DC\uAC04 {timeSpan.Minutes}\uBD84";
            }

            return $"{Math.Max(1, timeSpan.Minutes)}\uBD84";
        }

        private async Task SaveOfflineIncomeSnapshotAsync()
        {
            if (_hasSaveLoaded == false)
            {
                return;
            }

            if (CurrentUser == null)
            {
                return;
            }

            if (gameEntryPoint == null || gameEntryPoint.CoinIncome == null)
            {
                return;
            }

            await _saveRepository.SaveOfflineIncomeSnapshotAsync(
                CurrentUser,
                gameEntryPoint.CoinIncome.CoinPerMinute);
        }

        private void TrySubscribeRatingService()
        {
            if (_isRatingSubscribed)
            {
                return;
            }

            if (gameEntryPoint == null || gameEntryPoint.RatingService == null)
            {
                return;
            }

            gameEntryPoint.RatingService.OnRatingAccumulationChanged += HandleRatingAccumulationChanged;
            _isRatingSubscribed = true;
        }

        private void UnsubscribeRatingService()
        {
            if (_isRatingSubscribed == false)
            {
                return;
            }

            if (gameEntryPoint != null && gameEntryPoint.RatingService != null)
            {
                gameEntryPoint.RatingService.OnRatingAccumulationChanged -= HandleRatingAccumulationChanged;
            }

            _isRatingSubscribed = false;
        }

        private void ChangeState(LoginFlowState state)
        {
            CurrentState = state;
            OnStateChanged?.Invoke(state);
            #if UNITY_EDITOR
            if (shouldDebugLoginState)
            {
                CLog.Log($"LoginFlowState: {state}");
            }
            #endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogLoginInfo()
        {
            #if UNITY_EDITOR
            if (shouldDebugLoginInfo == false)
            {
                return;
            }

            string providerText = _hasLoginProvider ? CurrentLoginProvider.ToString() : "None";
            CLog.Log($"LoginInfo: IsLoggedIn={CurrentUser != null}, Provider={providerText}");
            #endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogLoginInfo(LoginProvider provider, bool isLoggedIn)
        {
            #if UNITY_EDITOR
            if (shouldDebugLoginInfo == false)
            {
                return;
            }

            CLog.Log($"LoginInfo: IsLoggedIn={isLoggedIn}, Provider={provider}");
            #endif
        }
    }
}
