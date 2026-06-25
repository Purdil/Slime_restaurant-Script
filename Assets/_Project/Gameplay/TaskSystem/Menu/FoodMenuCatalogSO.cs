using System.Collections.Generic;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.Menu
{
    [CreateAssetMenu(fileName = "FoodMenuCatalog", menuName = "Task/Menu/FoodMenuCatalog", order = 1)]
    public class FoodMenuCatalogSO : ScriptableObject
    {
        [SerializeField] private List<FoodMenuSO> menus = new();

        public IReadOnlyList<FoodMenuSO> Menus => menus;

        public bool TryGetRandomMenu(out FoodMenuSO menu)
        {
            menu = null;
            if (menus == null || menus.Count == 0)
            {
                return false;
            }

            int startIndex = Random.Range(0, menus.Count);
            for (int i = 0; i < menus.Count; i++)
            {
                int index = (startIndex + i) % menus.Count;
                if (menus[index] != null)
                {
                    menu = menus[index];
                    return true;
                }
            }

            return false;
        }
    }
}
