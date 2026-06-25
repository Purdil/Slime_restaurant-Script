using System;
using _Project.Gameplay.StatSystem;
using _Project.Gameplay.TaskSystem.Managers;
using _Project.UI.Scripts.MVP._Main.Main;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.Cleanliness
{
    public class DustSpawner : MonoBehaviour
    {
        [SerializeField] private StatDataSO dustSpawnMin;
        [SerializeField] private StatDataSO dustSpawnMax;
        [SerializeField] private OpenCloseEventChannel openCloseEventChannel;
        private RandomTick randomTick;
        private void Awake()
        {
            randomTick = new RandomTick(dustSpawnMin.Value, dustSpawnMax.Value);
            randomTick.OnTick += DustSpawn;
        }

        private void Update()
        {
            if (RestaurantRuntimeCloseService.IsClosed ||
                openCloseEventChannel != null && openCloseEventChannel.IsOpen == false)
            {
                return;
            }

            randomTick.UpdateTick();
        }

        private void OnDestroy()
        {
            randomTick.OnTick -= DustSpawn;
        }
        private void DustSpawn()
        {
            if (RestaurantRuntimeCloseService.IsClosed ||
                openCloseEventChannel != null && openCloseEventChannel.IsOpen == false)
            {
                return;
            }

            CleanlinessManager.Instance.DustSpawn();
        }
    }
}
