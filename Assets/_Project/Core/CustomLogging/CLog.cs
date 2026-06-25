using UnityEngine;

namespace _Project.Core.CustomLogging
{
    public static class CLog
    {

        public static void Log(object message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(message);
#endif
        }
        
        public static void LogError(object message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(message);
#endif
        }
        
        public static void LogWarning(object message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(message);
#endif
        }
        
        
    }
}