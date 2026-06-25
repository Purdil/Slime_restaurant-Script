using UnityEngine;

namespace _Project.Core.Manager
{
    [DefaultExecutionOrder(-10)]
    public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<T>();
                    if (_instance == null)
                    {
                        GameObject gameObject = new GameObject(typeof(T).Name);
                        _instance = gameObject.AddComponent<T>();
                    }
                }
                return _instance;
            }
            
        }
        
        public static bool IsNullInstance => _instance == null;

        protected virtual void Awake()
        {
            T[] managers = FindObjectsByType<T>(FindObjectsSortMode.None);
            
            if(_instance  == null)
                _instance = FindFirstObjectByType<T>();
            
            Debug.Assert(managers.Length <= 1,  "Length is many");
            if (managers.Length > 1 && _instance != this)
            {
                Destroy(gameObject);
            }
        }
        
        protected virtual void OnDestroy()
        {
            if(_instance == this)
                _instance = null;
        }
    }
}