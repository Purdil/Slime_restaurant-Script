using _Project.Core.ModuleSystem;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.AgentSystem.AgentModules.CustomerModule;
using _Project.Gameplay.TaskSystem;
using _Project.Gameplay.TaskSystem.TaskObject;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.AgentModules
{
    public class ServerGuidanceModule : MonoBehaviour, IModule
    {
        public bool IsGuiding => _guidingCustomer != null;
        public Agent GuidingCustomer => _guidingCustomer;
        public CustomerTable TargetTable => _targetTable;

        private Agent _owner;
        private Agent _guidingCustomer;
        private CustomerTable _targetTable;
        private EnterDoor _sourceDoor;
        private bool _hasStartedGuide;

        private void OnDisable()
        {
            CancelGuide();
        }

        public void Initialize(ModuleOwner moduleOwner)
        {
            _owner = moduleOwner as Agent;
            Debug.Assert(_owner != null, "ServerGuidanceModule은 Agent 타입의 ModuleOwner에서만 사용할 수 있습니다.");
        }

        public bool TryBeginGuide(
            Agent customer,
            CustomerTable table,
            EnterDoor sourceDoor = null)
        {
            if (_owner == null || customer == null || table == null || IsGuiding)
            {
                return false;
            }

            _guidingCustomer = customer;
            _targetTable = table;
            _sourceDoor = sourceDoor;
            _hasStartedGuide = false;
            return true;
        }

        public void MarkGuideStarted()
        {
            _hasStartedGuide = true;
        }

        public bool TryCompleteGuide(CustomerTable table, out Agent customer)
        {
            customer = null;

            if (table == null || _targetTable != table || IsGuidingCustomerValid() == false)
            {
                CancelGuide();
                return false;
            }

            customer = _guidingCustomer;
            ClearGuide();
            return true;
        }

        public void ClearGuide()
        {
            _guidingCustomer = null;
            _targetTable = null;
            _sourceDoor = null;
            _hasStartedGuide = false;
        }

        public void CancelGuide(TaskCancelReason reason = TaskCancelReason.Manual)
        {
            Agent customer = _guidingCustomer;
            CustomerTable table = _targetTable;
            EnterDoor sourceDoor = _sourceDoor;
            bool hasStartedGuide = _hasStartedGuide;

            ClearGuide();
            table?.CancelReservation();

            if (customer != null)
            {
                IAgentMoveModule moveModule = customer.GetModule<IAgentMoveModule>();
                moveModule?.StopFollow();
            }

            if (reason == TaskCancelReason.RestaurantClosed)
            {
                if (customer != null)
                {
                    customer.GetModule<CustomerDiningModule>()?.LeaveRestaurant(reason);
                }

                return;
            }

            if (hasStartedGuide && customer != null)
            {
                if (sourceDoor != null)
                {
                    sourceDoor.RestoreGuideCustomer(customer);
                }
            }
        }

        private bool IsGuidingCustomerValid()
        {
            if (_guidingCustomer == null || _guidingCustomer.IsActivate == false)
            {
                return false;
            }

            CustomerDiningModule diningModule = _guidingCustomer.GetModule<CustomerDiningModule>();
            return diningModule == null || diningModule.IsLeavingRestaurant == false;
        }
    }
}
