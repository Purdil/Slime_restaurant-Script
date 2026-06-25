using System.Collections.Generic;
using System.Linq;
using _Project.Core.CustomLogging;
using _Project.Core.Manager;
using _Project.Core.PoolManaging;
using _Project.Gameplay.AgentSystem.AgentGenerateSystem;
using _Project.Gameplay.AgentSystem.AgentModules;
using _Project.Gameplay.AgentSystem.Astar;
using _Project.Gameplay.TaskSystem.EventChannel;
using _Project.Gameplay.TaskSystem.Managers;
using _Project.Gameplay.TaskSystem.TaskObject;
using _Project.Gameplay.TaskSystem.TaskStructs;
using _Project.UI.Scripts.MVP._Main.Main;
using _Project.UI.Scripts.MVP.Shared;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Project.Gameplay.TaskSystem.Cleanliness
{
    public class CleanlinessManager : MonoSingleton<CleanlinessManager>
    {
        [SerializeField] private GridSystem gridSystem;
        [SerializeField] private GenerateTaskChannel generateTaskChannel;
        [SerializeField] private OpenCloseEventChannel openCloseEventChannel;
        [SerializeField] private PoolItemSO trashPoolItem;
        [SerializeField] private PoolItemSO dustPoolItem;
        
        [SerializeField] private TaskDefinitionSO trashDefinition;
        [SerializeField] private TaskDefinitionSO dustDefinition;
        
        [SerializeField] private int  _maxTrashCount;
        
        [SerializeField] private BakedDataSO bakedData;
        public ICleanlinessService CleanlinessService { get; private set; }
        protected override void Awake()
        {
            base.Awake();
            if (trashPoolItem == null || dustPoolItem == null ||
                trashDefinition == null || dustDefinition == null
                || bakedData == null || generateTaskChannel == null)
            {
                CLog.Log("CleanlinessManager의 인스펙터에 빠진 SO가 존재합니다.");
            }
            CleanlinessService = new CleanlinessService(_maxTrashCount);
        }

        public void ClearTrash()
        {
            CleanlinessService.ClearAllTrash();
        }

        public void TrashSpawn(Vector3 position)
        {
            if (RestaurantRuntimeCloseService.IsClosed ||
                openCloseEventChannel != null && openCloseEventChannel.IsOpen == false)
            {
                return;
            }

            TrashObject trashObject = PoolManager.Instance.Pop(trashPoolItem) as TrashObject;
            if (trashObject != null)
            {
                trashObject.transform.position = position;
                trashObject.OnInteracted += HandleTrash;
                CleanlinessService.AddTrash(trashObject);
                RaiseEvent(trashObject,trashDefinition);
            }
        }

        public void DustSpawn()
        {
            if (RestaurantRuntimeCloseService.IsClosed ||
                openCloseEventChannel != null && openCloseEventChannel.IsOpen == false)
            {
                return;
            }

            Debug.Assert(bakedData.points.Count != 0,$"{gameObject.name}에 잘못된 BakedData가 들어갔습니다.");
            NodeData randomNode = bakedData.points[Random.Range(0, bakedData.points.Count)];
            
            // 추후 건설쪽에 위치 확인을 요청할 예정.
            while (gridSystem.IsOccupied(randomNode.cellPosition))
            {
                randomNode = bakedData.points[Random.Range(0, bakedData.points.Count)];
            }
            
            Vector3 position = new Vector3(randomNode.worldPosition.x, randomNode.worldPosition.y, 0);
            TrashObject dustObject = PoolManager.Instance.Pop(dustPoolItem) as TrashObject;
            if (dustObject != null)
            {
                dustObject.transform.position = position;
                dustObject.OnInteracted += HandleTrash;
                CleanlinessService.AddTrash(dustObject);
                RaiseEvent(dustObject,dustDefinition);
            }
        }

        private void RaiseEvent(TrashObject trashObject,TaskDefinitionSO taskDefinition)
        {
            List<ITask> tasks = new List<ITask>();
            
            tasks.Add(new ValidateTrashTask(trashObject));
            tasks.Add(new MoveTask(trashObject.GetInteractPosition()));
            tasks.Add(new WorkTask(trashObject,false,taskDefinition.baseWorkAmount,taskDefinition.playAnimParam));
            TaskAssignment assignment = new TaskAssignment(
                tasks,
                TaskTypeEnum.Cleaner,
                null,
                null,
                () => CanKeepCleanAssignmentQueued(trashObject));
            generateTaskChannel.Raise(assignment);
        }

        private bool CanKeepCleanAssignmentQueued(TrashObject trashObject)
        {
            return trashObject != null &&
                   trashObject.CanInteract() &&
                   CleanlinessService != null &&
                   CleanlinessService.HasThisTrash(trashObject);
        }

        private void HandleTrash(TrashObject obj)
        {
            TrashCollect(obj);
        }

        public void TrashCollect(TrashObject trashObject)
        {
            trashObject.OnInteracted -= HandleTrash;
            PoolManager.Instance.Push(trashObject);
            CleanlinessService.RemoveTrash(trashObject);
        }
    }
}
