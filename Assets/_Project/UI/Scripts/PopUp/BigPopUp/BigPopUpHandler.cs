using System;
using System.Collections.Generic;
using _Project.Core.CustomLogging;
using UnityEngine;

namespace _Project.UI.Scripts.PopUp.BigPopUp
{
    public class BigPopUpHandler : IDisposable
    {
        private readonly PopUpStyleSO _popUpStyle;
        private readonly Queue<IBigPopUpRequest> _bigPopUpPendingRequests = new();
        private readonly Dictionary<BigPopUpType, IBigPopUpView> _bigPopUpViews;
        private IBigPopUpRequest _currentBigBigPopUpRequest;

        public BigPopUpHandler(BigPopUpViewEntry[] bigPopUpViews, PopUpStyleSO popUpStyle)
        {
           _popUpStyle = popUpStyle; 
            
            _bigPopUpViews = new Dictionary<BigPopUpType, IBigPopUpView>();
            foreach (var entry in bigPopUpViews)
                _bigPopUpViews.Add(entry.type, entry.viewObject.GetComponent<IBigPopUpView>());
        }

        public void Dispose()
        {
            _bigPopUpPendingRequests.Clear();
            if (_currentBigBigPopUpRequest == null) return;
            if (_bigPopUpViews.TryGetValue(_currentBigBigPopUpRequest.PopUpType, out IBigPopUpView view))
            {
                view.UnSubScribe();
                view.Hide();
                _currentBigBigPopUpRequest = null;
            }
        }
        
        public void HandleBigPopUp(IBigPopUpRequest request)
        {
            Debug.Log($"IsStackable: {request.IsStackable}");
            Debug.Log($"CurrentRequest null: {_currentBigBigPopUpRequest == null}");
            if (_currentBigBigPopUpRequest == null) OpenBigPopUpView(request);
            else if (request.IsStackable) _bigPopUpPendingRequests.Enqueue(request);
        }

        private void OpenBigPopUpView(IBigPopUpRequest request)
        {
            if (_bigPopUpViews.TryGetValue(request.PopUpType, out IBigPopUpView view))
            {
                _currentBigBigPopUpRequest = request;
                PopUpStyle style = _popUpStyle.GetPopUpStyle(request.PopUpData.MessageType);
                view.SubScribe(request, style, CleanUpBigPopUp);
                view.Show();
            }
        }

        private void CleanUpBigPopUp()
        {
            if (_bigPopUpViews.TryGetValue(_currentBigBigPopUpRequest.PopUpType, out IBigPopUpView view))
            {
                view.UnSubScribe();
                view.Hide();
            }
            _currentBigBigPopUpRequest = null;
            if(_bigPopUpPendingRequests.Count > 0)
                OpenBigPopUpView(_bigPopUpPendingRequests.Dequeue());
        }
    }
}