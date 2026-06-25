using System.Collections.Generic;
using _Project.Core.PoolManaging;
using _Project.Gameplay.AgentSystem.AgentModules;
using _Project.Gameplay.TaskSystem.Menu;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem._Agent.TaskAgent
{
    [CreateAssetMenu(fileName = "CustomerAgentProfile", menuName = "Agent/CustomerAgentProfileSO", order = 1)]
    public class CustomerAgentProfileSO : AgentProfileSO
    {
        [SerializeField] private List<FoodMenuSO> orderableMenus = new();
        [SerializeField] private PoolItemSO poolItem;
        [SerializeField] private CustomerDiningConfigSO diningConfig;
        [SerializeField, HideInInspector] private float tableWaitPenaltyTime = CustomerDiningConfigSO.DEFAULT_TABLE_WAIT_PENALTY_TIME;
        [SerializeField, HideInInspector] private float tableWaitRatingPenalty = CustomerDiningConfigSO.DEFAULT_TABLE_WAIT_RATING_PENALTY;

        public IReadOnlyList<FoodMenuSO> OrderableMenus => orderableMenus;
        public PoolItemSO PoolItem => poolItem;
        public CustomerDiningConfigSO DiningConfig => diningConfig;
        public float WaitTime => diningConfig == null ? CustomerDiningConfigSO.DEFAULT_WAIT_TIME : diningConfig.WaitTime;
        public float EatingSpeed => diningConfig == null ? CustomerDiningConfigSO.DEFAULT_EATING_SPEED : diningConfig.EatingSpeed;
        public float TableWaitPenaltyTime => diningConfig == null ? Mathf.Max(0f, tableWaitPenaltyTime) : diningConfig.TableWaitPenaltyTime;
        public float TableWaitRatingPenalty => diningConfig == null ? Mathf.Max(0f, tableWaitRatingPenalty) : diningConfig.TableWaitRatingPenalty;
        public float TrashRatingPenalty => diningConfig == null ? CustomerDiningConfigSO.DEFAULT_TRASH_RATING_PENALTY : diningConfig.TrashRatingPenalty;
        public float MaxFoodWaitRatingPenalty => diningConfig == null ? CustomerDiningConfigSO.DEFAULT_MAX_FOOD_WAIT_RATING_PENALTY : diningConfig.MaxFoodWaitRatingPenalty;
        public AnimParamSO EatAnimParam => diningConfig == null ? null : diningConfig.EatAnimParam;
    }
}
