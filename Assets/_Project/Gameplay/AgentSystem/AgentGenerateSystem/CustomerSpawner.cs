using _Project.Core.CustomLogging;
using _Project.Core.Manager;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem._Agent.TaskAgent;
using _Project.Gameplay.AgentSystem.AgentGenerateSystem.GenerateRequestStructs;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.AgentSystem.AgentModules.CustomerModule;
using _Project.Gameplay.AgentSystem.Astar;
using _Project.Gameplay.StatSystem;
using _Project.Gameplay.TaskSystem;
using _Project.Gameplay.TaskSystem.Cleanliness;
using _Project.Gameplay.TaskSystem.Managers;
using _Project.Gameplay.TaskSystem.TaskObject;
using _Project.Gameplay.TaskSystem.TaskStructs;
using _Project.UI.Scripts.MVP._Main.Main;
using _Project.UI.Scripts.MVP.Shared;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Project.Gameplay.AgentSystem.AgentGenerateSystem
{
    public class CustomerSpawner : MonoSingleton<CustomerSpawner>
    {
        [SerializeField] private OpenCloseEventChannel openCloseEventChannel;
        [SerializeField] private AgentProfileSO[] customerProfile;
        [SerializeField] private GameEntryPoint gameEntryPoint;
        [SerializeField] private float absoluteMinCreateTick;
        [SerializeField] private StatDataSO minCreateTick;
        [SerializeField] private StatDataSO maxCreateTick;
        [SerializeField] private float neutralRating = 3f;
        [SerializeField] private float maxRating = 5f;
        [SerializeField] private float minCreateTickIncreasePerRatingPoint = 0.5f;
        [SerializeField] private float maxCreateTickDecreasePerRatingPoint = 0.5f;
        [SerializeField] private float minTickGap = 0.25f;

        private InteractObjectManager _interactObjectManager;
        public GenerateAgentChannel channel;
        private RandomTick _randomTick;
        private StatManagingModule  _statManagingModule;
        
        private float _baseMinCreateTick;
        private float _baseMaxCreateTick;
        private float _curMinCreateTick;
        private float _curMaxCreateTick;
        private bool _hasBaseCreateTickInitialized;
        private bool _isRatingSubscribed;
        private bool _isStop;

        protected override void Awake()
        {
            base.Awake();
            _statManagingModule = GetComponentInChildren<StatManagingModule>();
            _statManagingModule.AddManagingStat(maxCreateTick);
            _statManagingModule.AddManagingStat(minCreateTick);
            openCloseEventChannel.OnEvent += HandleOpenClose;
        }


        private void Start()
        {
            if (InteractObjectManager.IsNullInstance == false
                && _interactObjectManager == null)
            {
                _interactObjectManager = InteractObjectManager.Instance;
            }

            Debug.Assert(_interactObjectManager != null, "인터렉트 오브젝트 매니저가 씬에 존재하지 않습니다.");
            Debug.Assert(gameEntryPoint != null, $"{gameObject.name}에 GameEntryPoint가 연결되지 않았습니다.");

            SubscribeRatingService();
            ApplyRatingSpawnRate(GetCurrentRating());
        }

        private void OnEnable()
        {
            _isStop = RestaurantRuntimeCloseService.IsClosed ||
                      openCloseEventChannel != null && openCloseEventChannel.IsOpen == false;

            if (_hasBaseCreateTickInitialized == false)
            {
                _statManagingModule.TryGetStatData(this.minCreateTick.StatType, out float minCreateTickValue);
                _statManagingModule.TryGetStatData(this.maxCreateTick.StatType, out float maxCreateTickValue);
                _baseMinCreateTick = minCreateTickValue;
                _baseMaxCreateTick = maxCreateTickValue;
                _hasBaseCreateTickInitialized = true;
            }

            _curMinCreateTick = _baseMinCreateTick;
            _curMaxCreateTick = _baseMaxCreateTick;
            
            Debug.Assert(_curMinCreateTick <= _curMaxCreateTick, $"{gameObject.name} 스탯에 이상이 있습니다.");
            
            _randomTick = new RandomTick(_curMinCreateTick, _curMaxCreateTick);
            _randomTick.OnTick += HandleGenerateCustomer;

            SubscribeRatingService();
            ApplyRatingSpawnRate(GetCurrentRating());

            if (InteractObjectManager.IsNullInstance == false
                && _interactObjectManager == null)
            {
                _interactObjectManager = InteractObjectManager.Instance;
            }
            
        }

        private void Update()
        {
            if (_randomTick == null)
            {
                return;
            }
            if (!_isStop)
                _randomTick.UpdateTick();
        }

        private void OnDisable()
        {
            if (_randomTick != null)
            {
                _randomTick.OnTick -= HandleGenerateCustomer;
            }

            UnsubscribeRatingService();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            openCloseEventChannel.OnEvent -= HandleOpenClose;
        }

        private void HandleOpenClose(bool obj)
        {
            _isStop = !obj;
            CLog.Log(_isStop);
            RestaurantRuntimeCloseService.SetRestaurantOpen(obj);
        }

        public void PlusMinAndMaxTick(float min, float max)
        {
            _baseMinCreateTick += min;
            _baseMaxCreateTick += max;
            ClampBaseCreateTick();
            ApplyRatingSpawnRate(GetCurrentRating());
        }
        public void MinusMinAndMaxTick(float min, float max)
        {
            _baseMinCreateTick -= min;
            _baseMaxCreateTick -= max;
            ClampBaseCreateTick();
            ApplyRatingSpawnRate(GetCurrentRating());
        }

        private void HandleGenerateCustomer()
        {
            if (_isStop ||
                RestaurantRuntimeCloseService.IsClosed ||
                openCloseEventChannel != null && openCloseEventChannel.IsOpen == false)
            {
                return;
            }

            channel.Raise(new GenerateAgentRequest(
                customerProfile[Random.Range(0,customerProfile.Length)],true, callback:HandleAgentSpawnCallback));
        }

        private void HandleAgentSpawnCallback(Agent obj)
        {
            TaskModule taskModule = obj.GetModule<TaskModule>();
            if (taskModule == null)
            {
                CLog.LogError($"{obj.AgentId} 가 TaskModule을 소유하지 않고 있습니다.");
                return;
            }

            EnterDoor door = _interactObjectManager.FindNearTaskObject<EnterDoor>(obj.transform.position);
            if (door == null || AstarManager.IsNullInstance)
            {
                CLog.LogError("문을 찾지 못했거나, 움직이지 못하는 위치입니다.");
                taskModule.AddTask(new ReturnAgentToPoolTask());
                return;
            }

            Vector3 customerLinePosition = door.GetInteractPosition(TaskTypeEnum.Customer);
            if (AstarManager.Instance.CanMovePosition(customerLinePosition) == false)
            {
                CLog.LogError("문을 찾지 못했거나, 움직이지 못하는 위치입니다.");
                taskModule.AddTask(new ReturnAgentToPoolTask());
                return;
            }

            obj.GetModule<IRenderer>().ControlManualAnimation(true);
            obj.GetModule<CustomerDiningModule>()?.SetHomePosition(obj.transform.position);
            if (AstarManager.Instance.CanMovePosition(obj.transform.position) == false)
            {
                obj.GetModule<IAgentMoveModule>()?.SetPosition(customerLinePosition);
            }
            taskModule.AddTask(new MoveTask(door.ReserveCustomerLinePosition(obj)));
            taskModule.AddTask(new WorkTask(door, true)); //문과 상호작용 작업
        }

        private void SubscribeRatingService()
        {
            if (_isRatingSubscribed)
            {
                return;
            }

            if (gameEntryPoint == null || gameEntryPoint.RatingService == null)
            {
                return;
            }

            gameEntryPoint.RatingService.OnRatingChanged += HandleRatingChanged;
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
                gameEntryPoint.RatingService.OnRatingChanged -= HandleRatingChanged;
            }

            _isRatingSubscribed = false;
        }

        private float GetCurrentRating()
        {
            if (gameEntryPoint == null || gameEntryPoint.RatingService == null)
            {
                return neutralRating;
            }

            return gameEntryPoint.RatingService.GetAverageRating();
        }

        private void HandleRatingChanged(float rating)
        {
            ApplyRatingSpawnRate(rating);
        }

        private void ApplyRatingSpawnRate(float rating)
        {
            float clampedRating = Mathf.Clamp(rating, 0f, maxRating);
            float nextMinCreateTick = _baseMinCreateTick;
            float nextMaxCreateTick = _baseMaxCreateTick;

            if (clampedRating < neutralRating)
            {
                nextMinCreateTick += (neutralRating - clampedRating) * minCreateTickIncreasePerRatingPoint;
            }

            if (clampedRating > neutralRating)
            {
                nextMaxCreateTick -= (clampedRating - neutralRating) * maxCreateTickDecreasePerRatingPoint;
            }

            nextMinCreateTick = Mathf.Max(absoluteMinCreateTick, nextMinCreateTick);
            nextMaxCreateTick = Mathf.Max(nextMinCreateTick + minTickGap, nextMaxCreateTick);

            SetSpawnTickStats(nextMinCreateTick, nextMaxCreateTick);
        }

        private void SetSpawnTickStats(float minValue, float maxValue)
        {
            _curMinCreateTick = minValue;
            _curMaxCreateTick = maxValue;

            _statManagingModule.ModifyStat(minCreateTick.StatType, _curMinCreateTick);
            _statManagingModule.ModifyStat(maxCreateTick.StatType, _curMaxCreateTick);
            _randomTick?.ReValueMinMax(_curMinCreateTick, _curMaxCreateTick);
        }

        private void ClampBaseCreateTick()
        {
            _baseMinCreateTick = Mathf.Max(absoluteMinCreateTick, _baseMinCreateTick);
            _baseMaxCreateTick = Mathf.Max(_baseMinCreateTick + minTickGap, _baseMaxCreateTick);
        }
    }
}
