using System;
using System.Collections.Generic;
using _Project.Core.CustomLogging;
using _Project.Core.Manager;
using _Project.Gameplay.TaskSystem.EventChannel;
using _Project.Gameplay.TaskSystem.TaskObject;
using UnityEngine;

namespace _Project.Gameplay.TaskSystem.Managers
{
    public class InteractObjectManager : MonoSingleton<InteractObjectManager>
    {
        [SerializeField] private InteractObjectRegisterChannel registChannel;
        private readonly Dictionary<Type, List<InteractTaskObject>> _interactObjects = new();

        protected override void Awake()
        {
            base.Awake();
            registChannel.OnEvent += HandleRegistObject;
        }

        public T FindNearTaskObject<T>(Vector3 position, Predicate<T> predicate = null) where T : InteractTaskObject
        {
            return FindNearTaskObject(position, TaskTypeEnum.None, false, predicate);
        }

        public bool TryFindNearTaskObject<T>(
            Vector3 position,
            out T nearObj,
            Predicate<T> predicate = null) where T : InteractTaskObject
        {
            nearObj = FindNearTaskObject(position, predicate);
            return nearObj != null;
        }

        public T FindNearInteractableTaskObject<T>(
            Vector3 position,
            TaskTypeEnum interactType = TaskTypeEnum.None,
            Predicate<T> predicate = null) where T : InteractTaskObject
        {
            return FindNearTaskObject(position, interactType, true, predicate);
        }

        public bool TryFindNearInteractableTaskObject<T>(
            Vector3 position,
            TaskTypeEnum interactType,
            out T nearObj,
            Predicate<T> predicate = null) where T : InteractTaskObject
        {
            nearObj = FindNearInteractableTaskObject(position, interactType, predicate);
            return nearObj != null;
        }

        public bool CanInteractTaskObject(InteractTaskObject taskObject, TaskTypeEnum interactType = TaskTypeEnum.None)
        {
            return taskObject != null && taskObject.CanInteract(interactType);
        }

        public bool HasTaskObject<T>(Predicate<T> predicate) where T : InteractTaskObject
        {
            foreach (T taskObject in EnumerateTaskObjects<T>())
            {
                if (predicate == null || predicate(taskObject))
                {
                    return true;
                }
            }

            return false;
        }

        public void FillTaskObjects<T>(List<T> results) where T : InteractTaskObject
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            foreach (T taskObject in EnumerateTaskObjects<T>())
            {
                results.Add(taskObject);
            }
        }

        public void CancelRestaurantRuntimeState(
            TaskCancelReason reason,
            bool shouldLeaveWaitingCustomers = true)
        {
            foreach (EnterDoor door in EnumerateTaskObjects<EnterDoor>())
            {
                door.CancelWaitingCustomers(reason, shouldLeaveWaitingCustomers);
            }

            foreach (CustomerTable table in EnumerateTaskObjects<CustomerTable>())
            {
                table.CancelRestaurantRuntimeState(reason);
            }

            foreach (CookingStationObject station in EnumerateTaskObjects<CookingStationObject>())
            {
                station.CancelRestaurantRuntimeState(reason);
            }
        }

        private T FindNearTaskObject<T>(
            Vector3 position,
            TaskTypeEnum interactType,
            bool shouldCheckInteractable,
            Predicate<T> predicate) where T : InteractTaskObject
        {
            if (HasTaskObject<T>() == false)
            {
                return null;
            }

            T nearObj = null;
            float minDistance = float.MaxValue;
            foreach (T castObj in EnumerateTaskObjects<T>())
            {
                if (shouldCheckInteractable && CanInteractTaskObject(castObj, interactType) == false)
                {
                    continue;
                }

                if (predicate != null && predicate(castObj) == false)
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude(
                    castObj.GetNearestInteractPosition(position, interactType) - position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearObj = castObj;
                }
            }

            return nearObj;
        }

        private bool HasTaskObject<T>() where T : InteractTaskObject
        {
            foreach (List<InteractTaskObject> interactObjects in _interactObjects.Values)
            {
                foreach (InteractTaskObject interactObject in interactObjects)
                {
                    if (interactObject is T)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private IEnumerable<T> EnumerateTaskObjects<T>() where T : InteractTaskObject
        {
            foreach (List<InteractTaskObject> interactObjects in _interactObjects.Values)
            {
                foreach (InteractTaskObject interactObject in interactObjects)
                {
                    if (interactObject is T castObj)
                    {
                        yield return castObj;
                    }
                }
            }
        }

        private void HandleRegistObject((MonoBehaviour obj, bool Regist)obj)
        {
            Type objType = obj.obj.GetType();
            InteractTaskObject castObj = (InteractTaskObject)obj.obj;
            if (castObj == null)
            {
                CLog.LogError("이 객체는 캐스팅이 안됩니다. InteractTaskObject를 넣어주세요.");
                return;
            }

            if (!obj.Regist)
            {
                if (_interactObjects.TryGetValue(objType, out List<InteractTaskObject> interactObjects))
                {
                    interactObjects.Remove(castObj);
                }

            }
            else if (_interactObjects.TryGetValue(objType, out List<InteractTaskObject> interactObjects))
            {
                if (interactObjects != null)
                {
                    if (interactObjects.Contains(castObj))
                    {
                        return;
                    }

                    interactObjects.Add(castObj);
                }
                else
                {
                    interactObjects = new List<InteractTaskObject>();
                    interactObjects.Add(castObj);
                    _interactObjects[objType] = interactObjects;
                }
            }
            else
            {
                _interactObjects.Add(objType, new List<InteractTaskObject>());
                _interactObjects[objType].Add(castObj);
            }
        }
    }
}
