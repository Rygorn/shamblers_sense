using HarmonyLib;

namespace Shamblers_Sense
{
    public class ShamblersSenseMain
    {
        private static bool _initialized = false;

        public static void Init()
        {
            if (_initialized) return;

            ShamblersSenseLogger.Log("Initializing Shamblers Sense mod...");

            var harmony = new Harmony("com.rygorn85.shamblerssense");
            harmony.PatchAll();

            ShamblersSenseLogger.Log("Harmony patches applied.");

            _initialized = true;
        }
    }
}