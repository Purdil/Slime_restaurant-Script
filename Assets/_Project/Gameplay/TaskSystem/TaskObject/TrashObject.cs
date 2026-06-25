using System;
using System.Collections.Generic;
using _Project.Core.PoolManaging;
using _Project.Gameplay.AgentSystem._Agent;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.AgentSystem.AgentModules.CustomerModule;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.TaskObject
{
    public class TrashObject : InteractTaskObject, IPoolable
    {
        [SerializeField] private float encounterRange = 0.5f;
        [SerializeField] private float encounterCheckInterval = 0.2f;

        public int PoolId { get; set; }
        public string Name => gameObject.name;
        public GameObject SelfObject => gameObject;
        public event Action<TrashObject> OnInteracted;

        private readonly HashSet<CustomerDiningModule> _encounteredCustomers = new();
        private float _encounterCheckElapsed;
        private TaskModule _curOwnerModule;
        
        private void Update()
        {
            _encounterCheckElapsed += Time.deltaTime;
            if (_encounterCheckElapsed < encounterCheckInterval)
            {
                return;
            }

            _encounterCheckElapsed = 0f;
            CheckCustomerEncounters();
        }

        public override void Interact(Agent data)
        {
            _curOwnerModule = data.GetModule<TaskModule>();
            if (_curOwnerModule == null)
                return;
            IsDesignated = true;
            _curOwnerModule.OnEndTask += HandleEndTask;
        }

        private void HandleEndTask()
        {
            if(_curOwnerModule != null)
                _curOwnerModule.OnEndTask -= HandleEndTask;
            _curOwnerModule = null;
            IsDesignated = false;
            OnInteracted?.Invoke(this);
        }

        public override Vector3 GetInteractPosition(TaskTypeEnum interactType = TaskTypeEnum.None)
        {
            return gameObject.transform.position;
        }

        public void OnPop()
        {
            _encounterCheckElapsed = 0f;
            _encounteredCustomers.Clear();
        }

        public void OnPush()
        {
            if (_curOwnerModule != null)
            {
                _curOwnerModule.OnEndTask -= HandleEndTask;
                _curOwnerModule = null;
            }
                
            IsDesignated = false;
            _encounterCheckElapsed = 0f;
            _encounteredCustomers.Clear();
        }

        private void CheckCustomerEncounters()
        {
            IReadOnlyList<CustomerDiningModule> customers = CustomerDiningModule.ActiveCustomers;
            float range = Mathf.Max(0f, encounterRange);
            float sqrRange = range * range;

            for (int i = 0; i < customers.Count; i++)
            {
                CustomerDiningModule customer = customers[i];
                if (CanRegisterEncounter(customer) == false)
                {
                    continue;
                }

                if (Vector2.SqrMagnitude(customer.EncounterPosition - transform.position) > sqrRange)
                {
                    continue;
                }

                customer.RegisterTrashEncounter();
                _encounteredCustomers.Add(customer);
            }
        }

        private bool CanRegisterEncounter(CustomerDiningModule customer)
        {
            return customer != null &&
                   customer.CanEncounterTrash &&
                   _encounteredCustomers.Contains(customer) == false;
        }
    }
}
