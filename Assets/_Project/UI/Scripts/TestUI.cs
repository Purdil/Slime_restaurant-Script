using System;
using _Project.Core.CustomLogging;
using _Project.Gameplay.TaskSystem.Cleanliness;
using _Project.UI.Scripts.MVP._Loading;
using _Project.UI.Scripts.MVP._Loading.BufferingLoading;
using _Project.UI.Scripts.MVP.Shared;
using _Project.UI.Scripts.MVP.Shared.Resource;
using _Project.UI.Scripts.PopUp;
using _Project.UI.Scripts.PopUp.BigPopUp;
using _Project.UI.Scripts.PopUp.SmallPopUp;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace _Project.UI.Scripts
{
    public class TestUI : MonoBehaviour
    {
        [SerializeField] private GameEntryPoint gameEntryPoint;
        private int _smallPopUpTest;
        private int _bigPopUpTest;

        private void Update()
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                gameEntryPoint.SmallPopUpEventChannel.Raise(new SmallPopUpData(PopUpMessageType.Message, "Test " + _smallPopUpTest++));
            }

            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                gameEntryPoint.BigPopUpEventChannel.Raise(new CancelConfirmBigPopUpRequest(
                    popUpData: new BigPopUpData(PopUpMessageType.Message, "Test" + _bigPopUpTest, "Test" + _bigPopUpTest++),
                    onConfirm: () => CLog.Log("Confirm"),
                    onCancel: () => CLog.Log("Cancel"),
                    onClose: () => CLog.Log("Close")));
            }

            if (Keyboard.current.f3Key.isPressed)
            {
                for (int i = 0; i < 1000; i++)
                {
                    RatingTest();
                }
            }
            if (Keyboard.current.f4Key.isPressed)
            {
                TestTrash();
            }

            if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                TestShowLoading();
            }
            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                TestHideLoading();
            }
            #endif
        }

        [ContextMenu("Test")]
        public void Test()
        {
            gameEntryPoint.ResourceService.Add(ResourceType.Coin, 600, ResourceChangeSource.Event);
        }
        [ContextMenu("Test2")]
        public void Test2()
        {
            gameEntryPoint.SmallPopUpEventChannel.Raise(new SmallPopUpData(PopUpMessageType.Message, "Test"));
        }

        [ContextMenu("RatingTest")]
        public void RatingTest()
        {
            float a = Random.Range(0.0f, 5.0f);
            gameEntryPoint.RatingEventChannel.Raise(a);
        }
        [ContextMenu("TestTrash")]
        public void TestTrash()
        {
            CleanlinessManager.Instance.TrashSpawn(new Vector3(Random.Range(-10, 10), Random.Range(-10, 10), 0));
        }

        [ContextMenu("TestShowLoading")]
        public void TestShowLoading()
        {
            gameEntryPoint.BufferingLoadingEventChannel.Raise(new BufferingLoadingData(true));
        }
        [ContextMenu("TestHideLoading")]
        public void TestHideLoading()
        {
            gameEntryPoint.BufferingLoadingEventChannel.Raise(new BufferingLoadingData(false));
        }
    }
}
