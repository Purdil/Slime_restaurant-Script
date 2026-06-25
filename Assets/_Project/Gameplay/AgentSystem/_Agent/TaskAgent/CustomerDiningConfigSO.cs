using _Project.Gameplay.AgentSystem.AgentModules;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem._Agent.TaskAgent
{
    [CreateAssetMenu(fileName = "CustomerDiningConfig", menuName = "Agent/CustomerDiningConfigSO", order = 2)]
    public class CustomerDiningConfigSO : ScriptableObject
    {
        public const float DEFAULT_WAIT_TIME = 30f;
        public const float DEFAULT_EATING_SPEED = 1f;
        public const float DEFAULT_TABLE_WAIT_PENALTY_TIME = 15f;
        public const float DEFAULT_TABLE_WAIT_RATING_PENALTY = 1f;
        public const float DEFAULT_TRASH_RATING_PENALTY = 0.25f;
        public const float DEFAULT_MAX_FOOD_WAIT_RATING_PENALTY = 2f;

        [SerializeField] private float waitTime = DEFAULT_WAIT_TIME;
        [SerializeField] private float eatingSpeed = DEFAULT_EATING_SPEED;
        [SerializeField] private float tableWaitPenaltyTime = DEFAULT_TABLE_WAIT_PENALTY_TIME;
        [SerializeField] private float tableWaitRatingPenalty = DEFAULT_TABLE_WAIT_RATING_PENALTY;
        [SerializeField] private float trashRatingPenalty = DEFAULT_TRASH_RATING_PENALTY;
        [SerializeField] private float maxFoodWaitRatingPenalty = DEFAULT_MAX_FOOD_WAIT_RATING_PENALTY;
        [SerializeField] private AnimParamSO eatAnimParam;

        public float WaitTime => Mathf.Max(0f, waitTime);
        public float EatingSpeed => Mathf.Max(0.01f, eatingSpeed);
        public float TableWaitPenaltyTime => Mathf.Max(0f, tableWaitPenaltyTime);
        public float TableWaitRatingPenalty => Mathf.Max(0f, tableWaitRatingPenalty);
        public float TrashRatingPenalty => Mathf.Max(0f, trashRatingPenalty);
        public float MaxFoodWaitRatingPenalty => Mathf.Max(0f, maxFoodWaitRatingPenalty);
        public AnimParamSO EatAnimParam => eatAnimParam;

        private void OnValidate()
        {
            waitTime = Mathf.Max(0f, waitTime);
            eatingSpeed = Mathf.Max(0.01f, eatingSpeed);
            tableWaitPenaltyTime = Mathf.Max(0f, tableWaitPenaltyTime);
            tableWaitRatingPenalty = Mathf.Max(0f, tableWaitRatingPenalty);
            trashRatingPenalty = Mathf.Max(0f, trashRatingPenalty);
            maxFoodWaitRatingPenalty = Mathf.Max(0f, maxFoodWaitRatingPenalty);
        }
    }
}
