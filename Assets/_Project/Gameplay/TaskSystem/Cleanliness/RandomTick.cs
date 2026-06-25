using System;
using _Project.Gameplay.StatSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Project.Gameplay.TaskSystem.Cleanliness
{
    public class RandomTick
    {
        private float _min;
        private float _max;
        public event Action OnTick;
        private float _currentTime;
        private float _targetTick;

        public RandomTick(float min, float max)
        {
            _min = min;
            _max = max;
            _targetTick = Random.Range(_min, _max);
        }

        /// <summary>
        /// Mono의 Update에서 호출해야됨
        /// </summary>
        public void UpdateTick()
        {
            _currentTime += Time.deltaTime;
            if (_currentTime >= _targetTick)
            {
                OnTick?.Invoke();
                _currentTime = 0;
                _targetTick = Random.Range(_min, _max);
            }
        }

        public void ReValueMinMax(float min, float max)
        {
            _min = min;
            _max = Mathf.Max(_min, max);
            _targetTick = Mathf.Clamp(_targetTick, _min, _max);
        }
    }
}
