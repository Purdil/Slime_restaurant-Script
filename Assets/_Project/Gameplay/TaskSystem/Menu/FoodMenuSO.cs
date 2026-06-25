using System.Collections.Generic;
using _Project.Core.PoolManaging;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.Menu
{
    [CreateAssetMenu(fileName = "FoodMenu", menuName = "Task/Menu/FoodMenu", order = 0)]
    public class FoodMenuSO : ScriptableObject
    {
        [SerializeField] private string menuId;
        [SerializeField] private string menuName;
        [SerializeField] private Sprite menuSprite;
        [SerializeField] private GameObject foodPrefab;
        [SerializeField] private PoolItemSO foodPoolItem;
        [SerializeField] private int price;
        [SerializeField] private float eatingTime = 5f;
        [SerializeField] private List<FoodCookingStepSO> cookingSteps = new();

        public string MenuId => menuId;
        public string MenuName => string.IsNullOrWhiteSpace(menuName) ? name : menuName;
        public Sprite MenuSprite => menuSprite;
        public GameObject FoodPrefab => foodPrefab;
        public PoolItemSO FoodPoolItem => foodPoolItem;
        public int Price => Mathf.Max(0, price);
        public float EatingTime => Mathf.Max(0f, eatingTime);
        public IReadOnlyList<FoodCookingStepSO> CookingSteps => cookingSteps;
    }
}
