using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.AgentSystem.AgentModules.CustomerModule;
using _Project.Gameplay.AgentSystem._Agent.TaskAgent.Agents;
using _Project.Gameplay.TaskSystem.OrderSystem;

namespace _Project.Gameplay.TaskSystem.Managers
{
    public static class RestaurantRuntimeCloseService
    {
        private static bool _isClosing;
        public static bool IsClosed { get; private set; } = false;

        public static void SetRestaurantOpen(bool isOpen)
        {
            if (isOpen)
            {
                IsClosed = false;
                return;
            }

            CloseRestaurantRuntime();
        }

        public static void CloseRestaurantRuntime(TaskCancelReason reason = TaskCancelReason.RestaurantClosed)
        {
            if (reason == TaskCancelReason.RestaurantClosed)
            {
                IsClosed = true;
            }

            if (_isClosing)
            {
                return;
            }

            _isClosing = true;
            try
            {
                TaskModule.CancelAllActiveStaffTasks(reason);

                if (RestaurantOrderManager.IsNullInstance == false)
                {
                    RestaurantOrderManager.Instance.CancelAllOrders(reason);
                }

                if (InteractObjectManager.IsNullInstance == false)
                {
                    InteractObjectManager.Instance.CancelRestaurantRuntimeState(reason);
                }

                CustomerDiningModule.LeaveAllActiveCustomers(reason);
            }
            finally
            {
                _isClosing = false;
            }
        }

        public static void ClearAccountRuntime()
        {
            if (_isClosing)
            {
                return;
            }

            _isClosing = true;
            try
            {
                TaskModule.CancelAllActiveStaffTasks(TaskCancelReason.AccountSwitch);

                if (RestaurantOrderManager.IsNullInstance == false)
                {
                    RestaurantOrderManager.Instance.CancelAllOrders(
                        TaskCancelReason.AccountSwitch,
                        false);
                }

                if (InteractObjectManager.IsNullInstance == false)
                {
                    InteractObjectManager.Instance.CancelRestaurantRuntimeState(
                        TaskCancelReason.AccountSwitch,
                        false);
                }

                CustomerAgent.ReturnAllActiveCustomersToPoolImmediately(TaskCancelReason.AccountSwitch);
                CustomerDiningModule.ReturnAllActiveCustomersToPoolImmediately(TaskCancelReason.AccountSwitch);
            }
            finally
            {
                _isClosing = false;
            }
        }
    }
}
