using UnityEngine;

namespace Shamblers_Sense
{
    public static class ShamblersSenseLogger
    {
        private const string prefix = "[ShamblersSense] ";

        public static void Log(string message)
        {
            Debug.Log(prefix + message);
        }

        public static void LogWarning(string message)
        {
            Debug.LogWarning(prefix + message);
        }

        public static void LogError(string message)
        {
            Debug.LogError(prefix + message);
        }
    }
}