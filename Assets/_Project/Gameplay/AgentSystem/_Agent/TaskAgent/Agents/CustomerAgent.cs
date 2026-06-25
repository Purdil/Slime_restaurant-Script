using System.Collections.Generic;
using _Project.Core.PoolManaging;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.AgentSystem.AgentModules.CustomerModule;
using _Project.Gameplay.TaskSystem;
using _Project.UI.Scripts.MVP._Main.Main;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem._Agent.TaskAgent.Agents
{
    public class CustomerAgent : Agent
    {
        private static readonly List<CustomerAgent> ActiveCustomers = new();

        [SerializeField] private OpenCloseEventChannel  openCloseEventChannel;
        public override TaskTypeEnum DefaultTaskType => TaskTypeEnum.Customer;

        protected override void Awake()
        {
            if (GetComponentInChildren<CustomerDiningModule>() == null)
            {
                gameObject.AddComponent<CustomerDiningModule>();
            }

            base.Awake();
        }

        private void OnEnable()
        {
            if (ActiveCustomers.Contains(this) == false)
            {
                ActiveCustomers.Add(this);
            }

            if (openCloseEventChannel == null)
            {
                return;
            }

            openCloseEventChannel.OnEvent += HandleOpenClose;
            if (openCloseEventChannel.IsOpen == false)
            {
                HandleOpenClose(false);
            }
        }

        private void OnDisable()
        {
            ActiveCustomers.Remove(this);

            if (openCloseEventChannel != null)
            {
                openCloseEventChannel.OnEvent -= HandleOpenClose;
            }
        }

        public static void ReturnAllActiveCustomersToPoolImmediately(TaskCancelReason reason)
        {
            for (int i = ActiveCustomers.Count - 1; i >= 0; i--)
            {
                CustomerAgent customer = ActiveCustomers[i];
                if (customer == null)
                {
                    ActiveCustomers.RemoveAt(i);
                    continue;
                }

                customer.ReturnToPoolImmediately(reason);
            }
        }

        private void HandleOpenClose(bool isOpen)
        {
            if (!isOpen)
            {
                GetModule<CustomerDiningModule>()?.LeaveRestaurant(TaskCancelReason.RestaurantClosed);
            }
            
        }

        private void ReturnToPoolImmediately(TaskCancelReason reason)
        {
            CustomerDiningModule diningModule = GetModule<CustomerDiningModule>();
            if (diningModule != null)
            {
                diningModule.ReturnToPoolImmediately(reason);
                return;
            }

            GetModule<TaskModule>()?.CancelAllTasks(reason);

            if (PoolManager.IsNullInstance == false)
            {
                PoolManager.Instance.Push(this);
                return;
            }

            gameObject.SetActive(false);
        }
    }
}
