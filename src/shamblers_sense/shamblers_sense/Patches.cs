// Patches.cs

using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace Shamblers_Sense
{
    [HarmonyPatch(typeof(AIDirectorChunkData))]
    [HarmonyPatch("CheckToSpawn")]
    public class Patch_AIDirectorChunkData_CheckToSpawn
    {
        static bool Prefix(AIDirectorChunkData __instance)
        {
            // Early exit if game settings disable zombies or heat
            if (!GameStats.GetBool(EnumGameStats.ZombieHordeMeter) || !GameStats.GetBool(EnumGameStats.IsSpawnEnemies))
                return false; // Skip original method

            float activity = __instance.ActivityLevel;
            ShamblersSenseLogger.Log($"CheckToSpawn called with ActivityLevel={activity}");

            // Your heat thresholds for screamer spawn
            float[] thresholds = new float[] { 30f, 50f, 70f, 90f };

            foreach (var threshold in thresholds)
            {
                if (activity >= threshold && !HasTriggeredAt(__instance, threshold))
                {
                    if (TrySpawnScoutAtThreshold(__instance, threshold))
                    {
                        MarkTriggeredAt(__instance, threshold);
                        // Don't reset heat so it can accumulate further
                        return false; // Skip original method, we handled spawn
                    }
                }
            }

            // Optionally, call original method if no threshold met
            return false; // Skip original to prevent default screamer spawn
        }

        private static bool TrySpawnScoutAtThreshold(AIDirectorChunkData chunkData, float threshold)
        {
            var evt = chunkData.FindBestEventAndReset();
            if (evt == null)
            {
                AIDirector.LogAI("Chunk event not found!", new object[0]);
                return false;
            }

            if (chunkData.Director.random.RandomFloat < 0.2f && !GameUtils.IsPlaytesting())
            {
                ShamblersSenseLogger.Log($"Spawning scout at heat threshold {threshold}");
                // We do NOT call SetLongDelay() here to avoid resetting heat.
                chunkData.SetLongDelay(); // optionally comment out if you want zero delay
                chunkData.Director.SpawnScouts(evt.Position.ToVector3());
                return true;
            }
            return false;
        }

        // Use a HashSet or similar on the chunkData to track triggered thresholds.
        // Since you can't modify the original class easily, use a WeakReference dictionary or similar.
        private static readonly System.Collections.Generic.Dictionary<AIDirectorChunkData, System.Collections.Generic.HashSet<float>> triggeredThresholds = new();

        private static bool HasTriggeredAt(AIDirectorChunkData chunkData, float threshold)
        {
            if (triggeredThresholds.TryGetValue(chunkData, out var set))
            {
                return set.Contains(threshold);
            }
            return false;
        }

        private static void MarkTriggeredAt(AIDirectorChunkData chunkData, float threshold)
        {
            if (!triggeredThresholds.TryGetValue(chunkData, out var set))
            {
                set = new System.Collections.Generic.HashSet<float>();
                triggeredThresholds[chunkData] = set;
            }
            set.Add(threshold);
        }
    }

    [HarmonyPatch(typeof(AIDirector))]
    [HarmonyPatch("GetMaxZombies")]
    public class Patch_AIDirector_GetMaxZombies
    {
        static void Postfix(ref int __result)
        {
            var player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player == null) return;

            var chunkCoord = player.GetChunkCoordinate();
            var chunkData = GameManager.Instance.World.aiDirector.GetChunkData(chunkCoord);

            if (chunkData != null)
            {
                float heat = chunkData.ActivityLevel;
                float halfwayHeat = 50f; // example threshold

                if (heat >= halfwayHeat)
                {
                    float boost = UnityEngine.Random.Range(0.10f, 0.15f);
                    int extra = Mathf.CeilToInt(__result * boost);
                    __result += extra;

                    ShamblersSenseLogger.Log($"Boosted max zombies by {extra} due to heat {heat}");
                }
            }
        }
    }
}
